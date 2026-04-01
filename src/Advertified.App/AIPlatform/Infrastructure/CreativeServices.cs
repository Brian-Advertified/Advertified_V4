using System.Text.Json;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class DbMediaPlanningIntegrationService : IMediaPlanningIntegrationService
{
    private readonly AppDbContext _db;

    public DbMediaPlanningIntegrationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MediaPlanningContext> BuildContextAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(item => item.User)
                .ThenInclude(item => item.BusinessProfile)
            .Include(item => item.PackageOrder)
            .Include(item => item.CampaignBrief)
            .FirstOrDefaultAsync(item => item.Id == campaignId, cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var preferredMedia = campaign.CampaignBrief?.GetList(nameof(Data.Entities.CampaignBrief.PreferredMediaTypesJson))
            ?? new List<string>();
        var channels = preferredMedia.Count > 0
            ? preferredMedia.Select(MapChannel).Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToArray()
            : new[] { AdvertisingChannel.Radio, AdvertisingChannel.Billboard, AdvertisingChannel.Digital };

        return new MediaPlanningContext(
            CampaignId: campaignId,
            BusinessName: campaign.User.BusinessProfile?.BusinessName ?? campaign.User.FullName,
            Industry: campaign.User.BusinessProfile?.Industry ?? "General",
            Location: campaign.User.BusinessProfile?.Province ?? "South Africa",
            Objective: campaign.CampaignBrief?.Objective ?? "Awareness",
            Budget: campaign.PackageOrder.SelectedBudget ?? campaign.PackageOrder.Amount,
            Tone: campaign.CampaignBrief?.CreativeNotes?.Trim() is { Length: > 0 } notes ? notes : "Balanced",
            AudienceLsm: $"{campaign.CampaignBrief?.TargetLsmMin ?? 4}-{campaign.CampaignBrief?.TargetLsmMax ?? 8}",
            AudienceAgeRange: $"{campaign.CampaignBrief?.TargetAgeMin ?? 25}-{campaign.CampaignBrief?.TargetAgeMax ?? 45}",
            Languages: campaign.CampaignBrief?.GetList(nameof(Data.Entities.CampaignBrief.TargetLanguagesJson))?.ToArray() ?? new[] { "English" },
            Channels: channels);
    }

    private static AdvertisingChannel? MapChannel(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "radio" => AdvertisingChannel.Radio,
            "tv" => AdvertisingChannel.Tv,
            "billboard" => AdvertisingChannel.Billboard,
            "ooh" => AdvertisingChannel.Billboard,
            "digital" => AdvertisingChannel.Digital,
            "newspaper" => AdvertisingChannel.Newspaper,
            _ => null
        };
    }
}

public sealed class StrategyCreativeGenerationEngine : ICreativeGenerationEngine
{
    private readonly IMultiAiProviderOrchestrator _orchestrator;
    private readonly IPromptLibraryService _promptLibraryService;
    private readonly IPromptInputBuilder _promptInputBuilder;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StrategyCreativeGenerationEngine(
        IMultiAiProviderOrchestrator orchestrator,
        IPromptLibraryService promptLibraryService,
        IPromptInputBuilder promptInputBuilder)
    {
        _orchestrator = orchestrator;
        _promptLibraryService = promptLibraryService;
        _promptInputBuilder = promptInputBuilder;
    }

    public async Task<IReadOnlyList<CreativeVariant>> GenerateAsync(
        CreativeBrief brief,
        CancellationToken cancellationToken)
    {
        var output = new List<CreativeVariant>();
        foreach (var channel in brief.Channels)
        {
            // Multi-language support: generate creatives for all requested languages.
            var languages = brief.Languages.Count > 0 ? brief.Languages : new[] { "English" };
            foreach (var language in languages)
            {
                var templateKeys = ResolveTemplateKeys(channel);
                var boundedTemplateKeys = templateKeys
                    .Take(Math.Max(1, brief.MaxVariantsPerChannel))
                    .ToArray();
                foreach (var templateKey in boundedTemplateKeys)
                {
                    var renderedPrompt = await RenderPromptWithFallbackAsync(
                        templateKey,
                        channel,
                        language,
                        brief,
                        cancellationToken);

                    var input = JsonSerializer.Serialize(new CreativeGenerateProviderInput(
                        renderedPrompt.RenderedSystemPrompt,
                        renderedPrompt.RenderedUserPrompt,
                        renderedPrompt.Template.OutputSchemaJson,
                        channel.ToString(),
                        language,
                        templateKey), SerializerOptions);

                    var aiPayload = await _orchestrator.ExecuteAsync(channel, "creative-generate", input, cancellationToken);
                    var payloadJson = BuildStructuredPayload(channel, language, templateKey, aiPayload);
                    output.Add(new CreativeVariant(
                        CreativeId: Guid.NewGuid(),
                        CampaignId: brief.CampaignId,
                        Channel: channel,
                        Language: language,
                        PayloadJson: payloadJson,
                        CreatedAt: DateTimeOffset.UtcNow));
                }
            }
        }

        return output;
    }

    private async Task<PromptRenderResult> RenderPromptWithFallbackAsync(
        string templateKey,
        AdvertisingChannel channel,
        string language,
        CreativeBrief brief,
        CancellationToken cancellationToken)
    {
        var variables = _promptInputBuilder.BuildVariables(brief, channel, language, templateKey);

        try
        {
            return await _promptLibraryService.RenderAsync(new PromptRenderRequest(
                templateKey,
                channel,
                language,
                brief.PromptVersion,
                variables), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Language fallback keeps generation resilient when non-English prompt variants are not seeded yet.
            return await _promptLibraryService.RenderAsync(new PromptRenderRequest(
                templateKey,
                channel,
                "English",
                brief.PromptVersion,
                variables), cancellationToken);
        }
    }

    private static IReadOnlyList<string> ResolveTemplateKeys(AdvertisingChannel channel)
    {
        return channel switch
        {
            AdvertisingChannel.Radio => new[] { "creative-brief-radio" },
            AdvertisingChannel.Tv => new[] { "creative-brief-tv" },
            AdvertisingChannel.Billboard => new[] { "creative-brief-billboard" },
            AdvertisingChannel.Newspaper => new[] { "creative-brief-newspaper" },
            AdvertisingChannel.Digital => new[]
            {
                "creative-brief-digital-meta",
                "creative-brief-digital-tiktok",
                "creative-brief-digital-google"
            },
            _ => new[] { "creative-brief-default" }
        };
    }

    private static string BuildStructuredPayload(
        AdvertisingChannel channel,
        string language,
        string templateKey,
        string aiPayload)
    {
        object content;
        try
        {
            content = JsonSerializer.Deserialize<JsonElement>(aiPayload);
        }
        catch
        {
            content = new { raw = aiPayload };
        }

        var envelope = new
        {
            schemaVersion = "1.0",
            channel = channel.ToString(),
            language,
            templateKey,
            content
        };

        return JsonSerializer.Serialize(envelope, SerializerOptions);
    }

    private sealed record CreativeGenerateProviderInput(
        string SystemPrompt,
        string UserPrompt,
        string OutputSchemaJson,
        string Channel,
        string Language,
        string TemplateKey);
}

public sealed class StrategyAssetGenerationPipeline : IAssetGenerationPipeline
{
    private readonly IVoiceAssetGenerationService _voiceAssetGenerationService;
    private readonly IImageAssetGenerationService _imageAssetGenerationService;
    private readonly IVideoAssetGenerationService _videoAssetGenerationService;
    private readonly IAssetJobService _assetJobService;

    public StrategyAssetGenerationPipeline(
        IVoiceAssetGenerationService voiceAssetGenerationService,
        IImageAssetGenerationService imageAssetGenerationService,
        IVideoAssetGenerationService videoAssetGenerationService,
        IAssetJobService assetJobService)
    {
        _voiceAssetGenerationService = voiceAssetGenerationService;
        _imageAssetGenerationService = imageAssetGenerationService;
        _videoAssetGenerationService = videoAssetGenerationService;
        _assetJobService = assetJobService;
    }

    public async Task<IReadOnlyList<AssetGenerationResult>> GenerateAssetsAsync(
        IReadOnlyList<AssetGenerationRequest> requests,
        CancellationToken cancellationToken)
    {
        var results = new List<AssetGenerationResult>();
        foreach (var request in requests)
        {
            var queued = request.Channel switch
            {
                AdvertisingChannel.Radio => await _voiceAssetGenerationService.QueueAsync(new VoiceAssetRequest(
                    request.CampaignId,
                    request.CreativeId,
                    request.PayloadJson,
                    "Standard",
                    "English"), cancellationToken),
                AdvertisingChannel.Tv => await _videoAssetGenerationService.QueueAsync(new VideoAssetRequest(
                    request.CampaignId,
                    request.CreativeId,
                    request.PayloadJson,
                    request.PayloadJson,
                    "English",
                    "16:9",
                    30), cancellationToken),
                AdvertisingChannel.Digital => await _videoAssetGenerationService.QueueAsync(new VideoAssetRequest(
                    request.CampaignId,
                    request.CreativeId,
                    request.PayloadJson,
                    request.PayloadJson,
                    "English",
                    "9:16",
                    15), cancellationToken),
                _ => await _imageAssetGenerationService.QueueAsync(new ImageAssetRequest(
                    request.CampaignId,
                    request.CreativeId,
                    request.PayloadJson,
                    "Bold",
                    1), cancellationToken)
            };

            var finalStatus = await WaitForCompletionAsync(queued.JobId, cancellationToken);
            var assetUrl = finalStatus?.AssetUrl ?? "https://assets.example.com/pending";
            var assetType = finalStatus?.AssetType ?? queued.AssetKind;

            results.Add(new AssetGenerationResult(
                request.CreativeId,
                request.Channel,
                assetType,
                assetUrl,
                DateTimeOffset.UtcNow));
        }

        return results;
    }

    private async Task<AssetJobStatusResult?> WaitForCompletionAsync(Guid jobId, CancellationToken cancellationToken)
    {
        const int maxAttempts = 15;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var status = await _assetJobService.GetStatusAsync(jobId, cancellationToken);
            if (status is null)
            {
                return null;
            }

            if (status.Status is "completed" or "failed")
            {
                return status;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
        }

        return await _assetJobService.GetStatusAsync(jobId, cancellationToken);
    }
}

public sealed class CreativeFeedbackRegenerationService : ICreativeFeedbackRegenerationService
{
    private readonly ICreativeCampaignOrchestrator _creativeCampaignOrchestrator;

    public CreativeFeedbackRegenerationService(ICreativeCampaignOrchestrator creativeCampaignOrchestrator)
    {
        _creativeCampaignOrchestrator = creativeCampaignOrchestrator;
    }

    public Task<GenerateCampaignCreativesResult> RegenerateAsync(
        RegenerationFeedback feedback,
        CancellationToken cancellationToken)
    {
        // Feedback is injected as prompt override for deterministic regeneration.
        var command = new GenerateCampaignCreativesCommand(
            CampaignId: feedback.CampaignId,
            PromptOverride: feedback.Feedback,
            PersistOutputs: true);

        return _creativeCampaignOrchestrator.GenerateAsync(command, cancellationToken);
    }
}

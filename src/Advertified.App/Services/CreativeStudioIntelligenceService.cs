using System.Text.Json;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Contracts.Creative;
using Advertified.App.Services.Abstractions;
using CampaignEntity = Advertified.App.Data.Entities.Campaign;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;

namespace Advertified.App.Services;

public sealed class CreativeStudioIntelligenceService : ICreativeStudioIntelligenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMediaPlanningIntegrationService _mediaPlanningIntegrationService;
    private readonly ICreativeGenerationEngine _creativeGenerationEngine;
    private readonly ICreativeQaService _creativeQaService;

    public CreativeStudioIntelligenceService(
        IMediaPlanningIntegrationService mediaPlanningIntegrationService,
        ICreativeGenerationEngine creativeGenerationEngine,
        ICreativeQaService creativeQaService)
    {
        _mediaPlanningIntegrationService = mediaPlanningIntegrationService;
        _creativeGenerationEngine = creativeGenerationEngine;
        _creativeQaService = creativeQaService;
    }

    public async Task<CreativeSystemResponse> GenerateAsync(
        CampaignEntity campaign,
        CampaignBriefEntity? brief,
        GenerateCreativeSystemRequest request,
        Guid? sourceCreativeSystemId,
        CancellationToken cancellationToken)
    {
        _ = sourceCreativeSystemId;

        var context = await _mediaPlanningIntegrationService.BuildContextAsync(campaign.Id, cancellationToken);
        var generationBrief = BuildBrief(campaign, request, context);
        var creatives = await _creativeGenerationEngine.GenerateAsync(generationBrief, cancellationToken);
        var scores = await _creativeQaService.ScoreAsync(generationBrief, creatives, cancellationToken);
        var channelAdaptations = creatives
            .Select(creative => MapChannelAdaptation(creative, scores.FirstOrDefault(score => score.CreativeId == creative.CreativeId)))
            .ToArray();

        var campaignLines = channelAdaptations
            .Select(item => item.HeadlineOrHook)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        var assumptions = scores
            .SelectMany(score => score.Issues ?? Array.Empty<string>())
            .Concat(scores.SelectMany(score => score.Suggestions ?? Array.Empty<string>()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        return new CreativeSystemResponse
        {
            CampaignSummary = new CreativeCampaignSummaryResponse
            {
                Brand = generationBrief.Brand,
                Product = campaign.CampaignName?.Trim() ?? generationBrief.Brand,
                Audience = string.Join(" | ", generationBrief.AudienceInsights),
                Objective = generationBrief.Objective,
                Tone = generationBrief.Tone,
                Channels = generationBrief.Channels.Select(FormatChannelLabel).ToArray(),
                Cta = generationBrief.CallToAction,
                Constraints = request.Constraints,
                Assumptions = assumptions
            },
            MasterIdea = new CreativeMasterIdeaResponse
            {
                CoreConcept = $"Unify {generationBrief.Channels.Count} channels behind one AI-generated campaign system.",
                CentralMessage = generationBrief.KeyMessage,
                EmotionalAngle = "Confidence and immediacy.",
                ValueProposition = "Channel-native creative directions with QA-backed scoring.",
                PlatformIdea = "One campaign idea, adapted by channel and language."
            },
            CampaignLineOptions = campaignLines.Length > 0 ? campaignLines : new[] { generationBrief.Brand, "Advertise now", "Pay later" },
            Storyboard = new CreativeNarrativeResponse
            {
                Hook = channelAdaptations.FirstOrDefault()?.HeadlineOrHook ?? "Lead with the strongest campaign hook.",
                Setup = $"Position {generationBrief.Brand} around {context.Location} demand and the chosen objective.",
                TensionOrProblem = "Audience attention is limited and needs a fast payoff.",
                Solution = generationBrief.KeyMessage,
                Payoff = "Carry one clear idea across every selected channel.",
                Cta = generationBrief.CallToAction,
                Scenes = BuildScenes(channelAdaptations)
            },
            ChannelAdaptations = channelAdaptations,
            VisualDirection = new CreativeVisualDirectionResponse
            {
                LookAndFeel = "Bold, commercially polished, and channel-native.",
                Typography = "Fast-read hierarchy with a clear CTA lockup.",
                ColorDirection = "Brand-led palette with one high-contrast action color.",
                Composition = "Single focal point per execution with minimal clutter.",
                ImageGenerationPrompts = new[]
                {
                    $"{generationBrief.Brand} campaign visual in {context.Location}, premium advertising style, strong CTA space",
                    $"{generationBrief.Brand} audience-in-context creative scene, commercial quality, high readability"
                }
            },
            AudioVoiceNotes = new[]
            {
                "Keep spoken copy concise and action-led.",
                "Maintain the same CTA across all approved variants."
            },
            ProductionNotes = new[]
            {
                "Review the lowest QA metrics first before finalizing outputs.",
                "Validate language and platform variants before export.",
                "Keep the key message consistent across channel adaptations."
            },
            OptionalVariations = new[]
            {
                "Urgency-led version",
                "Trust-led version",
                "Offer-led version"
            }
        };
    }

    private static CreativeBrief BuildBrief(
        CampaignEntity campaign,
        GenerateCreativeSystemRequest request,
        MediaPlanningContext context)
    {
        var brand = string.IsNullOrWhiteSpace(request.Brand)
            ? context.BusinessName
            : request.Brand.Trim();
        var objective = string.IsNullOrWhiteSpace(request.Objective)
            ? context.Objective
            : request.Objective.Trim();
        var tone = string.IsNullOrWhiteSpace(request.Tone)
            ? context.Tone
            : request.Tone.Trim();
        var channels = ResolveChannels(request.Channels, context.Channels);
        var cta = string.IsNullOrWhiteSpace(request.Cta) ? "Get started today" : request.Cta.Trim();
        var keyMessage = string.IsNullOrWhiteSpace(request.Prompt)
            ? $"High-impact campaign for {brand} in {context.Location} with objective {objective}."
            : request.Prompt.Trim();

        return new CreativeBrief(
            CampaignId: campaign.Id,
            Budget: context.Budget,
            Brand: brand,
            Objective: objective,
            Tone: tone,
            KeyMessage: keyMessage,
            CallToAction: cta,
            AudienceInsights: new[]
            {
                $"LSM {context.AudienceLsm}",
                $"Age {context.AudienceAgeRange}",
                context.Location
            },
            Languages: context.Languages.Count > 0 ? context.Languages : new[] { "English" },
            Channels: channels,
            PromptVersion: 0,
            MaxVariantsPerChannel: 1);
    }

    private static IReadOnlyList<AdvertisingChannel> ResolveChannels(
        IReadOnlyList<string>? requestedChannels,
        IReadOnlyList<AdvertisingChannel> fallbackChannels)
    {
        var resolved = (requestedChannels ?? Array.Empty<string>())
            .Select(ParseChannel)
            .Where(channel => channel.HasValue)
            .Select(channel => channel!.Value)
            .Distinct()
            .ToArray();

        return resolved.Length > 0 ? resolved : fallbackChannels;
    }

    private static AdvertisingChannel? ParseChannel(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "radio" => AdvertisingChannel.Radio,
            "tv" or "television" => AdvertisingChannel.Tv,
            "ooh" or "billboard" or "billboards" or "out of home" => AdvertisingChannel.Billboard,
            "newspaper" or "print" => AdvertisingChannel.Newspaper,
            "digital" or "meta" or "tiktok" or "google" or "social static" => AdvertisingChannel.Digital,
            _ => null
        };
    }

    private static CreativeChannelAdaptationResponse MapChannelAdaptation(
        CreativeVariant creative,
        CreativeQualityScore? score)
    {
        using var document = JsonDocument.Parse(creative.PayloadJson);
        var root = document.RootElement;
        var content = root.TryGetProperty("content", out var contentElement) ? contentElement : root;
        var platform = GetString(content, "platform") ?? GetString(root, "templateKey");
        var channelLabel = creative.Channel == AdvertisingChannel.Digital && !string.IsNullOrWhiteSpace(platform)
            ? HumanizeDigitalChannel(platform)
            : FormatChannelLabel(creative.Channel);
        var headline = GetFirst(content, "hook", "headline", "title", "message", "script") ?? channelLabel;
        var primaryCopy = GetFirst(content, "script", "primaryText", "body", "message", "headline") ?? headline;
        var cta = GetFirst(content, "cta", "callToAction") ?? "Get started today";
        var visualDirection = GetFirst(content, "visualDirection", "visualStyle", "visual", "artDirection")
            ?? DefaultVisualDirection(creative.Channel);
        var sections = BuildSections(content, creative, score, platform);

        return new CreativeChannelAdaptationResponse
        {
            Channel = channelLabel,
            Format = DescribeFormat(creative.Channel),
            HeadlineOrHook = headline,
            PrimaryCopy = primaryCopy,
            Cta = cta,
            VisualDirection = visualDirection,
            VoiceoverOrAudio = creative.Channel == AdvertisingChannel.Radio ? GetFirst(content, "voiceTone", "voiceover", "audioDirection") : null,
            RecommendedDirection = score?.Suggestions?.FirstOrDefault()
                ?? DefaultRecommendedDirection(creative.Channel),
            AdapterPrompt = GetString(root, "templateKey") ?? $"creative-brief-{creative.Channel.ToString().ToLowerInvariant()}",
            Sections = sections,
            Versions = BuildVersions(headline, primaryCopy, cta),
            ProductionAssets = ResolveProductionAssets(creative.Channel)
        };
    }

    private static IReadOnlyList<CreativeChannelSectionResponse> BuildSections(
        JsonElement content,
        CreativeVariant creative,
        CreativeQualityScore? score,
        string? platform)
    {
        var sections = new List<CreativeChannelSectionResponse>
        {
            new() { Label = "Language", Content = creative.Language }
        };

        if (!string.IsNullOrWhiteSpace(platform))
        {
            sections.Add(new CreativeChannelSectionResponse { Label = "Template", Content = HumanizeDigitalChannel(platform) });
        }

        if (TryGetArrayLength(content, "scenes", out var sceneCount))
        {
            sections.Add(new CreativeChannelSectionResponse { Label = "Scenes", Content = $"{sceneCount} scene(s)" });
        }

        if (score is not null)
        {
            sections.Add(new CreativeChannelSectionResponse
            {
                Label = "QA score",
                Content = $"{score.OverallScore:0.0} | {score.Status}"
            });
        }

        return sections;
    }

    private static IReadOnlyList<CreativeChannelVersionResponse> BuildVersions(string headline, string primaryCopy, string cta)
    {
        return new[]
        {
            new CreativeChannelVersionResponse { Label = "v1", Intent = "safe", HeadlineOrHook = headline, PrimaryCopy = primaryCopy, Cta = cta },
            new CreativeChannelVersionResponse { Label = "v2", Intent = "bolder", HeadlineOrHook = headline, PrimaryCopy = primaryCopy, Cta = cta },
            new CreativeChannelVersionResponse { Label = "v3", Intent = "experimental", HeadlineOrHook = headline, PrimaryCopy = primaryCopy, Cta = cta }
        };
    }

    private static IReadOnlyList<CreativeSceneResponse> BuildScenes(IReadOnlyList<CreativeChannelAdaptationResponse> channelAdaptations)
    {
        var scenes = channelAdaptations
            .Take(3)
            .Select((adaptation, index) => new CreativeSceneResponse
            {
                Order = index + 1,
                Title = adaptation.Channel,
                Purpose = "Carry the same campaign idea in a channel-native format.",
                Visual = adaptation.VisualDirection,
                CopyOrDialogue = adaptation.PrimaryCopy,
                OnScreenText = adaptation.HeadlineOrHook,
                Duration = adaptation.Format
            })
            .ToArray();

        return scenes.Length > 0
            ? scenes
            : new[]
            {
                new CreativeSceneResponse
                {
                    Order = 1,
                    Title = "Core story",
                    Purpose = "Anchor the campaign direction.",
                    Visual = "Single high-impact brand visual",
                    CopyOrDialogue = "One message. One CTA.",
                    OnScreenText = "Advertise now. Pay later.",
                    Duration = "10s"
                }
            };
    }

    private static string FormatChannelLabel(AdvertisingChannel channel)
    {
        return channel switch
        {
            AdvertisingChannel.Radio => "Radio",
            AdvertisingChannel.Tv => "TV",
            AdvertisingChannel.Billboard => "Billboards and Digital Screens",
            AdvertisingChannel.Newspaper => "Newspaper",
            AdvertisingChannel.Digital => "Digital",
            _ => channel.ToString()
        };
    }

    private static string DescribeFormat(AdvertisingChannel channel)
    {
        return channel switch
        {
            AdvertisingChannel.Radio => "30s audio spot",
            AdvertisingChannel.Tv => "30s video spot",
            AdvertisingChannel.Billboard => "Static outdoor creative",
            AdvertisingChannel.Newspaper => "Print ad",
            AdvertisingChannel.Digital => "Digital ad",
            _ => "Creative variant"
        };
    }

    private static string DefaultVisualDirection(AdvertisingChannel channel)
    {
        return channel switch
        {
            AdvertisingChannel.Radio => "Audio-led execution with a clear CTA close.",
            AdvertisingChannel.Tv => "Narrative sequence with branded close.",
            AdvertisingChannel.Billboard => "High-contrast layout with one dominant focal point.",
            AdvertisingChannel.Newspaper => "Headline-led print hierarchy with support copy.",
            AdvertisingChannel.Digital => "Platform-native motion-first creative.",
            _ => "Commercial campaign direction."
        };
    }

    private static string DefaultRecommendedDirection(AdvertisingChannel channel)
    {
        return channel switch
        {
            AdvertisingChannel.Radio => "Lead with the hook in the first five seconds.",
            AdvertisingChannel.Tv => "Anchor each scene to one payoff beat.",
            AdvertisingChannel.Billboard => "Keep the message readable at a glance.",
            AdvertisingChannel.Newspaper => "Front-load the strongest value message.",
            AdvertisingChannel.Digital => "Open with the strongest hook immediately.",
            _ => "Keep the message simple and action-led."
        };
    }

    private static IReadOnlyList<string> ResolveProductionAssets(AdvertisingChannel channel)
    {
        return channel switch
        {
            AdvertisingChannel.Radio => new[] { "Voice over", "Music bed", "Final master WAV" },
            AdvertisingChannel.Tv => new[] { "Shot list", "Voice over script", "Edit timeline" },
            AdvertisingChannel.Billboard => new[] { "Master artwork", "Print-ready PDF", "Mockup render" },
            AdvertisingChannel.Newspaper => new[] { "Print artwork", "Placement spec" },
            AdvertisingChannel.Digital => new[] { "Static variants", "Vertical video cut", "Copy sheet" },
            _ => new[] { "Final creative export" }
        };
    }

    private static string HumanizeDigitalChannel(string value)
    {
        var normalized = value.Replace("creative-brief-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('-', ' ')
            .Trim();

        return normalized.ToLowerInvariant() switch
        {
            "digital meta" => "Meta",
            "digital tiktok" => "TikTok",
            "digital google" => "Google",
            _ => string.Join(" ", normalized
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()))
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => value.ToString()
        };
    }

    private static string? GetFirst(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryGetArrayLength(JsonElement element, string propertyName, out int count)
    {
        count = 0;
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        count = value.GetArrayLength();
        return true;
    }
}

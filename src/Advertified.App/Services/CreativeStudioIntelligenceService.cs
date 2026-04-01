using Advertified.App.Contracts.Creative;
using Advertified.App.Services.Abstractions;
using CampaignEntity = Advertified.App.Data.Entities.Campaign;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;

namespace Advertified.App.Services;

public sealed class CreativeStudioIntelligenceService : ICreativeStudioIntelligenceService
{
    private readonly ICreativeGenerationOrchestrator _creativeGenerationOrchestrator;

    public CreativeStudioIntelligenceService(ICreativeGenerationOrchestrator creativeGenerationOrchestrator)
    {
        _creativeGenerationOrchestrator = creativeGenerationOrchestrator;
    }

    public async Task<CreativeSystemResponse> GenerateAsync(
        CampaignEntity campaign,
        CampaignBriefEntity? brief,
        GenerateCreativeSystemRequest request,
        Guid? sourceCreativeSystemId,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = await _creativeGenerationOrchestrator.BuildNormalizedRequestFromCampaignAsync(
            campaign.Id,
            request.Prompt,
            request.Objective,
            request.Tone,
            request.Channels,
            cancellationToken);

        normalizedRequest.Business.Name = string.IsNullOrWhiteSpace(request.Brand) ? normalizedRequest.Business.Name : request.Brand.Trim();
        normalizedRequest.Objective = string.IsNullOrWhiteSpace(request.Objective) ? normalizedRequest.Objective : request.Objective.Trim();
        normalizedRequest.Tone = string.IsNullOrWhiteSpace(request.Tone) ? normalizedRequest.Tone : request.Tone.Trim();

        var generated = await _creativeGenerationOrchestrator.GenerateAsync(normalizedRequest, sourceCreativeSystemId, true, cancellationToken);
        var channelAdaptations = MapChannelAdaptations(generated);
        var campaignLines = channelAdaptations
            .Select(x => x.HeadlineOrHook)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        return new CreativeSystemResponse
        {
            CampaignSummary = new CreativeCampaignSummaryResponse
            {
                Brand = normalizedRequest.Business.Name,
                Product = campaign.CampaignName?.Trim() ?? normalizedRequest.Business.Name,
                Audience = $"{normalizedRequest.Audience.Lsm} | {normalizedRequest.Audience.AgeRange}",
                Objective = normalizedRequest.Objective,
                Tone = normalizedRequest.Tone,
                Channels = normalizedRequest.Channels,
                Cta = channelAdaptations.FirstOrDefault()?.Cta ?? "Learn more today",
                Constraints = request.Constraints,
                Assumptions = generated.Metadata.Warnings
            },
            MasterIdea = new CreativeMasterIdeaResponse
            {
                CoreConcept = $"Unify {normalizedRequest.Channels.Count} channels behind a single outcome-led message.",
                CentralMessage = $"Bring {normalizedRequest.Business.Name} to {normalizedRequest.Business.Location} audiences with consistent CTA discipline.",
                EmotionalAngle = "Confidence and immediacy.",
                ValueProposition = "Structured creatives by channel, ready for production and scoring.",
                PlatformIdea = "One campaign idea, natively adapted per channel."
            },
            CampaignLineOptions = campaignLines.Length > 0 ? campaignLines : new[] { normalizedRequest.Business.Name, "Advertise now", "Pay later" },
            Storyboard = new CreativeNarrativeResponse
            {
                Hook = "Start with an immediate real-world problem.",
                Setup = $"Frame {normalizedRequest.Business.Name} as the fastest relevant solution in {normalizedRequest.Business.Location}.",
                TensionOrProblem = "Audience delay and uncertainty.",
                Solution = "Clear value with direct proof and social familiarity.",
                Payoff = "Simple next action that feels low friction.",
                Cta = channelAdaptations.FirstOrDefault()?.Cta ?? "Learn more today",
                Scenes = BuildScenes(channelAdaptations)
            },
            ChannelAdaptations = channelAdaptations,
            VisualDirection = new CreativeVisualDirectionResponse
            {
                LookAndFeel = "High contrast, fast read, premium commercial finish.",
                Typography = "Strong headline hierarchy with simple CTA lockup.",
                ColorDirection = "Brand-led primary with one contrast CTA color.",
                Composition = "Single focal point and minimal clutter for quick recall.",
                ImageGenerationPrompts = new[]
                {
                    $"{normalizedRequest.Business.Name} key visual in {normalizedRequest.Business.Location}, commercial quality, high readability, ad-ready layout",
                    $"{normalizedRequest.Business.Name} product-in-use scene, clean CTA space, modern SA urban context"
                }
            },
            AudioVoiceNotes = new[]
            {
                "Keep spoken copy short and action-forward.",
                "Use local cadence and plain language for trust."
            },
            ProductionNotes = new[]
            {
                "Keep CTA and campaign line consistent across channels.",
                "Validate language variants before final export.",
                "Prioritize score improvements on lowest metric first."
            },
            OptionalVariations = new[]
            {
                "Urgency-led version",
                "Price-led version",
                "Trust-led version"
            }
        };
    }

    private static IReadOnlyList<CreativeChannelAdaptationResponse> MapChannelAdaptations(
        Advertified.App.Contracts.Creatives.GenerateCreativesResponse generated)
    {
        var results = new List<CreativeChannelAdaptationResponse>();
        results.AddRange(generated.Creatives.Radio.Select(item => new CreativeChannelAdaptationResponse
        {
            Channel = "Radio",
            Format = "30s audio spot",
            HeadlineOrHook = item.Script.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? item.Script,
            PrimaryCopy = item.Script,
            Cta = item.Cta,
            VisualDirection = "N/A",
            VoiceoverOrAudio = item.VoiceTone,
            RecommendedDirection = "Lead with tension in first 5 seconds.",
            AdapterPrompt = "Create high-recall local radio script with one CTA.",
            Sections = new[]
            {
                new CreativeChannelSectionResponse { Label = "Language", Content = item.Language },
                new CreativeChannelSectionResponse { Label = "Duration", Content = $"{item.Duration}s" }
            },
            Versions = new[]
            {
                new CreativeChannelVersionResponse { Label = "v1", Intent = "safe", HeadlineOrHook = item.Script, PrimaryCopy = item.Script, Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v2", Intent = "bolder", HeadlineOrHook = item.Script, PrimaryCopy = item.Script, Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v3", Intent = "experimental", HeadlineOrHook = item.Script, PrimaryCopy = item.Script, Cta = item.Cta }
            },
            ProductionAssets = new[] { "Voice over", "Music bed", "Final master WAV" }
        }));

        results.AddRange(generated.Creatives.Billboard.Select(item => new CreativeChannelAdaptationResponse
        {
            Channel = "Billboard",
            Format = "OOH static",
            HeadlineOrHook = item.Headline,
            PrimaryCopy = item.Subtext,
            Cta = item.Cta,
            VisualDirection = item.VisualDirection,
            RecommendedDirection = "Keep to seven-word readable headline max.",
            AdapterPrompt = "Produce single-message OOH copy with clear CTA cue.",
            Sections = new[]
            {
                new CreativeChannelSectionResponse { Label = "Headline", Content = item.Headline },
                new CreativeChannelSectionResponse { Label = "Subtext", Content = item.Subtext }
            },
            Versions = new[]
            {
                new CreativeChannelVersionResponse { Label = "v1", Intent = "safe", HeadlineOrHook = item.Headline, PrimaryCopy = item.Subtext, Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v2", Intent = "bolder", HeadlineOrHook = item.Headline, PrimaryCopy = item.Subtext, Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v3", Intent = "experimental", HeadlineOrHook = item.Headline, PrimaryCopy = item.Subtext, Cta = item.Cta }
            },
            ProductionAssets = new[] { "Master artwork", "Print-ready PDF", "Mockup render" }
        }));

        results.AddRange(generated.Creatives.Tv.Select(item => new CreativeChannelAdaptationResponse
        {
            Channel = "TV",
            Format = $"{item.Duration}s spot",
            HeadlineOrHook = item.Scenes.FirstOrDefault()?.Description ?? "TV creative",
            PrimaryCopy = string.Join(" ", item.Scenes.Select(x => x.Dialogue)),
            Cta = item.Cta,
            VisualDirection = "Narrative sequence with branded close.",
            RecommendedDirection = "Anchor each scene to one payoff beat.",
            AdapterPrompt = "Generate scene-based TV script with strong CTA close.",
            Sections = item.Scenes.Select(scene => new CreativeChannelSectionResponse
            {
                Label = $"Scene {scene.Scene}",
                Content = $"{scene.Description} | {scene.Dialogue}"
            }).ToArray(),
            Versions = new[]
            {
                new CreativeChannelVersionResponse { Label = "v1", Intent = "safe", HeadlineOrHook = "Standard narrative", PrimaryCopy = "Balanced pacing", Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v2", Intent = "bolder", HeadlineOrHook = "Fast-cut dynamic", PrimaryCopy = "Higher urgency", Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v3", Intent = "experimental", HeadlineOrHook = "POV-led story", PrimaryCopy = "Unconventional framing", Cta = item.Cta }
            },
            ProductionAssets = new[] { "Shot list", "Voice over script", "Edit timeline" }
        }));

        results.AddRange(generated.Creatives.Newspaper.Select(item => new CreativeChannelAdaptationResponse
        {
            Channel = "Newspaper",
            Format = "Print ad",
            HeadlineOrHook = item.Headline,
            PrimaryCopy = item.Body,
            Cta = item.Cta,
            VisualDirection = "Headline-led print layout.",
            RecommendedDirection = "Front-load value proposition in first sentence.",
            AdapterPrompt = "Create print copy with clear hierarchy and CTA.",
            Sections = new[]
            {
                new CreativeChannelSectionResponse { Label = "Headline", Content = item.Headline },
                new CreativeChannelSectionResponse { Label = "Body", Content = item.Body }
            },
            Versions = new[]
            {
                new CreativeChannelVersionResponse { Label = "v1", Intent = "safe", HeadlineOrHook = item.Headline, PrimaryCopy = item.Body, Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v2", Intent = "bolder", HeadlineOrHook = item.Headline, PrimaryCopy = item.Body, Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v3", Intent = "experimental", HeadlineOrHook = item.Headline, PrimaryCopy = item.Body, Cta = item.Cta }
            },
            ProductionAssets = new[] { "Print artwork", "Editorial placement spec" }
        }));

        results.AddRange(generated.Creatives.Digital.Select(item => new CreativeChannelAdaptationResponse
        {
            Channel = item.Platform,
            Format = "Digital ad",
            HeadlineOrHook = item.Hook ?? item.Headline,
            PrimaryCopy = item.Script ?? item.PrimaryText,
            Cta = item.Cta,
            VisualDirection = "Platform-native motion-first creative.",
            RecommendedDirection = "Open with hook in first 2 seconds.",
            AdapterPrompt = "Build conversion-focused digital copy variants.",
            Sections = new[]
            {
                new CreativeChannelSectionResponse { Label = "Platform", Content = item.Platform },
                new CreativeChannelSectionResponse { Label = "Primary text", Content = item.PrimaryText }
            },
            Versions = new[]
            {
                new CreativeChannelVersionResponse { Label = "v1", Intent = "safe", HeadlineOrHook = item.Headline, PrimaryCopy = item.PrimaryText, Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v2", Intent = "bolder", HeadlineOrHook = item.Headline, PrimaryCopy = item.PrimaryText, Cta = item.Cta },
                new CreativeChannelVersionResponse { Label = "v3", Intent = "experimental", HeadlineOrHook = item.Headline, PrimaryCopy = item.PrimaryText, Cta = item.Cta }
            },
            ProductionAssets = new[] { "Static variants", "Vertical video cut", "Copy sheet" }
        }));

        return results;
    }

    private static IReadOnlyList<CreativeSceneResponse> BuildScenes(IReadOnlyList<CreativeChannelAdaptationResponse> channelAdaptations)
    {
        var sceneSummaries = channelAdaptations
            .Take(3)
            .Select((adaptation, index) => new CreativeSceneResponse
            {
                Order = index + 1,
                Title = adaptation.Channel,
                Purpose = "Carry the same campaign idea in channel-native form.",
                Visual = adaptation.VisualDirection,
                CopyOrDialogue = adaptation.PrimaryCopy,
                OnScreenText = adaptation.HeadlineOrHook,
                Duration = "10s"
            })
            .ToArray();

        return sceneSummaries.Length > 0
            ? sceneSummaries
            : new[]
            {
                new CreativeSceneResponse
                {
                    Order = 1,
                    Title = "Core story",
                    Purpose = "Anchor campaign message",
                    Visual = "Single strong visual",
                    CopyOrDialogue = "One message. One CTA.",
                    OnScreenText = "Advertise now. Pay later.",
                    Duration = "10s"
                }
            };
    }
}

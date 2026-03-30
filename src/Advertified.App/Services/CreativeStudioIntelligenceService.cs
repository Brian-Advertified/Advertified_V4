using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.App.Configuration;
using Advertified.App.Contracts.Creative;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Microsoft.Extensions.Options;
using CampaignEntity = Advertified.App.Data.Entities.Campaign;
using CampaignBriefEntity = Advertified.App.Data.Entities.CampaignBrief;

namespace Advertified.App.Services;

public sealed class CreativeStudioIntelligenceService : ICreativeStudioIntelligenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<CreativeStudioIntelligenceService> _logger;

    public CreativeStudioIntelligenceService(
        HttpClient httpClient,
        IOptions<OpenAIOptions> options,
        ILogger<CreativeStudioIntelligenceService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CreativeSystemResponse> GenerateAsync(
        CampaignEntity campaign,
        CampaignBriefEntity? brief,
        GenerateCreativeSystemRequest request,
        CancellationToken cancellationToken)
    {
        if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var aiResult = await TryGenerateWithOpenAiAsync(campaign, brief, request, cancellationToken);
            if (aiResult is not null)
            {
                return aiResult;
            }
        }

        return BuildFallbackResponse(campaign, brief, request);
    }

    private async Task<CreativeSystemResponse?> TryGenerateWithOpenAiAsync(
        CampaignEntity campaign,
        CampaignBriefEntity? brief,
        GenerateCreativeSystemRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = JsonContent.Create(
            new OpenAiChatCompletionRequest
            {
                Model = _options.Model,
                ResponseFormat = new OpenAiResponseFormat { Type = "json_object" },
                Messages = new[]
                {
                    new OpenAiChatMessage
                    {
                        Role = "system",
                        Content = BuildSystemPrompt()
                    },
                    new OpenAiChatMessage
                    {
                        Role = "user",
                        Content = BuildPrompt(campaign, brief, request)
                    }
                }
            },
            mediaType: null,
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Creative studio generation failed. Status {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
            return null;
        }

        var completion = await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(JsonOptions, cancellationToken);
        var rawContent = completion?.Choices?
            .Select(choice => choice.Message?.Content)
            .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content));

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<CreativeSystemResponse>(rawContent, JsonOptions);
            return result is null ? null : Normalize(result);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Creative studio response could not be parsed.");
            return null;
        }
    }

    private static string BuildSystemPrompt()
    {
        return
            "You are the core intelligence of Advertified Creative Studio.\n" +
            "You operate as a high-performance creative system used ONLY by a Creative Manager.\n" +
            "You are not a chatbot. You are not a generic copywriter. You are a structured creative engine.\n\n" +
            "Your job is to transform a single campaign input into a complete, multi-channel, production-ready creative system.\n" +
            "Always follow this sequence: extract context, master campaign idea, storyboard, native channel adaptation, production outputs.\n" +
            "Be sharp, structured, decisive, and operational. Do not explain basics. Do not add fluff.\n" +
            "One strong idea beats many weak ones. Strategy first, execution second. Outputs must be commercially usable.\n" +
            "Assume OpenAI handles thinking/copy/storyboard, image generation handles frames and statics, Runway-style tools handle short-form video, and ElevenLabs-style tools handle voiceovers and radio.\n" +
            "For every requested channel, preserve the same core campaign idea while adapting to native channel behavior and respecting hard format constraints.\n" +
            "For every requested channel, include structured channel sections and exactly three variants: v1 safe, v2 bolder, v3 experimental.\n" +
            "Return strict JSON only.\n";
    }

    private static string BuildPrompt(CampaignEntity campaign, CampaignBriefEntity? brief, GenerateCreativeSystemRequest request)
    {
        var summary = BuildCampaignSummary(campaign, brief, request);
        var recommendationSummary = campaign.CampaignRecommendations
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Summary)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
            ?? "No approved recommendation summary is available yet.";

        var briefNotes = string.Join(
            " | ",
            new[]
            {
                brief?.TargetAudienceNotes,
                brief?.CreativeNotes,
                brief?.SpecialRequirements
            }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));

        return
            "Generate a response with this exact JSON shape:\n" +
            "{\n" +
            "  \"campaignSummary\": { \"brand\": \"string\", \"product\": \"string\", \"audience\": \"string\", \"objective\": \"string\", \"tone\": \"string\", \"channels\": [\"string\"], \"cta\": \"string\", \"constraints\": [\"string\"], \"assumptions\": [\"string\"] },\n" +
            "  \"masterIdea\": { \"coreConcept\": \"string\", \"centralMessage\": \"string\", \"emotionalAngle\": \"string\", \"valueProposition\": \"string\", \"platformIdea\": \"string\" },\n" +
            "  \"campaignLineOptions\": [\"string\", \"string\", \"string\"],\n" +
            "  \"storyboard\": { \"hook\": \"string\", \"setup\": \"string\", \"tensionOrProblem\": \"string\", \"solution\": \"string\", \"payoff\": \"string\", \"cta\": \"string\", \"scenes\": [{ \"order\": 1, \"title\": \"string\", \"purpose\": \"string\", \"visual\": \"string\", \"copyOrDialogue\": \"string\", \"onScreenText\": \"string\", \"duration\": \"string\" }] },\n" +
            "  \"channelAdaptations\": [{ \"channel\": \"string\", \"format\": \"string\", \"headlineOrHook\": \"string\", \"primaryCopy\": \"string\", \"cta\": \"string\", \"visualDirection\": \"string\", \"voiceoverOrAudio\": \"string\", \"recommendedDirection\": \"string\", \"adapterPrompt\": \"string\", \"sections\": [{ \"label\": \"string\", \"content\": \"string\" }], \"versions\": [{ \"label\": \"v1\", \"intent\": \"safe\", \"headlineOrHook\": \"string\", \"primaryCopy\": \"string\", \"cta\": \"string\" }, { \"label\": \"v2\", \"intent\": \"bolder\", \"headlineOrHook\": \"string\", \"primaryCopy\": \"string\", \"cta\": \"string\" }, { \"label\": \"v3\", \"intent\": \"experimental\", \"headlineOrHook\": \"string\", \"primaryCopy\": \"string\", \"cta\": \"string\" }], \"productionAssets\": [\"string\"] }],\n" +
            "  \"visualDirection\": { \"lookAndFeel\": \"string\", \"typography\": \"string\", \"colorDirection\": \"string\", \"composition\": \"string\", \"imageGenerationPrompts\": [\"string\"] },\n" +
            "  \"audioVoiceNotes\": [\"string\"],\n" +
            "  \"productionNotes\": [\"string\"],\n" +
            "  \"optionalVariations\": [\"string\"]\n" +
            "}\n\n" +
            "Creative Manager brief:\n" +
            $"- Prompt: {request.Prompt.Trim()}\n" +
            $"- Brand: {summary.Brand}\n" +
            $"- Product: {summary.Product}\n" +
            $"- Audience: {summary.Audience}\n" +
            $"- Objective: {summary.Objective}\n" +
            $"- Tone: {summary.Tone}\n" +
            $"- Channels: {(summary.Channels.Count > 0 ? string.Join(", ", summary.Channels) : "Not specified")}\n" +
            $"- CTA: {summary.Cta}\n" +
            $"- Constraints: {(summary.Constraints.Count > 0 ? string.Join(" | ", summary.Constraints) : "None specified")}\n" +
            $"- Assumptions already made: {(summary.Assumptions.Count > 0 ? string.Join(" | ", summary.Assumptions) : "None")}\n\n" +
            "Advertified campaign context:\n" +
            $"- Campaign name: {ResolveCampaignName(campaign)}\n" +
            $"- Business name: {campaign.User?.BusinessProfile?.BusinessName ?? campaign.User?.FullName ?? "Not captured"}\n" +
            $"- Package band: {campaign.PackageBand?.Name ?? "Not captured"}\n" +
            $"- Budget: R {(campaign.PackageOrder?.SelectedBudget ?? campaign.PackageOrder?.Amount ?? 0):N0}\n" +
            $"- Recommendation summary: {recommendationSummary}\n" +
            $"- Brief notes: {(string.IsNullOrWhiteSpace(briefNotes) ? "Not captured" : briefNotes)}\n\n" +
            "Channel adaptation engine:\n" +
            BuildChannelPromptRules(summary, request);
    }

    private static CreativeSystemResponse BuildFallbackResponse(
        CampaignEntity campaign,
        CampaignBriefEntity? brief,
        GenerateCreativeSystemRequest request)
    {
        var summary = BuildCampaignSummary(campaign, brief, request);
        var platformIdea = $"{summary.Product}: turn attention into immediate action.";
        var storyboard = BuildScenes(summary);

        return Normalize(new CreativeSystemResponse
        {
            CampaignSummary = summary,
            MasterIdea = new CreativeMasterIdeaResponse
            {
                CoreConcept = $"Make {summary.Product} feel like the obvious next move for {summary.Audience}.",
                CentralMessage = $"{summary.Product} gives the audience a clear reason to act now instead of delaying.",
                EmotionalAngle = "Confidence, momentum, and visible progress.",
                ValueProposition = $"{summary.Product} removes friction and makes the benefit feel immediate.",
                PlatformIdea = platformIdea
            },
            CampaignLineOptions = new[]
            {
                $"{summary.Product}. Move now.",
                $"Choose {summary.Product}. Keep moving.",
                $"{summary.Product} that turns attention into action."
            },
            Storyboard = storyboard,
            ChannelAdaptations = summary.Channels.Select(channel => BuildChannelAdaptation(channel, summary)).ToArray(),
            VisualDirection = new CreativeVisualDirectionResponse
            {
                LookAndFeel = "Bold, clean, commercially polished, and built for fast comprehension.",
                Typography = "Heavy headline type, compact support copy, and a hard CTA hierarchy.",
                ColorDirection = "One dominant brand color, one contrast accent, and disciplined neutrals.",
                Composition = "Single focal point, short headline, decisive CTA placement, and minimal clutter.",
                ImageGenerationPrompts = new[]
                {
                    $"{summary.Brand} commercial campaign key visual for {summary.Product}, bold focal subject, premium advertising style, clean headline space",
                    $"{summary.Brand} short-form video frame for {summary.Product}, dynamic composition, premium lighting, strong CTA space"
                }
            },
            AudioVoiceNotes = new[]
            {
                "Keep the delivery direct, confident, and commercially warm.",
                "Lead with the audience tension quickly, then land the offer and CTA cleanly."
            },
            ProductionNotes = new[]
            {
                "Keep the campaign line and CTA consistent across every asset.",
                "Build for immediate first-glance readability before adding supporting detail.",
                "Prepare editable variants for shorter, bolder, or more premium revisions."
            },
            OptionalVariations = new[]
            {
                "Shorter: compress to one promise, one proof point, one CTA.",
                "Bolder: raise contrast, simplify language, and sharpen the payoff.",
                "More premium: reduce noise, increase restraint, and elevate finishing."
            }
        });
    }

    private static CreativeCampaignSummaryResponse BuildCampaignSummary(
        CampaignEntity campaign,
        CampaignBriefEntity? brief,
        GenerateCreativeSystemRequest request)
    {
        var assumptions = new List<string>();

        static string Pick(string? directValue, string? fallbackValue, string assumption, List<string> assumptions)
        {
            if (!string.IsNullOrWhiteSpace(directValue))
            {
                return directValue.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fallbackValue))
            {
                assumptions.Add(assumption);
                return fallbackValue.Trim();
            }

            assumptions.Add(assumption);
            return "Not specified";
        }

        var channels = request.Channels
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Select(channel => channel.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (channels.Count == 0)
        {
            channels = InferChannels(campaign, brief);
            if (channels.Count > 0)
            {
                assumptions.Add("Channels inferred from campaign brief and recommendation mix.");
            }
        }

        if (channels.Count == 0)
        {
            channels = new List<string> { "Billboard", "Display", "Radio", "Video/TV" };
            assumptions.Add("Channels assumed for a standard multi-channel rollout.");
        }

        var constraints = request.Constraints
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (brief?.CreativeReady is false)
        {
            constraints.Add("Source creative assets may still need to be gathered.");
        }

        return new CreativeCampaignSummaryResponse
        {
            Brand = Pick(request.Brand, campaign.User?.BusinessProfile?.BusinessName, "Brand inferred from the campaign business profile.", assumptions),
            Product = Pick(request.Product, ResolveCampaignName(campaign), "Product inferred from the campaign name.", assumptions),
            Audience = Pick(request.Audience, brief?.TargetAudienceNotes, "Audience inferred from campaign brief notes.", assumptions),
            Objective = Pick(request.Objective, brief?.Objective, "Objective inferred from campaign brief.", assumptions),
            Tone = Pick(request.Tone, InferTone(brief), "Tone inferred from creative notes and campaign context.", assumptions),
            Channels = channels,
            Cta = Pick(request.Cta, "Contact Advertified to get started", "CTA assumed because none was provided.", assumptions),
            Constraints = constraints,
            Assumptions = assumptions
        };
    }

    private static List<string> InferChannels(CampaignEntity campaign, CampaignBriefEntity? brief)
    {
        var channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in brief?.GetList(nameof(CampaignBriefEntity.PreferredMediaTypesJson)) ?? Enumerable.Empty<string>())
        {
            if (TryNormalizeChannel(value, out var normalized))
            {
                channels.Add(normalized);
            }
        }

        foreach (var value in campaign.CampaignRecommendations
                     .SelectMany(recommendation => recommendation.RecommendationItems)
                     .Select(item => item.InventoryType))
        {
            if (TryNormalizeChannel(value, out var normalized))
            {
                channels.Add(normalized);
            }
        }

        return channels.ToList();
    }

    private static bool TryNormalizeChannel(string? value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant() switch
        {
            "ooh" or "billboard" or "outdoor" => "Billboard",
            "radio" => "Radio",
            "tv" or "television" or "video" => "Video/TV",
            "digital" or "display" => "Display",
            "sms" => "SMS",
            "tiktok" => "TikTok",
            "social" or "social static" or "meta" or "meta/social static" => "Social Static",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(normalized);
    }

    private static string ResolveCampaignName(CampaignEntity campaign)
    {
        if (!string.IsNullOrWhiteSpace(campaign.CampaignName))
        {
            return campaign.CampaignName.Trim();
        }

        return campaign.PackageBand?.Name is { Length: > 0 } packageName
            ? $"{packageName} campaign"
            : "Campaign";
    }

    private static string? InferTone(CampaignBriefEntity? brief)
    {
        var source = string.Join(" ", new[] { brief?.CreativeNotes, brief?.SpecialRequirements, brief?.TargetAudienceNotes })
            .ToLowerInvariant();

        if (source.Contains("premium")) return "Premium and restrained";
        if (source.Contains("bold") || source.Contains("high impact")) return "Bold and high-contrast";
        if (source.Contains("gen z") || source.Contains("youth")) return "Fast, current, and expressive";
        if (source.Contains("performance")) return "Performance-led and direct";
        return "Clear, confident, and commercially sharp";
    }

    private static CreativeNarrativeResponse BuildScenes(CreativeCampaignSummaryResponse summary)
    {
        return new CreativeNarrativeResponse
        {
            Hook = $"Show the audience why waiting is the wrong move, then introduce {summary.Product}.",
            Setup = $"{summary.Brand} enters with a direct promise tied to {summary.Objective}.",
            TensionOrProblem = "The audience wants momentum or visible results, but the current path feels slow or uncertain.",
            Solution = $"{summary.Product} reframes the decision as simple, immediate, and worthwhile.",
            Payoff = "The audience sees a clear win and a confident next step.",
            Cta = summary.Cta,
            Scenes = new[]
            {
                new CreativeSceneResponse { Order = 1, Title = "Hook", Purpose = "Stop attention immediately.", Visual = "Single high-contrast image that dramatizes the audience problem.", CopyOrDialogue = $"Still waiting? {summary.Product} moves now.", OnScreenText = "Move now", Duration = "0-3s" },
                new CreativeSceneResponse { Order = 2, Title = "Problem", Purpose = "Make the tension explicit.", Visual = "Show friction, delay, or missed momentum in one readable frame.", CopyOrDialogue = "The longer the wait, the harder the move.", OnScreenText = "Waiting costs momentum", Duration = "3-7s" },
                new CreativeSceneResponse { Order = 3, Title = "Solution", Purpose = "Introduce the offer as the answer.", Visual = "Product-forward reveal with clean branded framing.", CopyOrDialogue = $"{summary.Product} gives the audience a clear reason to act now.", OnScreenText = summary.Brand, Duration = "7-12s" },
                new CreativeSceneResponse { Order = 4, Title = "Payoff", Purpose = "Land the benefit and CTA.", Visual = "Confident end frame with brand, benefit, and CTA.", CopyOrDialogue = summary.Cta, OnScreenText = summary.Cta, Duration = "12-15s" }
            }
        };
    }

    private static CreativeChannelAdaptationResponse BuildChannelAdaptation(string channel, CreativeCampaignSummaryResponse summary)
    {
        return channel switch
        {
            "Billboard" => BuildBillboardAdaptation(summary),
            "Radio" => BuildRadioAdaptation(summary),
            "Display" => BuildDisplayAdaptation(summary),
            "SMS" => BuildSmsAdaptation(summary),
            "TikTok" => BuildTikTokAdaptation(summary),
            "Video/TV" => BuildVideoAdaptation(summary),
            "Social Static" => BuildSocialStaticAdaptation(summary),
            _ => BuildGenericAdaptation(channel, summary)
        };
    }

    private static string BuildChannelPromptRules(CreativeCampaignSummaryResponse summary, GenerateCreativeSystemRequest request)
    {
        var remixInstruction = string.IsNullOrWhiteSpace(request.IterationLabel)
            ? "No extra remix instruction."
            : $"Iteration label: {request.IterationLabel.Trim()}. Tighten or remix the channel executions without breaking the master idea unless the brief explicitly asks for a concept shift.";

        return string.Join(
            "\n\n",
            new[]
            {
                "Use this universal adapter shell internally for every channel:",
                "You are the Channel Adaptation Engine for Advertified Creative Studio.\n" +
                "Your job is to adapt a master campaign into a specific channel format.\n" +
                "You MUST preserve the core campaign idea, adapt to the channel's native behavior, respect format constraints, and produce production-ready output.\n" +
                "You MUST NOT reuse copy directly from other channels, generalize the idea, or break the campaign's core message.\n" +
                $"Preserve this message spine: product {summary.Product}, audience {summary.Audience}, objective {summary.Objective}, CTA {summary.Cta}, tone {summary.Tone}.",
                $"Version every channel adaptation as v1 safe, v2 bolder, and v3 experimental. {remixInstruction}",
                string.Join("\n\n", summary.Channels.Select(BuildSingleChannelPromptRule))
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildSingleChannelPromptRule(string channel)
    {
        return channel switch
        {
            "Billboard" =>
                "CHANNEL: BILLBOARD\n" +
                "Rules: Max 6-8 words, readable in under 3 seconds, no complex phrasing, one idea only, high-contrast language, brand or product clear or implied, CTA optional and minimal.\n" +
                "Output: 5 headline options, 1 recommended direction, visual direction (simple, bold, iconic).\n" +
                "Focus: Clarity over cleverness. Impact over explanation.",
            "TikTok" =>
                "CHANNEL: TIKTOK\n" +
                "Rules: Hook in first 1-3 seconds, native not corporate, fast pacing, pattern interruption, conversational language.\n" +
                "Output: Hook (3 variations), full script (15-30 sec), scene breakdown, on-screen text, CTA.\n" +
                "Focus: Scroll-stopping opening. Relatability plus momentum.",
            "Radio" =>
                "CHANNEL: RADIO\n" +
                "Rules: 15 or 30 seconds max, conversational tone, clear pacing, no visual references, one-listen comprehension, strong CTA at end.\n" +
                "Output: Script (timed), voice direction, key emphasis points, CTA line.\n" +
                "Focus: Clarity, audio rhythm, memorability.",
            "SMS" =>
                "CHANNEL: SMS\n" +
                "Rules: Prefer max 160 characters, personal and direct, one clear action, no fluff, compliant and readable.\n" +
                "Output: 3 variations, CTA link placeholder, optional urgency variant.\n" +
                "Focus: Action over storytelling.",
            "Display" =>
                "CHANNEL: DISPLAY ADS\n" +
                "Rules: Short and benefit-driven, clear hierarchy headline to support to CTA, scannable in under 2 seconds, avoid clutter.\n" +
                "Output: Headline options (3-5), supporting line, CTA button text, visual direction.\n" +
                "Focus: Conversion clarity.",
            "Video/TV" =>
                "CHANNEL: VIDEO / TV\n" +
                "Rules: Structured narrative, visual-first storytelling, each scene must progress the message, brand integration before the end, strong payoff.\n" +
                "Output: Scene-by-scene breakdown, voiceover script, visual direction per scene, supers/text overlays, CTA ending.\n" +
                "Focus: Narrative clarity. Visual storytelling.",
            "Social Static" =>
                "CHANNEL: SOCIAL STATIC\n" +
                "Rules: Thumb-stopping, minimal text, strong visual-message pairing, platform-native tone.\n" +
                "Output: Primary text, headline, caption options, visual concept.\n" +
                "Focus: Stop the scroll.",
            _ =>
                $"CHANNEL: {channel.ToUpperInvariant()}\n" +
                "Rules: Adapt natively, protect the core idea, keep the execution channel-specific and production-ready.\n" +
                "Output: Hook or headline, primary copy, CTA, visual direction.\n" +
                "Focus: Native clarity."
        };
    }

    private static CreativeChannelAdaptationResponse BuildBillboardAdaptation(CreativeCampaignSummaryResponse summary)
    {
        var headline = $"{summary.Product}. Move now.";
        return BuildAdaptation(
            channel: "Billboard",
            format: "Ultra-short outdoor",
            headlineOrHook: headline,
            primaryCopy: "One message. One benefit. One action.",
            cta: summary.Cta,
            visualDirection: "Massive focal visual, six words or fewer, instant readability.",
            voiceoverOrAudio: null,
            recommendedDirection: "Choose the shortest, hardest-working line and let the image do the rest.",
            sections: new[]
            {
                Section("Headline options", $"{summary.Product}. Move now. | Don't wait. Move. | {summary.Brand} moves faster. | Choose {summary.Product}. | Start with {summary.Product}."),
                Section("Recommended direction", headline),
                Section("Visual direction", "Simple, bold, iconic. One subject. One line. Maximum contrast.")
            },
            versions: BuildVersions(summary, headline, "Sharpen readability before adding any cleverness."),
            adapterPrompt: BuildResolvedAdapterPrompt("Billboard", summary),
            productionAssets: new[] { "Key visual", "Headline lockup", "Brand end board" });
    }

    private static CreativeChannelAdaptationResponse BuildTikTokAdaptation(CreativeCampaignSummaryResponse summary)
    {
        var hook = "Wait, why are you still doing it the slow way?";
        return BuildAdaptation(
            channel: "TikTok",
            format: "Native short-form social video",
            headlineOrHook: hook,
            primaryCopy: $"Start with the tension, flip it with {summary.Product}, then close on payoff and CTA without sounding like an ad read.",
            cta: summary.Cta,
            visualDirection: "Fast cuts, direct-to-camera or POV, kinetic captions, one clear visual thesis per beat.",
            voiceoverOrAudio: "Punchy, current, and paced for thumb-stop retention.",
            recommendedDirection: "Lead with pattern interruption, then keep every beat moving toward payoff.",
            sections: new[]
            {
                Section("Hook (3 variations)", $"{hook} | Stop scrolling: this is the easier move. | Nobody tells you this part until now."),
                Section("Full script (15-30 sec)", $"Open on tension, show the friction, reveal {summary.Product}, land the payoff, then close with {summary.Cta}."),
                Section("Scene breakdown", "1) Interrupt. 2) Relatable problem. 3) Product flip. 4) Benefit. 5) CTA."),
                Section("On-screen text", "Move now | Waiting costs momentum | Easier starts here"),
                Section("CTA", summary.Cta)
            },
            versions: BuildVersions(summary, hook, "Push pace and contrast while staying native."),
            adapterPrompt: BuildResolvedAdapterPrompt("TikTok", summary),
            productionAssets: new[] { "15s script", "Caption set", "Opening frame options" });
    }

    private static CreativeChannelAdaptationResponse BuildRadioAdaptation(CreativeCampaignSummaryResponse summary)
    {
        var headline = "Open with the tension, then land the offer fast.";
        return BuildAdaptation(
            channel: "Radio",
            format: "30-second radio",
            headlineOrHook: headline,
            primaryCopy: $"If the audience is ready for {summary.Objective}, {summary.Product} is the next move. Don't wait for perfect. Move with what works.",
            cta: summary.Cta,
            visualDirection: "No visual layer. Build rhythm through pacing and a decisive final line.",
            voiceoverOrAudio: "Conversational delivery, warm authority, urgency in the final five seconds.",
            recommendedDirection: "Optimize for one-listen clarity and let the CTA own the final beat.",
            sections: new[]
            {
                Section("Script (timed)", $"0-5s problem setup. 5-18s introduce {summary.Product}. 18-26s benefit and payoff. 26-30s CTA: {summary.Cta}."),
                Section("Voice direction", "Conversational, confident, commercially warm."),
                Section("Key emphasis points", $"Audience tension | {summary.Product} as solution | {summary.Cta}"),
                Section("CTA line", summary.Cta)
            },
            versions: BuildVersions(summary, headline, "Use rhythm and recall to carry the message."),
            adapterPrompt: BuildResolvedAdapterPrompt("Radio", summary),
            productionAssets: new[] { "30s script", "15s cutdown", "VO direction" });
    }

    private static CreativeChannelAdaptationResponse BuildSmsAdaptation(CreativeCampaignSummaryResponse summary)
    {
        var headline = $"{summary.Brand}: {summary.Product}.";
        return BuildAdaptation(
            channel: "SMS",
            format: "Direct-response SMS",
            headlineOrHook: headline,
            primaryCopy: $"Act now. {summary.Cta}",
            cta: summary.Cta,
            visualDirection: "Text only. Every word must earn space.",
            voiceoverOrAudio: null,
            recommendedDirection: "Keep it direct, personal, and focused on one action.",
            sections: new[]
            {
                Section("3 variations", $"{summary.Brand}: {summary.Product}. {summary.Cta} [LINK] | Ready to move? {summary.Product} is here. {summary.Cta} [LINK] | Don't wait. {summary.Cta} [LINK]"),
                Section("CTA link placeholder", "[LINK]"),
                Section("Optional urgency variant", $"Today only: {summary.Cta} [LINK]")
            },
            versions: BuildVersions(summary, headline, "Trim harder and increase urgency carefully."),
            adapterPrompt: BuildResolvedAdapterPrompt("SMS", summary),
            productionAssets: new[] { "160-char version", "Short link", "Compliant opt-out variant" });
    }

    private static CreativeChannelAdaptationResponse BuildDisplayAdaptation(CreativeCampaignSummaryResponse summary)
    {
        var headline = $"{summary.Product} that turns attention into action.";
        return BuildAdaptation(
            channel: "Display",
            format: "Static and animated display",
            headlineOrHook: headline,
            primaryCopy: "Lead with the strongest audience benefit, then support it with one friction-free proof point.",
            cta: summary.Cta,
            visualDirection: "Product or outcome-led frame, clean CTA button, one message per panel.",
            voiceoverOrAudio: null,
            recommendedDirection: "Build the hierarchy so the value lands before the support line finishes scanning.",
            sections: new[]
            {
                Section("Headline options", $"{headline} | Move sooner with {summary.Product} | Less friction. More action."),
                Section("Supporting line", $"Built for {summary.Audience} who want {summary.Objective}."),
                Section("CTA button text", "Get started"),
                Section("Visual direction", "Short, benefit-led, uncluttered, with strong button contrast.")
            },
            versions: BuildVersions(summary, headline, "Increase conversion pressure through hierarchy, not clutter."),
            adapterPrompt: BuildResolvedAdapterPrompt("Display", summary),
            productionAssets: new[] { "HTML5 resize set", "Static fallback set", "CTA variants" });
    }

    private static CreativeChannelAdaptationResponse BuildVideoAdaptation(CreativeCampaignSummaryResponse summary)
    {
        var headline = "Open with tension, build clarity, close with payoff.";
        return BuildAdaptation(
            channel: "Video/TV",
            format: "15s or 30s structured video",
            headlineOrHook: headline,
            primaryCopy: "Drive the same master idea through a clean scene progression: problem, shift, solution, payoff, CTA.",
            cta: summary.Cta,
            visualDirection: "Polished cinematography, strong hierarchy, memorable end board.",
            voiceoverOrAudio: "Controlled authority with enough energy to carry progression.",
            recommendedDirection: "Keep the narrative visual-first, but ensure the brand lands before the final payoff.",
            sections: new[]
            {
                Section("Scene-by-scene breakdown", "1) Hook. 2) Problem. 3) Product reveal. 4) Payoff. 5) CTA end frame."),
                Section("Voiceover script", $"Lead with tension, introduce {summary.Product}, deliver the value, and close with {summary.Cta}."),
                Section("Visual direction per scene", "Escalate clarity scene by scene and end on a clean brand board."),
                Section("Supers / text overlays", "Move now | Stop waiting | Clear next step"),
                Section("CTA ending", summary.Cta)
            },
            versions: BuildVersions(summary, headline, "Push narrative contrast while keeping the commercial message clear."),
            adapterPrompt: BuildResolvedAdapterPrompt("Video/TV", summary),
            productionAssets: new[] { "Scene board", "VO script", "Supers list", "End board pack" });
    }

    private static CreativeChannelAdaptationResponse BuildSocialStaticAdaptation(CreativeCampaignSummaryResponse summary)
    {
        var headline = $"{summary.Product}. Stop scrolling.";
        return BuildAdaptation(
            channel: "Social Static",
            format: "Thumb-stopping social static",
            headlineOrHook: headline,
            primaryCopy: "Pair a minimal line with an image that does most of the persuasion work.",
            cta: summary.Cta,
            visualDirection: "Minimal text, strong visual-message pairing, instant feed readability.",
            voiceoverOrAudio: null,
            recommendedDirection: "Let the visual carry the surprise and keep the copy stripped down.",
            sections: new[]
            {
                Section("Primary text", $"{summary.Product} gives {summary.Audience} a faster next move."),
                Section("Headline", headline),
                Section("Caption options", $"{summary.Cta} | The easier move starts here. | Ready when you are."),
                Section("Visual concept", "Single arresting image with one short line and a clear brand anchor.")
            },
            versions: BuildVersions(summary, headline, "Make the feed-stop stronger while staying platform-native."),
            adapterPrompt: BuildResolvedAdapterPrompt("Social Static", summary),
            productionAssets: new[] { "Primary static", "Caption variants", "Headline lockup" });
    }

    private static CreativeChannelAdaptationResponse BuildGenericAdaptation(string channel, CreativeCampaignSummaryResponse summary)
    {
        var headline = $"{summary.Product} with a clear next move.";
        return BuildAdaptation(
            channel: channel,
            format: "Channel-native adaptation",
            headlineOrHook: headline,
            primaryCopy: $"Adapt the master idea for {channel} while preserving the same benefit, tension, and CTA spine.",
            cta: summary.Cta,
            visualDirection: "Keep the same core visual logic, then compress or expand by channel behavior.",
            voiceoverOrAudio: null,
            recommendedDirection: "Hold the core idea steady and tune the execution behavior for the channel.",
            sections: new[]
            {
                Section("Hook / headline", headline),
                Section("Primary copy", $"One clear adaptation for {channel}."),
                Section("CTA", summary.Cta),
                Section("Visual direction", "Native, sharp, production-ready.")
            },
            versions: BuildVersions(summary, headline, $"Tighten and remix specifically for {channel}."),
            adapterPrompt: BuildResolvedAdapterPrompt(channel, summary),
            productionAssets: new[] { "Primary copy", "Channel variant", "CTA lockup" });
    }

    private static CreativeChannelAdaptationResponse BuildAdaptation(
        string channel,
        string format,
        string headlineOrHook,
        string primaryCopy,
        string cta,
        string visualDirection,
        string? voiceoverOrAudio,
        string recommendedDirection,
        IReadOnlyList<CreativeChannelSectionResponse> sections,
        IReadOnlyList<CreativeChannelVersionResponse> versions,
        string adapterPrompt,
        IReadOnlyList<string> productionAssets)
    {
        return new CreativeChannelAdaptationResponse
        {
            Channel = channel,
            Format = format,
            HeadlineOrHook = headlineOrHook,
            PrimaryCopy = primaryCopy,
            Cta = cta,
            VisualDirection = visualDirection,
            VoiceoverOrAudio = voiceoverOrAudio,
            RecommendedDirection = recommendedDirection,
            AdapterPrompt = adapterPrompt,
            Sections = sections,
            Versions = versions,
            ProductionAssets = productionAssets
        };
    }

    private static CreativeChannelSectionResponse Section(string label, string content)
        => new()
        {
            Label = label,
            Content = content
        };

    private static IReadOnlyList<CreativeChannelVersionResponse> BuildVersions(
        CreativeCampaignSummaryResponse summary,
        string baseHeadline,
        string variationInstruction)
    {
        return new[]
        {
            new CreativeChannelVersionResponse
            {
                Label = "v1",
                Intent = "safe",
                HeadlineOrHook = baseHeadline,
                PrimaryCopy = $"Safe version: keep the message highly legible for {summary.Audience} and stay close to the central CTA. {variationInstruction}",
                Cta = summary.Cta
            },
            new CreativeChannelVersionResponse
            {
                Label = "v2",
                Intent = "bolder",
                HeadlineOrHook = $"{summary.Product}. Don't wait.",
                PrimaryCopy = $"Bolder version: increase contrast, urgency, and distinctiveness while keeping the campaign commercially usable. {variationInstruction}",
                Cta = summary.Cta
            },
            new CreativeChannelVersionResponse
            {
                Label = "v3",
                Intent = "experimental",
                HeadlineOrHook = $"What happens when {summary.Product} hits at the right moment?",
                PrimaryCopy = $"Experimental version: take a bigger swing, add more surprise or tension, but preserve the same campaign message spine and CTA. {variationInstruction}",
                Cta = summary.Cta
            }
        };
    }

    private static string BuildResolvedAdapterPrompt(string channel, CreativeCampaignSummaryResponse summary)
    {
        return
            "You are the Channel Adaptation Engine for Advertified Creative Studio.\n" +
            "Your job is to adapt a master campaign into a specific channel format.\n" +
            "You MUST preserve the core campaign idea, adapt to the channel's native behavior, respect format constraints, and produce production-ready output.\n" +
            "You MUST NOT reuse copy directly from other channels, generalize the idea, or break the campaign's core message.\n\n" +
            "MASTER CAMPAIGN INPUT\n" +
            $"{summary.Product} should feel like the obvious next move for {summary.Audience}.\n" +
            $"Key message: {summary.Product} helps achieve {summary.Objective}.\n" +
            $"CTA: {summary.Cta}\n" +
            $"Tone: {summary.Tone}\n\n" +
            "CHANNEL TO ADAPT\n" +
            $"{channel}\n\n" +
            "OUTPUT REQUIREMENTS\n" +
            "Follow the channel rules strictly. Output must be clean, structured, and ready for production.";
    }

    private static CreativeSystemResponse Normalize(CreativeSystemResponse response)
    {
        response.CampaignSummary ??= new CreativeCampaignSummaryResponse();
        response.MasterIdea ??= new CreativeMasterIdeaResponse();
        response.Storyboard ??= new CreativeNarrativeResponse();
        response.VisualDirection ??= new CreativeVisualDirectionResponse();
        response.CampaignLineOptions ??= Array.Empty<string>();
        response.ChannelAdaptations ??= Array.Empty<CreativeChannelAdaptationResponse>();
        response.AudioVoiceNotes ??= Array.Empty<string>();
        response.ProductionNotes ??= Array.Empty<string>();
        response.OptionalVariations ??= Array.Empty<string>();
        response.CampaignSummary.Channels ??= Array.Empty<string>();
        response.CampaignSummary.Constraints ??= Array.Empty<string>();
        response.CampaignSummary.Assumptions ??= Array.Empty<string>();
        response.Storyboard.Scenes ??= Array.Empty<CreativeSceneResponse>();
        response.VisualDirection.ImageGenerationPrompts ??= Array.Empty<string>();

        foreach (var item in response.ChannelAdaptations)
        {
            item.RecommendedDirection ??= string.Empty;
            item.AdapterPrompt ??= string.Empty;
            item.Sections ??= Array.Empty<CreativeChannelSectionResponse>();
            item.Versions ??= Array.Empty<CreativeChannelVersionResponse>();
            item.ProductionAssets ??= Array.Empty<string>();
        }

        return response;
    }

    private sealed class OpenAiChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("response_format")]
        public OpenAiResponseFormat ResponseFormat { get; set; } = new();

        [JsonPropertyName("messages")]
        public IReadOnlyList<OpenAiChatMessage> Messages { get; set; } = Array.Empty<OpenAiChatMessage>();
    }

    private sealed class OpenAiResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }

    private sealed class OpenAiChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OpenAiChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public IReadOnlyList<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiChatMessage? Message { get; set; }
    }
}

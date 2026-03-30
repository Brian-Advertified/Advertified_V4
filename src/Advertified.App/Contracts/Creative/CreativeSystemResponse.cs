namespace Advertified.App.Contracts.Creative;

public sealed class CreativeSystemResponse
{
    public CreativeCampaignSummaryResponse CampaignSummary { get; set; } = new();
    public CreativeMasterIdeaResponse MasterIdea { get; set; } = new();
    public IReadOnlyList<string> CampaignLineOptions { get; set; } = Array.Empty<string>();
    public CreativeNarrativeResponse Storyboard { get; set; } = new();
    public IReadOnlyList<CreativeChannelAdaptationResponse> ChannelAdaptations { get; set; } = Array.Empty<CreativeChannelAdaptationResponse>();
    public CreativeVisualDirectionResponse VisualDirection { get; set; } = new();
    public IReadOnlyList<string> AudioVoiceNotes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ProductionNotes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> OptionalVariations { get; set; } = Array.Empty<string>();
}

public sealed class CreativeCampaignSummaryResponse
{
    public string Brand { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public IReadOnlyList<string> Channels { get; set; } = Array.Empty<string>();
    public string Cta { get; set; } = string.Empty;
    public IReadOnlyList<string> Constraints { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Assumptions { get; set; } = Array.Empty<string>();
}

public sealed class CreativeMasterIdeaResponse
{
    public string CoreConcept { get; set; } = string.Empty;
    public string CentralMessage { get; set; } = string.Empty;
    public string EmotionalAngle { get; set; } = string.Empty;
    public string ValueProposition { get; set; } = string.Empty;
    public string PlatformIdea { get; set; } = string.Empty;
}

public sealed class CreativeNarrativeResponse
{
    public string Hook { get; set; } = string.Empty;
    public string Setup { get; set; } = string.Empty;
    public string TensionOrProblem { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string Payoff { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
    public IReadOnlyList<CreativeSceneResponse> Scenes { get; set; } = Array.Empty<CreativeSceneResponse>();
}

public sealed class CreativeSceneResponse
{
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Visual { get; set; } = string.Empty;
    public string CopyOrDialogue { get; set; } = string.Empty;
    public string? OnScreenText { get; set; }
    public string? Duration { get; set; }
}

public sealed class CreativeChannelAdaptationResponse
{
    public string Channel { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string HeadlineOrHook { get; set; } = string.Empty;
    public string PrimaryCopy { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
    public string VisualDirection { get; set; } = string.Empty;
    public string? VoiceoverOrAudio { get; set; }
    public string RecommendedDirection { get; set; } = string.Empty;
    public string AdapterPrompt { get; set; } = string.Empty;
    public IReadOnlyList<CreativeChannelSectionResponse> Sections { get; set; } = Array.Empty<CreativeChannelSectionResponse>();
    public IReadOnlyList<CreativeChannelVersionResponse> Versions { get; set; } = Array.Empty<CreativeChannelVersionResponse>();
    public IReadOnlyList<string> ProductionAssets { get; set; } = Array.Empty<string>();
}

public sealed class CreativeChannelSectionResponse
{
    public string Label { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class CreativeChannelVersionResponse
{
    public string Label { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string HeadlineOrHook { get; set; } = string.Empty;
    public string PrimaryCopy { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
}

public sealed class CreativeVisualDirectionResponse
{
    public string LookAndFeel { get; set; } = string.Empty;
    public string Typography { get; set; } = string.Empty;
    public string ColorDirection { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public IReadOnlyList<string> ImageGenerationPrompts { get; set; } = Array.Empty<string>();
}

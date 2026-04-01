namespace Advertified.App.AIPlatform.Api;

public sealed class VoicePackResponse
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "ElevenLabs";
    public string Name { get; set; } = string.Empty;
    public string? Accent { get; set; }
    public string? Language { get; set; }
    public string? Tone { get; set; }
    public string? Persona { get; set; }
    public string[] UseCases { get; set; } = Array.Empty<string>();
    public string? SampleAudioUrl { get; set; }
    public string PromptTemplate { get; set; } = string.Empty;
    public string PricingTier { get; set; } = "standard";
    public bool IsClientSpecific { get; set; }
    public bool IsClonedVoice { get; set; }
    public string[] AudienceTags { get; set; } = Array.Empty<string>();
    public string[] ObjectiveTags { get; set; } = Array.Empty<string>();
    public int SortOrder { get; set; }
}

public sealed class VoicePackRecommendationResponse
{
    public Guid VoicePackId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
}

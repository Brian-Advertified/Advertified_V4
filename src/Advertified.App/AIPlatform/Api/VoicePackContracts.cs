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
    public int SortOrder { get; set; }
}

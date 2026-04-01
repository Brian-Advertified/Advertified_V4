namespace Advertified.App.Configuration;

public sealed class ElevenLabsOptions
{
    public const string SectionName = "ElevenLabs";

    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.elevenlabs.io";
    public string DefaultVoiceId { get; set; } = string.Empty;
    public string DefaultModelId { get; set; } = "eleven_multilingual_v2";
    public int TimeoutSeconds { get; set; } = 60;
}


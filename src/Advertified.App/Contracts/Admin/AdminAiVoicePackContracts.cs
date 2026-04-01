namespace Advertified.App.Contracts.Admin;

public sealed class AdminAiVoicePackResponse
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "ElevenLabs";
    public string Name { get; set; } = string.Empty;
    public string? Accent { get; set; }
    public string? Language { get; set; }
    public string? Tone { get; set; }
    public string? Persona { get; set; }
    public string[] UseCases { get; set; } = Array.Empty<string>();
    public string VoiceId { get; set; } = string.Empty;
    public string? SampleAudioUrl { get; set; }
    public string PromptTemplate { get; set; } = string.Empty;
    public string PricingTier { get; set; } = "standard";
    public bool IsClientSpecific { get; set; }
    public Guid? ClientUserId { get; set; }
    public bool IsClonedVoice { get; set; }
    public string[] AudienceTags { get; set; } = Array.Empty<string>();
    public string[] ObjectiveTags { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UpsertAdminAiVoicePackRequest
{
    public string Provider { get; set; } = "ElevenLabs";
    public string Name { get; set; } = string.Empty;
    public string? Accent { get; set; }
    public string? Language { get; set; }
    public string? Tone { get; set; }
    public string? Persona { get; set; }
    public string[] UseCases { get; set; } = Array.Empty<string>();
    public string VoiceId { get; set; } = string.Empty;
    public string? SampleAudioUrl { get; set; }
    public string PromptTemplate { get; set; } = string.Empty;
    public string PricingTier { get; set; } = "standard";
    public bool IsClientSpecific { get; set; }
    public Guid? ClientUserId { get; set; }
    public bool IsClonedVoice { get; set; }
    public string[] AudienceTags { get; set; } = Array.Empty<string>();
    public string[] ObjectiveTags { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

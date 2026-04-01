namespace Advertified.App.Contracts.Admin;

public sealed class AdminAiVoiceProfileResponse
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "ElevenLabs";
    public string Label { get; set; } = string.Empty;
    public string VoiceId { get; set; } = string.Empty;
    public string? Language { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UpsertAdminAiVoiceProfileRequest
{
    public string Provider { get; set; } = "ElevenLabs";
    public string Label { get; set; } = string.Empty;
    public string VoiceId { get; set; } = string.Empty;
    public string? Language { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}


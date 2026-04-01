namespace Advertified.App.Contracts.Admin;

public sealed class AdminAiVoiceTemplateResponse
{
    public Guid Id { get; set; }
    public int TemplateNumber { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PromptTemplate { get; set; } = string.Empty;
    public string PrimaryVoicePackName { get; set; } = string.Empty;
    public string[] FallbackVoicePackNames { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UpsertAdminAiVoiceTemplateRequest
{
    public int TemplateNumber { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PromptTemplate { get; set; } = string.Empty;
    public string PrimaryVoicePackName { get; set; } = string.Empty;
    public string[] FallbackVoicePackNames { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

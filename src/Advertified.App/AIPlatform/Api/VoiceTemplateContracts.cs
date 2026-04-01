namespace Advertified.App.AIPlatform.Api;

public sealed class VoiceTemplateResponse
{
    public Guid Id { get; set; }
    public int TemplateNumber { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PromptTemplate { get; set; } = string.Empty;
    public string PrimaryVoicePackName { get; set; } = string.Empty;
    public string[] FallbackVoicePackNames { get; set; } = Array.Empty<string>();
}

public sealed class SelectVoiceTemplateRequest
{
    public Guid CampaignId { get; set; }
    public string Product { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Audience { get; set; }
    public string? Goal { get; set; }
    public string? BudgetTier { get; set; }
    public string? Language { get; set; }
    public string? Platform { get; set; }
    public string? Objective { get; set; }
    public string? Brand { get; set; }
    public string? Business { get; set; }
    public string? EventName { get; set; }
    public string? Offer { get; set; }
}

public sealed class SelectVoiceTemplateResponse
{
    public int TemplateNumber { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string PromptTemplate { get; set; } = string.Empty;
    public string FinalPrompt { get; set; } = string.Empty;
    public string PrimaryVoicePackName { get; set; } = string.Empty;
    public Guid? PrimaryVoicePackId { get; set; }
    public string[] FallbackVoicePackNames { get; set; } = Array.Empty<string>();
    public Guid[] FallbackVoicePackIds { get; set; } = Array.Empty<Guid>();
}

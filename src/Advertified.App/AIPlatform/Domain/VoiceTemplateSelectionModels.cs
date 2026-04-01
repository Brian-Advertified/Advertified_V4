namespace Advertified.App.AIPlatform.Domain;

public sealed record VoiceTemplateSelectionInput(
    Guid CampaignId,
    string Product,
    string? Industry,
    string? Audience,
    string? Goal,
    string? BudgetTier,
    string? Language,
    string? Platform,
    string? Objective,
    string? Brand,
    string? Business,
    string? EventName,
    string? Offer);

public sealed record VoiceTemplateSelectionItem(
    Guid Id,
    int TemplateNumber,
    string Category,
    string Name,
    string PromptTemplate,
    string PrimaryVoicePackName,
    string[] FallbackVoicePackNames);

public sealed record VoiceTemplateSelectionResult(
    int TemplateNumber,
    string TemplateName,
    string PromptTemplate,
    string FinalPrompt,
    string PrimaryVoicePackName,
    Guid? PrimaryVoicePackId,
    string[] FallbackVoicePackNames,
    Guid[] FallbackVoicePackIds);

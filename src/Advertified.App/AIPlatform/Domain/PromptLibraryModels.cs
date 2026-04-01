namespace Advertified.App.AIPlatform.Domain;

public sealed record PromptVariableDefinition(
    string Name,
    string Description,
    bool IsRequired,
    string? DefaultValue);

public sealed record PromptTemplateDefinition(
    Guid Id,
    string Key,
    AdvertisingChannel Channel,
    string Language,
    int Version,
    string SystemPrompt,
    string TemplatePrompt,
    string OutputSchemaJson,
    IReadOnlyList<PromptVariableDefinition> Variables,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string VersionLabel = "v1",
    decimal? PerformanceScore = null,
    int UsageCount = 0,
    string? BaseSystemPromptKey = null);

public sealed record PromptRenderRequest(
    string Key,
    AdvertisingChannel Channel,
    string Language,
    int? Version,
    IReadOnlyDictionary<string, string> Variables);

public sealed record PromptRenderResult(
    PromptTemplateDefinition Template,
    string RenderedSystemPrompt,
    string RenderedUserPrompt);

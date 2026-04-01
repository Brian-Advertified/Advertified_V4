using Advertified.App.AIPlatform.Domain;

namespace Advertified.App.AIPlatform.Api;

public sealed class PromptVariableDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
}

public sealed class UpsertPromptTemplateRequest
{
    public Guid? Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public AdvertisingChannel Channel { get; set; } = AdvertisingChannel.Digital;
    public string Language { get; set; } = "English";
    public int Version { get; set; } = 1;
    public string VersionLabel { get; set; } = "v1";
    public string SystemPrompt { get; set; } = string.Empty;
    public string TemplatePrompt { get; set; } = string.Empty;
    public string OutputSchemaJson { get; set; } = string.Empty;
    public decimal? PerformanceScore { get; set; }
    public int UsageCount { get; set; }
    public string? BaseSystemPromptKey { get; set; }
    public bool IsActive { get; set; } = true;
    public List<PromptVariableDto> Variables { get; set; } = new();
}

public sealed class PromptTemplateResponse
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = "English";
    public int Version { get; set; }
    public string VersionLabel { get; set; } = "v1";
    public string SystemPrompt { get; set; } = string.Empty;
    public string TemplatePrompt { get; set; } = string.Empty;
    public string OutputSchemaJson { get; set; } = string.Empty;
    public decimal? PerformanceScore { get; set; }
    public int UsageCount { get; set; }
    public string? BaseSystemPromptKey { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<PromptVariableDto> Variables { get; set; } = new();
}

public sealed class RenderPromptRequest
{
    public string Key { get; set; } = string.Empty;
    public AdvertisingChannel Channel { get; set; } = AdvertisingChannel.Digital;
    public string Language { get; set; } = "English";
    public int? Version { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RenderPromptResponse
{
    public PromptTemplateResponse Template { get; set; } = new();
    public string RenderedSystemPrompt { get; set; } = string.Empty;
    public string RenderedUserPrompt { get; set; } = string.Empty;
}

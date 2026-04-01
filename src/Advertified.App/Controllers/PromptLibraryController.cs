using Advertified.App.AIPlatform.Api;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/v2/ai-platform/prompts")]
public sealed class PromptLibraryController : ControllerBase
{
    private readonly IPromptLibraryService _promptLibraryService;

    public PromptLibraryController(IPromptLibraryService promptLibraryService)
    {
        _promptLibraryService = promptLibraryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PromptTemplateResponse>>> List(
        [FromQuery] AdvertisingChannel? channel,
        [FromQuery] string? language,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var rows = await _promptLibraryService.ListAsync(channel, language, includeInactive, cancellationToken);
        return Ok(rows.Select(Map).ToArray());
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<PromptTemplateResponse>> Get(
        string key,
        [FromQuery] AdvertisingChannel channel,
        [FromQuery] string language = "English",
        [FromQuery] int? version = null,
        CancellationToken cancellationToken = default)
    {
        var row = await _promptLibraryService.GetAsync(key, channel, language, version, cancellationToken);
        return Ok(Map(row));
    }

    [HttpPost]
    public async Task<ActionResult<PromptTemplateResponse>> Upsert(
        [FromBody] UpsertPromptTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            throw new InvalidOperationException("key is required.");
        }

        var saved = await _promptLibraryService.UpsertAsync(new PromptTemplateDefinition(
            request.Id ?? Guid.Empty,
            request.Key.Trim(),
            request.Channel,
            request.Language.Trim(),
            request.Version,
            request.SystemPrompt,
            request.TemplatePrompt,
            request.OutputSchemaJson,
            request.Variables.Select(item => new PromptVariableDefinition(
                item.Name.Trim(),
                item.Description.Trim(),
                item.IsRequired,
                string.IsNullOrWhiteSpace(item.DefaultValue) ? null : item.DefaultValue.Trim())).ToArray(),
            request.IsActive,
            DateTimeOffset.UtcNow,
            request.VersionLabel,
            request.PerformanceScore,
            request.UsageCount,
            string.IsNullOrWhiteSpace(request.BaseSystemPromptKey) ? null : request.BaseSystemPromptKey.Trim()), cancellationToken);

        return Ok(Map(saved));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _promptLibraryService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("render")]
    public async Task<ActionResult<RenderPromptResponse>> Render(
        [FromBody] RenderPromptRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            throw new InvalidOperationException("key is required.");
        }

        var result = await _promptLibraryService.RenderAsync(new PromptRenderRequest(
            request.Key.Trim(),
            request.Channel,
            request.Language.Trim(),
            request.Version,
            request.Variables), cancellationToken);

        return Ok(new RenderPromptResponse
        {
            Template = Map(result.Template),
            RenderedSystemPrompt = result.RenderedSystemPrompt,
            RenderedUserPrompt = result.RenderedUserPrompt
        });
    }

    private static PromptTemplateResponse Map(PromptTemplateDefinition template)
    {
        return new PromptTemplateResponse
        {
            Id = template.Id,
            Key = template.Key,
            Channel = template.Channel.ToString(),
            Language = template.Language,
            Version = template.Version,
            VersionLabel = template.VersionLabel,
            SystemPrompt = template.SystemPrompt,
            TemplatePrompt = template.TemplatePrompt,
            OutputSchemaJson = template.OutputSchemaJson,
            PerformanceScore = template.PerformanceScore,
            UsageCount = template.UsageCount,
            BaseSystemPromptKey = template.BaseSystemPromptKey,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            Variables = template.Variables.Select(item => new PromptVariableDto
            {
                Name = item.Name,
                Description = item.Description,
                IsRequired = item.IsRequired,
                DefaultValue = item.DefaultValue
            }).ToList()
        };
    }
}

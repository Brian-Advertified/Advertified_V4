using System.Text.Json;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class DbPromptTemplateRepository : IPromptTemplateRepository
{
    private readonly AppDbContext _db;

    public DbPromptTemplateRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PromptTemplateDefinition?> GetAsync(
        string key,
        AdvertisingChannel channel,
        string language,
        int? version,
        CancellationToken cancellationToken)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        var query = _db.AiPromptTemplates
            .AsNoTracking()
            .Where(item => item.Key == key && item.Channel == channel.ToString() && item.Language == normalizedLanguage);

        if (version.HasValue)
        {
            query = query.Where(item => item.Version == version.Value);
        }

        var entity = await query
            .OrderByDescending(item => item.Version)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<PromptTemplateDefinition>> ListAsync(
        AdvertisingChannel? channel,
        string? language,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _db.AiPromptTemplates.AsNoTracking().AsQueryable();
        if (channel.HasValue)
        {
            query = query.Where(item => item.Channel == channel.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            query = query.Where(item => item.Language == NormalizeLanguage(language));
        }

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        var rows = await query
            .OrderBy(item => item.Key)
            .ThenBy(item => item.Channel)
            .ThenBy(item => item.Language)
            .ThenByDescending(item => item.Version)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToArray();
    }

    public async Task<PromptTemplateDefinition> UpsertAsync(PromptTemplateDefinition template, CancellationToken cancellationToken)
    {
        var normalizedLanguage = NormalizeLanguage(template.Language);
        var existing = await _db.AiPromptTemplates
            .FirstOrDefaultAsync(item =>
                item.Key == template.Key &&
                item.Channel == template.Channel.ToString() &&
                item.Language == normalizedLanguage &&
                item.Version == template.Version,
                cancellationToken);

        if (existing is null)
        {
            existing = new AiPromptTemplate
            {
                Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id,
                Key = template.Key,
                Channel = template.Channel.ToString(),
                Language = normalizedLanguage,
                Version = template.Version,
                CreatedAt = DateTime.UtcNow
            };
            _db.AiPromptTemplates.Add(existing);
        }

        existing.SystemPrompt = template.SystemPrompt;
        existing.TemplatePrompt = template.TemplatePrompt;
        existing.OutputSchema = template.OutputSchemaJson;
        existing.VariablesJson = JsonSerializer.Serialize(template.Variables);
        existing.VersionLabel = string.IsNullOrWhiteSpace(template.VersionLabel) ? $"v{template.Version}" : template.VersionLabel.Trim();
        existing.PerformanceScore = template.PerformanceScore;
        existing.UsageCount = Math.Max(0, template.UsageCount);
        existing.BaseSystemPromptKey = string.IsNullOrWhiteSpace(template.BaseSystemPromptKey) ? null : template.BaseSystemPromptKey.Trim();
        existing.IsActive = template.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return Map(existing);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var existing = await _db.AiPromptTemplates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        _db.AiPromptTemplates.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static PromptTemplateDefinition Map(AiPromptTemplate entity)
    {
        var variables = JsonSerializer.Deserialize<IReadOnlyList<PromptVariableDefinition>>(entity.VariablesJson)
            ?? Array.Empty<PromptVariableDefinition>();

        var channel = Enum.TryParse<AdvertisingChannel>(entity.Channel, true, out var parsedChannel)
            ? parsedChannel
            : AdvertisingChannel.Digital;

        return new PromptTemplateDefinition(
            entity.Id,
            entity.Key,
            channel,
            entity.Language,
            entity.Version,
            entity.SystemPrompt,
            entity.TemplatePrompt,
            entity.OutputSchema,
            variables,
            entity.IsActive,
            new DateTimeOffset(entity.CreatedAt, TimeSpan.Zero),
            entity.VersionLabel,
            entity.PerformanceScore,
            entity.UsageCount,
            entity.BaseSystemPromptKey);
    }

    private static string NormalizeLanguage(string language)
    {
        return language.Trim() switch
        {
            "" => "English",
            var value => value
        };
    }
}

public sealed class PromptLibraryService : IPromptLibraryService
{
    private readonly IPromptTemplateRepository _repository;

    public PromptLibraryService(IPromptTemplateRepository repository)
    {
        _repository = repository;
    }

    // Backward-compatible call used by existing orchestration.
    public async Task<PromptTemplate> GetLatestAsync(string key, CancellationToken cancellationToken)
    {
        var template = await _repository.GetAsync(key, AdvertisingChannel.Digital, "English", null, cancellationToken);
        if (template is null)
        {
            throw new InvalidOperationException($"Prompt template '{key}' was not found.");
        }

        return new PromptTemplate(
            template.Key,
            template.Version,
            template.SystemPrompt,
            template.TemplatePrompt,
            template.OutputSchemaJson);
    }

    public async Task<PromptTemplateDefinition> GetAsync(
        string key,
        AdvertisingChannel channel,
        string language,
        int? version,
        CancellationToken cancellationToken)
    {
        var template = await _repository.GetAsync(key, channel, language, version, cancellationToken);
        if (template is null)
        {
            throw new InvalidOperationException(
                $"Prompt template '{key}' for channel '{channel}' and language '{language}' was not found.");
        }

        return template;
    }

    public Task<PromptTemplateDefinition> UpsertAsync(PromptTemplateDefinition template, CancellationToken cancellationToken)
    {
        return _repository.UpsertAsync(template, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return _repository.DeleteAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<PromptTemplateDefinition>> ListAsync(
        AdvertisingChannel? channel,
        string? language,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        return _repository.ListAsync(channel, language, includeInactive, cancellationToken);
    }

    public async Task<PromptRenderResult> RenderAsync(PromptRenderRequest request, CancellationToken cancellationToken)
    {
        var template = await GetAsync(request.Key, request.Channel, request.Language, request.Version, cancellationToken);
        var resolved = ResolveVariables(template, request.Variables);
        var baseSystemPrompt = await GetBaseSystemPromptAsync(template, request.Language, cancellationToken);
        var renderedSystem = ApplyVariables(baseSystemPrompt, resolved);
        var renderedUser = ApplyVariables(template.TemplatePrompt, resolved);

        return new PromptRenderResult(template, renderedSystem, renderedUser);
    }

    private async Task<string> GetBaseSystemPromptAsync(
        PromptTemplateDefinition template,
        string language,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(template.BaseSystemPromptKey))
        {
            return template.SystemPrompt;
        }

        var baseTemplate = await _repository.GetAsync(
            template.BaseSystemPromptKey,
            AdvertisingChannel.Digital,
            language,
            null,
            cancellationToken);

        if (baseTemplate is null)
        {
            return template.SystemPrompt;
        }

        return $"{baseTemplate.SystemPrompt.Trim()}\n\n{template.SystemPrompt.Trim()}";
    }

    private static Dictionary<string, string> ResolveVariables(
        PromptTemplateDefinition template,
        IReadOnlyDictionary<string, string> provided)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in template.Variables)
        {
            if (provided.TryGetValue(variable.Name, out var providedValue) && !string.IsNullOrWhiteSpace(providedValue))
            {
                resolved[variable.Name] = providedValue.Trim();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(variable.DefaultValue))
            {
                resolved[variable.Name] = variable.DefaultValue.Trim();
                continue;
            }

            if (variable.IsRequired)
            {
                throw new InvalidOperationException($"Missing required prompt variable '{variable.Name}'.");
            }
        }

        return resolved;
    }

    private static string ApplyVariables(string templateText, IReadOnlyDictionary<string, string> variables)
    {
        var output = templateText;
        foreach (var pair in variables)
        {
            output = output.Replace($"{{{{{pair.Key}}}}}", pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }
}

public sealed class DbAiIdempotencyService : IAiIdempotencyService
{
    private readonly AppDbContext _db;

    public DbAiIdempotencyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> GetJobIdAsync(string scope, string key, CancellationToken cancellationToken)
    {
        var existing = await _db.AiIdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

        return existing?.JobId;
    }

    public async Task SaveJobIdAsync(string scope, string key, Guid jobId, CancellationToken cancellationToken)
    {
        var existing = await _db.AiIdempotencyRecords
            .FirstOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

        if (existing is null)
        {
            _db.AiIdempotencyRecords.Add(new AiIdempotencyRecord
            {
                Id = Guid.NewGuid(),
                Scope = scope,
                Key = key,
                JobId = jobId,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.JobId = jobId;
            existing.CreatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

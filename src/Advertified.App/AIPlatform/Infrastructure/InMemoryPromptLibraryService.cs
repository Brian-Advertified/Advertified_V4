using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class InMemoryPromptLibraryService : IPromptLibraryService
{
    // In-memory fallback catalog used for local development tests.
    private static readonly List<PromptTemplateDefinition> Templates = new()
    {
        new PromptTemplateDefinition(
            Guid.NewGuid(),
            "creative-brief-default",
            AdvertisingChannel.Digital,
            "English",
            1,
            "You are an advertising creative engine. Return strict JSON only.",
            "Generate channel-native variants aligned to objective, audience and CTA.",
            "{\"type\":\"object\",\"required\":[\"channel\",\"language\",\"creative\",\"cta\"]}",
            Array.Empty<PromptVariableDefinition>(),
            true,
            DateTimeOffset.UtcNow),
        new PromptTemplateDefinition(
            Guid.NewGuid(),
            "creative-qa-default",
            AdvertisingChannel.Digital,
            "English",
            1,
            "You are an ad QA evaluator. Return scorecard JSON only.",
            "Score clarity, brand fit, emotional impact, and CTA strength.",
            "{\"type\":\"object\",\"required\":[\"metrics\",\"overall\",\"issues\",\"status\"]}",
            Array.Empty<PromptVariableDefinition>(),
            true,
            DateTimeOffset.UtcNow)
    };

    public Task<PromptTemplate> GetLatestAsync(string key, CancellationToken cancellationToken)
    {
        var template = Templates
            .Where(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Version)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Prompt template '{key}' was not found.");

        return Task.FromResult(new PromptTemplate(template.Key, template.Version, template.SystemPrompt, template.TemplatePrompt, template.OutputSchemaJson));
    }

    public Task<int> GetLatestVersionAsync(
        string key,
        AdvertisingChannel channel,
        string language,
        CancellationToken cancellationToken)
    {
        var template = Templates
            .Where(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)
                && item.Channel == channel
                && string.Equals(item.Language, language, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Version)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Prompt template '{key}' for channel '{channel}' and language '{language}' was not found.");

        return Task.FromResult(template.Version);
    }

    public Task<PromptTemplateDefinition> GetAsync(
        string key,
        AdvertisingChannel channel,
        string language,
        int? version,
        CancellationToken cancellationToken)
    {
        var query = Templates.Where(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase) &&
            item.Channel == channel &&
            string.Equals(item.Language, language, StringComparison.OrdinalIgnoreCase));

        if (version.HasValue)
        {
            query = query.Where(item => item.Version == version.Value);
        }

        var template = query.OrderByDescending(item => item.Version).FirstOrDefault()
            ?? throw new InvalidOperationException($"Prompt template '{key}' was not found.");

        return Task.FromResult(template);
    }

    public Task<PromptTemplateDefinition> UpsertAsync(PromptTemplateDefinition template, CancellationToken cancellationToken)
    {
        var existingIndex = Templates.FindIndex(item =>
            item.Key == template.Key &&
            item.Channel == template.Channel &&
            string.Equals(item.Language, template.Language, StringComparison.OrdinalIgnoreCase) &&
            item.Version == template.Version);

        var stored = template with
        {
            Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id,
            CreatedAt = template.CreatedAt == default ? DateTimeOffset.UtcNow : template.CreatedAt
        };

        if (existingIndex >= 0)
        {
            Templates[existingIndex] = stored;
        }
        else
        {
            Templates.Add(stored);
        }

        return Task.FromResult(stored);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var existingIndex = Templates.FindIndex(item => item.Id == id);
        if (existingIndex < 0)
        {
            return Task.FromResult(false);
        }

        Templates.RemoveAt(existingIndex);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<PromptTemplateDefinition>> ListAsync(
        AdvertisingChannel? channel,
        string? language,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = Templates.AsEnumerable();
        if (channel.HasValue)
        {
            query = query.Where(item => item.Channel == channel.Value);
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            query = query.Where(item => string.Equals(item.Language, language, StringComparison.OrdinalIgnoreCase));
        }

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        return Task.FromResult<IReadOnlyList<PromptTemplateDefinition>>(query
            .OrderBy(item => item.Key)
            .ThenBy(item => item.Channel)
            .ThenBy(item => item.Language)
            .ThenByDescending(item => item.Version)
            .ToArray());
    }

    public async Task<PromptRenderResult> RenderAsync(PromptRenderRequest request, CancellationToken cancellationToken)
    {
        var template = await GetAsync(request.Key, request.Channel, request.Language, request.Version, cancellationToken);
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var variable in template.Variables)
        {
            if (request.Variables.TryGetValue(variable.Name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                resolved[variable.Name] = value.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(variable.DefaultValue))
            {
                resolved[variable.Name] = variable.DefaultValue.Trim();
            }
            else if (variable.IsRequired)
            {
                throw new InvalidOperationException($"Missing required prompt variable '{variable.Name}'.");
            }
        }

        var renderedSystem = ReplaceVariables(template.SystemPrompt, resolved);
        var renderedUser = ReplaceVariables(template.TemplatePrompt, resolved);
        return new PromptRenderResult(template, renderedSystem, renderedUser);
    }

    private static string ReplaceVariables(string input, IReadOnlyDictionary<string, string> variables)
    {
        var output = input;
        foreach (var variable in variables)
        {
            output = output.Replace($"{{{{{variable.Key}}}}}", variable.Value, StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }
}

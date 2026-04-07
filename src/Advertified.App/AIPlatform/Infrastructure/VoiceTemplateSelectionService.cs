using System.Text.Json;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.AIPlatform.Infrastructure;

public sealed class VoiceTemplateSelectionService : IVoiceTemplateSelectionService
{
    private readonly AppDbContext _db;

    public VoiceTemplateSelectionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<VoiceTemplateSelectionItem>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.AiVoicePromptTemplates
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.TemplateNumber)
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(row => new VoiceTemplateSelectionItem(
                row.Id,
                row.TemplateNumber,
                row.Category,
                row.Name,
                row.PromptTemplate,
                row.PrimaryVoicePackName,
                DeserializeList(row.FallbackVoicePackNamesJson)))
            .ToArray();
    }

    public async Task<VoiceTemplateSelectionResult> SelectAsync(VoiceTemplateSelectionInput input, CancellationToken cancellationToken)
    {
        if (input.CampaignId == Guid.Empty)
        {
            throw new InvalidOperationException("CampaignId is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Product))
        {
            throw new InvalidOperationException("Product is required.");
        }

        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Where(item => item.Id == input.CampaignId)
            .Select(item => new { item.UserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found.");

        var templateNumber = SelectTemplateNumber(input);
        var template = await _db.AiVoicePromptTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TemplateNumber == templateNumber && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"Template {templateNumber} is not configured.");

        var primaryVoicePack = await ResolveVoicePackIdAsync(
            template.PrimaryVoicePackName,
            campaign.UserId,
            cancellationToken);
        var fallbackNames = DeserializeList(template.FallbackVoicePackNamesJson);
        var fallbackIds = new List<Guid>();
        foreach (var fallbackName in fallbackNames)
        {
            var fallbackId = await ResolveVoicePackIdAsync(fallbackName, campaign.UserId, cancellationToken);
            if (fallbackId.HasValue)
            {
                fallbackIds.Add(fallbackId.Value);
            }
        }

        var finalPrompt = ApplyTemplate(template.PromptTemplate, input);
        return new VoiceTemplateSelectionResult(
            template.TemplateNumber,
            template.Name,
            template.PromptTemplate,
            finalPrompt,
            template.PrimaryVoicePackName,
            primaryVoicePack,
            fallbackNames,
            fallbackIds.Distinct().ToArray());
    }

    private static int SelectTemplateNumber(VoiceTemplateSelectionInput input)
    {
        var goal = Normalize(input.Goal);
        var audience = Normalize(input.Audience);
        var platform = Normalize(input.Platform);
        var language = Normalize(input.Language);
        var industry = Normalize(input.Industry);

        if ((goal == "conversion" || goal == "sales") && audience.Contains("township", StringComparison.Ordinal))
        {
            return 1;
        }

        if (platform == "radio" && (goal == "awareness" || goal == "brand_awareness"))
        {
            return 11;
        }

        if ((goal == "engagement" || goal == "viral") && audience.Contains("youth", StringComparison.Ordinal))
        {
            return 21;
        }

        if (industry.Contains("finance", StringComparison.Ordinal) || industry.Contains("insurance", StringComparison.Ordinal))
        {
            return 15;
        }

        if (language == "zulu")
        {
            return 41;
        }

        if (language == "afrikaans")
        {
            return 42;
        }

        if (language == "mixed")
        {
            return 43;
        }

        if (platform == "tv" || platform == "video")
        {
            return 54;
        }

        return 12;
    }

    private async Task<Guid?> ResolveVoicePackIdAsync(string voicePackName, Guid? clientUserId, CancellationToken cancellationToken)
    {
        var trimmedName = voicePackName.Trim();
        if (trimmedName.Length == 0)
        {
            return null;
        }

        var row = await _db.AiVoicePacks
            .AsNoTracking()
            .Where(item => item.IsActive && item.Name == trimmedName)
            .Where(item => !item.IsClientSpecific || (clientUserId.HasValue && item.ClientUserId == clientUserId.Value))
            .OrderBy(item => item.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        return row?.Id;
    }

    private static string ApplyTemplate(string template, VoiceTemplateSelectionInput input)
    {
        return template
            .Replace("{product}", input.Product?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{audience}", input.Audience?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{industry}", input.Industry?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{goal}", input.Goal?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{objective}", input.Objective?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{brand}", input.Brand?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{business}", input.Business?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{event_name}", input.EventName?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{offer}", input.Offer?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string[] DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}

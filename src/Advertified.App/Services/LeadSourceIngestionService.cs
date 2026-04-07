using Advertified.App.Contracts.Leads;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Advertified.App.Services;

public sealed class LeadSourceIngestionService : ILeadSourceIngestionService
{
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly AppDbContext _db;

    public LeadSourceIngestionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<LeadSourceIngestionResult> IngestAsync(
        IReadOnlyList<IngestLeadSourceItemRequest> leads,
        CancellationToken cancellationToken)
    {
        if (leads.Count == 0)
        {
            return new LeadSourceIngestionResult();
        }

        var createdCount = 0;
        var updatedCount = 0;
        var touchedLeads = new List<Lead>();

        foreach (var item in leads)
        {
            if (string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.Location) ||
                string.IsNullOrWhiteSpace(item.Category) ||
                string.IsNullOrWhiteSpace(item.Source))
            {
                continue;
            }

            var normalizedWebsite = NormalizeWebsite(item.Website);
            var normalizedName = NormalizeText(item.Name);
            var normalizedLocation = NormalizeLocation(item.Location);
            var normalizedCategory = NormalizeCategory(item.Category);
            var normalizedSource = NormalizeText(item.Source);
            var normalizedSourceReference = TrimToNull(item.SourceReference);
            var now = DateTime.UtcNow;

            var existingLead = await FindExistingLeadAsync(
                normalizedWebsite,
                normalizedName,
                normalizedLocation,
                normalizedSource,
                normalizedSourceReference,
                cancellationToken);

            if (existingLead is null)
            {
                existingLead = new Lead
                {
                    Name = normalizedName,
                    Website = normalizedWebsite,
                    Location = normalizedLocation,
                    Category = normalizedCategory,
                    Source = normalizedSource,
                    SourceReference = normalizedSourceReference,
                    LastDiscoveredAt = now,
                };

                _db.Leads.Add(existingLead);
                createdCount++;
            }
            else
            {
                existingLead.Name = normalizedName;
                existingLead.Website ??= normalizedWebsite;
                existingLead.Location = normalizedLocation;
                existingLead.Category = normalizedCategory;
                existingLead.Source = normalizedSource;
                existingLead.SourceReference ??= normalizedSourceReference;
                existingLead.LastDiscoveredAt = now;
                updatedCount++;
            }

            touchedLeads.Add(existingLead);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new LeadSourceIngestionResult
        {
            CreatedCount = createdCount,
            UpdatedCount = updatedCount,
            Leads = touchedLeads
                .OrderByDescending(x => x.LastDiscoveredAt ?? x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .ToList(),
        };
    }

    private async Task<Lead?> FindExistingLeadAsync(
        string? normalizedWebsite,
        string normalizedName,
        string normalizedLocation,
        string normalizedSource,
        string? normalizedSourceReference,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(normalizedSourceReference))
        {
            var bySourceReference = await _db.Leads
                .FirstOrDefaultAsync(
                    x => x.Source == normalizedSource && x.SourceReference == normalizedSourceReference,
                    cancellationToken);

            if (bySourceReference is not null)
            {
                return bySourceReference;
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedWebsite))
        {
            var byWebsite = await _db.Leads
                .FirstOrDefaultAsync(x => x.Website == normalizedWebsite, cancellationToken);

            if (byWebsite is not null)
            {
                return byWebsite;
            }
        }

        return await _db.Leads.FirstOrDefaultAsync(
            x => x.Name == normalizedName && x.Location == normalizedLocation,
            cancellationToken);
    }

    private static string? NormalizeWebsite(string? website)
    {
        var trimmed = TrimToNull(website);
        if (trimmed is null)
        {
            return null;
        }

        var withoutScheme = trimmed
            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (withoutScheme.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            withoutScheme = withoutScheme[4..];
        }

        var slashIndex = withoutScheme.IndexOf('/');
        if (slashIndex >= 0)
        {
            withoutScheme = withoutScheme[..slashIndex];
        }

        var queryIndex = withoutScheme.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
        {
            withoutScheme = withoutScheme[..queryIndex];
        }

        return TrimToNull(withoutScheme?.TrimEnd('.'));
    }

    private static string NormalizeLocation(string value)
    {
        var normalized = NormalizeText(value);
        if (normalized.Contains(','))
        {
            normalized = normalized.Split(',', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        }

        return normalized;
    }

    private static string NormalizeCategory(string value)
    {
        var normalized = NormalizeText(value);
        if (normalized.Contains('|'))
        {
            normalized = normalized.Split('|', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        }

        return normalized;
    }

    private static string NormalizeText(string value)
    {
        var trimmed = value.Trim();
        trimmed = MultiWhitespaceRegex.Replace(trimmed, " ");
        return trimmed;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

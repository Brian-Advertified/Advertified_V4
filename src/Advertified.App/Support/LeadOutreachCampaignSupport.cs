using System.Text.RegularExpressions;
using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class LeadOutreachCampaignSupport
{
    private static readonly Regex LeadSourceIdRegex = new(
        @"lead source id\s*:\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsLeadOutreachCampaign(Campaign campaign)
    {
        if (campaign.ProspectLeadId.HasValue || campaign.PackageOrder?.ProspectLeadId.HasValue == true)
        {
            return true;
        }

        var notes = ResolveNotes(campaign);
        if (string.IsNullOrWhiteSpace(notes))
        {
            return false;
        }

        return notes.Contains("Why you are receiving this:", StringComparison.OrdinalIgnoreCase)
            || notes.Contains("Archetype:", StringComparison.OrdinalIgnoreCase)
            || notes.Contains("Lead intelligence summary:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool RequiresLeadConfidenceGate(Campaign campaign)
    {
        var notes = ResolveNotes(campaign);
        if (string.IsNullOrWhiteSpace(notes))
        {
            return false;
        }

        return notes.Contains("Why you are receiving this:", StringComparison.OrdinalIgnoreCase)
            || notes.Contains("Archetype:", StringComparison.OrdinalIgnoreCase)
            || notes.Contains("Lead intelligence summary:", StringComparison.OrdinalIgnoreCase)
            || LeadSourceIdRegex.IsMatch(notes);
    }

    public static bool TryGetSourceLeadId(Campaign campaign, out int leadId)
    {
        var notes = ResolveNotes(campaign);
        if (string.IsNullOrWhiteSpace(notes))
        {
            leadId = default;
            return false;
        }

        var match = LeadSourceIdRegex.Match(notes);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out leadId) || leadId <= 0)
        {
            leadId = default;
            return false;
        }

        return true;
    }

    private static string? ResolveNotes(Campaign campaign)
    {
        return campaign.CampaignBrief?.SpecialRequirements ?? campaign.CampaignBrief?.CreativeNotes;
    }
}

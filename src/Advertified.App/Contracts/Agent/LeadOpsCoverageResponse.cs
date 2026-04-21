namespace Advertified.App.Contracts.Agent;

public sealed class LeadOpsCoverageResponse
{
    public DateTimeOffset GeneratedAtUtc { get; set; }

    public int TotalLeadCount { get; set; }

    public int OwnedLeadCount { get; set; }

    public int UnownedLeadCount { get; set; }

    public int AmbiguousOwnerCount { get; set; }

    public int UncontactedLeadCount { get; set; }

    public int LeadsWithNextActionCount { get; set; }

    public int ProspectLeadCount { get; set; }

    public int ActiveDealCount { get; set; }

    public int WonLeadCount { get; set; }

    public decimal LeadToProspectRatePercent { get; set; }

    public decimal LeadToSaleRatePercent { get; set; }

    public IReadOnlyList<LeadOpsCoverageSourceResponse> Sources { get; set; } = Array.Empty<LeadOpsCoverageSourceResponse>();

    public IReadOnlyList<LeadOpsCoverageItemResponse> Items { get; set; } = Array.Empty<LeadOpsCoverageItemResponse>();
}

public sealed class LeadOpsCoverageSourceResponse
{
    public string Source { get; set; } = string.Empty;

    public int LeadCount { get; set; }

    public int ProspectCount { get; set; }

    public int WonCount { get; set; }
}

public sealed class LeadOpsCoverageItemResponse
{
    public string RecordKey { get; set; } = string.Empty;

    public int LeadId { get; set; }

    public string LeadName { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string? SourceReference { get; set; }

    public string UnifiedStatus { get; set; } = string.Empty;

    public Guid? OwnerAgentUserId { get; set; }

    public string? OwnerAgentName { get; set; }

    public string OwnerResolution { get; set; } = string.Empty;

    public string AssignmentStatus { get; set; } = string.Empty;

    public bool HasBeenContacted { get; set; }

    public DateTimeOffset? FirstContactedAt { get; set; }

    public string ContactStatus { get; set; } = string.Empty;

    public DateTimeOffset? LastContactedAt { get; set; }

    public string NextAction { get; set; } = string.Empty;

    public DateTimeOffset? NextActionDueAt { get; set; }

    public DateTimeOffset? NextFollowUpAt { get; set; }

    public DateTimeOffset? SlaDueAt { get; set; }

    public string Priority { get; set; } = string.Empty;

    public IReadOnlyList<string> AttentionReasons { get; set; } = Array.Empty<string>();

    public int OpenLeadActionCount { get; set; }

    public bool HasProspect { get; set; }

    public Guid? ProspectLeadId { get; set; }

    public Guid? ActiveCampaignId { get; set; }

    public Guid? WonCampaignId { get; set; }

    public bool ConvertedToSale { get; set; }

    public string? LastOutcome { get; set; }

    public string RoutePath { get; set; } = string.Empty;
}

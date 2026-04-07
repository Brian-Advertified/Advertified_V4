using Advertified.App.Data.Entities;

namespace Advertified.App.Services;

public sealed class LeadSourceIngestionResult
{
    public int CreatedCount { get; init; }

    public int UpdatedCount { get; init; }

    public IReadOnlyList<Lead> Leads { get; init; } = Array.Empty<Lead>();
}

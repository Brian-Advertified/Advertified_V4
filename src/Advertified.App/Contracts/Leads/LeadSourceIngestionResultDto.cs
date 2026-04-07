namespace Advertified.App.Contracts.Leads;

public sealed class LeadSourceIngestionResultDto
{
    public int CreatedCount { get; init; }

    public int UpdatedCount { get; init; }

    public IReadOnlyList<LeadDto> Leads { get; init; } = Array.Empty<LeadDto>();
}

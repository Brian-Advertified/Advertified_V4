namespace Advertified.App.Contracts.Leads;

public sealed class IngestLeadSourceBatchRequest
{
    public IReadOnlyList<IngestLeadSourceItemRequest> Leads { get; init; } = Array.Empty<IngestLeadSourceItemRequest>();
}

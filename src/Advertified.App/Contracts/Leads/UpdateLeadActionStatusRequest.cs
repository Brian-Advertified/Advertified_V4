namespace Advertified.App.Contracts.Leads;

public sealed class UpdateLeadActionStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

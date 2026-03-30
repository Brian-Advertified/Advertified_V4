namespace Advertified.App.Contracts.Admin;

public sealed class AdminPackageOrdersResponse
{
    public IReadOnlyCollection<AdminPackageOrderItemResponse> Items { get; set; } = Array.Empty<AdminPackageOrderItemResponse>();
}

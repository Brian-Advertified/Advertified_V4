using Advertified.App.Contracts.Packages;

namespace Advertified.App.Services.Abstractions;

public interface IPackagePurchaseService
{
    Task<CreatePackageOrderResponse> CreatePendingOrderAsync(Guid userId, CreatePackageOrderRequest request, CancellationToken cancellationToken);
    Task<CreatePackageOrderResponse> InitiateCheckoutAsync(Guid userId, Guid packageOrderId, string paymentProvider, Guid? recommendationId, CancellationToken cancellationToken);
    Task MarkOrderPaidAsync(Guid packageOrderId, string paymentReference, CancellationToken cancellationToken);
    Task MarkOrderFailedAsync(Guid packageOrderId, string? paymentReference, CancellationToken cancellationToken);
}

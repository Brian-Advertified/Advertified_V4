using Advertified.App.Contracts.Payments;

namespace Advertified.App.Services.Abstractions;

public interface IPaymentStateCache
{
    Task SetAsync(string paymentReference, PaymentStateCacheEntry entry, CancellationToken cancellationToken);
    Task<PaymentStateCacheEntry?> GetAsync(string paymentReference, CancellationToken cancellationToken);
}

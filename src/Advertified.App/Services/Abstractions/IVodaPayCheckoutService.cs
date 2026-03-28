using Advertified.App.Contracts.Payments;
using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IVodaPayCheckoutService
{
    Task<VodaPayCheckoutSession> InitiateAsync(
        PackageOrder order,
        PackageBand band,
        UserAccount user,
        BusinessProfile? businessProfile,
        CancellationToken cancellationToken);
}

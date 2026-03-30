using Advertified.App.Support;

namespace Advertified.App.Services.Abstractions;

public interface IPricingSettingsProvider
{
    Task<PricingSettingsSnapshot> GetCurrentAsync(CancellationToken cancellationToken);
}

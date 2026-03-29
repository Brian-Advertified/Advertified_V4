using Advertified.App.Contracts.Packages;

namespace Advertified.App.Services.Abstractions;

public interface IPackageAreaService
{
    Task<IReadOnlyList<PackageAreaOptionResponse>> GetAreasAsync(CancellationToken cancellationToken);
}

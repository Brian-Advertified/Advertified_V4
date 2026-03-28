using Advertified.App.Contracts.Packages;

namespace Advertified.App.Services.Abstractions;

public interface IPackageCatalogService
{
    IReadOnlyCollection<PackageBandDto> GetPackageBands();
}

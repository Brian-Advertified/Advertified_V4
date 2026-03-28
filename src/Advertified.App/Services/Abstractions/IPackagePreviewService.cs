using Advertified.App.Contracts.Packages;

namespace Advertified.App.Services.Abstractions;

public interface IPackagePreviewService
{
    Task<PackagePreviewResult> GeneratePreviewAsync(Guid packageBandId, decimal budget, string? selectedArea, CancellationToken cancellationToken);
}

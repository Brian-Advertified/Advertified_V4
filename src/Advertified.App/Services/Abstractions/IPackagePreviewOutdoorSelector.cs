using Advertified.App.Contracts.Packages;

namespace Advertified.App.Services.Abstractions;

public interface IPackagePreviewOutdoorSelector
{
    IReadOnlyList<OohPreviewRow> SelectExamples(IReadOnlyList<OohPreviewRow> candidates, PackagePreviewAreaProfile selectedArea, decimal budget, decimal budgetRatio);

    IReadOnlyList<PackagePreviewMapPoint> BuildMapPoints(IReadOnlyList<OohPreviewRow> rows, PackagePreviewAreaProfile selectedArea);
}

namespace Advertified.App.Services.Abstractions;

public interface IPackagePreviewFormatter
{
    IReadOnlyList<string> BuildExampleLocations(IReadOnlyList<OohPreviewRow> rows, PackagePreviewAreaProfile selectedArea);

    string GetCoverageLabel(string bandCode, decimal budget, decimal minBudget, decimal maxBudget);

    IReadOnlyList<string> BuildMediaMix(string bandCode, decimal budget);
}

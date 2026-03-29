namespace Advertified.App.Services.Abstractions;

public interface IPackagePreviewBroadcastSelector
{
    IReadOnlyList<string> BuildRadioSupportExamples(
        IReadOnlyList<BroadcastInventoryRecord> records,
        PackagePreviewAreaProfile selectedArea,
        string bandCode,
        decimal budget,
        decimal budgetRatio);

    IReadOnlyList<string> BuildTvSupportExamples(
        IReadOnlyList<BroadcastInventoryRecord> records,
        PackagePreviewAreaProfile selectedArea,
        string bandCode,
        decimal budget,
        decimal budgetRatio);
}

namespace Advertified.App.Services.Abstractions;

public interface IPackagePreviewReachEstimator
{
    string Estimate(string bandCode, decimal budgetRatio, int oohCount, int radioCount);
}

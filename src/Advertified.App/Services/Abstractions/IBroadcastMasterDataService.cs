using Advertified.App.Contracts.Admin;

namespace Advertified.App.Services.Abstractions;

public interface IBroadcastMasterDataService
{
    Task<AdminOutletMasterDataResponse> GetOutletMasterDataAsync(CancellationToken cancellationToken);
    string NormalizeLanguageCode(string? value);
    string NormalizeProvinceCode(string? value);
    string NormalizeCoverageType(string? value);
    string NormalizeCatalogHealth(string? value);
    string NormalizeLanguageForMatching(string? value);
    string NormalizeGeographyForMatching(string? value);
}

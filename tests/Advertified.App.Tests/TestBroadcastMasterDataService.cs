using Advertified.App.Contracts.Admin;
using Advertified.App.Services.Abstractions;

internal sealed class TestBroadcastMasterDataService : IBroadcastMasterDataService
{
    public Task<AdminOutletMasterDataResponse> GetOutletMasterDataAsync(CancellationToken cancellationToken)
        => Task.FromResult(new AdminOutletMasterDataResponse());

    public string NormalizeLanguageCode(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        return normalized switch
        {
            "isizulu" or "zulu" => "zulu",
            "isixhosa" or "xhosa" => "xhosa",
            "afrikaans" => "afrikaans",
            "english" => "english",
            _ => normalized
        };
    }

    public string NormalizeProvinceCode(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        return normalized switch
        {
            "western_cape" => "western_cape",
            "kwazulu_natal" => "kwazulu_natal",
            "national" => "national",
            _ => "gauteng"
        };
    }

    public string NormalizeCoverageType(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    public string NormalizeCatalogHealth(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    public string NormalizeLanguageForMatching(string? value) => NormalizeLanguageCode(value);
    public string NormalizeGeographyForMatching(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        return normalized switch
        {
            "soweto" => "johannesburg",
            _ => normalized
        };
    }
}

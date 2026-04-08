namespace Advertified.App.Contracts.Admin;

public sealed class AdminOutletMasterDataResponse
{
    public IReadOnlyList<AdminLookupOptionResponse> Languages { get; init; } = Array.Empty<AdminLookupOptionResponse>();
    public IReadOnlyList<AdminLookupOptionResponse> Provinces { get; init; } = Array.Empty<AdminLookupOptionResponse>();
    public IReadOnlyList<AdminLookupOptionResponse> CoverageTypes { get; init; } = Array.Empty<AdminLookupOptionResponse>();
    public IReadOnlyList<AdminLookupOptionResponse> CatalogHealthStates { get; init; } = Array.Empty<AdminLookupOptionResponse>();
    public IReadOnlyList<AdminLookupOptionResponse> Cities { get; init; } = Array.Empty<AdminLookupOptionResponse>();
    public IReadOnlyList<AdminLookupOptionResponse> AudienceKeywords { get; init; } = Array.Empty<AdminLookupOptionResponse>();
    public IReadOnlyList<string> BroadcastFrequencySuggestions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TargetAudienceSuggestions { get; init; } = Array.Empty<string>();
}

public sealed class AdminLookupOptionResponse
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

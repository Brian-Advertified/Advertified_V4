namespace Advertified.App.Services.Abstractions;

public interface ILeadMasterDataService
{
    LeadMasterTokenSet GetTokenSet();

    MasterLocationMatch? ResolveLocation(string? value);

    MasterLocationMatch? ResolveNearestLocation(double latitude, double longitude, double maxDistanceKm = 50d) => null;

    MasterIndustryMatch? ResolveIndustry(string? value);

    MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints);

    MasterLanguageMatch? ResolveLanguage(string? value);
}

public sealed class LeadMasterTokenSet
{
    public IReadOnlyList<string> LocationTokens { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> IndustryTokens { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> LanguageTokens { get; init; } = Array.Empty<string>();
}

public sealed class MasterLocationMatch
{
    public string CanonicalName { get; init; } = string.Empty;

    public string? LocationType { get; init; }

    public string? ParentCity { get; init; }

    public string? Province { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double? DistanceKm { get; init; }
}

public sealed class MasterIndustryMatch
{
    public string Code { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;
}

public sealed class MasterLanguageMatch
{
    public string Code { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;
}

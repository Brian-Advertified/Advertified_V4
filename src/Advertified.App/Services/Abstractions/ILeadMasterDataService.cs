namespace Advertified.App.Services.Abstractions;

public interface ILeadMasterDataService
{
    LeadMasterTokenSet GetTokenSet();

    MasterLocationMatch? ResolveLocation(string? value);

    MasterIndustryMatch? ResolveIndustry(string? value);

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

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
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

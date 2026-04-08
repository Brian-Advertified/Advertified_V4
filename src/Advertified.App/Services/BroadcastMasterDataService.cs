using Advertified.App.Contracts.Admin;
using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class BroadcastMasterDataService : IBroadcastMasterDataService
{
    private readonly NpgsqlDataSource _dataSource;

    public BroadcastMasterDataService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    private static readonly AdminLookupOptionResponse[] LanguageOptions =
    {
        CreateOption("!xuntali", "!Xuntali"),
        CreateOption("english", "English"),
        CreateOption("afrikaans", "Afrikaans"),
        CreateOption("chinyanja", "Chinyanja"),
        CreateOption("french", "French"),
        CreateOption("isizulu", "isiZulu"),
        CreateOption("isixhosa", "isiXhosa"),
        CreateOption("kiswahili", "Kiswahili"),
        CreateOption("khwedam", "Khwedam"),
        CreateOption("setswana", "Setswana"),
        CreateOption("portuguese", "Portuguese"),
        CreateOption("sesotho", "Sesotho"),
        CreateOption("sepedi", "Sepedi"),
        CreateOption("siswati", "Siswati"),
        CreateOption("isindebele", "isiNdebele"),
        CreateOption("tshivenda", "Tshivenda"),
        CreateOption("xitsonga", "Xitsonga"),
        CreateOption("multilingual", "Multilingual"),
        CreateOption("unknown", "Unknown")
    };

    private static readonly AdminLookupOptionResponse[] ProvinceOptions =
    {
        CreateOption("eastern_cape", "Eastern Cape"),
        CreateOption("free_state", "Free State"),
        CreateOption("gauteng", "Gauteng"),
        CreateOption("kwazulu_natal", "KwaZulu-Natal"),
        CreateOption("limpopo", "Limpopo"),
        CreateOption("mpumalanga", "Mpumalanga"),
        CreateOption("north_west", "North West"),
        CreateOption("northern_cape", "Northern Cape"),
        CreateOption("western_cape", "Western Cape"),
        CreateOption("national", "National")
    };

    private static readonly AdminLookupOptionResponse[] CoverageTypeOptions =
    {
        CreateOption("local", "Local"),
        CreateOption("regional", "Regional"),
        CreateOption("national", "National"),
        CreateOption("digital", "Digital"),
        CreateOption("mixed", "Mixed"),
        CreateOption("unknown", "Unknown")
    };

    private static readonly AdminLookupOptionResponse[] CatalogHealthOptions =
    {
        CreateOption("strong", "Strong"),
        CreateOption("mixed", "Mixed"),
        CreateOption("mixed_not_fully_healthy", "Mixed not fully healthy"),
        CreateOption("unknown", "Unknown"),
        CreateOption("weak_partial_pricing", "Weak partial pricing"),
        CreateOption("weak_unpriced", "Weak unpriced"),
        CreateOption("weak_no_inventory", "Weak no inventory")
    };

    private static readonly AdminLookupOptionResponse[] CityOptions =
    {
        CreateOption("Johannesburg", "Johannesburg"),
        CreateOption("Pretoria", "Pretoria"),
        CreateOption("Sandton", "Sandton"),
        CreateOption("Randburg", "Randburg"),
        CreateOption("Soweto", "Soweto"),
        CreateOption("Cape Town", "Cape Town"),
        CreateOption("Bellville", "Bellville"),
        CreateOption("Durban", "Durban"),
        CreateOption("Umhlanga", "Umhlanga"),
        CreateOption("Pietermaritzburg", "Pietermaritzburg"),
        CreateOption("Gqeberha", "Gqeberha"),
        CreateOption("East London", "East London"),
        CreateOption("Bloemfontein", "Bloemfontein"),
        CreateOption("Polokwane", "Polokwane"),
        CreateOption("Mbombela", "Mbombela"),
        CreateOption("Rustenburg", "Rustenburg")
    };

    private static readonly AdminLookupOptionResponse[] AudienceKeywordOptions =
    {
        CreateOption("commuters", "Commuters"),
        CreateOption("professionals", "Professionals"),
        CreateOption("youth", "Youth"),
        CreateOption("families", "Families"),
        CreateOption("shoppers", "Shoppers"),
        CreateOption("smes", "SMEs"),
        CreateOption("mass market", "Mass market"),
        CreateOption("premium audience", "Premium audience"),
        CreateOption("pan-african", "Pan-African")
    };

    private static readonly string[] BroadcastFrequencySuggestions =
    {
        "Hourly",
        "Daily",
        "Weekdays only",
        "Weekends only",
        "Drive time",
        "All day rotation"
    };

    private static readonly string[] TargetAudienceSuggestions =
    {
        "General audience",
        "Youth audience",
        "Working professionals",
        "Households and families",
        "Business decision-makers",
        "Pan-African / beyond-SA audience"
    };

    private static readonly Dictionary<string, string> ProvinceAliases = BuildAliasMap(ProvinceOptions);
    private static readonly Dictionary<string, string> LanguageAliases = BuildAliasMap(LanguageOptions);
    private static readonly Dictionary<string, string> CoverageTypeAliases = BuildAliasMap(CoverageTypeOptions);
    private static readonly Dictionary<string, string> CatalogHealthAliases = BuildAliasMap(CatalogHealthOptions);

    public async Task<AdminOutletMasterDataResponse> GetOutletMasterDataAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var languages = (await connection.QueryAsync<AdminLookupOptionResponse>(new CommandDefinition(
            "select code as Value, label as Label from ref_language order by sort_order, label;",
            cancellationToken: cancellationToken))).ToArray();

        var provinces = (await connection.QueryAsync<AdminLookupOptionResponse>(new CommandDefinition(
            "select code as Value, label as Label from ref_province order by sort_order, label;",
            cancellationToken: cancellationToken))).ToArray();

        var coverageTypes = (await connection.QueryAsync<AdminLookupOptionResponse>(new CommandDefinition(
            "select code as Value, label as Label from ref_broadcast_coverage_type order by sort_order, label;",
            cancellationToken: cancellationToken))).ToArray();

        var catalogHealthStates = (await connection.QueryAsync<AdminLookupOptionResponse>(new CommandDefinition(
            "select code as Value, label as Label from ref_catalog_health order by sort_order, label;",
            cancellationToken: cancellationToken))).ToArray();

        return new AdminOutletMasterDataResponse
        {
            Languages = languages.Length > 0 ? languages : LanguageOptions,
            Provinces = provinces.Length > 0 ? provinces : ProvinceOptions,
            CoverageTypes = coverageTypes.Length > 0 ? coverageTypes : CoverageTypeOptions,
            CatalogHealthStates = catalogHealthStates.Length > 0 ? catalogHealthStates : CatalogHealthOptions,
            Cities = CityOptions,
            AudienceKeywords = AudienceKeywordOptions,
            BroadcastFrequencySuggestions = BroadcastFrequencySuggestions,
            TargetAudienceSuggestions = TargetAudienceSuggestions
        };
    }

    public string NormalizeLanguageCode(string? value) => NormalizeFromAliases(value, LanguageAliases);
    public string NormalizeProvinceCode(string? value) => NormalizeFromAliases(value, ProvinceAliases);
    public string NormalizeCoverageType(string? value) => NormalizeFromAliases(value, CoverageTypeAliases);
    public string NormalizeCatalogHealth(string? value) => NormalizeFromAliases(value, CatalogHealthAliases);
    public string NormalizeLanguageForMatching(string? value) => NormalizeMatchToken(NormalizeLanguageCode(value));
    public string NormalizeGeographyForMatching(string? value)
    {
        var normalizedProvince = NormalizeProvinceCode(value);
        if (!string.IsNullOrWhiteSpace(normalizedProvince))
        {
            return NormalizeMatchToken(normalizedProvince);
        }

        return NormalizeMatchToken(value);
    }

    private static AdminLookupOptionResponse CreateOption(string value, string label) => new()
    {
        Value = value,
        Label = label
    };

    private static Dictionary<string, string> BuildAliasMap(IEnumerable<AdminLookupOptionResponse> options)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
        {
            var canonical = option.Value.Trim().ToLowerInvariant();
            map[canonical] = canonical;
            map[option.Label.Trim().ToLowerInvariant()] = canonical;
            map[option.Label.Trim().Replace("-", "_").Replace(" ", "_").ToLowerInvariant()] = canonical;
        }

        map["kzn"] = "kwazulu_natal";
        map["zulu"] = "isizulu";
        map["xhosa"] = "isixhosa";
        map["sotho"] = "sesotho";
        map["tswana"] = "setswana";
        map["pedi"] = "sepedi";
        map["swati"] = "siswati";
        map["ndebele"] = "isindebele";
        map["venda"] = "tshivenda";
        map["tsonga"] = "xitsonga";
        return map;
    }

    private static string NormalizeFromAliases(string? value, IReadOnlyDictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var normalized = trimmed.ToLowerInvariant();
        if (aliases.TryGetValue(normalized, out var canonical))
        {
            return canonical;
        }

        normalized = normalized.Replace("-", "_").Replace(" ", "_");
        if (aliases.TryGetValue(normalized, out canonical))
        {
            return canonical;
        }

        return normalized;
    }

    private static string NormalizeMatchToken(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);
    }
}

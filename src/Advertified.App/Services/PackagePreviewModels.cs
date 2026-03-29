namespace Advertified.App.Services;

public sealed class PackagePreviewAreaProfile
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> FallbackExampleLocations { get; set; } = new();

    public List<string> ProvinceTerms { get; set; } = new();

    public List<string> CityTerms { get; set; } = new();

    public List<string> StationTerms { get; set; } = new();
}

internal sealed class PackagePreviewAreaProfileRow
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? FallbackLocationsJson { get; set; }

    public string? Province { get; set; }

    public string? City { get; set; }

    public string? StationOrChannelName { get; set; }
}

public sealed class OohPreviewRow
{
    public string Suburb { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string? RegionClusterCode { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public string? GpsCoordinates { get; set; }

    public decimal Cost { get; set; }

    public long TrafficCount { get; set; }
}

internal sealed class RadioPreviewRow
{
    public Guid SourceId { get; set; }

    public string StationName { get; set; } = string.Empty;

    public string InventoryName { get; set; } = string.Empty;

    public string Daypart { get; set; } = string.Empty;

    public string InventoryKind { get; set; } = string.Empty;

    public decimal Cost { get; set; }

    public string? GeographyScope { get; set; }

    public string? RegionClusterCode { get; set; }

    public string MarketScope { get; set; } = string.Empty;

    public string MarketTier { get; set; } = string.Empty;

    public int MonthlyListenership { get; set; }

    public decimal BrandStrengthScore { get; set; }

    public decimal CoverageScore { get; set; }

    public decimal AudiencePowerScore { get; set; }

    public string PrimaryAudience { get; set; } = string.Empty;

    public bool IsFlagshipStation { get; set; }

    public bool IsPremiumStation { get; set; }

    public string? ReferenceShowName { get; set; }

    public string? AudienceSummary { get; set; }

    public string? SourceUrl { get; set; }
}

internal sealed class RadioPreviewCandidate
{
    public RadioPreviewCandidate(RadioPreviewRow row, decimal score)
    {
        Row = row;
        Score = score;
    }

    public RadioPreviewRow Row { get; }

    public decimal Score { get; }
}

internal sealed class TvPreviewRow
{
    public Guid SourceId { get; set; }

    public string ChannelName { get; set; } = string.Empty;

    public string ProgrammeName { get; set; } = string.Empty;

    public string Daypart { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public decimal Cost { get; set; }

    public string? AudienceSummary { get; set; }
}

internal sealed class BroadcastPackageCandidate
{
    public string Name { get; set; } = string.Empty;
    public decimal? InvestmentZar { get; set; }
    public decimal? PackageCostZar { get; set; }
    public decimal? CostPerMonthZar { get; set; }
    public int? Exposure { get; set; }
    public int? TotalExposure { get; set; }
    public int? NumberOfSpots { get; set; }
    public string? Notes { get; set; }
}

internal sealed class BroadcastRateCandidate
{
    public string GroupName { get; set; } = string.Empty;
    public string SlotLabel { get; set; } = string.Empty;
    public decimal RateZar { get; set; }
    public string? ProgrammeName { get; set; }
}

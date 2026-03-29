namespace Advertified.App.Contracts.Admin;

public sealed class AdminOutletPricingResponse
{
    public string OutletCode { get; set; } = string.Empty;
    public string OutletName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string CoverageType { get; set; } = string.Empty;
    public bool HasPricing { get; set; }
    public IReadOnlyList<AdminOutletPricingPackageResponse> Packages { get; set; } = Array.Empty<AdminOutletPricingPackageResponse>();
    public IReadOnlyList<AdminOutletSlotRateResponse> SlotRates { get; set; } = Array.Empty<AdminOutletSlotRateResponse>();
}

public sealed class AdminOutletPricingPackageResponse
{
    public Guid Id { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string? PackageType { get; set; }
    public int? ExposureCount { get; set; }
    public int? MonthlyExposureCount { get; set; }
    public decimal? ValueZar { get; set; }
    public decimal? DiscountZar { get; set; }
    public decimal? SavingZar { get; set; }
    public decimal? InvestmentZar { get; set; }
    public decimal? CostPerMonthZar { get; set; }
    public int? DurationMonths { get; set; }
    public int? DurationWeeks { get; set; }
    public string? Notes { get; set; }
    public string? SourceName { get; set; }
    public DateOnly? SourceDate { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AdminOutletSlotRateResponse
{
    public Guid Id { get; set; }
    public string DayGroup { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int AdDurationSeconds { get; set; }
    public decimal RateZar { get; set; }
    public string RateType { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public DateOnly? SourceDate { get; set; }
    public bool IsActive { get; set; }
}

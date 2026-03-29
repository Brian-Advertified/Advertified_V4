namespace Advertified.App.Contracts.Admin;

public sealed class UpsertAdminOutletPricingPackageRequest
{
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
    public bool IsActive { get; set; } = true;
}

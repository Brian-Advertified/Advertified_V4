namespace Advertified.App.Contracts.Admin;

public sealed class AdminDashboardResponse
{
    public AdminSummaryResponse Summary { get; set; } = new();
    public IReadOnlyList<AdminAlertResponse> Alerts { get; set; } = Array.Empty<AdminAlertResponse>();
    public IReadOnlyList<AdminOutletResponse> Outlets { get; set; } = Array.Empty<AdminOutletResponse>();
    public IReadOnlyList<AdminImportDocumentResponse> RecentImports { get; set; } = Array.Empty<AdminImportDocumentResponse>();
    public AdminHealthResponse Health { get; set; } = new();
    public IReadOnlyList<AdminHealthIssueResponse> HealthIssues { get; set; } = Array.Empty<AdminHealthIssueResponse>();
    public IReadOnlyList<AdminAreaMappingResponse> Areas { get; set; } = Array.Empty<AdminAreaMappingResponse>();
    public IReadOnlyList<AdminPackageSettingResponse> PackageSettings { get; set; } = Array.Empty<AdminPackageSettingResponse>();
    public AdminPricingSettingsResponse PricingSettings { get; set; } = new();
    public IReadOnlyList<AdminEnginePolicyResponse> EnginePolicies { get; set; } = Array.Empty<AdminEnginePolicyResponse>();
    public IReadOnlyList<AdminPreviewRuleResponse> PreviewRules { get; set; } = Array.Empty<AdminPreviewRuleResponse>();
    public AdminMonitoringResponse Monitoring { get; set; } = new();
    public IReadOnlyList<AdminUserResponse> Users { get; set; } = Array.Empty<AdminUserResponse>();
    public IReadOnlyList<AdminAuditEntryResponse> AuditEntries { get; set; } = Array.Empty<AdminAuditEntryResponse>();
    public AdminIntegrationStatusResponse Integrations { get; set; } = new();
}

public sealed class AdminSummaryResponse
{
    public int ActiveOutlets { get; set; }
    public int WeakOutlets { get; set; }
    public int SourceDocuments { get; set; }
    public int FallbackRatePercent { get; set; }
}

public sealed class AdminAlertResponse
{
    public string Title { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Owner { get; set; } = "Admin";
}

public sealed class AdminOutletResponse
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string CoverageType { get; set; } = string.Empty;
    public string GeographyLabel { get; set; } = string.Empty;
    public string CatalogHealth { get; set; } = string.Empty;
    public bool HasPricing { get; set; }
    public int PackageCount { get; set; }
    public int SlotRateCount { get; set; }
    public decimal? MinPackagePrice { get; set; }
    public decimal? MinSlotRate { get; set; }
    public string? LanguageDisplay { get; set; }
    public string? BroadcastFrequency { get; set; }
}

public sealed class AdminImportDocumentResponse
{
    public string SourceFile { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? SupplierOrStation { get; set; }
    public string? DocumentTitle { get; set; }
    public string? Notes { get; set; }
    public int? PageCount { get; set; }
    public DateTime ImportedAt { get; set; }
}

public sealed class AdminHealthResponse
{
    public int StrongCount { get; set; }
    public int MixedCount { get; set; }
    public int WeakUnpricedCount { get; set; }
    public int WeakNoInventoryCount { get; set; }
}

public sealed class AdminHealthIssueResponse
{
    public string OutletCode { get; set; } = string.Empty;
    public string OutletName { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
}

public sealed class AdminAreaMappingResponse
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MappingCount { get; set; }
}

public sealed class AdminPackageSettingResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MinBudget { get; set; }
    public decimal MaxBudget { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? RecommendedSpend { get; set; }
    public bool IsRecommended { get; set; }
    public string PackagePurpose { get; set; } = string.Empty;
    public string AudienceFit { get; set; } = string.Empty;
    public string QuickBenefit { get; set; } = string.Empty;
    public string IncludeRadio { get; set; } = string.Empty;
    public string IncludeTv { get; set; } = string.Empty;
    public string LeadTime { get; set; } = string.Empty;
    public IReadOnlyList<string> Benefits { get; set; } = Array.Empty<string>();
}

public sealed class AdminPricingSettingsResponse
{
    public decimal AiStudioReservePercent { get; set; }
    public decimal OohMarkupPercent { get; set; }
    public decimal RadioMarkupPercent { get; set; }
    public decimal TvMarkupPercent { get; set; }
}

public sealed class AdminEnginePolicyResponse
{
    public string PackageCode { get; set; } = string.Empty;
    public decimal BudgetFloor { get; set; }
    public int MinimumNationalRadioCandidates { get; set; }
    public bool RequireNationalCapableRadio { get; set; }
    public bool RequirePremiumNationalRadio { get; set; }
    public int NationalRadioBonus { get; set; }
    public int NonNationalRadioPenalty { get; set; }
    public int RegionalRadioPenalty { get; set; }
}

public sealed class AdminPreviewRuleResponse
{
    public string PackageCode { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string TierCode { get; set; } = string.Empty;
    public string TierLabel { get; set; } = string.Empty;
    public IReadOnlyList<string> TypicalInclusions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> IndicativeMix { get; set; } = Array.Empty<string>();
}

public sealed class AdminMonitoringResponse
{
    public int TotalCampaigns { get; set; }
    public int PlanningReadyCount { get; set; }
    public int WaitingOnClientCount { get; set; }
    public int InventoryRows { get; set; }
    public int ActiveAreaCount { get; set; }
    public int RecommendationCount { get; set; }
}

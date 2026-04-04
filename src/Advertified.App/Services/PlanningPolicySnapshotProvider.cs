using Advertified.App.Configuration;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class PlanningPolicySnapshotProvider
{
    private readonly Npgsql.NpgsqlDataSource? _dataSource;
    private readonly PlanningPolicyOptions _fallbackOptions;

    public PlanningPolicySnapshotProvider(Npgsql.NpgsqlDataSource dataSource, PlanningPolicyOptions fallbackOptions)
    {
        _dataSource = dataSource;
        _fallbackOptions = fallbackOptions;
    }

    public PlanningPolicySnapshotProvider(PlanningPolicyOptions fallbackOptions)
    {
        _fallbackOptions = fallbackOptions;
    }

    public PlanningPolicyOptions GetCurrent()
    {
        if (_dataSource is null)
        {
            return new PlanningPolicyOptions
            {
                Scale = ClonePolicy(_fallbackOptions.Scale),
                Dominance = ClonePolicy(_fallbackOptions.Dominance),
            };
        }

        using var connection = _dataSource.OpenConnection();

        var rows = connection.Query<EnginePolicyOverrideRecord>(
            @"
            select
                package_code as PackageCode,
                budget_floor as BudgetFloor,
                minimum_national_radio_candidates as MinimumNationalRadioCandidates,
                require_national_capable_radio as RequireNationalCapableRadio,
                require_premium_national_radio as RequirePremiumNationalRadio,
                national_radio_bonus as NationalRadioBonus,
                non_national_radio_penalty as NonNationalRadioPenalty,
                regional_radio_penalty as RegionalRadioPenalty
            from admin_engine_policy_overrides;");

        var result = new PlanningPolicyOptions
        {
            Scale = ClonePolicy(_fallbackOptions.Scale),
            Dominance = ClonePolicy(_fallbackOptions.Dominance),
        };

        foreach (var row in rows)
        {
            if (row.PackageCode.Equals("scale", StringComparison.OrdinalIgnoreCase))
            {
                Apply(result.Scale, row);
            }
            else if (row.PackageCode.Equals("dominance", StringComparison.OrdinalIgnoreCase))
            {
                Apply(result.Dominance, row);
            }
        }

        return result;
    }

    private static PackagePlanningPolicy ClonePolicy(PackagePlanningPolicy source)
    {
        return new PackagePlanningPolicy
        {
            BudgetFloor = source.BudgetFloor,
            MinimumNationalRadioCandidates = source.MinimumNationalRadioCandidates,
            RequireNationalCapableRadio = source.RequireNationalCapableRadio,
            RequirePremiumNationalRadio = source.RequirePremiumNationalRadio,
            NationalRadioBonus = source.NationalRadioBonus,
            NonNationalRadioPenalty = source.NonNationalRadioPenalty,
            RegionalRadioPenalty = source.RegionalRadioPenalty,
        };
    }

    private static void Apply(PackagePlanningPolicy target, EnginePolicyOverrideRecord source)
    {
        target.BudgetFloor = source.BudgetFloor;
        target.MinimumNationalRadioCandidates = source.MinimumNationalRadioCandidates;
        target.RequireNationalCapableRadio = source.RequireNationalCapableRadio;
        target.RequirePremiumNationalRadio = source.RequirePremiumNationalRadio;
        target.NationalRadioBonus = source.NationalRadioBonus;
        target.NonNationalRadioPenalty = source.NonNationalRadioPenalty;
        target.RegionalRadioPenalty = source.RegionalRadioPenalty;
    }

    private sealed class EnginePolicyOverrideRecord
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
}

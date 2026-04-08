using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
namespace Advertified.App.Services;

public sealed class PlanningPolicyService : IPlanningPolicyService
{
    private readonly PlanningPolicyOptions _policyOptions;

    public PlanningPolicyService(PlanningPolicySnapshotProvider snapshotProvider)
    {
        _policyOptions = snapshotProvider.GetCurrent();
    }

    public PlanningPolicyContext BuildPolicyContext(CampaignPlanningRequest request)
    {
        var applicablePolicy = GetApplicablePackagePolicy(request);
        return new PlanningPolicyContext
        {
            PackagePolicyCode = request.SelectedBudget >= _policyOptions.Dominance.BudgetFloor ? "dominance" : "scale",
            BudgetFloor = applicablePolicy.BudgetFloor,
            MinimumNationalRadioCandidates = applicablePolicy.MinimumNationalRadioCandidates,
            RequireNationalCapableRadio = applicablePolicy.RequireNationalCapableRadio,
            RequirePremiumNationalRadio = applicablePolicy.RequirePremiumNationalRadio,
            NationalRadioBonus = applicablePolicy.NationalRadioBonus,
            NonNationalRadioPenalty = applicablePolicy.NonNationalRadioPenalty,
            RegionalRadioPenalty = applicablePolicy.RegionalRadioPenalty,
            RequestedMixLabel = BuildRequestedMixLabel(request),
            RequestedChannelShares = GetRequestedChannelShares(request),
            RequiredChannels = GetRequiredChannels(request)
        };
    }

    public PlanningPolicyOutcome ApplyHigherBandRadioEligibility(List<InventoryCandidate> candidates, CampaignPlanningRequest request)
    {
        if (request.SelectedBudget < _policyOptions.Scale.BudgetFloor)
        {
            return new PlanningPolicyOutcome(candidates, Array.Empty<string>(), Array.Empty<PlanningCandidateRejection>());
        }

        var radioCandidates = candidates
            .Where(candidate => candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (radioCandidates.Count == 0)
        {
            return new PlanningPolicyOutcome(candidates, new[] { "radio_inventory_unavailable" }, Array.Empty<PlanningCandidateRejection>());
        }

        var nationalRadioCandidates = radioCandidates
            .Where(candidate => IsNationalCapableRadioCandidate(candidate, request))
            .ToList();

        var applicablePolicy = GetApplicablePackagePolicy(request);
        if (nationalRadioCandidates.Count < applicablePolicy.MinimumNationalRadioCandidates)
        {
            return new PlanningPolicyOutcome(candidates, new[] { "national_radio_inventory_insufficient", "policy_relaxed" }, Array.Empty<PlanningCandidateRejection>());
        }

        var removedRadioCandidates = radioCandidates
            .Where(candidate => !IsNationalCapableRadioCandidate(candidate, request))
            .Select(candidate => new PlanningCandidateRejection(
                "policy",
                "radio_not_national_capable",
                candidate.SourceId,
                candidate.DisplayName,
                candidate.MediaType.Trim().ToLowerInvariant()))
            .ToList();

        return new PlanningPolicyOutcome(
            candidates
                .Where(candidate => !candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) || IsNationalCapableRadioCandidate(candidate, request))
                .ToList(),
            Array.Empty<string>(),
            removedRadioCandidates);
    }

    public decimal GetHigherBandRadioBonus(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        if (!candidate.MediaType.Equals("Radio", StringComparison.OrdinalIgnoreCase) || request.SelectedBudget < _policyOptions.Scale.BudgetFloor)
        {
            return 0m;
        }

        var isNational = IsNationalCapableRadioCandidate(candidate, request);
        var isRegionalOnly = IsRegionalOrProvincialRadioCandidate(candidate);
        var applicablePolicy = GetApplicablePackagePolicy(request);

        if (isNational)
        {
            return applicablePolicy.NationalRadioBonus;
        }

        return isRegionalOnly
            ? -applicablePolicy.RegionalRadioPenalty
            : -applicablePolicy.NonNationalRadioPenalty;
    }

    public bool IsNationalCapableRadioCandidate(InventoryCandidate candidate, CampaignPlanningRequest request)
    {
        var marketTier = candidate.MarketTier?.Trim() ?? string.Empty;
        var coverage = ResolveCandidateCoverage(candidate);
        var isNational = coverage == "national";
        var isFlagshipOrPremium = candidate.IsFlagshipStation
            || candidate.IsPremiumStation
            || marketTier.Equals("flagship", StringComparison.OrdinalIgnoreCase)
            || marketTier.Equals("premium", StringComparison.OrdinalIgnoreCase);

        if (request.SelectedBudget >= _policyOptions.Dominance.BudgetFloor)
        {
            return _policyOptions.Dominance.RequirePremiumNationalRadio
                ? isNational && isFlagshipOrPremium
                : isNational;
        }

        if (!_policyOptions.Scale.RequireNationalCapableRadio)
        {
            return true;
        }

        return isNational || candidate.IsFlagshipStation;
    }

    public string GetPricingModel(InventoryCandidate candidate)
    {
        if (candidate.Metadata.TryGetValue("pricingModel", out var value) && value is not null)
        {
            var normalized = value.ToString();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized.Trim();
            }
        }

        return candidate.SourceType switch
        {
            "radio_package" => "package_total",
            "radio_slot" => "per_spot_rate_card",
            "ooh" => "fixed_placement_total",
            _ => candidate.PackageOnly ? "package_total" : "unit_rate"
        };
    }

    public bool IsRepeatableCandidate(InventoryCandidate candidate)
    {
        return !IsPackageTotalCandidate(candidate) && !IsFixedPlacementCandidate(candidate);
    }

    public IReadOnlyList<string> GetRequiredChannels(CampaignPlanningRequest request)
    {
        return GetRequestedChannelShares(request)
            .Select(share => share.Channel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<RequestedChannelShare> GetRequestedChannelShares(CampaignPlanningRequest request)
    {
        var shares = new List<RequestedChannelShare>();
        AddRequestedShare(shares, "radio", request.TargetRadioShare);
        AddRequestedShare(shares, "ooh", request.TargetOohShare);
        AddRequestedShare(shares, "digital", request.TargetDigitalShare);
        AddRequestedShare(shares, "tv", request.TargetTvShare);

        var explicitTotal = shares.Sum(entry => entry.Share);
        var hasExplicitTvShare = request.TargetTvShare.GetValueOrDefault() > 0;
        var includeTv = request.PreferredMediaTypes
            .Any(media => string.Equals(media?.Trim(), "tv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(media?.Trim(), "television", StringComparison.OrdinalIgnoreCase));
        var tvShare = Math.Max(0, 100 - explicitTotal);
        if (!hasExplicitTvShare && includeTv && tvShare > 0)
        {
            shares.Add(new RequestedChannelShare
            {
                Channel = "tv",
                Share = tvShare
            });
        }

        return shares;
    }

    public int? GetTargetShare(string? mediaType, CampaignPlanningRequest request)
    {
        var normalized = mediaType?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "radio" => request.TargetRadioShare,
            "ooh" => request.TargetOohShare,
            "tv" => request.TargetTvShare,
            "digital" => request.TargetDigitalShare,
            _ => null
        };
    }

    public string? BuildRequestedMixLabel(CampaignPlanningRequest request)
    {
        var parts = new List<string>();
        if (request.TargetRadioShare.HasValue) parts.Add($"Radio {request.TargetRadioShare.Value}%");
        if (request.TargetOohShare.HasValue) parts.Add($"Billboards and Digital Screens {request.TargetOohShare.Value}%");
        if (request.TargetTvShare.HasValue) parts.Add($"TV {request.TargetTvShare.Value}%");
        if (request.TargetDigitalShare.HasValue) parts.Add($"Digital {request.TargetDigitalShare.Value}%");
        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    }

    private PackagePlanningPolicy GetApplicablePackagePolicy(CampaignPlanningRequest request)
    {
        return request.SelectedBudget >= _policyOptions.Dominance.BudgetFloor
            ? _policyOptions.Dominance
            : _policyOptions.Scale;
    }

    private static void AddRequestedShare(List<RequestedChannelShare> shares, string channel, int? share)
    {
        if (share.GetValueOrDefault() > 0)
        {
            shares.Add(new RequestedChannelShare
            {
                Channel = channel,
                Share = share!.Value
            });
        }
    }

    private static bool IsRegionalOrProvincialRadioCandidate(InventoryCandidate candidate)
    {
        var coverage = ResolveCandidateCoverage(candidate);
        return coverage is "provincial" or "local";
    }

    private static string ResolveCandidateCoverage(InventoryCandidate candidate)
    {
        var scope = (candidate.MarketScope ?? string.Empty).Trim().ToLowerInvariant();
        if (scope.Contains("national", StringComparison.OrdinalIgnoreCase))
        {
            return "national";
        }

        if (scope.Contains("provincial", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("regional", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("province", StringComparison.OrdinalIgnoreCase))
        {
            return "provincial";
        }

        if (scope.Contains("local", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("city", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("suburb", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("metro", StringComparison.OrdinalIgnoreCase))
        {
            return "local";
        }

        var clusterCode = (candidate.RegionClusterCode ?? string.Empty).Trim().ToLowerInvariant();
        if (clusterCode == "national")
        {
            return "national";
        }

        if (!string.IsNullOrWhiteSpace(clusterCode))
        {
            return "provincial";
        }

        if (!string.IsNullOrWhiteSpace(candidate.City) || !string.IsNullOrWhiteSpace(candidate.Suburb))
        {
            return "local";
        }

        if (!string.IsNullOrWhiteSpace(candidate.Province))
        {
            return "provincial";
        }

        return "unknown";
    }

    private bool IsPackageTotalCandidate(InventoryCandidate candidate)
    {
        return GetPricingModel(candidate).Equals("package_total", StringComparison.OrdinalIgnoreCase)
            || candidate.PackageOnly;
    }

    private bool IsFixedPlacementCandidate(InventoryCandidate candidate)
    {
        return GetPricingModel(candidate).Equals("fixed_placement_total", StringComparison.OrdinalIgnoreCase);
    }
}


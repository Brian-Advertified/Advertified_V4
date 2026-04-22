using Advertified.App.Configuration;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
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
        AddRequestedShare(shares, "digital", request.TargetDigitalShare);
        AddRequestedShare(shares, "tv", request.TargetTvShare);

        if (request.BudgetAllocation?.ChannelAllocations.Count > 0)
        {
            var allocationShares = request.BudgetAllocation.ChannelAllocations
                .Where(allocation => allocation.Weight > 0m)
                .Select(allocation => new RequestedChannelShare
                {
                    Channel = NormalizeChannel(allocation.Channel),
                    Share = (int)Math.Round(allocation.Weight * 100m, MidpointRounding.AwayFromZero)
                })
                .Where(allocation => allocation.Share > 0)
                .OrderByDescending(allocation => allocation.Share)
                .ToArray();
            if (allocationShares.Length > 0)
            {
                return allocationShares;
            }
        }

        AddRequestedShare(shares, PlanningChannelSupport.OohAlias, request.TargetOohShare);
        if (shares.Count > 0)
        {
            return shares;
        }

        return Array.Empty<RequestedChannelShare>();
    }

    public int? GetTargetShare(string? mediaType, CampaignPlanningRequest request)
    {
        var normalized = PlanningChannelSupport.NormalizeChannel(mediaType);
        var allocationChannel = PlanningChannelSupport.IsOohFamilyChannel(normalized)
            ? PlanningChannelSupport.OohAlias
            : normalized;
        var explicitShare = normalized switch
        {
            "radio" => request.TargetRadioShare,
            "ooh" => request.TargetOohShare,
            "billboard" => request.TargetOohShare,
            "digital_screen" => request.TargetOohShare,
            "tv" => request.TargetTvShare,
            "digital" => request.TargetDigitalShare,
            _ => null
        };

        if (explicitShare.GetValueOrDefault() > 0)
        {
            return explicitShare;
        }

        if (request.BudgetAllocation?.ChannelAllocations.Count > 0 && !string.IsNullOrWhiteSpace(allocationChannel))
        {
            var allocation = request.BudgetAllocation.ChannelAllocations
                .FirstOrDefault(entry => string.Equals(NormalizeChannel(entry.Channel), allocationChannel, StringComparison.OrdinalIgnoreCase));
            if (allocation is not null && allocation.Weight > 0m)
            {
                return (int)Math.Round(allocation.Weight * 100m, MidpointRounding.AwayFromZero);
            }
        }

        return explicitShare;
    }

    public IReadOnlyDictionary<string, decimal>? GetChannelSpendTargets(CampaignPlanningRequest request)
    {
        if (request.BudgetAllocation?.ChannelAllocations.Count > 0)
        {
            return request.BudgetAllocation.ChannelAllocations
                .Where(entry => entry.Amount > 0m)
                .GroupBy(entry => NormalizeChannelBudgetKey(entry.Channel), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(entry => entry.Amount),
                    StringComparer.OrdinalIgnoreCase);
        }

        return null;
    }

    public string NormalizeChannelBudgetKey(string? mediaType)
    {
        var normalized = PlanningChannelSupport.NormalizeChannel(mediaType);
        return PlanningChannelSupport.IsOohFamilyChannel(normalized)
            ? PlanningChannelSupport.OohAlias
            : normalized;
    }

    public decimal GetChannelOvershootTolerance(decimal targetAmount)
    {
        return Math.Max(5000m, decimal.Round(targetAmount * 0.10m, 2, MidpointRounding.AwayFromZero));
    }

    public string? BuildRequestedMixLabel(CampaignPlanningRequest request)
    {
        var parts = GetRequestedChannelShares(request)
            .Select(share => share.Channel switch
            {
                "radio" => $"Radio {share.Share}%",
                "billboard" => $"Billboards and Digital Screens {share.Share}%",
                "digital_screen" => $"Billboards and Digital Screens {share.Share}%",
                "ooh" => $"Billboards and Digital Screens {share.Share}%",
                "tv" => $"TV {share.Share}%",
                "digital" => $"Digital {share.Share}%",
                _ => $"{share.Channel} {share.Share}%"
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static string NormalizeChannel(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "television" => "tv",
            "billboards and digital screens" => PlanningChannelSupport.OohAlias,
            "billboards or digital screens" => PlanningChannelSupport.OohAlias,
            _ => PlanningChannelSupport.NormalizeChannel(value)
        };
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


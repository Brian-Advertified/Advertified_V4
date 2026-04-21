using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class PlanningBudgetAllocationService : IPlanningBudgetAllocationService
{
    private readonly PlanningBudgetAllocationSnapshotProvider _snapshotProvider;

    public PlanningBudgetAllocationService(PlanningBudgetAllocationSnapshotProvider snapshotProvider)
    {
        _snapshotProvider = snapshotProvider;
    }

    public PlanningBudgetAllocation Resolve(CampaignPlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var snapshot = _snapshotProvider.GetCurrent();
        var audienceSegment = ResolveAudienceSegment(request);
        var channelWeights = ResolveChannelWeights(snapshot, request);
        var geoRule = ResolveGeoRule(snapshot.GeoRules, request, audienceSegment);
        var geoWeights = NormalizeWeights(geoRule?.Weights, GetGeoFallbackWeights(request));

        return BuildAllocation(
            request.SelectedBudget,
            audienceSegment,
            geoRule?.PolicyKey ?? "fallback",
            channelWeights.PolicyKey,
            channelWeights.Weights,
            geoWeights,
            geoRule?.NearbyRadiusKm);
    }

    public PlanningBudgetAllocation RebalanceChannelTargets(CampaignPlanningRequest request, IReadOnlyDictionary<string, int> channelShares)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = request.BudgetAllocation ?? Resolve(request);
        var normalizedChannelWeights = NormalizeWeights(
            channelShares.ToDictionary(
                entry => entry.Key,
                entry => decimal.Round(entry.Value / 100m, 4, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase),
            current.ChannelAllocations.ToDictionary(entry => entry.Channel, entry => entry.Weight, StringComparer.OrdinalIgnoreCase));

        return BuildAllocation(
            request.SelectedBudget,
            current.AudienceSegment,
            current.GeoPolicyKey,
            current.ChannelPolicyKey,
            normalizedChannelWeights,
            current.GeoAllocations.ToDictionary(entry => entry.Bucket, entry => entry.Weight, StringComparer.OrdinalIgnoreCase),
            current.GeoAllocations.FirstOrDefault(entry => entry.Bucket.Equals("nearby", StringComparison.OrdinalIgnoreCase))?.RadiusKm);
    }

    private static PlanningBudgetAllocation BuildAllocation(
        decimal budget,
        string audienceSegment,
        string geoPolicyKey,
        string channelPolicyKey,
        IReadOnlyDictionary<string, decimal> channelWeights,
        IReadOnlyDictionary<string, decimal> geoWeights,
        double? nearbyRadiusKm)
    {
        var allocation = new PlanningBudgetAllocation
        {
            AudienceSegment = audienceSegment,
            ChannelPolicyKey = channelPolicyKey,
            GeoPolicyKey = geoPolicyKey,
            ChannelAllocations = channelWeights
                .Select(entry => new PlanningChannelAllocation
                {
                    Channel = entry.Key,
                    Weight = entry.Value,
                    Amount = decimal.Round(budget * entry.Value, 2, MidpointRounding.AwayFromZero)
                })
                .OrderByDescending(entry => entry.Weight)
                .ToList(),
            GeoAllocations = geoWeights
                .Select(entry => new PlanningGeoAllocation
                {
                    Bucket = entry.Key,
                    Weight = entry.Value,
                    Amount = decimal.Round(budget * entry.Value, 2, MidpointRounding.AwayFromZero),
                    RadiusKm = entry.Key.Equals("nearby", StringComparison.OrdinalIgnoreCase) ? nearbyRadiusKm : null
                })
                .OrderByDescending(entry => entry.Weight)
                .ToList()
        };

        foreach (var channel in allocation.ChannelAllocations)
        {
            foreach (var geo in allocation.GeoAllocations)
            {
                allocation.CompositeAllocations.Add(new PlanningAllocationLine
                {
                    Channel = channel.Channel,
                    Bucket = geo.Bucket,
                    Weight = decimal.Round(channel.Weight * geo.Weight, 4, MidpointRounding.AwayFromZero),
                    Amount = decimal.Round(budget * channel.Weight * geo.Weight, 2, MidpointRounding.AwayFromZero),
                    RadiusKm = geo.RadiusKm
                });
            }
        }

        return allocation;
    }

    private static (string PolicyKey, IReadOnlyDictionary<string, decimal> Weights) ResolveChannelWeights(
        PlanningBudgetAllocationPolicySnapshot snapshot,
        CampaignPlanningRequest request)
    {
        if (HasExplicitTargetMix(request))
        {
            var explicitWeights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["radio"] = ToWeight(request.TargetRadioShare),
                ["digital"] = ToWeight(request.TargetDigitalShare),
                ["tv"] = ToWeight(request.TargetTvShare)
            };
            AddOohWeight(explicitWeights, ToWeight(request.TargetOohShare));
            return ("explicit_request", NormalizeWeights(explicitWeights, GetExplicitRequestFallbackWeights(request, snapshot.BudgetBands, request.SelectedBudget)));
        }

        var matchedBand = ResolveBudgetBand(snapshot.BudgetBands, request.SelectedBudget);
        if (matchedBand is null)
        {
            throw new InvalidOperationException("Planning allocation budget bands are not configured.");
        }

        return (
            BuildBudgetBandPolicyKey(matchedBand.Name),
            BuildBudgetBandWeights(matchedBand, snapshot.GlobalRules, request));
    }

    private static GeoAllocationPolicyRule? ResolveGeoRule(
        IReadOnlyList<GeoAllocationPolicyRule> rules,
        CampaignPlanningRequest request,
        string audienceSegment)
    {
        return rules
            .Where(rule => IsMatch(rule.Objective, request.Objective))
            .Where(rule => IsMatch(rule.AudienceSegment, audienceSegment))
            .Where(rule => IsMatch(rule.GeographyScope, request.GeographyScope))
            .Where(rule => IsBudgetMatch(rule.MinBudget, rule.MaxBudget, request.SelectedBudget))
            .OrderByDescending(rule => GetSpecificity(rule.Objective, rule.AudienceSegment, rule.GeographyScope, rule.MinBudget, rule.MaxBudget))
            .ThenByDescending(rule => rule.Priority)
            .FirstOrDefault();
    }

    private static IReadOnlyDictionary<string, decimal> GetExplicitRequestFallbackWeights(
        CampaignPlanningRequest request,
        IReadOnlyList<BudgetBandAllocationPolicyRule> budgetBands,
        decimal selectedBudget)
    {
        var fallback = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["radio"] = 0.30m,
            ["digital"] = 0.35m
        };
        AddOohWeight(fallback, ToWeight(request.TargetOohShare));
        return fallback;
    }

    private static IReadOnlyDictionary<string, decimal> GetGeoFallbackWeights(CampaignPlanningRequest request)
    {
        var scope = NormalizeScope(request.GeographyScope);
        return scope switch
        {
            "local" => new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["origin"] = 0.60m,
                ["nearby"] = 0.40m
            },
            "national" => new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["origin"] = 0.35m,
                ["nearby"] = 0.20m,
                ["wider"] = 0.45m
            },
            _ => new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["origin"] = 0.50m,
                ["nearby"] = 0.25m,
                ["wider"] = 0.25m
            }
        };
    }

    private static IReadOnlyDictionary<string, decimal> NormalizeWeights(
        IReadOnlyDictionary<string, decimal>? source,
        IReadOnlyDictionary<string, decimal> fallback)
    {
        var raw = (source is not null && source.Count > 0 ? source : fallback)
            .Where(entry => entry.Value > 0m)
            .ToDictionary(entry => NormalizeChannelOrBucket(entry.Key), entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        if (raw.Count == 0)
        {
            raw = fallback.ToDictionary(entry => NormalizeChannelOrBucket(entry.Key), entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        }

        var total = raw.Sum(entry => entry.Value);
        if (total <= 0m)
        {
            total = 1m;
        }

        return raw.ToDictionary(
            entry => entry.Key,
            entry => decimal.Round(entry.Value / total, 4, MidpointRounding.AwayFromZero),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveAudienceSegment(CampaignPlanningRequest request)
    {
        var signals = CampaignStrategySupport.BuildSignals(request);
        if (signals.PremiumAudience
            || string.Equals(request.PricePositioning, "premium", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.PricePositioning, "luxury", StringComparison.OrdinalIgnoreCase))
        {
            return "premium";
        }

        return "general";
    }

    private static bool HasExplicitTargetMix(CampaignPlanningRequest request)
    {
        return request.TargetRadioShare.GetValueOrDefault() > 0
            || request.TargetOohShare.GetValueOrDefault() > 0
            || request.TargetDigitalShare.GetValueOrDefault() > 0
            || request.TargetTvShare.GetValueOrDefault() > 0;
    }

    private static decimal ToWeight(int? value) => value is > 0 ? value.Value / 100m : 0m;

    private static bool IsMatch(string? configured, string? actual)
    {
        return string.IsNullOrWhiteSpace(configured)
            || string.Equals(configured.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBudgetMatch(decimal? minBudget, decimal? maxBudget, decimal actualBudget)
    {
        return (!minBudget.HasValue || actualBudget >= minBudget.Value)
            && (!maxBudget.HasValue || actualBudget <= maxBudget.Value);
    }

    private static int GetSpecificity(string? objective, string? audienceSegment, string? geographyScope, decimal? minBudget, decimal? maxBudget)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(objective)) score += 4;
        if (!string.IsNullOrWhiteSpace(audienceSegment)) score += 3;
        if (!string.IsNullOrWhiteSpace(geographyScope)) score += 2;
        if (minBudget.HasValue || maxBudget.HasValue) score += 1;
        return score;
    }

    private static string NormalizeChannel(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "television" => "tv",
            "billboards or digital screens" => PlanningChannelSupport.OohAlias,
            "billboards and digital screens" => PlanningChannelSupport.OohAlias,
            _ => (value ?? string.Empty).Trim().ToLowerInvariant()
        };
    }

    private static string NormalizeScope(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "regional" => "provincial",
            _ => (value ?? string.Empty).Trim().ToLowerInvariant()
        };
    }

    private static string NormalizeChannelOrBucket(string? value)
    {
        var normalized = NormalizeChannel(value);
        return normalized switch
        {
            "origin_area" => "origin",
            "origin_anchor" => "origin",
            "nearby_radius" => "nearby",
            "nearby_ring" => "nearby",
            "rest_of_scope" => "wider",
            "broader_coverage" => "wider",
            _ => normalized
        };
    }

    private static BudgetBandAllocationPolicyRule? ResolveBudgetBand(
        IReadOnlyList<BudgetBandAllocationPolicyRule> budgetBands,
        decimal budget)
    {
        if (budgetBands.Count == 0)
        {
            return null;
        }

        var orderedBands = budgetBands
            .OrderBy(rule => rule.Min)
            .ThenBy(rule => rule.Max)
            .ToArray();

        return orderedBands.FirstOrDefault(rule => budget >= rule.Min && budget <= rule.Max)
            ?? orderedBands.LastOrDefault(rule => budget >= rule.Min)
            ?? orderedBands[0];
    }

    private static IReadOnlyDictionary<string, decimal> BuildBudgetBandWeights(
        BudgetBandAllocationPolicyRule budgetBand,
        PlanningAllocationGlobalRules globalRules,
        CampaignPlanningRequest request)
    {
        var ooh = ClampRatio(budgetBand.OohTarget);
        if (globalRules.MaxOoh > 0m)
        {
            ooh = Math.Min(ooh, ClampRatio(globalRules.MaxOoh));
        }

        var tv = 0m;
        if (budgetBand.TvEligible
            && globalRules.EnforceTvFloorIfPreferred
            && IsPreferredChannel(request, "tv"))
        {
            tv = ClampRatio(budgetBand.TvMin);
        }

        if (ooh + tv > 1m)
        {
            tv = Math.Max(0m, 1m - ooh);
        }

        var remaining = Math.Max(0m, 1m - ooh - tv);
        var radioMid = ResolveMidpoint(budgetBand.RadioRange);
        var digitalMid = ResolveMidpoint(budgetBand.DigitalRange);

        var radio = 0m;
        var digital = 0m;
        var totalMid = radioMid + digitalMid;
        if (remaining > 0m && totalMid > 0m)
        {
            radio = decimal.Round(remaining * (radioMid / totalMid), 4, MidpointRounding.AwayFromZero);
            digital = Math.Max(0m, remaining - radio);
        }
        else if (remaining > 0m)
        {
            radio = decimal.Round(remaining / 2m, 4, MidpointRounding.AwayFromZero);
            digital = Math.Max(0m, remaining - radio);
        }

        var minDigital = ClampRatio(globalRules.MinDigital);
        if (minDigital > 0m && digital < minDigital)
        {
            digital = Math.Min(minDigital, remaining);
            radio = Math.Max(0m, remaining - digital);
        }

        var weights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["tv"] = tv,
            ["radio"] = radio,
            ["digital"] = digital
        };
        AddOohWeight(weights, ooh);

        return NormalizeWeights(weights, new Dictionary<string, decimal>(weights, StringComparer.OrdinalIgnoreCase));
    }

    private static void AddOohWeight(
        IDictionary<string, decimal> weights,
        decimal oohWeight)
    {
        if (oohWeight <= 0m)
        {
            return;
        }

        weights[PlanningChannelSupport.OohAlias] = decimal.Round(oohWeight, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal ResolveMidpoint(IReadOnlyList<decimal> range)
    {
        if (range.Count == 0)
        {
            return 0m;
        }

        if (range.Count == 1)
        {
            return ClampRatio(range[0]);
        }

        return decimal.Round((ClampRatio(range[0]) + ClampRatio(range[1])) / 2m, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal ClampRatio(decimal value)
    {
        return Math.Min(1m, Math.Max(0m, value));
    }

    private static bool IsPreferredChannel(CampaignPlanningRequest request, string channel)
    {
        return request.PreferredMediaTypes
            .Select(NormalizeChannel)
            .SelectMany(PlanningChannelSupport.ExpandRequestedChannel)
            .Any(preferred => string.Equals(preferred, channel, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildBudgetBandPolicyKey(string name)
    {
        var trimmed = (name ?? string.Empty).Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return "budget_band";
        }

        var normalized = string.Concat(trimmed.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        return $"budget_band_{normalized.Trim('_')}";
    }
}

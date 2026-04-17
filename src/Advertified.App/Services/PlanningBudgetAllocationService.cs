using Advertified.App.Contracts.Campaigns;
using Advertified.App.Domain.Campaigns;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class PlanningBudgetAllocationService : IPlanningBudgetAllocationService
{
    private static readonly string[] DefaultChannels = { "ooh", "radio", "digital", "tv" };
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
        var channelWeights = ResolveChannelWeights(snapshot.ChannelRules, request, audienceSegment);
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
            GetChannelFallbackWeights(request));

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
        IReadOnlyList<ChannelAllocationPolicyRule> rules,
        CampaignPlanningRequest request,
        string audienceSegment)
    {
        if (HasExplicitTargetMix(request))
        {
            return ("explicit_request", NormalizeWeights(new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["radio"] = ToWeight(request.TargetRadioShare),
                ["ooh"] = ToWeight(request.TargetOohShare),
                ["digital"] = ToWeight(request.TargetDigitalShare),
                ["tv"] = ToWeight(request.TargetTvShare)
            }, GetChannelFallbackWeights(request)));
        }

        var matchedRule = rules
            .Where(rule => IsMatch(rule.Objective, request.Objective))
            .Where(rule => IsMatch(rule.AudienceSegment, audienceSegment))
            .Where(rule => IsMatch(rule.GeographyScope, request.GeographyScope))
            .Where(rule => IsBudgetMatch(rule.MinBudget, rule.MaxBudget, request.SelectedBudget))
            .OrderByDescending(rule => GetSpecificity(rule.Objective, rule.AudienceSegment, rule.GeographyScope, rule.MinBudget, rule.MaxBudget))
            .ThenByDescending(rule => rule.Priority)
            .FirstOrDefault();

        return (
            matchedRule?.PolicyKey ?? "fallback",
            NormalizeWeights(matchedRule?.Weights, GetChannelFallbackWeights(request)));
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

    private static IReadOnlyDictionary<string, decimal> GetChannelFallbackWeights(CampaignPlanningRequest request)
    {
        var preferredChannels = request.PreferredMediaTypes
            .Select(NormalizeChannel)
            .Where(static channel => !string.IsNullOrWhiteSpace(channel))
            .Except(request.ExcludedMediaTypes.Select(NormalizeChannel), StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var eligibleChannels = preferredChannels.Length > 0 ? preferredChannels : DefaultChannels
            .Except(request.ExcludedMediaTypes.Select(NormalizeChannel), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (eligibleChannels.Length == 0)
        {
            eligibleChannels = DefaultChannels;
        }

        var equalWeight = decimal.Round(1m / eligibleChannels.Length, 4, MidpointRounding.AwayFromZero);
        return eligibleChannels.ToDictionary(channel => channel, _ => equalWeight, StringComparer.OrdinalIgnoreCase);
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
            "billboards and digital screens" => "ooh",
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
}

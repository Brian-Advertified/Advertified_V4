using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadStrategyEngine : ILeadStrategyEngine
{
    private static readonly string[] SupportedChannels = { "Digital", "OOH", "Radio" };

    public LeadStrategyResult Build(
        LeadBusinessProfile businessProfile,
        LeadIndustryPolicyProfile industryPolicy,
        LeadIndustryContext? industryContext,
        LeadOpportunityProfile opportunityProfile,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections)
    {
        var baseSplit = ResolveBaseSplit(opportunityProfile.Key, industryPolicy.PreferredChannels, industryContext?.Channels.BaseBudgetSplit);
        var opportunityAdjustedSplit = ApplyOpportunityAdjustments(baseSplit, opportunityProfile);
        var adjustedSplit = ApplyChannelFitAdjustments(opportunityAdjustedSplit, channelDetections);
        var normalizedSplit = NormalizeSplit(adjustedSplit);

        var channelPlans = normalizedSplit
            .Select(item => new LeadStrategyChannelPlan
            {
                Channel = item.Key,
                BudgetSharePercent = item.Value,
                Reason = BuildChannelReason(item.Key, businessProfile, opportunityProfile, channelDetections)
            })
            .OrderByDescending(item => item.BudgetSharePercent)
            .ToArray();

        return new LeadStrategyResult
        {
            Archetype = opportunityProfile.Name,
            Objective = industryContext?.Campaign.DefaultObjective
                ?? industryPolicy.ObjectiveOverride
                ?? opportunityProfile.SuggestedCampaignType,
            Channels = channelPlans,
            GeoTargets = new[] { businessProfile.PrimaryLocation }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
            Timing = "30-day launch with weekly optimization checkpoints",
            Rationale = BuildStrategyRationale(businessProfile, opportunityProfile, industryContext)
        };
    }

    private static Dictionary<string, int> ResolveBaseSplit(
        string archetypeKey,
        IReadOnlyList<string> preferredChannels,
        IReadOnlyDictionary<string, int>? contextBaseSplit)
    {
        if (contextBaseSplit is not null && contextBaseSplit.Count > 0)
        {
            return contextBaseSplit.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        var policySplit = BuildPolicyPreferredSplit(preferredChannels);
        if (policySplit is not null)
        {
            return policySplit;
        }

        return archetypeKey.ToLowerInvariant() switch
        {
            "active_scaler" => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["OOH"] = 35,
                ["Radio"] = 30,
                ["Digital"] = 35
            },
            "promo_dependent_retailer" => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Radio"] = 35,
                ["OOH"] = 35,
                ["Digital"] = 30
            },
            "invisible_local_business" => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Digital"] = 45,
                ["OOH"] = 35,
                ["Radio"] = 20
            },
            "digital_only_player" => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["OOH"] = 40,
                ["Radio"] = 30,
                ["Digital"] = 30
            },
            _ => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Digital"] = 50,
                ["OOH"] = 30,
                ["Radio"] = 20
            }
        };
    }

    private static Dictionary<string, int>? BuildPolicyPreferredSplit(IReadOnlyList<string> preferredChannels)
    {
        var normalizedChannels = preferredChannels
            .Select(NormalizePolicyChannel)
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        if (normalizedChannels.Length == 0)
        {
            return null;
        }

        var split = SupportedChannels.ToDictionary(channel => channel, static _ => 0, StringComparer.OrdinalIgnoreCase);
        var weights = normalizedChannels.Length switch
        {
            1 => new[] { 100 },
            2 => new[] { 60, 40 },
            _ => new[] { 50, 30, 20 }
        };

        for (var index = 0; index < normalizedChannels.Length; index++)
        {
            split[normalizedChannels[index]!] = weights[index];
        }

        return split;
    }

    private static Dictionary<string, int> ApplyOpportunityAdjustments(
        Dictionary<string, int> split,
        LeadOpportunityProfile opportunityProfile)
    {
        var adjusted = new Dictionary<string, int>(split, StringComparer.OrdinalIgnoreCase);
        foreach (var channel in opportunityProfile.RecommendedChannels.Select(NormalizePolicyChannel))
        {
            if (string.IsNullOrWhiteSpace(channel) || !adjusted.ContainsKey(channel))
            {
                continue;
            }

            adjusted[channel] += 4;
        }

        return adjusted;
    }

    private static Dictionary<string, int> ApplyChannelFitAdjustments(
        Dictionary<string, int> split,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections)
    {
        var adjusted = new Dictionary<string, int>(split, StringComparer.OrdinalIgnoreCase);
        foreach (var detection in channelDetections)
        {
            var mappedChannel = MapDetectionChannel(detection.Channel);
            if (mappedChannel is null || !adjusted.ContainsKey(mappedChannel))
            {
                continue;
            }

            if (detection.Score >= 70)
            {
                adjusted[mappedChannel] += 5;
            }
            else if (detection.Score <= 25)
            {
                adjusted[mappedChannel] = Math.Max(5, adjusted[mappedChannel] - 5);
            }
        }

        return adjusted;
    }

    private static Dictionary<string, int> NormalizeSplit(Dictionary<string, int> split)
    {
        var total = split.Values.Sum();
        if (total <= 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Digital"] = 50,
                ["OOH"] = 30,
                ["Radio"] = 20
            };
        }

        var normalized = split.ToDictionary(
            pair => pair.Key,
            pair => (int)Math.Round(pair.Value * 100m / total, MidpointRounding.AwayFromZero),
            StringComparer.OrdinalIgnoreCase);

        var delta = 100 - normalized.Values.Sum();
        if (delta != 0)
        {
            var topKey = normalized.OrderByDescending(pair => pair.Value).First().Key;
            normalized[topKey] += delta;
        }

        return normalized;
    }

    private static string BuildChannelReason(
        string channel,
        LeadBusinessProfile businessProfile,
        LeadOpportunityProfile opportunityProfile,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections)
    {
        var detection = channelDetections.FirstOrDefault(item =>
            string.Equals(MapDetectionChannel(item.Channel), channel, StringComparison.OrdinalIgnoreCase));
        var fitSignal = detection is null
            ? "No strong activity signals detected yet"
            : $"Detected score {detection.Score}/100";

        return $"{opportunityProfile.Name} playbook for {businessProfile.BusinessType}. {fitSignal}.";
    }

    private static string BuildStrategyRationale(
        LeadBusinessProfile businessProfile,
        LeadOpportunityProfile opportunityProfile,
        LeadIndustryContext? industryContext)
    {
        var audiencePersona = industryContext?.Audience.PrimaryPersona;
        var geographyBias = industryContext?.Channels.GeographyBias;
        var funnelShape = industryContext?.Campaign.FunnelShape;

        var segments = new[]
        {
            $"Strategy prioritizes business fit for {businessProfile.BusinessType} in {businessProfile.PrimaryLocation}.",
            !string.IsNullOrWhiteSpace(audiencePersona)
                ? $"Audience focus centers on {audiencePersona.ToLowerInvariant()}."
                : null,
            !string.IsNullOrWhiteSpace(funnelShape)
                ? $"Funnel approach is {funnelShape.Replace('_', ' ')}."
                : null,
            !string.IsNullOrWhiteSpace(geographyBias)
                ? $"Geography bias is {geographyBias.Replace('-', ' ')}."
                : null,
            $"Opportunity gaps are anchored to the {opportunityProfile.Name} playbook."
        };

        return string.Join(" ", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    private static string? MapDetectionChannel(string channel)
    {
        return channel.ToLowerInvariant() switch
        {
            "social" => "Digital",
            "search" => "Digital",
            "billboards_ooh" => "OOH",
            "radio" => "Radio",
            _ => null
        };
    }

    private static string? NormalizePolicyChannel(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return null;
        }

        var normalized = channel.Trim().ToLowerInvariant();
        return normalized switch
        {
            "search" or "social" or "display" or "digital" => "Digital",
            "ooh" or "billboards_ooh" => "OOH",
            "radio" => "Radio",
            _ => null
        };
    }
}

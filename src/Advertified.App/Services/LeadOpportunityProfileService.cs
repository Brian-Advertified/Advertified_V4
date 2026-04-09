using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadOpportunityProfileService : ILeadOpportunityProfileService
{
    private const int StrongChannelMin = 60;
    private const int WeakChannelMax = 39;

    public LeadOpportunityProfile Build(
        Lead lead,
        Signal? latestSignal,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections,
        LeadIndustryPolicyProfile industryPolicy)
    {
        var archetype = InferBaseArchetype(lead, latestSignal, channelDetections);
        return ApplyIndustryPolicy(archetype, industryPolicy);
    }

    private static LeadOpportunityProfile InferBaseArchetype(
        Lead lead,
        Signal? latestSignal,
        IReadOnlyList<LeadChannelDetectionResult> channelDetections)
    {
        var social = GetChannelScore(channelDetections, "social");
        var search = GetChannelScore(channelDetections, "search");
        var ooh = GetChannelScore(channelDetections, "billboards_ooh");
        var radio = GetChannelScore(channelDetections, "radio");
        var hasPromo = latestSignal?.HasPromo ?? false;
        var websiteActive = latestSignal?.WebsiteUpdatedRecently ?? false;
        var hasWebsite = !string.IsNullOrWhiteSpace(lead.Website);
        var digitalStrong = social >= StrongChannelMin || search >= StrongChannelMin;
        var discountHeavyCategory = ContainsAny(lead.Category, "retail", "grocery", "supermarket", "discount");
        var locallyScoped = !ContainsAny(lead.Location, "national");

        if (social >= StrongChannelMin
            && hasPromo
            && (websiteActive || hasWebsite)
            && (search <= WeakChannelMax || ooh <= WeakChannelMax))
        {
            return new LeadOpportunityProfile
            {
                Key = "active_scaler",
                Name = "Active Scaler",
                SuggestedCampaignType = "brand_presence",
                DetectedGaps = new[]
                {
                    "You already show strong campaign momentum in digital channels.",
                    search <= WeakChannelMax
                        ? "Limited evidence of structured high-intent search capture."
                        : "Search capture exists, but there is room to improve conversion-focused demand capture.",
                    ooh <= WeakChannelMax
                        ? "No strong evidence of Billboards and Digital Screens activity was found."
                        : "Offline visibility can still be expanded to reinforce existing momentum.",
                },
                ExpectedOutcome = "Expected impact: stronger full-funnel coverage, better local visibility, and improved conversion of demand your digital activity is already generating.",
                RecommendedChannels = new[] { "OOH", "Radio", "Digital" },
                WhyActNow = "There is already active demand in your category. The immediate opportunity is to capture more of it before competitors extend into the same high-visibility channels.",
            };
        }

        if (hasPromo && social < StrongChannelMin && search <= WeakChannelMax && discountHeavyCategory)
        {
            return new LeadOpportunityProfile
            {
                Key = "promo_dependent_retailer",
                Name = "Promo-Dependent Retailer",
                SuggestedCampaignType = "promotion",
                DetectedGaps = new[]
                {
                    "Promotional activity is visible, but channel strength remains uneven.",
                    "Limited evidence of always-on search capture for high-intent demand.",
                    "Limited evidence of sustained awareness coverage beyond promotion cycles.",
                },
                ExpectedOutcome = "Expected impact: more consistent customer flow, stronger baseline demand between promotions, and improved campaign stability.",
                RecommendedChannels = new[] { "Radio", "OOH", "Digital" },
                WhyActNow = "Promotion-driven demand is present, but without stronger baseline awareness, customer flow remains volatile between cycles.",
            };
        }

        if (locallyScoped && (!hasWebsite || !websiteActive) && search <= WeakChannelMax && social <= WeakChannelMax)
        {
            return new LeadOpportunityProfile
            {
                Key = "invisible_local_business",
                Name = "Invisible Local Business",
                SuggestedCampaignType = "leads",
                DetectedGaps = new[]
                {
                    "There appears to be limited visibility when customers search in your local area.",
                    "Digital demand capture signals are currently weak.",
                    "Local awareness channels are underutilized relative to potential foot traffic.",
                },
                ExpectedOutcome = "Expected impact: stronger local discoverability, more inbound enquiries, and improved walk-in conversion potential.",
                RecommendedChannels = new[] { "Digital", "OOH" },
                WhyActNow = "Customers are already searching locally. Improving discoverability now can convert existing intent into measurable foot traffic.",
            };
        }

        if (digitalStrong && search >= 40 && ooh <= WeakChannelMax && radio <= WeakChannelMax)
        {
            return new LeadOpportunityProfile
            {
                Key = "digital_only_player",
                Name = "Digital-Only Player",
                SuggestedCampaignType = "awareness",
                DetectedGaps = new[]
                {
                    "Digital presence is established and generating momentum.",
                    "No strong evidence of offline visibility channels was found.",
                    "Growth may be constrained by over-reliance on digital-only reach.",
                },
                ExpectedOutcome = "Expected impact: expanded audience reach, improved brand recall, and reduced dependence on saturated digital inventory.",
                RecommendedChannels = new[] { "OOH", "Radio", "Digital" },
                WhyActNow = "Digital momentum is visible, but audience growth can plateau without additional offline reach.",
            };
        }

        return new LeadOpportunityProfile
        {
            Key = "passive_untapped_business",
            Name = "Passive / Untapped Business",
            SuggestedCampaignType = "awareness",
            DetectedGaps = new[]
            {
                "There is limited current evidence of sustained campaign activity.",
                "Demand capture and awareness signals are both low.",
                "A stronger baseline marketing foundation is needed before optimization.",
            },
            ExpectedOutcome = "Expected impact: stronger baseline visibility, more consistent lead flow, and a practical foundation for staged growth.",
            RecommendedChannels = new[] { "Digital", "OOH" },
            WhyActNow = "The earlier visibility is established, the faster demand signals can compound into consistent growth.",
        };
    }

    private static LeadOpportunityProfile ApplyIndustryPolicy(LeadOpportunityProfile baseProfile, LeadIndustryPolicyProfile policy)
    {
        var mergedChannels = baseProfile.RecommendedChannels
            .Concat(policy.PreferredChannels)
            .Select(channel => channel.Trim())
            .Where(channel => channel.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LeadOpportunityProfile
        {
            Key = baseProfile.Key,
            Name = baseProfile.Name,
            SuggestedCampaignType = policy.ObjectiveOverride ?? baseProfile.SuggestedCampaignType,
            DetectedGaps = baseProfile.DetectedGaps
                .Concat(new[] { policy.AdditionalGap })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray(),
            ExpectedOutcome = string.Join(
                " ",
                new[] { baseProfile.ExpectedOutcome, policy.AdditionalOutcome }
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
            RecommendedChannels = mergedChannels,
            WhyActNow = baseProfile.WhyActNow,
        };
    }

    private static int GetChannelScore(IReadOnlyList<LeadChannelDetectionResult> channelDetections, string channel)
    {
        return channelDetections.FirstOrDefault(item =>
            item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))?.Score ?? 0;
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate =>
            value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }
}

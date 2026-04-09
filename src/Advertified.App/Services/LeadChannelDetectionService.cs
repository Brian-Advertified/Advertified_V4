using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadChannelDetectionService : ILeadChannelDetectionService
{
    private static readonly string[] Channels =
    {
        "social",
        "search",
        "display",
        "tv",
        "radio",
        "billboards_ooh",
        "print",
        "influencer"
    };

    private readonly ILeadMasterDataService _leadMasterDataService;

    public LeadChannelDetectionService(ILeadMasterDataService leadMasterDataService)
    {
        _leadMasterDataService = leadMasterDataService;
    }

    public LeadChannelDetectionService()
        : this(new NoOpLeadMasterDataService())
    {
    }

    public IReadOnlyList<LeadChannelDetectionResult> Detect(
        Lead lead,
        Signal? signal,
        IReadOnlyList<LeadSignalEvidence>? evidences = null)
    {
        return Channels
            .Select(channel => BuildChannelResult(channel, lead, signal, evidences))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Channel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private LeadChannelDetectionResult BuildChannelResult(
        string channel,
        Lead lead,
        Signal? signal,
        IReadOnlyList<LeadSignalEvidence>? evidences)
    {
        var evidence = BuildPersistedEvidence(channel, evidences);
        var leadCategory = lead.Category.Trim();
        var hasWebsite = !string.IsNullOrWhiteSpace(lead.Website);
        var freshnessMultiplier = GetFreshnessMultiplier(signal?.CreatedAt);
        var offlineChannel = channel is "tv" or "radio" or "billboards_ooh" or "print";
        var hasSignalTypeEvidence = evidence
            .Select(item => item.Type)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        switch (channel)
        {
            case "social":
                if (signal?.HasMetaAds == true && !hasSignalTypeEvidence.Contains("meta_pixel_detected") && !hasSignalTypeEvidence.Contains("meta_ad_library_active_ads"))
                {
                    evidence.Add(CreateEvidence("meta_ad_proxy_signal", "website_signal", 30, 0.75m, freshnessMultiplier, "Website markers suggest possible Meta advertising activity."));
                }
                if (signal?.HasPromo == true && !hasSignalTypeEvidence.Contains("campaign_mention"))
                {
                    evidence.Add(CreateEvidence("campaign_mention", "website_signal", 15, 0.8m, freshnessMultiplier, "Promotional activity suggests active campaign traffic."));
                }
                if (signal?.WebsiteUpdatedRecently == true && !hasSignalTypeEvidence.Contains("recent_content_refresh"))
                {
                    evidence.Add(CreateEvidence("recent_content_refresh", "website_signal", 8, 0.7m, freshnessMultiplier, "Recent website updates support active social campaigns."));
                }
                AddCategoryPriors(channel, leadCategory, evidence);
                break;

            case "search":
                if (hasWebsite && !hasSignalTypeEvidence.Contains("conversion_surface"))
                {
                    evidence.Add(CreateEvidence("conversion_surface", "website_signal", 10, 0.7m, freshnessMultiplier, "A live website provides a destination for paid search traffic."));
                }
                if (signal?.HasPromo == true && !hasSignalTypeEvidence.Contains("paid_landing_page_pattern"))
                {
                    evidence.Add(CreateEvidence("paid_landing_page_pattern", "website_signal", 10, 0.75m, freshnessMultiplier, "Promotional website patterns can support search campaigns."));
                }
                if (signal?.WebsiteUpdatedRecently == true && !hasSignalTypeEvidence.Contains("fresh_website"))
                {
                    evidence.Add(CreateEvidence("fresh_website", "website_signal", 8, 0.7m, freshnessMultiplier, "Recently updated site content supports search marketing."));
                }
                if (!hasWebsite)
                {
                    evidence.Add(CreateEvidence("missing_website", "contradiction", -5, 1.0m, 1.0m, "No website reduces evidence for search activity."));
                }
                AddCategoryPriors(channel, leadCategory, evidence);
                break;

            case "display":
                if (signal?.HasMetaAds == true)
                {
                    evidence.Add(CreateEvidence("retargeting_stack", "website_signal", 12, 0.8m, freshnessMultiplier, "Meta ad signals can correlate with broader display retargeting."));
                }
                if (signal?.WebsiteUpdatedRecently == true)
                {
                    evidence.Add(CreateEvidence("active_campaign_destination", "website_signal", 6, 0.6m, freshnessMultiplier, "Recent website updates can support display campaign refreshes."));
                }
                if (!hasWebsite)
                {
                    evidence.Add(CreateEvidence("missing_display_destination", "contradiction", -6, 1.0m, 1.0m, "No website lowers the likelihood of active display campaigns."));
                }
                break;

            case "tv":
            case "radio":
            case "billboards_ooh":
            case "print":
            case "influencer":
                AddCategoryPriors(channel, leadCategory, evidence);

                if (signal?.HasPromo == true)
                {
                    var weight = channel is "radio" or "billboards_ooh" ? 10 : 6;
                    evidence.Add(CreateEvidence("campaign_proxy", "internal_rule", weight, 0.6m, freshnessMultiplier, "Promotional activity can indicate broader awareness media support."));
                }

                if (channel == "radio" && hasWebsite)
                {
                    evidence.Add(CreateEvidence("regional_business_surface", "internal_rule", 4, 0.6m, freshnessMultiplier, "An established business website modestly supports radio inference."));
                }

                if (channel == "billboards_ooh" && hasWebsite)
                {
                    evidence.Add(CreateEvidence("physical_brand_presence_proxy", "internal_rule", 4, 0.6m, freshnessMultiplier, "A maintained business presence modestly supports outdoor awareness activity."));
                }

                if (channel == "tv" && !hasWebsite)
                {
                    evidence.Add(CreateEvidence("limited_scale_signal", "contradiction", -12, 1.0m, 1.0m, "No website makes current TV activity less likely."));
                }

                if (channel == "billboards_ooh" && !hasWebsite)
                {
                    evidence.Add(CreateEvidence("limited_scale_signal", "contradiction", -10, 1.0m, 1.0m, "No web presence lowers confidence in active OOH campaigns."));
                }

                if (channel == "radio" && !hasWebsite)
                {
                    evidence.Add(CreateEvidence("limited_scale_signal", "contradiction", -6, 1.0m, 1.0m, "No web presence weakens radio confidence."));
                }

                if (channel == "print" && !hasWebsite)
                {
                    evidence.Add(CreateEvidence("digital_only_gap", "contradiction", -4, 1.0m, 1.0m, "No owned web surface reduces print support confidence."));
                }
                break;
        }

        var hasDirectEvidence = evidence.Any(x => IsDirectEvidence(x.Type));
        var rawScore = evidence.Sum(x => x.EffectiveWeight);
        var score = (int)Math.Clamp(Math.Round(rawScore, MidpointRounding.AwayFromZero), 0, 100);

        if (hasDirectEvidence)
        {
            score = Math.Max(score, 80);
        }

        // Offline channels stay conservative until we have direct evidence connectors.
        if (offlineChannel && !hasDirectEvidence && score > 49)
        {
            score = 49;
        }

        if (channel == "influencer" && score > 59)
        {
            score = 59;
        }

        var confidence = MapConfidence(score);
        var status = score <= 19 ? "no_evidence" : "evidence_found";
        var dominantReason = evidence
            .Where(x => x.EffectiveWeight > 0)
            .OrderByDescending(x => x.EffectiveWeight)
            .Select(x => x.Value)
            .FirstOrDefault()
            ?? "No usable evidence found for this channel yet.";

        return new LeadChannelDetectionResult
        {
            LeadId = lead.Id,
            Channel = channel,
            Score = score,
            Confidence = confidence,
            Status = status,
            DominantReason = dominantReason,
            LastEvidenceAtUtc = signal?.CreatedAt,
            Signals = evidence
                .OrderByDescending(x => x.EffectiveWeight)
                .ThenByDescending(x => x.Weight)
                .ToList()
        };
    }

    private static LeadChannelSignalEvidence CreateEvidence(
        string type,
        string source,
        int weight,
        decimal reliabilityMultiplier,
        decimal freshnessMultiplier,
        string value)
    {
        return new LeadChannelSignalEvidence
        {
            Type = type,
            Source = source,
            Weight = weight,
            ReliabilityMultiplier = reliabilityMultiplier,
            FreshnessMultiplier = freshnessMultiplier,
            EffectiveWeight = Math.Round(weight * reliabilityMultiplier * freshnessMultiplier, 2, MidpointRounding.AwayFromZero),
            Value = value
        };
    }

    private static decimal GetFreshnessMultiplier(DateTime? observedAtUtc)
    {
        if (!observedAtUtc.HasValue)
        {
            return 0.4m;
        }

        var ageInDays = (DateTime.UtcNow - observedAtUtc.Value).TotalDays;
        return ageInDays switch
        {
            <= 30 => 1.0m,
            <= 90 => 0.85m,
            <= 180 => 0.65m,
            <= 365 => 0.4m,
            _ => 0.2m
        };
    }

    private static string MapConfidence(int score)
    {
        return score switch
        {
            <= 19 => "no_evidence",
            <= 39 => "weak_signal",
            <= 59 => "weakly_inferred",
            <= 79 => "strongly_inferred",
            _ => "detected"
        };
    }

    private static bool IsDirectEvidence(string signalType)
    {
        return signalType switch
        {
            // Keep "direct evidence" strict. Website pattern signals are inference, not proof.
            "meta_ad_library_active_ads" => true,
            "google_ads_evidence" => true,
            "google_ads_transparency_signal" => true,
            "display_creative_detected" => true,
            "tv_ad_found" => true,
            "radio_promotion_found" => true,
            "outdoor_campaign_listing" => true,
            "print_insert_found" => true,
            "paid_partnership_found" => true,
            _ => false
        };
    }

    private static List<LeadChannelSignalEvidence> BuildPersistedEvidence(
        string channel,
        IReadOnlyList<LeadSignalEvidence>? evidences)
    {
        if (evidences is null || evidences.Count == 0)
        {
            return new List<LeadChannelSignalEvidence>();
        }

        return evidences
            .Where(item => item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))
            .Select(item => new LeadChannelSignalEvidence
            {
                Type = item.SignalType,
                Source = item.Source,
                Weight = item.Weight,
                ReliabilityMultiplier = item.ReliabilityMultiplier,
                FreshnessMultiplier = item.FreshnessMultiplier,
                EffectiveWeight = item.EffectiveWeight,
                Value = item.Value
            })
            .ToList();
    }

    private void AddCategoryPriors(string channel, string category, ICollection<LeadChannelSignalEvidence> evidence)
    {
        var industryCode = _leadMasterDataService.ResolveIndustry(category)?.Code;
        var normalized = category.ToLowerInvariant();

        if (ContainsAny(normalized, "dealership", "dealer", "auto", "car"))
        {
            if (channel == "radio") evidence.Add(CreateEvidence("category_prior", "internal_rule", 10, 0.7m, 1.0m, "Auto and dealership businesses commonly use radio."));
            if (channel == "billboards_ooh") evidence.Add(CreateEvidence("category_prior", "internal_rule", 8, 0.7m, 1.0m, "Auto and dealership businesses commonly use outdoor advertising."));
            if (channel == "search") evidence.Add(CreateEvidence("category_prior", "internal_rule", 6, 0.7m, 1.0m, "Auto and dealership businesses commonly invest in search."));
        }

        if (industryCode == "retail" || ContainsAny(normalized, "supermarket", "grocery", "furniture"))
        {
            if (channel == "billboards_ooh") evidence.Add(CreateEvidence("industry_pattern", "internal_rule", 10, 0.7m, 1.0m, "Retail-led businesses often use OOH for reach."));
            if (channel == "radio") evidence.Add(CreateEvidence("industry_pattern", "internal_rule", 10, 0.7m, 1.0m, "Retail-led businesses often use radio for promotion."));
            if (channel == "print") evidence.Add(CreateEvidence("industry_pattern", "internal_rule", 8, 0.7m, 1.0m, "Retail-led businesses often use print inserts or flyers."));
            if (channel == "social") evidence.Add(CreateEvidence("industry_pattern", "internal_rule", 6, 0.7m, 1.0m, "Retail-led businesses often run social promotions."));
        }

        if (ContainsAny(normalized, "saas", "software", "b2b"))
        {
            if (channel == "social") evidence.Add(CreateEvidence("category_prior", "internal_rule", 12, 0.7m, 1.0m, "Software and B2B companies commonly use paid social."));
            if (channel == "search") evidence.Add(CreateEvidence("category_prior", "internal_rule", 12, 0.7m, 1.0m, "Software and B2B companies commonly use search."));
            if (channel == "tv") evidence.Add(CreateEvidence("budget_profile_mismatch", "contradiction", -8, 1.0m, 1.0m, "TV is less common for software and B2B businesses."));
        }

        if (ContainsAny(normalized, "luxury", "fashion", "beauty"))
        {
            if (channel == "billboards_ooh") evidence.Add(CreateEvidence("category_prior", "internal_rule", 8, 0.7m, 1.0m, "Luxury and fashion brands often use OOH."));
            if (channel == "influencer") evidence.Add(CreateEvidence("category_prior", "internal_rule", 10, 0.7m, 1.0m, "Luxury and beauty brands often use creators."));
            if (channel == "print") evidence.Add(CreateEvidence("category_prior", "internal_rule", 5, 0.7m, 1.0m, "Luxury and beauty brands still appear in print."));
            if (channel == "social") evidence.Add(CreateEvidence("category_prior", "internal_rule", 8, 0.7m, 1.0m, "Luxury and beauty brands commonly use social."));
        }

        if (industryCode == "funeral_services")
        {
            if (channel == "radio") evidence.Add(CreateEvidence("category_prior", "internal_rule", 12, 0.7m, 1.0m, "Funeral services often rely on radio."));
        }

        if (industryCode == "food_hospitality")
        {
            if (channel == "social") evidence.Add(CreateEvidence("category_prior", "internal_rule", 10, 0.7m, 1.0m, "Restaurants and food brands often use social."));
            if (channel == "radio") evidence.Add(CreateEvidence("category_prior", "internal_rule", 4, 0.7m, 1.0m, "Restaurants sometimes use radio promotions."));
            if (channel == "influencer") evidence.Add(CreateEvidence("category_prior", "internal_rule", 8, 0.7m, 1.0m, "Food brands often work with creators."));
            if (channel == "tv") evidence.Add(CreateEvidence("budget_profile_mismatch", "contradiction", -12, 1.0m, 1.0m, "Single-location food brands are less likely to use TV."));
        }

        if (industryCode == "healthcare")
        {
            if (channel == "search") evidence.Add(CreateEvidence("category_prior", "internal_rule", 10, 0.7m, 1.0m, "Healthcare businesses often rely on search."));
            if (channel == "social") evidence.Add(CreateEvidence("category_prior", "internal_rule", 6, 0.7m, 1.0m, "Healthcare businesses often use social."));
            if (channel == "radio") evidence.Add(CreateEvidence("category_prior", "internal_rule", 4, 0.7m, 1.0m, "Healthcare businesses sometimes use radio."));
        }

        if (industryCode == "fitness")
        {
            if (channel == "social") evidence.Add(CreateEvidence("category_prior", "internal_rule", 10, 0.7m, 1.0m, "Fitness brands commonly advertise on social."));
            if (channel == "search") evidence.Add(CreateEvidence("category_prior", "internal_rule", 8, 0.7m, 1.0m, "Fitness brands often use search."));
            if (channel == "radio") evidence.Add(CreateEvidence("category_prior", "internal_rule", 4, 0.7m, 1.0m, "Fitness brands sometimes use radio."));
            if (channel == "influencer") evidence.Add(CreateEvidence("category_prior", "internal_rule", 4, 0.7m, 1.0m, "Fitness brands sometimes work with creators."));
        }
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class NoOpLeadMasterDataService : ILeadMasterDataService
    {
        public LeadMasterTokenSet GetTokenSet() => new();
        public MasterLocationMatch? ResolveLocation(string? value) => null;
        public MasterIndustryMatch? ResolveIndustry(string? value) => null;
        public MasterIndustryMatch? ResolveIndustryFromHints(IReadOnlyList<string> hints) => null;
        public MasterLanguageMatch? ResolveLanguage(string? value) => null;
    }
}

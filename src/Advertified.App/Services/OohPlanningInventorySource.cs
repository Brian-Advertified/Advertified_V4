using Advertified.App.Contracts.Campaigns;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Dapper;
using Npgsql;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class OohPlanningInventorySource : IOohPlanningInventorySource
{
    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly IPricingSettingsProvider _pricingSettingsProvider;
    private readonly ICommercialFlightPricingResolver _commercialFlightPricingResolver;

    public OohPlanningInventorySource(
        Npgsql.NpgsqlDataSource dataSource,
        IPricingSettingsProvider pricingSettingsProvider,
        ICommercialFlightPricingResolver commercialFlightPricingResolver)
    {
        _dataSource = dataSource;
        _pricingSettingsProvider = pricingSettingsProvider;
        _commercialFlightPricingResolver = commercialFlightPricingResolver;
    }

    public async Task<List<OohPlanningInventoryRow>> GetCandidatesAsync(CampaignPlanningRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"
select
    oii.id as SourceId,
    'ooh' as SourceType,
    coalesce(nullif(oii.site_name, ''), 'Billboard or Digital Screen Site') as DisplayName,
    'OOH' as MediaType,
    coalesce(nullif(oii.media_type, ''), nullif(oii.metadata_json ->> 'media_type', ''), nullif(oii.metadata_json ->> 'media_subtype', ''), 'Placement') as Subtype,
    oii.province as Province,
    oii.city as City,
    oii.suburb as Suburb,
    coalesce(nullif(oii.suburb, ''), nullif(oii.city, ''), nullif(oii.province, '')) as Area,
    coalesce(nullif(oii.metadata_json ->> 'language', ''), 'N/A') as Language,
    coalesce(
        nullif(split_part(regexp_replace(coalesce(oii.audience_income_fit, ''), '[^0-9]+', ' ', 'g'), ' ', 1), '')::int,
        null
    ) as LsmMin,
    coalesce(
        nullif(split_part(regexp_replace(coalesce(oii.audience_income_fit, ''), '[^0-9]+', ' ', 'g'), ' ', 2), '')::int,
        nullif(split_part(regexp_replace(coalesce(oii.audience_income_fit, ''), '[^0-9]+', ' ', 'g'), ' ', 1), '')::int
    ) as LsmMax,
    coalesce(
        oii.discounted_rate_zar,
        oii.rate_card_zar,
        oii.monthly_rate_zar,
        nullif(regexp_replace(coalesce(oii.metadata_json ->> 'discounted_rate_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
        nullif(regexp_replace(coalesce(oii.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
        nullif(regexp_replace(coalesce(oii.metadata_json ->> 'monthly_rate_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
        0
    ) as Cost,
    coalesce(oii.is_available, true) as IsAvailable,
    false as PackageOnly,
    coalesce(nullif(oii.metadata_json ->> 'time_band', ''), nullif(oii.metadata_json ->> 'daypart', ''), 'always_on') as TimeBand,
    null as DayType,
    coalesce(nullif(oii.media_type, ''), nullif(oii.metadata_json ->> 'slot_type', ''), 'placement') as SlotType,
    nullif(oii.metadata_json ->> 'duration_seconds', '')::int as DurationSeconds,
    coalesce(nullif(oii.metadata_json ->> 'region_cluster_code', ''), '') as RegionClusterCode,
    coalesce(nullif(oii.metadata_json ->> 'geography_scope', ''), nullif(oii.province, ''), '') as MarketScope,
    null as MarketTier,
    null::int as MonthlyListenership,
    false as IsFlagshipStation,
    false as IsPremiumStation,
    coalesce(oii.latitude, nullif(oii.metadata_json ->> 'latitude', '')::double precision, nullif(oii.metadata_json ->> 'lat', '')::double precision) as Latitude,
    coalesce(oii.longitude, nullif(oii.metadata_json ->> 'longitude', '')::double precision, nullif(oii.metadata_json ->> 'lng', '')::double precision, nullif(oii.metadata_json ->> 'lon', '')::double precision) as Longitude,
    jsonb_strip_nulls(
        coalesce(oii.metadata_json, '{}'::jsonb) ||
        jsonb_build_object(
            'inventoryIntelligenceNotes', oii.intelligence_notes,
            'inventory_intelligence_notes', oii.intelligence_notes,
            'siteCode', oii.site_code,
            'site_code', oii.site_code,
            'venueType', oii.venue_type,
            'venue_type', oii.venue_type,
            'premiumMassFit', oii.premium_mass_fit,
            'premium_mass_fit', oii.premium_mass_fit,
            'pricePositioningFit', oii.price_positioning_fit,
            'price_positioning_fit', oii.price_positioning_fit,
            'audienceIncomeFit', oii.audience_income_fit,
            'audience_income_fit', oii.audience_income_fit,
            'discountedRateZar', oii.discounted_rate_zar,
            'discounted_rate_zar', oii.discounted_rate_zar,
            'rateCardZar', oii.rate_card_zar,
            'rate_card_zar', oii.rate_card_zar,
            'monthlyRateZar', oii.monthly_rate_zar,
            'monthly_rate_zar', oii.monthly_rate_zar,
            'youthFit', oii.youth_fit,
            'youth_fit', oii.youth_fit,
            'familyFit', oii.family_fit,
            'family_fit', oii.family_fit,
            'professionalFit', oii.professional_fit,
            'professional_fit', oii.professional_fit,
            'commuterFit', oii.commuter_fit,
            'commuter_fit', oii.commuter_fit,
            'touristFit', oii.tourist_fit,
            'tourist_fit', oii.tourist_fit,
            'highValueShopperFit', oii.high_value_shopper_fit,
            'high_value_shopper_fit', oii.high_value_shopper_fit
        ) ||
        jsonb_build_object(
            'audienceAgeSkew', oii.audience_age_skew,
            'audience_age_skew', oii.audience_age_skew,
            'audienceGenderSkew', oii.audience_gender_skew,
            'audience_gender_skew', oii.audience_gender_skew,
            'dwellTimeScore', oii.dwell_time_score,
            'dwell_time_score', oii.dwell_time_score,
            'environmentType', oii.environment_type,
            'environment_type', oii.environment_type,
            'buyingBehaviourFit', oii.buying_behaviour_fit,
            'buying_behaviour_fit', oii.buying_behaviour_fit,
            'dataConfidence', oii.data_confidence,
            'data_confidence', oii.data_confidence,
            'primaryAudienceTags', oii.primary_audience_tags_json,
            'primary_audience_tags', oii.primary_audience_tags_json,
            'trafficCount', oii.traffic_count,
            'traffic_count', oii.traffic_count,
            'secondaryAudienceTags', oii.secondary_audience_tags_json,
            'secondary_audience_tags', oii.secondary_audience_tags_json,
            'recommendationTags', oii.recommendation_tags_json,
            'recommendation_tags', oii.recommendation_tags_json
        ) ||
        coalesce(oii.metadata_json, '{}'::jsonb)
    )::text as MetadataJson
from ooh_inventory_intelligence oii
where oii.is_active = true
  and coalesce(
        oii.discounted_rate_zar,
        oii.rate_card_zar,
        oii.monthly_rate_zar,
        nullif(regexp_replace(coalesce(oii.metadata_json ->> 'discounted_rate_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
        nullif(regexp_replace(coalesce(oii.metadata_json ->> 'rate_card_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
        nullif(regexp_replace(coalesce(oii.metadata_json ->> 'monthly_rate_zar', ''), '[^0-9.]', '', 'g'), '')::numeric,
        0
      ) <= @Budget;";

        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        var pricingSettings = await _pricingSettingsProvider.GetCurrentAsync(cancellationToken);
        var rows = await conn.QueryAsync<OohPlanningInventoryRow>(new CommandDefinition(
            sql,
            new { Budget = request.SelectedBudget },
            cancellationToken: cancellationToken));

        return rows
            .Select(row =>
            {
                row.Subtype = OohInventoryNormalizer.NormalizeSubtype(
                    row.Subtype,
                    row.SlotType,
                    row.DisplayName,
                    row.City,
                    row.Suburb,
                    row.Province);
                row.SlotType = OohInventoryNormalizer.NormalizeSlotType(row.SlotType, row.Subtype);
                row.MediaType = PlanningChannelSupport.ClassifyOohChannel(row.Subtype, row.SlotType, row.DisplayName);

                var rawCost = row.Cost;
                var markedUpCost = PricingPolicy.ApplyMarkup(rawCost, row.MediaType, row.Subtype, pricingSettings);
                var metadata = DeserializeMetadata(row.MetadataJson);
                var offerDurationWeeks = ParseNullableInt(GetMetadataValue(metadata, "durationWeeks") ?? GetMetadataValue(metadata, "duration_weeks"));
                var offerDurationMonths = ParseNullableInt(GetMetadataValue(metadata, "durationMonths") ?? GetMetadataValue(metadata, "duration_months"));
                var pricingModel = GetMetadataValue(metadata, "pricingModel")
                    ?? (offerDurationWeeks.HasValue || offerDurationMonths.HasValue ? "fixed_term_package" : "monthly");
                var resolution = _commercialFlightPricingResolver.Resolve(
                    request,
                    row.MediaType,
                    pricingModel,
                    markedUpCost,
                    markedUpCost,
                    offerDurationWeeks,
                    offerDurationMonths,
                    row.PackageOnly,
                    allowsProration: pricingModel.Contains("monthly", StringComparison.OrdinalIgnoreCase));

                row.Cost = resolution.QuotedCost;
                row.MetadataJson = SerializeMetadata(metadata, resolution, offerDurationWeeks, offerDurationMonths);
                return row;
            })
            .Where(row => row.Cost > 0m && row.Cost <= request.SelectedBudget)
            .ToList();
    }

    private static Dictionary<string, object?> DeserializeMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(metadataJson) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string SerializeMetadata(
        Dictionary<string, object?> metadata,
        CommercialPriceResolution resolution,
        int? offerDurationWeeks,
        int? offerDurationMonths)
    {
        metadata["quotedCostZar"] = resolution.QuotedCost;
        metadata["quoted_cost_zar"] = resolution.QuotedCost;
        metadata["monthlyCostEstimateZar"] = resolution.ComparableMonthlyCost;
        metadata["monthly_cost_estimate_zar"] = resolution.ComparableMonthlyCost;
        metadata["appliedDurationLabel"] = resolution.AppliedDurationLabel;
        metadata["applied_duration_label"] = resolution.AppliedDurationLabel;
        metadata["requestedDurationLabel"] = resolution.RequestedDurationLabel;
        metadata["requested_duration_label"] = resolution.RequestedDurationLabel;
        metadata["durationFitScore"] = resolution.DurationFitScore;
        metadata["duration_fit_score"] = resolution.DurationFitScore;
        metadata["commercialPenalty"] = resolution.CommercialPenalty;
        metadata["commercial_penalty"] = resolution.CommercialPenalty;
        metadata["commercialExplanation"] = resolution.Explanation;
        metadata["commercial_explanation"] = resolution.Explanation;
        metadata["requestedStartDate"] = resolution.RequestedStartDate?.ToString("yyyy-MM-dd");
        metadata["requested_start_date"] = resolution.RequestedStartDate?.ToString("yyyy-MM-dd");
        metadata["requestedEndDate"] = resolution.RequestedEndDate?.ToString("yyyy-MM-dd");
        metadata["requested_end_date"] = resolution.RequestedEndDate?.ToString("yyyy-MM-dd");
        metadata["resolvedStartDate"] = resolution.ResolvedStartDate?.ToString("yyyy-MM-dd");
        metadata["resolved_start_date"] = resolution.ResolvedStartDate?.ToString("yyyy-MM-dd");
        metadata["resolvedEndDate"] = resolution.ResolvedEndDate?.ToString("yyyy-MM-dd");
        metadata["resolved_end_date"] = resolution.ResolvedEndDate?.ToString("yyyy-MM-dd");
        metadata["offerDurationWeeks"] = offerDurationWeeks;
        metadata["offer_duration_weeks"] = offerDurationWeeks;
        metadata["offerDurationMonths"] = offerDurationMonths;
        metadata["offer_duration_months"] = offerDurationMonths;
        return JsonSerializer.Serialize(metadata);
    }

    private static string? GetMetadataValue(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) && value is not null
            ? value.ToString()
            : null;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }
}

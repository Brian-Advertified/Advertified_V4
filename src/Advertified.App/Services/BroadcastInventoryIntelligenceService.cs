using Advertified.App.Services.Abstractions;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using System.Text.Json;

namespace Advertified.App.Services;

public sealed class BroadcastInventoryIntelligenceService : IBroadcastInventoryIntelligenceService
{
    private const string RadioCacheKey = "broadcast_inventory_intelligence_radio_v1";
    private const string TvCacheKey = "broadcast_inventory_intelligence_tv_v1";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly NpgsqlDataSource _dataSource;
    private readonly IMemoryCache _memoryCache;

    public BroadcastInventoryIntelligenceService(NpgsqlDataSource dataSource, IMemoryCache memoryCache)
    {
        _dataSource = dataSource;
        _memoryCache = memoryCache;
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>> GetRadioIntelligenceByInternalKeyAsync(CancellationToken cancellationToken)
    {
        return GetAsync(
            RadioCacheKey,
            @"
            select
                internal_key as InternalKey,
                station_tier as StationTier,
                station_format as StationFormat,
                audience_income_fit as AudienceIncomeFit,
                premium_mass_fit as PremiumMassFit,
                price_positioning_fit as PricePositioningFit,
                youth_fit as YouthFit,
                family_fit as FamilyFit,
                professional_fit as ProfessionalFit,
                commuter_fit as CommuterFit,
                high_value_client_fit as HighValueClientFit,
                business_decision_maker_fit as BusinessDecisionMakerFit,
                household_decision_maker_fit as HouseholdDecisionMakerFit,
                morning_drive_fit as MorningDriveFit,
                workday_fit as WorkdayFit,
                afternoon_drive_fit as AfternoonDriveFit,
                evening_fit as EveningFit,
                weekend_fit as WeekendFit,
                urban_rural_fit as UrbanRuralFit,
                language_context_fit as LanguageContextFit,
                buying_behaviour_fit as BuyingBehaviourFit,
                brand_safety_fit as BrandSafetyFit,
                objective_fit_primary as ObjectiveFitPrimary,
                objective_fit_secondary as ObjectiveFitSecondary,
                audience_age_skew as AudienceAgeSkew,
                audience_gender_skew as AudienceGenderSkew,
                content_environment as ContentEnvironment,
                presenter_or_show_context as PresenterOrShowContext,
                genre_fit as GenreFit,
                intelligence_notes as IntelligenceNotes,
                data_confidence as DataConfidence,
                updated_by as UpdatedBy,
                source_file as SourceFile,
                primary_audience_tags_json::text as PrimaryAudienceTagsJson,
                secondary_audience_tags_json::text as SecondaryAudienceTagsJson,
                recommendation_tags_json::text as RecommendationTagsJson,
                source_urls_json::text as SourceUrlsJson,
                metadata_json::text as MetadataJson
            from radio_inventory_intelligence
            where is_active = true
              and internal_key is not null
              and btrim(internal_key) <> '';
            ",
            BuildRadioMetadata,
            cancellationToken);
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>> GetTvIntelligenceByInternalKeyAsync(CancellationToken cancellationToken)
    {
        return GetAsync(
            TvCacheKey,
            @"
            select
                internal_key as InternalKey,
                channel_tier as ChannelTier,
                channel_format as ChannelFormat,
                genre_fit as GenreFit,
                audience_income_fit as AudienceIncomeFit,
                premium_mass_fit as PremiumMassFit,
                price_positioning_fit as PricePositioningFit,
                youth_fit as YouthFit,
                family_fit as FamilyFit,
                professional_fit as ProfessionalFit,
                household_decision_maker_fit as HouseholdDecisionMakerFit,
                high_value_client_fit as HighValueClientFit,
                news_affairs_fit as NewsAffairsFit,
                sport_fit as SportFit,
                entertainment_fit as EntertainmentFit,
                appointment_viewing_fit as AppointmentViewingFit,
                co_viewing_fit as CoViewingFit,
                language_context_fit as LanguageContextFit,
                buying_behaviour_fit as BuyingBehaviourFit,
                brand_safety_fit as BrandSafetyFit,
                objective_fit_primary as ObjectiveFitPrimary,
                objective_fit_secondary as ObjectiveFitSecondary,
                audience_age_skew as AudienceAgeSkew,
                audience_gender_skew as AudienceGenderSkew,
                content_environment as ContentEnvironment,
                programme_context as ProgrammeContext,
                intelligence_notes as IntelligenceNotes,
                data_confidence as DataConfidence,
                updated_by as UpdatedBy,
                primary_audience_tags_json::text as PrimaryAudienceTagsJson,
                secondary_audience_tags_json::text as SecondaryAudienceTagsJson,
                recommendation_tags_json::text as RecommendationTagsJson,
                source_urls_json::text as SourceUrlsJson,
                metadata_json::text as MetadataJson
            from tv_inventory_intelligence
            where is_active = true
              and internal_key is not null
              and btrim(internal_key) <> '';
            ",
            BuildTvMetadata,
            cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>> GetAsync(
        string cacheKey,
        string sql,
        Func<IntelligenceRow, IReadOnlyDictionary<string, object?>> projector,
        CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? cached)
            && cached is not null)
        {
            return cached;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<IntelligenceRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        var snapshot = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.InternalKey))
            .GroupBy(row => row.InternalKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => projector(group.Last()),
                StringComparer.OrdinalIgnoreCase);

        _memoryCache.Set(cacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    private static IReadOnlyDictionary<string, object?> BuildRadioMetadata(IntelligenceRow row)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["stationTier"] = row.StationTier,
            ["station_tier"] = row.StationTier,
            ["stationFormat"] = row.StationFormat,
            ["station_format"] = row.StationFormat,
            ["audienceIncomeFit"] = row.AudienceIncomeFit,
            ["audience_income_fit"] = row.AudienceIncomeFit,
            ["premiumMassFit"] = row.PremiumMassFit,
            ["premium_mass_fit"] = row.PremiumMassFit,
            ["pricePositioningFit"] = row.PricePositioningFit,
            ["price_positioning_fit"] = row.PricePositioningFit,
            ["youthFit"] = row.YouthFit,
            ["youth_fit"] = row.YouthFit,
            ["familyFit"] = row.FamilyFit,
            ["family_fit"] = row.FamilyFit,
            ["professionalFit"] = row.ProfessionalFit,
            ["professional_fit"] = row.ProfessionalFit,
            ["commuterFit"] = row.CommuterFit,
            ["commuter_fit"] = row.CommuterFit,
            ["highValueClientFit"] = row.HighValueClientFit,
            ["high_value_client_fit"] = row.HighValueClientFit,
            ["businessDecisionMakerFit"] = row.BusinessDecisionMakerFit,
            ["business_decision_maker_fit"] = row.BusinessDecisionMakerFit,
            ["householdDecisionMakerFit"] = row.HouseholdDecisionMakerFit,
            ["household_decision_maker_fit"] = row.HouseholdDecisionMakerFit,
            ["morningDriveFit"] = row.MorningDriveFit,
            ["morning_drive_fit"] = row.MorningDriveFit,
            ["workdayFit"] = row.WorkdayFit,
            ["workday_fit"] = row.WorkdayFit,
            ["afternoonDriveFit"] = row.AfternoonDriveFit,
            ["afternoon_drive_fit"] = row.AfternoonDriveFit,
            ["eveningFit"] = row.EveningFit,
            ["evening_fit"] = row.EveningFit,
            ["weekendFit"] = row.WeekendFit,
            ["weekend_fit"] = row.WeekendFit,
            ["urbanRuralFit"] = row.UrbanRuralFit,
            ["urban_rural_fit"] = row.UrbanRuralFit,
            ["languageContextFit"] = row.LanguageContextFit,
            ["language_context_fit"] = row.LanguageContextFit,
            ["buyingBehaviourFit"] = row.BuyingBehaviourFit,
            ["buying_behaviour_fit"] = row.BuyingBehaviourFit,
            ["brandSafetyFit"] = row.BrandSafetyFit,
            ["brand_safety_fit"] = row.BrandSafetyFit,
            ["objectiveFitPrimary"] = row.ObjectiveFitPrimary,
            ["objective_fit_primary"] = row.ObjectiveFitPrimary,
            ["objectiveFitSecondary"] = row.ObjectiveFitSecondary,
            ["objective_fit_secondary"] = row.ObjectiveFitSecondary,
            ["audienceAgeSkew"] = row.AudienceAgeSkew,
            ["audience_age_skew"] = row.AudienceAgeSkew,
            ["audienceGenderSkew"] = row.AudienceGenderSkew,
            ["audience_gender_skew"] = row.AudienceGenderSkew,
            ["contentEnvironment"] = row.ContentEnvironment,
            ["content_environment"] = row.ContentEnvironment,
            ["presenterOrShowContext"] = row.PresenterOrShowContext,
            ["presenter_or_show_context"] = row.PresenterOrShowContext,
            ["genreFit"] = row.GenreFit,
            ["genre_fit"] = row.GenreFit,
            ["inventoryIntelligenceNotes"] = row.IntelligenceNotes,
            ["inventory_intelligence_notes"] = row.IntelligenceNotes,
            ["dataConfidence"] = row.DataConfidence,
            ["data_confidence"] = row.DataConfidence,
            ["updatedBy"] = row.UpdatedBy,
            ["updated_by"] = row.UpdatedBy,
            ["sourceFile"] = row.SourceFile,
            ["source_file"] = row.SourceFile
        };

        AddJsonArray(metadata, row.PrimaryAudienceTagsJson, "primaryAudienceTags", "primary_audience_tags");
        AddJsonArray(metadata, row.SecondaryAudienceTagsJson, "secondaryAudienceTags", "secondary_audience_tags");
        AddJsonArray(metadata, row.RecommendationTagsJson, "recommendationTags", "recommendation_tags");
        AddJsonArray(metadata, row.SourceUrlsJson, "sourceUrls", "source_urls");
        AddMetadataJson(metadata, row.MetadataJson);
        return metadata;
    }

    private static IReadOnlyDictionary<string, object?> BuildTvMetadata(IntelligenceRow row)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["channelTier"] = row.ChannelTier,
            ["channel_tier"] = row.ChannelTier,
            ["channelFormat"] = row.ChannelFormat,
            ["channel_format"] = row.ChannelFormat,
            ["genreFit"] = row.GenreFit,
            ["genre_fit"] = row.GenreFit,
            ["audienceIncomeFit"] = row.AudienceIncomeFit,
            ["audience_income_fit"] = row.AudienceIncomeFit,
            ["premiumMassFit"] = row.PremiumMassFit,
            ["premium_mass_fit"] = row.PremiumMassFit,
            ["pricePositioningFit"] = row.PricePositioningFit,
            ["price_positioning_fit"] = row.PricePositioningFit,
            ["youthFit"] = row.YouthFit,
            ["youth_fit"] = row.YouthFit,
            ["familyFit"] = row.FamilyFit,
            ["family_fit"] = row.FamilyFit,
            ["professionalFit"] = row.ProfessionalFit,
            ["professional_fit"] = row.ProfessionalFit,
            ["householdDecisionMakerFit"] = row.HouseholdDecisionMakerFit,
            ["household_decision_maker_fit"] = row.HouseholdDecisionMakerFit,
            ["highValueClientFit"] = row.HighValueClientFit,
            ["high_value_client_fit"] = row.HighValueClientFit,
            ["newsAffairsFit"] = row.NewsAffairsFit,
            ["news_affairs_fit"] = row.NewsAffairsFit,
            ["sportFit"] = row.SportFit,
            ["sport_fit"] = row.SportFit,
            ["entertainmentFit"] = row.EntertainmentFit,
            ["entertainment_fit"] = row.EntertainmentFit,
            ["appointmentViewingFit"] = row.AppointmentViewingFit,
            ["appointment_viewing_fit"] = row.AppointmentViewingFit,
            ["coViewingFit"] = row.CoViewingFit,
            ["co_viewing_fit"] = row.CoViewingFit,
            ["languageContextFit"] = row.LanguageContextFit,
            ["language_context_fit"] = row.LanguageContextFit,
            ["buyingBehaviourFit"] = row.BuyingBehaviourFit,
            ["buying_behaviour_fit"] = row.BuyingBehaviourFit,
            ["brandSafetyFit"] = row.BrandSafetyFit,
            ["brand_safety_fit"] = row.BrandSafetyFit,
            ["objectiveFitPrimary"] = row.ObjectiveFitPrimary,
            ["objective_fit_primary"] = row.ObjectiveFitPrimary,
            ["objectiveFitSecondary"] = row.ObjectiveFitSecondary,
            ["objective_fit_secondary"] = row.ObjectiveFitSecondary,
            ["audienceAgeSkew"] = row.AudienceAgeSkew,
            ["audience_age_skew"] = row.AudienceAgeSkew,
            ["audienceGenderSkew"] = row.AudienceGenderSkew,
            ["audience_gender_skew"] = row.AudienceGenderSkew,
            ["contentEnvironment"] = row.ContentEnvironment,
            ["content_environment"] = row.ContentEnvironment,
            ["programmeContext"] = row.ProgrammeContext,
            ["programme_context"] = row.ProgrammeContext,
            ["inventoryIntelligenceNotes"] = row.IntelligenceNotes,
            ["inventory_intelligence_notes"] = row.IntelligenceNotes,
            ["dataConfidence"] = row.DataConfidence,
            ["data_confidence"] = row.DataConfidence,
            ["updatedBy"] = row.UpdatedBy,
            ["updated_by"] = row.UpdatedBy
        };

        AddJsonArray(metadata, row.PrimaryAudienceTagsJson, "primaryAudienceTags", "primary_audience_tags");
        AddJsonArray(metadata, row.SecondaryAudienceTagsJson, "secondaryAudienceTags", "secondary_audience_tags");
        AddJsonArray(metadata, row.RecommendationTagsJson, "recommendationTags", "recommendation_tags");
        AddJsonArray(metadata, row.SourceUrlsJson, "sourceUrls", "source_urls");
        AddMetadataJson(metadata, row.MetadataJson);
        return metadata;
    }

    private static void AddJsonArray(IDictionary<string, object?> metadata, string? rawJson, string camelKey, string snakeKey)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var items = document.RootElement
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<object?>()
                .ToArray();

            if (items.Length == 0)
            {
                return;
            }

            metadata[camelKey] = items;
            metadata[snakeKey] = items;
        }
        catch (JsonException)
        {
        }
    }

    private static void AddMetadataJson(IDictionary<string, object?> metadata, string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!metadata.ContainsKey(property.Name))
                {
                    metadata[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.Clone();
                }
            }
        }
        catch (JsonException)
        {
        }
    }

    private sealed class IntelligenceRow
    {
        public string InternalKey { get; set; } = string.Empty;
        public string? StationTier { get; set; }
        public string? StationFormat { get; set; }
        public string? AudienceIncomeFit { get; set; }
        public string? PremiumMassFit { get; set; }
        public string? PricePositioningFit { get; set; }
        public string? YouthFit { get; set; }
        public string? FamilyFit { get; set; }
        public string? ProfessionalFit { get; set; }
        public string? CommuterFit { get; set; }
        public string? HighValueClientFit { get; set; }
        public string? BusinessDecisionMakerFit { get; set; }
        public string? HouseholdDecisionMakerFit { get; set; }
        public string? MorningDriveFit { get; set; }
        public string? WorkdayFit { get; set; }
        public string? AfternoonDriveFit { get; set; }
        public string? EveningFit { get; set; }
        public string? WeekendFit { get; set; }
        public string? UrbanRuralFit { get; set; }
        public string? LanguageContextFit { get; set; }
        public string? BuyingBehaviourFit { get; set; }
        public string? BrandSafetyFit { get; set; }
        public string? ObjectiveFitPrimary { get; set; }
        public string? ObjectiveFitSecondary { get; set; }
        public string? AudienceAgeSkew { get; set; }
        public string? AudienceGenderSkew { get; set; }
        public string? ContentEnvironment { get; set; }
        public string? PresenterOrShowContext { get; set; }
        public string? GenreFit { get; set; }
        public string? IntelligenceNotes { get; set; }
        public string? DataConfidence { get; set; }
        public string? UpdatedBy { get; set; }
        public string? SourceFile { get; set; }
        public string? ChannelTier { get; set; }
        public string? ChannelFormat { get; set; }
        public string? NewsAffairsFit { get; set; }
        public string? SportFit { get; set; }
        public string? EntertainmentFit { get; set; }
        public string? AppointmentViewingFit { get; set; }
        public string? CoViewingFit { get; set; }
        public string? ProgrammeContext { get; set; }
        public string? PrimaryAudienceTagsJson { get; set; }
        public string? SecondaryAudienceTagsJson { get; set; }
        public string? RecommendationTagsJson { get; set; }
        public string? SourceUrlsJson { get; set; }
        public string? MetadataJson { get; set; }
    }
}


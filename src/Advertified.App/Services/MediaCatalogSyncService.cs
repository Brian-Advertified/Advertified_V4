using System.Data;
using Advertified.App.Configuration;
using Advertified.App.Services.Abstractions;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Advertified.App.Services;

public sealed class MediaCatalogSyncService : IMediaCatalogSyncService
{
    private static readonly Regex PackageCostRegex = new(@"Package Cost\s*[:\-]?\s*R\s*([\d\s,\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AverageCostPerSpotRegex = new(@"Aver(?:age)?\s+cost\s+per\s+spot\s*[:\-]?\s*R\s*([\d\s,\.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SpotCountRegex = new(@"No(?:\.| of)?\s*spots?\s*[:\-]?\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ProgrammeRateRegex = new(@"^(?<time>\d{2}:\d{2})\s+(?<programme>[A-Za-z0-9&@'\/\-\.\s\(\)]{3,120}?)\s+R\s*(?<rate>[\d\s,\.]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] KnownRadioTokens =
    {
        "algoa", "jozi", "kaya", "smile", "fm", "radio", "y packages", "sabc-radio", "sabc_sports_rate"
    };

    private readonly string _connectionString;
    private readonly MediaCatalogOptions _options;

    public MediaCatalogSyncService(IOptions<MediaCatalogOptions> options, IConfiguration configuration)
    {
        _options = options.Value;
        _connectionString = configuration.GetConnectionString("Advertified")
            ?? throw new InvalidOperationException("Connection string 'Advertified' is not configured.");
    }

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SourceDirectory) || !Directory.Exists(_options.SourceDirectory))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await SeedTaxonomiesAsync(connection, cancellationToken);
        await SeedRegionClustersAsync(connection, cancellationToken);
        await SeedReferenceSourcesAsync(connection, cancellationToken);
        await SyncSourceDocumentsAsync(connection, cancellationToken);
        await SyncSourceDocumentPagesAsync(connection, cancellationToken);
        await SyncRadioStationProfilesAsync(connection, cancellationToken);
        await SyncRadioShowsAsync(connection, cancellationToken);
        await SyncRadioInventoryAsync(connection, cancellationToken);
        await SyncRadioTagsAsync(connection, cancellationToken);
        await SyncTvChannelsAsync(connection, cancellationToken);
        await SyncTvProgrammesAsync(connection, cancellationToken);
        await SyncTvInventoryAsync(connection, cancellationToken);
        await SyncTvTagsAsync(connection, cancellationToken);
        await ApplyRegionClusterMappingsAsync(connection, cancellationToken);
    }

    private async Task SeedTaxonomiesAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        var audienceCodes = new (string Code, string DisplayName)[]
        {
            ("commuters", "Commuters"),
            ("professionals", "Professionals"),
            ("retail", "Retail shoppers"),
            ("stay_at_home", "Stay-at-home audience"),
            ("business", "Business audience"),
            ("sport", "Sport audience"),
            ("lifestyle", "Lifestyle audience"),
            ("family", "Family audience"),
            ("youth", "Youth audience")
        };

        var contentCodes = new (string Code, string DisplayName)[]
        {
            ("breakfast", "Breakfast"),
            ("midday", "Midday"),
            ("drive", "Drive"),
            ("news", "News"),
            ("music", "Music"),
            ("sport", "Sport"),
            ("business", "Business"),
            ("lifestyle", "Lifestyle"),
            ("talk", "Talk")
        };

        const string audienceSql = @"
insert into audience_taxonomy (code, display_name)
values (@Code, @DisplayName)
on conflict (code) do update
set display_name = excluded.display_name;";

        const string contentSql = @"
insert into content_taxonomy (code, display_name)
values (@Code, @DisplayName)
on conflict (code) do update
set display_name = excluded.display_name;";

        foreach (var audience in audienceCodes)
        {
            await connection.ExecuteAsync(new CommandDefinition(audienceSql, new { audience.Code, audience.DisplayName }, cancellationToken: cancellationToken));
        }

        foreach (var content in contentCodes)
        {
            await connection.ExecuteAsync(new CommandDefinition(contentSql, new { content.Code, content.DisplayName }, cancellationToken: cancellationToken));
        }
    }

    private async Task SeedRegionClustersAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string clusterSql = @"
insert into region_clusters (code, name, description)
values (@Code, @Name, @Description)
on conflict (code) do update
set name = excluded.name,
    description = excluded.description,
    updated_at = now();";

        const string mappingSql = @"
insert into region_cluster_mappings (
    cluster_id,
    province,
    city,
    station_or_channel_name,
    metadata_json
)
select
    rc.id,
    @Province,
    @City,
    @StationOrChannelName,
    cast(@MetadataJson as jsonb)
from region_clusters rc
where rc.code = @ClusterCode
on conflict (
    cluster_id,
    coalesce(lower(province), ''),
    coalesce(lower(city), ''),
    coalesce(lower(station_or_channel_name), '')
) do update
set metadata_json = excluded.metadata_json,
    updated_at = now();";

        var clusters = new[]
        {
            new RegionClusterSeed("gauteng", "Gauteng", "Johannesburg, Pretoria, Midrand, and Ekurhuleni urban commuter market."),
            new RegionClusterSeed("western-cape", "Western Cape", "Cape Town, surrounding suburbs, lifestyle, retail, and tourism market."),
            new RegionClusterSeed("eastern-cape", "Eastern Cape", "Gqeberha and East London regional coastal market."),
            new RegionClusterSeed("kzn", "KwaZulu-Natal", "Durban and Pietermaritzburg high-population coastal market."),
            new RegionClusterSeed("national", "National", "Multi-province or nationwide campaigns across leading metros and national channels.")
        };

        foreach (var cluster in clusters)
        {
            await connection.ExecuteAsync(new CommandDefinition(clusterSql, cluster, cancellationToken: cancellationToken));
        }

        var mappings = new[]
        {
            new RegionClusterMappingSeed("gauteng", "Gauteng", "Johannesburg", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("gauteng", "Gauteng", "Pretoria", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("gauteng", "Gauteng", "Midrand", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("gauteng", "Gauteng", "Soweto", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("gauteng", null, null, "Jozi FM", JsonSerializer.Serialize(new { kind = "station" })),
            new RegionClusterMappingSeed("gauteng", null, null, "Kaya 959", JsonSerializer.Serialize(new { kind = "station" })),
            new RegionClusterMappingSeed("national", null, null, "Metro FM", JsonSerializer.Serialize(new { kind = "station" })),
            new RegionClusterMappingSeed("gauteng", null, null, "YFM", JsonSerializer.Serialize(new { kind = "station" })),

            new RegionClusterMappingSeed("western-cape", "Western Cape", "Cape Town", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("western-cape", "Western Cape", "Bellville", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("western-cape", "Western Cape", "Wynberg", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("western-cape", null, null, "Smile 90.4FM", JsonSerializer.Serialize(new { kind = "station" })),
            new RegionClusterMappingSeed("western-cape", null, null, "Good Hope FM", JsonSerializer.Serialize(new { kind = "station" })),
            new RegionClusterMappingSeed("western-cape", null, null, "KFM", JsonSerializer.Serialize(new { kind = "station" })),

            new RegionClusterMappingSeed("eastern-cape", "Eastern Cape", "Gqeberha", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("eastern-cape", "Eastern Cape", "Port Elizabeth", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("eastern-cape", "Eastern Cape", "East London", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("eastern-cape", null, null, "Algoa FM", JsonSerializer.Serialize(new { kind = "station" })),

            new RegionClusterMappingSeed("kzn", "KwaZulu-Natal", "Durban", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("kzn", "KwaZulu-Natal", "Pietermaritzburg", null, JsonSerializer.Serialize(new { kind = "city" })),
            new RegionClusterMappingSeed("kzn", null, null, "East Coast Radio", JsonSerializer.Serialize(new { kind = "station" })),
            new RegionClusterMappingSeed("kzn", null, null, "Ukhozi FM", JsonSerializer.Serialize(new { kind = "station" })),

            new RegionClusterMappingSeed("national", null, null, "SABC 1", JsonSerializer.Serialize(new { kind = "channel" })),
            new RegionClusterMappingSeed("national", null, null, "SABC 2", JsonSerializer.Serialize(new { kind = "channel" })),
            new RegionClusterMappingSeed("national", null, null, "SABC 3", JsonSerializer.Serialize(new { kind = "channel" })),
            new RegionClusterMappingSeed("national", null, null, "SABC News Channel", JsonSerializer.Serialize(new { kind = "channel" })),
            new RegionClusterMappingSeed("national", null, null, "SABC Sport", JsonSerializer.Serialize(new { kind = "channel" })),
            new RegionClusterMappingSeed("national", null, null, "e.tv", JsonSerializer.Serialize(new { kind = "channel" }))
        };

        foreach (var mapping in mappings)
        {
            await connection.ExecuteAsync(new CommandDefinition(mappingSql, mapping, cancellationToken: cancellationToken));
        }
    }

    private async Task SeedReferenceSourcesAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
insert into media_reference_sources (
    media_channel,
    station_or_channel_name,
    show_or_programme_name,
    source_url,
    source_title,
    source_type,
    geography_summary,
    audience_summary,
    age_groups_json,
    language_summary,
    market_summary,
    notes,
    metadata_json
)
values (
    @MediaChannel,
    @StationOrChannelName,
    @ShowOrProgrammeName,
    @SourceUrl,
    @SourceTitle,
    @SourceType,
    @GeographySummary,
    @AudienceSummary,
    cast(@AgeGroupsJson as jsonb),
    @LanguageSummary,
    @MarketSummary,
    @Notes,
    cast(@MetadataJson as jsonb)
)
on conflict (source_url) do update
set station_or_channel_name = excluded.station_or_channel_name,
    show_or_programme_name = excluded.show_or_programme_name,
    source_title = excluded.source_title,
    source_type = excluded.source_type,
    geography_summary = excluded.geography_summary,
    audience_summary = excluded.audience_summary,
    age_groups_json = excluded.age_groups_json,
    language_summary = excluded.language_summary,
    market_summary = excluded.market_summary,
    notes = excluded.notes,
    metadata_json = excluded.metadata_json,
    updated_at = now();";

        var sources = new[]
        {
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Algoa FM",
                ShowOrProgrammeName = "Breakfast and Drive",
                SourceUrl = "https://www.algoafm.co.za/shows",
                SourceTitle = "Algoa FM Show Lineup",
                SourceType = "official_station_page",
                GeographySummary = "Eastern Cape and Garden Route coverage",
                AudienceSummary = "Breakfast and drive-time audiences across regional commuter and lifestyle markets",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-54", "commuter-age adults" }),
                LanguageSummary = "English-led commercial radio",
                MarketSummary = "Regional radio",
                Notes = "Use for weekday show windows and commuter-led context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28" })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Algoa FM",
                ShowOrProgrammeName = "Algoa FM Breakfast with Wayne, Lee and Charlie T",
                SourceUrl = "https://www.algoafm.co.za/podcasts/algoa-fm-breakfast-with-wayne-lee-and-charlie-t#breakfast",
                SourceTitle = "Algoa FM Breakfast with Wayne, Lee and Charlie T",
                SourceType = "official_show_page",
                GeographySummary = "Eastern Cape and Garden Route coverage",
                AudienceSummary = "Regional commuter audience with broad adult lifestyle appeal during breakfast drive",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-54", "working adults" }),
                LanguageSummary = "English-led commercial radio",
                MarketSummary = "Regional radio",
                Notes = "Weekdays 06:00-09:00 breakfast show.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "breakfast", match_keywords = new[] { "breakfast", "morning" } })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Algoa FM",
                ShowOrProgrammeName = "The Drive with Simon Bechus",
                SourceUrl = "https://www.algoafm.co.za/shows#drive",
                SourceTitle = "Algoa FM Show Lineup",
                SourceType = "official_show_page",
                GeographySummary = "Eastern Cape and Garden Route coverage",
                AudienceSummary = "Regional drive-time audience of commuters and lifestyle listeners heading home",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-54", "working adults" }),
                LanguageSummary = "English-led commercial radio",
                MarketSummary = "Regional radio",
                Notes = "Weekdays 15:00-19:00 drive show.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "drive", match_keywords = new[] { "drive", "afternoon" } })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Kaya 959",
                ShowOrProgrammeName = "Drive 959 and Kaya Biz",
                SourceUrl = "https://www.kaya959.co.za/shows/",
                SourceTitle = "Kaya 959 Shows",
                SourceType = "official_station_page",
                GeographySummary = "Johannesburg and Gauteng urban market",
                AudienceSummary = "Urban talk, drive, and business-oriented audiences",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-49", "urban professionals" }),
                LanguageSummary = "English-led urban talk and music mix",
                MarketSummary = "Metro radio",
                Notes = "Use for urban professional, business, and drive-time context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28" })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Kaya 959",
                ShowOrProgrammeName = "Siz The World",
                SourceUrl = "https://www.kaya959.co.za/#siz-the-world",
                SourceTitle = "Kaya 959 Home",
                SourceType = "official_show_page",
                GeographySummary = "Johannesburg and Gauteng urban market",
                AudienceSummary = "Breakfast commuters and urban professionals starting the workday",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-49", "urban professionals" }),
                LanguageSummary = "English-led urban talk and music mix",
                MarketSummary = "Metro radio",
                Notes = "Weekdays 06:00-09:00 breakfast show.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "breakfast", match_keywords = new[] { "breakfast", "morning" } })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Kaya 959",
                ShowOrProgrammeName = "Feel Good",
                SourceUrl = "https://www.kaya959.co.za/#feel-good",
                SourceTitle = "Kaya 959 Home",
                SourceType = "official_show_page",
                GeographySummary = "Johannesburg and Gauteng urban market",
                AudienceSummary = "Midday lifestyle audience with urban adult listenership across Gauteng",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-49", "urban adults" }),
                LanguageSummary = "English-led urban talk and music mix",
                MarketSummary = "Metro radio",
                Notes = "Weekdays 12:00-15:00 midday show.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "midday", match_keywords = new[] { "midday", "lifestyle", "retail" } })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Kaya 959",
                ShowOrProgrammeName = "Drive 959",
                SourceUrl = "https://www.kaya959.co.za/shows/drive-959/#drive",
                SourceTitle = "Drive 959",
                SourceType = "official_show_page",
                GeographySummary = "Johannesburg and Gauteng urban market",
                AudienceSummary = "Urban professionals and commuter audience in afternoon drive",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-49", "urban professionals" }),
                LanguageSummary = "English-led urban talk and music mix",
                MarketSummary = "Metro radio",
                Notes = "Weekdays 15:00-18:00 drive show.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "drive", match_keywords = new[] { "drive", "commuter" } })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Kaya 959",
                ShowOrProgrammeName = "Kaya Biz",
                SourceUrl = "https://www.kaya959.co.za/#kaya-biz",
                SourceTitle = "Kaya 959 Home",
                SourceType = "official_show_page",
                GeographySummary = "Johannesburg and Gauteng urban market",
                AudienceSummary = "Business-minded audience, entrepreneurs, and professionals",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-54", "professionals" }),
                LanguageSummary = "English-led urban talk and music mix",
                MarketSummary = "Metro radio",
                Notes = "Weekdays 18:00-20:00 business show.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "midday", match_keywords = new[] { "biz", "business", "workzone", "powerweek", "lunchtime" } })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Jozi FM",
                ShowOrProgrammeName = "Big Breakfast and Afternoon Fix",
                SourceUrl = "https://www.jozifm.co.za/jozifm-23/",
                SourceTitle = "Jozi FM About",
                SourceType = "official_station_page",
                GeographySummary = "Soweto and wider Gauteng community reach",
                AudienceSummary = "Community radio audience with multilingual local relevance",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "18-49", "community adults" }),
                LanguageSummary = "English, isiZulu, Sesotho, Sepedi, and Setswana",
                MarketSummary = "Community radio",
                Notes = "Use for Soweto/Gauteng geography and multilingual audience context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28" })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Jozi FM",
                ShowOrProgrammeName = "Big Breakfast Show",
                SourceUrl = "https://www.jozifm.co.za/maq-apr2023/#big-breakfast",
                SourceTitle = "Jozi FM Big Breakfast Show",
                SourceType = "official_show_page",
                GeographySummary = "Soweto and wider Gauteng community reach",
                AudienceSummary = "Community breakfast audience with multilingual commuter and family relevance",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "18-49", "community adults" }),
                LanguageSummary = "English, isiZulu, Sesotho, Sepedi, and Setswana",
                MarketSummary = "Community radio",
                Notes = "Breakfast show used for morning commuter context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "breakfast", match_keywords = new[] { "breakfast", "morning" } })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Jozi FM",
                ShowOrProgrammeName = "The Afternoon Fix",
                SourceUrl = "https://www.jozifm.co.za/home/#afternoon-fix",
                SourceTitle = "Jozi FM Home",
                SourceType = "official_show_page",
                GeographySummary = "Soweto and wider Gauteng community reach",
                AudienceSummary = "Community drive-time audience with lifestyle, family, and local relevance",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "18-49", "community adults" }),
                LanguageSummary = "English, isiZulu, Sesotho, Sepedi, and Setswana",
                MarketSummary = "Community radio",
                Notes = "Drive show shown on Jozi FM official site as 15:00-18:00.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "drive", match_keywords = new[] { "drive", "afternoon" } })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Smile 90.4FM",
                ShowOrProgrammeName = "Breakfast and Drive",
                SourceUrl = "https://smilefm.co.za/wp-content/uploads/2024/06/WhySmileFM_feb24.pdf",
                SourceTitle = "Why Smile 90.4FM",
                SourceType = "official_media_kit",
                GeographySummary = "Cape Town and Western Cape urban market",
                AudienceSummary = "Adult contemporary radio audience across breakfast and drive",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-54", "working adults" }),
                LanguageSummary = "English-led adult contemporary format",
                MarketSummary = "Regional commercial radio",
                Notes = "Use for Cape Town lifestyle and adult contemporary context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28" })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Smile 90.4FM",
                ShowOrProgrammeName = "Ryan O'Connor Breakfast Show",
                SourceUrl = "https://smilefm.co.za/presenters/ryan-o-connor/#breakfast",
                SourceTitle = "Ryan O'Connor",
                SourceType = "official_show_page",
                GeographySummary = "Cape Town and Western Cape urban market",
                AudienceSummary = "Cape Town morning commuters with affluent family-oriented adult contemporary audience",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "35+", "working adults" }),
                LanguageSummary = "English and Afrikaans adult contemporary radio",
                MarketSummary = "Regional commercial radio",
                Notes = "Weekdays 06:00-09:00 flagship breakfast show.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "breakfast", match_keywords = new[] { "breakfast", "morning" } })
            },
            new
            {
                MediaChannel = "radio",
                StationOrChannelName = "Smile 90.4FM",
                ShowOrProgrammeName = "JoyRide with Angel Campey",
                SourceUrl = "https://smilefm.co.za/about-us/#joyride",
                SourceTitle = "Smile FM About Us",
                SourceType = "official_show_page",
                GeographySummary = "Cape Town and Western Cape urban market",
                AudienceSummary = "Cape Town drive-home audience with lifestyle and entertainment appeal",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "35+", "working adults" }),
                LanguageSummary = "English and Afrikaans adult contemporary radio",
                MarketSummary = "Regional commercial radio",
                Notes = "Drive show used for afternoon commute context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "drive", match_keywords = new[] { "drive", "joyride", "afternoon" } })
            },
            new
            {
                MediaChannel = "tv",
                StationOrChannelName = "SABC 3",
                ShowOrProgrammeName = "Expresso",
                SourceUrl = "https://www.sabc3.co.za/sabc/home/channel/tvshows/details?id=4af9a5e1-30f4-43e2-bf09-6124dd16be3c",
                SourceTitle = "Expresso",
                SourceType = "official_programme_page",
                GeographySummary = "National free-to-air TV coverage",
                AudienceSummary = "Morning lifestyle audience with adult household and working viewer appeal",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-54", "adult households" }),
                LanguageSummary = "English-led television",
                MarketSummary = "National television",
                Notes = "Breakfast and morning lifestyle programme on SABC 3.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "breakfast", genre = "lifestyle", match_keywords = new[] { "expresso", "morning", "breakfast" } })
            },
            new
            {
                MediaChannel = "tv",
                StationOrChannelName = "SABC News Channel",
                ShowOrProgrammeName = "News packages",
                SourceUrl = "https://www.sabcplus.com/channels/sabc-news",
                SourceTitle = "SABC News Channel",
                SourceType = "official_channel_page",
                GeographySummary = "National free-to-air and digital news coverage",
                AudienceSummary = "News, current affairs, and adult information-seeking audience",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-64", "adult viewers" }),
                LanguageSummary = "English-led news television",
                MarketSummary = "National television",
                Notes = "Use for news package and current affairs context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "midday", genre = "news", match_keywords = new[] { "news", "current affairs" } })
            },
            new
            {
                MediaChannel = "tv",
                StationOrChannelName = "SABC Sport",
                ShowOrProgrammeName = "Soccer Live",
                SourceUrl = "https://www.sabcplus.com/channels/sabc-sport",
                SourceTitle = "SABC Sport",
                SourceType = "official_channel_page",
                GeographySummary = "National sports coverage",
                AudienceSummary = "Sport-focused audience with strong male-skewed event viewing and live-match intent",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "18-49", "sport audience" }),
                LanguageSummary = "Multilingual sports television",
                MarketSummary = "National television",
                Notes = "Use for live sport and sports package context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "drive", genre = "sport", match_keywords = new[] { "sport", "soccer", "match", "nba", "boxing" } })
            },
            new
            {
                MediaChannel = "tv",
                StationOrChannelName = "SABC 1",
                ShowOrProgrammeName = "YO TV",
                SourceUrl = "https://www.sabcplus.com/channels/sabc1",
                SourceTitle = "SABC 1",
                SourceType = "official_channel_page",
                GeographySummary = "National free-to-air TV coverage",
                AudienceSummary = "Youth and family audience with strong after-school relevance",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "13-24", "family co-viewing" }),
                LanguageSummary = "Multilingual free-to-air television",
                MarketSummary = "National television",
                Notes = "Use for youth and family-friendly scheduling context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "midday", genre = "youth", match_keywords = new[] { "yo tv", "kids", "youth", "afternoon" } })
            },
            new
            {
                MediaChannel = "tv",
                StationOrChannelName = "e.tv",
                ShowOrProgrammeName = "e.tv News",
                SourceUrl = "https://www.etv.co.za/shows/etv-news",
                SourceTitle = "e.tv News",
                SourceType = "official_programme_page",
                GeographySummary = "National free-to-air TV coverage",
                AudienceSummary = "Mass-market adult audience for evening news and current affairs",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "25-64", "adult viewers" }),
                LanguageSummary = "English-led television",
                MarketSummary = "National television",
                Notes = "Use for e.tv evening news context.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "drive", genre = "news", match_keywords = new[] { "news", "evening", "etv news" } })
            },
            new
            {
                MediaChannel = "tv",
                StationOrChannelName = "e.tv",
                ShowOrProgrammeName = "House of Zwide",
                SourceUrl = "https://www.etv.co.za/shows/house-zwide",
                SourceTitle = "House of Zwide",
                SourceType = "official_programme_page",
                GeographySummary = "National free-to-air TV coverage",
                AudienceSummary = "Lifestyle and entertainment audience with strong soap-opera reach",
                AgeGroupsJson = JsonSerializer.Serialize(new[] { "18-49", "mass-market adults" }),
                LanguageSummary = "English-led television",
                MarketSummary = "National television",
                Notes = "Use for evening entertainment and lifestyle-led placements.",
                MetadataJson = JsonSerializer.Serialize(new { seeded_from = "official_web_research", verified_on = "2026-03-28", daypart = "drive", genre = "lifestyle", match_keywords = new[] { "drama", "lifestyle", "entertainment", "prime" } })
            }
        };

        foreach (var source in sources)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, source, cancellationToken: cancellationToken));
        }
    }

    private async Task SyncRadioStationProfilesAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string upsertSql = @"
insert into radio_stations (
    name,
    normalized_name,
    station_group,
    market_scope,
    market_tier,
    monthly_listenership,
    weekly_listenership,
    audience_summary,
    primary_audience,
    secondary_audiences_json,
    language_summary,
    age_groups_json,
    lsm_min,
    lsm_max,
    coverage_summary,
    province_coverage_json,
    city_coverage_json,
    is_flagship_station,
    is_premium_station,
    brand_strength_score,
    coverage_score,
    audience_power_score,
    source_url,
    source_document_id
)
values (
    @StationName,
    normalize_station_name(@StationName),
    @StationGroup,
    @MarketScope,
    @MarketTier,
    @MonthlyListenership,
    @WeeklyListenership,
    @AudienceSummary,
    @PrimaryAudience,
    cast(@SecondaryAudiencesJson as jsonb),
    @LanguageSummary,
    cast(@AgeGroupsJson as jsonb),
    @LsmMin,
    @LsmMax,
    @CoverageSummary,
    cast(@ProvinceCoverageJson as jsonb),
    cast(@CityCoverageJson as jsonb),
    @IsFlagshipStation,
    @IsPremiumStation,
    @BrandStrengthScore,
    @CoverageScore,
    @AudiencePowerScore,
    @SourceUrl,
    @SourceDocumentId
)
on conflict (normalized_name) do update
set station_group = excluded.station_group,
    market_scope = excluded.market_scope,
    market_tier = excluded.market_tier,
    monthly_listenership = excluded.monthly_listenership,
    weekly_listenership = coalesce(excluded.weekly_listenership, radio_stations.weekly_listenership),
    audience_summary = excluded.audience_summary,
    primary_audience = excluded.primary_audience,
    secondary_audiences_json = excluded.secondary_audiences_json,
    language_summary = excluded.language_summary,
    age_groups_json = excluded.age_groups_json,
    lsm_min = excluded.lsm_min,
    lsm_max = excluded.lsm_max,
    coverage_summary = excluded.coverage_summary,
    province_coverage_json = excluded.province_coverage_json,
    city_coverage_json = excluded.city_coverage_json,
    is_flagship_station = excluded.is_flagship_station,
    is_premium_station = excluded.is_premium_station,
    brand_strength_score = excluded.brand_strength_score,
    coverage_score = excluded.coverage_score,
    audience_power_score = excluded.audience_power_score,
    source_url = excluded.source_url,
    source_document_id = excluded.source_document_id;";

        const string syncInventorySql = @"
update radio_inventory_items rii
set audience_summary = coalesce(rii.audience_summary, rs.audience_summary),
    inventory_tier = case
        when rs.is_flagship_station or rs.is_premium_station or coalesce(rs.market_tier, '') in ('flagship', 'premium') then 'premium'
        when coalesce(rs.market_scope, '') in ('national', 'regional') then 'core'
        else 'starter'
    end,
    estimated_reach = greatest(coalesce(rs.monthly_listenership / 24, 0), 0),
    preview_priority = coalesce(rs.brand_strength_score, 0) + coalesce(rs.audience_power_score, 0),
    planning_priority = coalesce(rs.coverage_score, 0) + coalesce(rs.audience_power_score, 0),
    is_entry_friendly = case
        when coalesce(rs.market_scope, '') in ('regional', 'metro') and not rs.is_flagship_station then true
        else false
    end,
    is_boost_friendly = case
        when coalesce(rs.market_scope, '') in ('regional', 'metro', 'national') then true
        else false
    end,
    is_scale_friendly = case
        when rs.is_premium_station or rs.is_flagship_station or coalesce(rs.market_scope, '') = 'national' then true
        else false
    end,
    is_dominance_friendly = case
        when rs.is_flagship_station or coalesce(rs.market_tier, '') in ('flagship', 'premium') then true
        else false
    end
from radio_stations rs
where rs.id = rii.station_id;";

        var sourceDocumentId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            @"
            select id
            from source_documents
            where lower(source_file_name) like '%sabc-radio-rates-fiscal-2024-2025%'
            order by created_at desc
            limit 1;
            ",
            cancellationToken: cancellationToken));

        foreach (var seed in GetSabcRadioStationProfileSeeds())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                upsertSql,
                new
                {
                    seed.StationName,
                    seed.StationGroup,
                    seed.MarketScope,
                    seed.MarketTier,
                    seed.MonthlyListenership,
                    seed.WeeklyListenership,
                    seed.AudienceSummary,
                    seed.PrimaryAudience,
                    SecondaryAudiencesJson = JsonSerializer.Serialize(seed.SecondaryAudiences),
                    seed.LanguageSummary,
                    AgeGroupsJson = JsonSerializer.Serialize(seed.AgeGroups),
                    seed.LsmMin,
                    seed.LsmMax,
                    seed.CoverageSummary,
                    ProvinceCoverageJson = JsonSerializer.Serialize(seed.ProvinceCoverage),
                    CityCoverageJson = JsonSerializer.Serialize(seed.CityCoverage),
                    seed.IsFlagshipStation,
                    seed.IsPremiumStation,
                    seed.BrandStrengthScore,
                    seed.CoverageScore,
                    seed.AudiencePowerScore,
                    seed.SourceUrl,
                    SourceDocumentId = sourceDocumentId
                },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(syncInventorySql, cancellationToken: cancellationToken));
    }

    private async Task SyncSourceDocumentsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
insert into source_documents (
    source_file_name,
    source_path,
    media_channel,
    supplier_name,
    document_title,
    document_type,
    metadata_json
)
values (
    @SourceFileName,
    @SourcePath,
    @MediaChannel,
    @SupplierName,
    @DocumentTitle,
    @DocumentType,
    cast(@MetadataJson as jsonb)
)
on conflict (source_path) do update
set media_channel = excluded.media_channel,
    supplier_name = excluded.supplier_name,
    document_title = excluded.document_title,
    document_type = excluded.document_type,
    metadata_json = excluded.metadata_json,
    updated_at = now();";

        var files = Directory
            .EnumerateFiles(_options.SourceDirectory!, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var extension = Path.GetExtension(path);
                return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);
            });

        foreach (var path in files)
        {
            var fileName = Path.GetFileName(path);
            var mediaChannel = ClassifyMediaChannel(fileName);
            var documentType = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            var supplier = InferSupplier(fileName);
            var metadataJson = JsonSerializer.Serialize(new
            {
                seeded_from = "filesystem",
                classification = mediaChannel
            });

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    SourceFileName = fileName,
                    SourcePath = path,
                    MediaChannel = mediaChannel,
                    SupplierName = supplier,
                    DocumentTitle = Path.GetFileNameWithoutExtension(fileName),
                    DocumentType = documentType,
                    MetadataJson = metadataJson
                },
                cancellationToken: cancellationToken));
        }
    }

    private async Task SyncSourceDocumentPagesAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
insert into source_document_pages (source_document_id, page_number, raw_text)
select
    sd.id,
    rip.page,
    rip.page_text
from raw_import_pages rip
join source_documents sd on lower(sd.source_file_name) = lower(rip.source_file)
where rip.page is not null
on conflict (source_document_id, page_number) do update
set raw_text = excluded.raw_text;";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private async Task SyncRadioShowsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
with show_candidates as (
    select
        rs.id as station_id,
        sd.id as source_document_id,
        coalesce(nullif(trim(rsg.package_name), ''), 'Unnamed show') as show_name,
        case
            when lower(coalesce(rsg.package_name, '')) like '%breakfast%' or lower(coalesce(rsg.package_name, '')) like '%morning%' then 'breakfast'
            when lower(coalesce(rsg.package_name, '')) like '%drive%' or lower(coalesce(rsg.package_name, '')) like '%afternoon%' then 'drive'
            when lower(coalesce(rsg.package_name, '')) like '%workzone%' or lower(coalesce(rsg.package_name, '')) like '%powerweek%' then 'midday'
            when lower(coalesce(rsg.package_name, '')) like '%lunch%' or lower(coalesce(rsg.package_name, '')) like '%retail%' or lower(coalesce(rsg.package_name, '')) like '%weekend%' then 'midday'
            when coalesce(rsg.monday_friday_windows, '') like '06:%' or coalesce(rsg.monday_friday_windows, '') like '07:%' then 'breakfast'
            when coalesce(rsg.monday_friday_windows, '') like '15:%' or coalesce(rsg.monday_friday_windows, '') like '16:%' then 'drive'
            when coalesce(rsg.monday_friday_windows, '') <> '' then 'midday'
            else null
        end as default_daypart,
        case
            when split_part(coalesce(rsg.monday_friday_windows, ''), '-', 1) ~ '^\d{2}:\d{2}$'
                then split_part(rsg.monday_friday_windows, '-', 1)::time
            else null
        end as default_start_time,
        case
            when split_part(coalesce(rsg.monday_friday_windows, ''), '-', 2) ~ '^\d{2}:\d{2}$'
                then split_part(rsg.monday_friday_windows, '-', 2)::time
            else null
        end as default_end_time,
        mrs.language_summary as language,
        coalesce(mrs.market_summary, 'regional') as geography_scope,
        case
            when mrs.geography_summary ilike '%gauteng%' then 'Gauteng'
            when mrs.geography_summary ilike '%western cape%' then 'Western Cape'
            when mrs.geography_summary ilike '%eastern cape%' then 'Eastern Cape'
            else null
        end as province,
        case
            when mrs.geography_summary ilike '%johannesburg%' then 'Johannesburg'
            when mrs.geography_summary ilike '%cape town%' then 'Cape Town'
            when mrs.geography_summary ilike '%port elizabeth%' then 'Gqeberha'
            when mrs.geography_summary ilike '%garden route%' then 'George'
            when mrs.geography_summary ilike '%soweto%' then 'Soweto'
            else null
        end as city,
        mrs.audience_summary,
        coalesce(mrs.age_groups_json, '[]'::jsonb) as age_groups_json,
        mrs.source_url,
        jsonb_build_object(
            'station', rsg.station,
            'package_name', rsg.package_name,
            'weekday_windows', rsg.monday_friday_windows,
            'saturday_windows', rsg.saturday_windows,
            'sunday_windows', rsg.sunday_windows,
            'reference_source', mrs.source_url
        ) as metadata_json
    from radio_slot_grids rsg
    join radio_stations rs on rs.normalized_name = normalize_station_name(rsg.station)
    left join source_documents sd on lower(sd.source_file_name) = lower(rsg.source_file)
    left join lateral (
        select m.*
        from media_reference_sources m
        where m.media_channel = 'radio'
          and normalize_station_name(m.station_or_channel_name) = rs.normalized_name
        order by
            case
                when rsg.package_name ilike '%breakfast%' and m.show_or_programme_name ilike '%breakfast%' then 0
                when rsg.package_name ilike '%drive%' and m.show_or_programme_name ilike '%drive%' then 0
                when rsg.package_name ilike '%biz%' and m.show_or_programme_name ilike '%biz%' then 0
                when (
                        rsg.package_name ilike '%workzone%'
                        or rsg.package_name ilike '%powerweek%'
                        or rsg.package_name ilike '%lunchtime%'
                        or rsg.package_name ilike '%retail%'
                        or rsg.package_name ilike '%weekend%'
                     )
                     and coalesce(m.metadata_json ->> 'daypart', '') = 'midday' then 0
                else 1
            end,
            m.updated_at desc
        limit 1
    ) mrs on true
    where coalesce(trim(rsg.package_name), '') <> ''
),
deduped_shows as (
    select distinct on (station_id, show_name)
        station_id,
        source_document_id,
        show_name,
        default_daypart,
        default_start_time,
        default_end_time,
        language,
        geography_scope,
        province,
        city,
        audience_summary,
        age_groups_json,
        source_url,
        metadata_json
    from show_candidates
    order by station_id, show_name, source_document_id nulls last
)
insert into radio_shows (
    station_id,
    source_document_id,
    show_name,
    default_daypart,
    default_start_time,
    default_end_time,
    language,
    geography_scope,
    province,
    city,
    audience_summary,
    age_groups_json,
    source_url,
    metadata_json
)
select
    station_id,
    source_document_id,
    show_name,
    default_daypart,
    default_start_time,
    default_end_time,
    language,
    geography_scope,
    province,
    city,
    audience_summary,
    age_groups_json,
    source_url,
    metadata_json
from deduped_shows
on conflict (station_id, show_name) do update
set default_daypart = excluded.default_daypart,
    default_start_time = excluded.default_start_time,
    default_end_time = excluded.default_end_time,
    language = coalesce(excluded.language, radio_shows.language),
    geography_scope = coalesce(excluded.geography_scope, radio_shows.geography_scope),
    province = coalesce(excluded.province, radio_shows.province),
    city = coalesce(excluded.city, radio_shows.city),
    audience_summary = coalesce(excluded.audience_summary, radio_shows.audience_summary),
    age_groups_json = excluded.age_groups_json,
    source_url = coalesce(excluded.source_url, radio_shows.source_url),
    metadata_json = excluded.metadata_json,
    updated_at = now();";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private async Task SyncRadioInventoryAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string deleteSql = @"
delete from radio_inventory_items rii
where not exists (select 1 from radio_packages_final rpf where rpf.id = rii.id)
  and not exists (select 1 from radio_slots_final rsf where rsf.id = rii.id);";

        const string packageSql = @"
insert into radio_inventory_items (
    id,
    station_id,
    show_id,
    source_document_id,
    inventory_kind,
    inventory_name,
    package_cost_zar,
    language,
    geography_scope,
    province,
    city,
    audience_summary,
    age_groups_json,
    source_url,
    is_available,
    metadata_json
)
select
    rpf.id,
    rpf.station_id,
    rsw.id,
    sd.id,
    'package',
    concat(rs.name, ' - ', rpf.name),
    rpf.total_cost,
    rsw.language,
    coalesce(rsw.geography_scope, mrs.market_summary, 'regional'),
    rsw.province,
    rsw.city,
    coalesce(rsw.audience_summary, mrs.audience_summary),
    coalesce(rsw.age_groups_json, mrs.age_groups_json, '[]'::jsonb),
    coalesce(rsw.source_url, mrs.source_url),
    true,
    coalesce(rpf.metadata_json, '{}'::jsonb) || jsonb_build_object('reference_source', mrs.source_url)
from radio_packages_final rpf
join radio_stations rs on rs.id = rpf.station_id
left join source_documents sd on lower(sd.source_file_name) = lower(coalesce(rpf.metadata_json ->> 'source_file', ''))
left join radio_shows rsw on rsw.station_id = rpf.station_id and lower(rsw.show_name) = lower(rpf.name)
left join lateral (
    select m.*
    from media_reference_sources m
    where m.media_channel = 'radio'
      and normalize_station_name(m.station_or_channel_name) = rs.normalized_name
    order by m.updated_at desc
    limit 1
) mrs on true
on conflict (id) do update
set show_id = excluded.show_id,
    source_document_id = excluded.source_document_id,
    package_cost_zar = excluded.package_cost_zar,
    language = coalesce(excluded.language, radio_inventory_items.language),
    geography_scope = coalesce(excluded.geography_scope, radio_inventory_items.geography_scope),
    province = coalesce(excluded.province, radio_inventory_items.province),
    city = coalesce(excluded.city, radio_inventory_items.city),
    audience_summary = coalesce(excluded.audience_summary, radio_inventory_items.audience_summary),
    age_groups_json = excluded.age_groups_json,
    source_url = coalesce(excluded.source_url, radio_inventory_items.source_url),
    metadata_json = excluded.metadata_json,
    updated_at = now();";

        const string slotSql = @"
insert into radio_inventory_items (
    id,
    station_id,
    show_id,
    source_document_id,
    inventory_kind,
    inventory_name,
    daypart,
    slot_type,
    duration_seconds,
    rate_zar,
    language,
    geography_scope,
    province,
    city,
    audience_summary,
    age_groups_json,
    source_url,
    is_available,
    metadata_json
)
select
    rsf.id,
    rsf.station_id,
    rsw.id,
    sd.id,
    case
        when rsf.source_kind = 'sabc_rate_table' then 'rate_card'
        else 'slot'
    end,
    concat(rs.name, ' - ', coalesce(rsw.show_name, rsf.time_band, rsf.slot_type, 'Radio slot')),
    coalesce(rsw.default_daypart, rsf.time_band),
    rsf.slot_type,
    rsf.duration_seconds,
    rsf.rate,
    rsw.language,
    coalesce(rsw.geography_scope, mrs.market_summary, 'regional'),
    rsw.province,
    rsw.city,
    coalesce(rsw.audience_summary, mrs.audience_summary),
    coalesce(rsw.age_groups_json, mrs.age_groups_json, '[]'::jsonb),
    coalesce(rsw.source_url, mrs.source_url),
    true,
    coalesce(rsf.metadata_json, '{}'::jsonb) || jsonb_build_object('reference_source', mrs.source_url)
from radio_slots_final rsf
join radio_stations rs on rs.id = rsf.station_id
left join source_documents sd on lower(sd.source_file_name) = lower(coalesce(rsf.metadata_json ->> 'source_file', ''))
left join radio_shows rsw on rsw.station_id = rsf.station_id and lower(rsw.show_name) = lower(coalesce(rsf.metadata_json ->> 'package_name', ''))
left join lateral (
    select m.*
    from media_reference_sources m
    where m.media_channel = 'radio'
      and normalize_station_name(m.station_or_channel_name) = rs.normalized_name
    order by
        case
            when coalesce(rsw.default_daypart, rsf.time_band, '') ilike '%breakfast%' and m.show_or_programme_name ilike '%breakfast%' then 0
            when coalesce(rsw.default_daypart, rsf.time_band, '') ilike '%drive%' and m.show_or_programme_name ilike '%drive%' then 0
            when lower(coalesce(rsw.show_name, '')) like '%biz%' and m.show_or_programme_name ilike '%biz%' then 0
            else 1
        end,
        m.updated_at desc
    limit 1
) mrs on true
on conflict (id) do update
set show_id = excluded.show_id,
    source_document_id = excluded.source_document_id,
    daypart = excluded.daypart,
    slot_type = excluded.slot_type,
    duration_seconds = excluded.duration_seconds,
    rate_zar = excluded.rate_zar,
    language = coalesce(excluded.language, radio_inventory_items.language),
    geography_scope = coalesce(excluded.geography_scope, radio_inventory_items.geography_scope),
    province = coalesce(excluded.province, radio_inventory_items.province),
    city = coalesce(excluded.city, radio_inventory_items.city),
    audience_summary = coalesce(excluded.audience_summary, radio_inventory_items.audience_summary),
    age_groups_json = excluded.age_groups_json,
    source_url = coalesce(excluded.source_url, radio_inventory_items.source_url),
    metadata_json = excluded.metadata_json,
    updated_at = now();";

        await connection.ExecuteAsync(new CommandDefinition(deleteSql, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(packageSql, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(slotSql, cancellationToken: cancellationToken));
    }

    private async Task SyncRadioTagsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string contentSql = @"
insert into radio_inventory_content_tags (radio_inventory_item_id, content_taxonomy_id)
select distinct rii.id, ct.id
from radio_inventory_items rii
join content_taxonomy ct on ct.code = case
    when coalesce(rii.daypart, '') ilike '%breakfast%' then 'breakfast'
    when coalesce(rii.daypart, '') ilike '%drive%' then 'drive'
    when lower(rii.inventory_name) like '%workzone%' or lower(rii.inventory_name) like '%powerweek%' then 'business'
    when lower(rii.inventory_name) like '%retail%' or lower(rii.inventory_name) like '%weekend%' or lower(rii.inventory_name) like '%lunchtime%' then 'lifestyle'
    when coalesce(rii.daypart, '') <> '' then 'midday'
    when lower(rii.inventory_name) like '%sport%' then 'sport'
    when lower(rii.inventory_name) like '%business%' then 'business'
    when lower(rii.inventory_name) like '%lifestyle%' then 'lifestyle'
    else 'talk'
end
on conflict do nothing;";

        const string audienceSql = @"
insert into radio_inventory_audience_tags (radio_inventory_item_id, audience_taxonomy_id)
select distinct rii.id, atx.id
from radio_inventory_items rii
cross join lateral (
    values
        (case when coalesce(rii.daypart, '') ilike '%breakfast%' or coalesce(rii.daypart, '') ilike '%drive%' then 'commuters' end),
        (case when coalesce(rii.daypart, '') ilike '%breakfast%' then 'professionals' end),
        (case when lower(rii.inventory_name) like '%workzone%' or lower(rii.inventory_name) like '%powerweek%' then 'business' end),
        (case when coalesce(rii.daypart, '') ilike '%midday%' then 'retail' end),
        (case when coalesce(rii.daypart, '') ilike '%midday%' then 'stay_at_home' end),
        (case when lower(rii.inventory_name) like '%retail%' or lower(rii.inventory_name) like '%weekend%' or lower(rii.inventory_name) like '%lunchtime%' then 'lifestyle' end),
        (case when lower(rii.inventory_name) like '%sport%' then 'sport' end),
        (case when lower(rii.inventory_name) like '%business%' then 'business' end),
        (case when lower(rii.inventory_name) like '%lifestyle%' then 'lifestyle' end)
) as tags(code)
join audience_taxonomy atx on atx.code = tags.code
where tags.code is not null
on conflict do nothing;";

        await connection.ExecuteAsync(new CommandDefinition(contentSql, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(audienceSql, cancellationToken: cancellationToken));
    }

    private async Task SyncTvChannelsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
insert into tv_channels (channel_name, language, geography_scope, metadata_json)
select
    channel_name,
    max(language) as language,
    max(geography_scope) as geography_scope,
    jsonb_build_object('seeded_from', 'media_catalog_sync') as metadata_json
from (
    select
        trim(mrs.station_or_channel_name) as channel_name,
        mrs.language_summary as language,
        coalesce(mrs.market_summary, 'national') as geography_scope
    from media_reference_sources mrs
    where mrs.media_channel = 'tv'
      and coalesce(trim(mrs.station_or_channel_name), '') <> ''

    union all

    select
        case
            when lower(sd.source_file_name) like '%etv%' then 'e.tv'
            when lower(sd.source_file_name) like '%sport%' then 'SABC Sport'
            when lower(sd.source_file_name) like '%news%' then 'SABC News Channel'
            else 'SABC 3'
        end,
        null,
        'national'
    from source_documents sd
    where sd.media_channel = 'tv'
) seeded
group by channel_name
on conflict (channel_name) do update
set language = coalesce(excluded.language, tv_channels.language),
    geography_scope = coalesce(excluded.geography_scope, tv_channels.geography_scope),
    metadata_json = excluded.metadata_json;";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private async Task SyncTvProgrammesAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string referenceSql = @"
with reference_programmes as (
    select
        tc.id as channel_id,
        sd.id as source_document_id,
        mrs.show_or_programme_name as programme_name,
        coalesce(mrs.metadata_json ->> 'genre', 'general') as genre,
        coalesce(mrs.metadata_json ->> 'daypart', 'midday') as daypart,
        mrs.language_summary as language,
        mrs.audience_summary,
        coalesce(mrs.age_groups_json, '[]'::jsonb) as age_groups_json,
        mrs.source_url,
        jsonb_build_object(
            'reference_source', mrs.source_url,
            'source_type', mrs.source_type,
            'seeded_from', 'media_reference_sources'
        ) as metadata_json
    from media_reference_sources mrs
    join tv_channels tc on lower(tc.channel_name) = lower(mrs.station_or_channel_name)
    left join source_documents sd on lower(sd.source_file_name) like '%' || lower(replace(mrs.station_or_channel_name, '.', '')) || '%'
    where mrs.media_channel = 'tv'
      and coalesce(trim(mrs.show_or_programme_name), '') <> ''
)
insert into tv_programmes (
    channel_id,
    source_document_id,
    programme_name,
    genre,
    daypart,
    language,
    audience_summary,
    age_groups_json,
    source_url,
    metadata_json
)
select
    channel_id,
    source_document_id,
    programme_name,
    genre,
    daypart,
    language,
    audience_summary,
    age_groups_json,
    source_url,
    metadata_json
from reference_programmes
on conflict (channel_id, programme_name) do update
set source_document_id = coalesce(excluded.source_document_id, tv_programmes.source_document_id),
    genre = coalesce(excluded.genre, tv_programmes.genre),
    daypart = coalesce(excluded.daypart, tv_programmes.daypart),
    language = coalesce(excluded.language, tv_programmes.language),
    audience_summary = coalesce(excluded.audience_summary, tv_programmes.audience_summary),
    age_groups_json = excluded.age_groups_json,
    source_url = coalesce(excluded.source_url, tv_programmes.source_url),
    metadata_json = excluded.metadata_json,
    updated_at = now();";

        await connection.ExecuteAsync(new CommandDefinition(referenceSql, cancellationToken: cancellationToken));
    }

    private async Task SyncTvInventoryAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string querySql = @"
select
    sdp.id as PageId,
    sd.id as SourceDocumentId,
    sd.source_file_name as SourceFileName,
    sdp.page_number as PageNumber,
    sdp.raw_text as RawText
from source_document_pages sdp
join source_documents sd on sd.id = sdp.source_document_id
where sd.media_channel = 'tv'
order by sd.source_file_name, sdp.page_number;";

        const string deleteSql = @"
delete from tv_inventory_items;
delete from tv_programmes tp
where not exists (
    select 1
    from media_reference_sources mrs
    join tv_channels tc on lower(tc.channel_name) = lower(mrs.station_or_channel_name)
    where mrs.media_channel = 'tv'
      and tc.id = tp.channel_id
      and lower(mrs.show_or_programme_name) = lower(tp.programme_name)
);";

        await connection.ExecuteAsync(new CommandDefinition(deleteSql, cancellationToken: cancellationToken));

        var rows = (await connection.QueryAsync<TvSourcePageRow>(
            new CommandDefinition(querySql, cancellationToken: cancellationToken)))
            .ToList();

        var tvSeeds = new List<TvInventorySeed>();

        foreach (var row in rows)
        {
            var text = row.RawText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (PackageCostRegex.IsMatch(text))
            {
                var channelName = InferTvChannelName(row.SourceFileName, text);
                var programmeName = InferTvPackageName(text, channelName);
                var packageCost = ParseMoney(PackageCostRegex.Match(text).Groups[1].Value);
                var averageCost = ParseMoney(AverageCostPerSpotRegex.Match(text).Groups[1].Value);
                var spotCount = ParseNullableInt(SpotCountRegex.Match(text).Groups[1].Value);
                var genre = InferTvGenre(programmeName, text);
                var daypart = InferTvDaypart(programmeName, text);
                var audienceSummary = InferTvAudienceSummary(channelName, programmeName, genre, daypart, text);
                var sourceUrl = InferTvSourceUrl(channelName, programmeName, genre);

                tvSeeds.Add(new TvInventorySeed
                {
                    ChannelName = channelName,
                    ProgrammeName = programmeName,
                    SourceDocumentId = row.SourceDocumentId,
                    InventoryKind = "package",
                    InventoryName = $"{channelName} - {programmeName}",
                    Daypart = daypart,
                    SlotType = "package",
                    DurationSeconds = 30,
                    RateZar = averageCost,
                    PackageCostZar = packageCost,
                    Language = InferTvLanguage(channelName),
                    GeographyScope = "national",
                    AudienceSummary = audienceSummary,
                    AgeGroupsJson = InferTvAgeGroupsJson(programmeName, genre, daypart),
                    SourceUrl = sourceUrl,
                    Genre = genre,
                    MetadataJson = JsonSerializer.Serialize(new
                    {
                        source_page = row.PageNumber,
                        source_file = row.SourceFileName,
                        parsed_from = "tv_package_page",
                        spots_count = spotCount
                    })
                });
            }

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.Length > 180)
                {
                    continue;
                }

                var match = ProgrammeRateRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var programmeName = CleanProgrammeName(match.Groups["programme"].Value);
                if (!IsMeaningfulTvProgramme(programmeName))
                {
                    continue;
                }

                var channelName = InferTvChannelName(row.SourceFileName, text, programmeName);
                var rate = ParseMoney(match.Groups["rate"].Value);
                if (rate <= 0m)
                {
                    continue;
                }

                var daypart = InferTvDaypart(programmeName, text, match.Groups["time"].Value);
                var genre = InferTvGenre(programmeName, text);
                tvSeeds.Add(new TvInventorySeed
                {
                    ChannelName = channelName,
                    ProgrammeName = programmeName,
                    SourceDocumentId = row.SourceDocumentId,
                    InventoryKind = "rate_card",
                    InventoryName = $"{channelName} - {programmeName}",
                    Daypart = daypart,
                    SlotType = "scheduled_slot",
                    DurationSeconds = 30,
                    RateZar = rate,
                    PackageCostZar = null,
                    Language = InferTvLanguage(channelName),
                    GeographyScope = "national",
                    AudienceSummary = InferTvAudienceSummary(channelName, programmeName, genre, daypart, text),
                    AgeGroupsJson = InferTvAgeGroupsJson(programmeName, genre, daypart),
                    SourceUrl = InferTvSourceUrl(channelName, programmeName, genre),
                    Genre = genre,
                    MetadataJson = JsonSerializer.Serialize(new
                    {
                        source_page = row.PageNumber,
                        source_file = row.SourceFileName,
                        parsed_from = "tv_schedule_page",
                        slot_time = match.Groups["time"].Value
                    })
                });
            }
        }

        var channelMap = (await connection.QueryAsync<(Guid Id, string ChannelName)>(
            new CommandDefinition("select id, channel_name as ChannelName from tv_channels;", cancellationToken: cancellationToken)))
            .ToDictionary(x => x.ChannelName, x => x.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var channelName in tvSeeds.Select(x => x.ChannelName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (channelMap.ContainsKey(channelName))
            {
                continue;
            }

            var channelId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(@"
insert into tv_channels (channel_name, language, geography_scope, metadata_json)
values (@ChannelName, @Language, 'national', '{}'::jsonb)
returning id;", new { ChannelName = channelName, Language = InferTvLanguage(channelName) }, cancellationToken: cancellationToken));

            channelMap[channelName] = channelId;
        }

        foreach (var seed in tvSeeds)
        {
            var channelId = channelMap[seed.ChannelName];
            var programmeId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(@"
insert into tv_programmes (
    channel_id,
    source_document_id,
    programme_name,
    genre,
    daypart,
    language,
    audience_summary,
    age_groups_json,
    source_url,
    metadata_json
)
values (
    @ChannelId,
    @SourceDocumentId,
    @ProgrammeName,
    @Genre,
    @Daypart,
    @Language,
    @AudienceSummary,
    cast(@AgeGroupsJson as jsonb),
    @SourceUrl,
    cast(@MetadataJson as jsonb)
)
on conflict (channel_id, programme_name) do update
set source_document_id = coalesce(excluded.source_document_id, tv_programmes.source_document_id),
    genre = coalesce(excluded.genre, tv_programmes.genre),
    daypart = coalesce(excluded.daypart, tv_programmes.daypart),
    language = coalesce(excluded.language, tv_programmes.language),
    audience_summary = coalesce(excluded.audience_summary, tv_programmes.audience_summary),
    age_groups_json = excluded.age_groups_json,
    source_url = coalesce(excluded.source_url, tv_programmes.source_url),
    metadata_json = excluded.metadata_json,
    updated_at = now()
returning id;",
                new
                {
                    ChannelId = channelId,
                    seed.SourceDocumentId,
                    seed.ProgrammeName,
                    seed.Genre,
                    seed.Daypart,
                    seed.Language,
                    seed.AudienceSummary,
                    seed.AgeGroupsJson,
                    seed.SourceUrl,
                    seed.MetadataJson
                },
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(@"
insert into tv_inventory_items (
    channel_id,
    programme_id,
    source_document_id,
    inventory_kind,
    inventory_name,
    slot_type,
    duration_seconds,
    rate_zar,
    package_cost_zar,
    language,
    geography_scope,
    audience_summary,
    age_groups_json,
    source_url,
    is_available,
    metadata_json
)
values (
    @ChannelId,
    @ProgrammeId,
    @SourceDocumentId,
    @InventoryKind,
    @InventoryName,
    @SlotType,
    @DurationSeconds,
    @RateZar,
    @PackageCostZar,
    @Language,
    @GeographyScope,
    @AudienceSummary,
    cast(@AgeGroupsJson as jsonb),
    @SourceUrl,
    true,
    cast(@MetadataJson as jsonb)
);",
                new
                {
                    ChannelId = channelId,
                    ProgrammeId = programmeId,
                    seed.SourceDocumentId,
                    seed.InventoryKind,
                    seed.InventoryName,
                    seed.SlotType,
                    seed.DurationSeconds,
                    seed.RateZar,
                    seed.PackageCostZar,
                    seed.Language,
                    seed.GeographyScope,
                    seed.AudienceSummary,
                    seed.AgeGroupsJson,
                    seed.SourceUrl,
                    seed.MetadataJson
                },
                cancellationToken: cancellationToken));
        }
    }

    private async Task SyncTvTagsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string contentSql = @"
insert into tv_inventory_content_tags (tv_inventory_item_id, content_taxonomy_id)
select distinct tvi.id, ct.id
from tv_inventory_items tvi
join content_taxonomy ct on ct.code = case
    when lower(coalesce(tvi.inventory_name, '')) like '%news%' then 'news'
    when lower(coalesce(tvi.inventory_name, '')) like '%sport%' or lower(coalesce(tvi.inventory_name, '')) like '%soccer%' or lower(coalesce(tvi.inventory_name, '')) like '%nba%' then 'sport'
    when lower(coalesce(tvi.inventory_name, '')) like '%lifestyle%' or lower(coalesce(tvi.inventory_name, '')) like '%expresso%' then 'lifestyle'
    when lower(coalesce(tvi.inventory_name, '')) like '%business%' then 'business'
    when lower(coalesce(tvi.inventory_name, '')) like '%breakfast%' then 'breakfast'
    when lower(coalesce(tvi.inventory_name, '')) like '%drive%' or lower(coalesce(tvi.inventory_name, '')) like '%prime%' then 'drive'
    else 'talk'
end
on conflict do nothing;";

        const string audienceSql = @"
insert into tv_inventory_audience_tags (tv_inventory_item_id, audience_taxonomy_id)
select distinct tvi.id, atx.id
from tv_inventory_items tvi
cross join lateral (
    values
        (case when lower(coalesce(tvi.audience_summary, '')) like '%commuter%' then 'commuters' end),
        (case when lower(coalesce(tvi.audience_summary, '')) like '%professional%' then 'professionals' end),
        (case when lower(coalesce(tvi.audience_summary, '')) like '%sport%' then 'sport' end),
        (case when lower(coalesce(tvi.audience_summary, '')) like '%lifestyle%' then 'lifestyle' end),
        (case when lower(coalesce(tvi.audience_summary, '')) like '%youth%' or lower(coalesce(tvi.inventory_name, '')) like '%yo tv%' then 'youth' end),
        (case when lower(coalesce(tvi.audience_summary, '')) like '%family%' then 'family' end),
        (case when lower(coalesce(tvi.audience_summary, '')) like '%business%' then 'business' end)
) as tags(code)
join audience_taxonomy atx on atx.code = tags.code
where tags.code is not null
on conflict do nothing;";

        await connection.ExecuteAsync(new CommandDefinition(contentSql, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(audienceSql, cancellationToken: cancellationToken));
    }

    private async Task ApplyRegionClusterMappingsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        var statements = new[]
        {
            @"
update inventory_items_final iif
set region_cluster_id = rc.id
from region_clusters rc
where (
        rc.code = 'gauteng'
        and (
            lower(coalesce(iif.province, '')) = 'gauteng'
            or lower(coalesce(iif.city, '')) in ('johannesburg', 'pretoria', 'midrand', 'soweto')
        )
    )
    or (
        rc.code = 'western-cape'
        and (
            lower(coalesce(iif.province, '')) = 'western cape'
            or lower(coalesce(iif.city, '')) in ('cape town', 'bellville', 'wynberg')
        )
    )
    or (
        rc.code = 'eastern-cape'
        and (
            lower(coalesce(iif.province, '')) = 'eastern cape'
            or lower(coalesce(iif.city, '')) in ('gqeberha', 'port elizabeth', 'east london')
        )
    )
    or (
        rc.code = 'kzn'
        and (
            lower(coalesce(iif.province, '')) = 'kwazulu-natal'
            or lower(coalesce(iif.city, '')) in ('durban', 'pietermaritzburg')
        )
    );",
            @"
update radio_stations rs
set region_cluster_id = rcm.cluster_id
from region_cluster_mappings rcm
where rcm.station_or_channel_name is not null
  and normalize_station_name(rcm.station_or_channel_name) = normalize_station_name(rs.name);",
            @"
update radio_shows rsw
set region_cluster_id = coalesce(rs.region_cluster_id, rsw.region_cluster_id)
from radio_stations rs
where rs.id = rsw.station_id;",
            @"
update radio_inventory_items rii
set region_cluster_id = coalesce(rsw.region_cluster_id, rii.region_cluster_id)
from radio_shows rsw
where rsw.id = rii.show_id;",
            @"
update radio_inventory_items rii
set region_cluster_id = coalesce(rs.region_cluster_id, rii.region_cluster_id)
from radio_stations rs
where rs.id = rii.station_id
  and rii.region_cluster_id is null;",
            @"
update tv_channels tc
set region_cluster_id = rcm.cluster_id
from region_cluster_mappings rcm
where rcm.station_or_channel_name is not null
  and lower(rcm.station_or_channel_name) = lower(tc.channel_name);",
            @"
update tv_programmes tp
set region_cluster_id = coalesce(tc.region_cluster_id, tp.region_cluster_id)
from tv_channels tc
where tc.id = tp.channel_id;",
            @"
update tv_inventory_items tvi
set region_cluster_id = coalesce(tp.region_cluster_id, tvi.region_cluster_id)
from tv_programmes tp
where tp.id = tvi.programme_id;",
            @"
update tv_inventory_items tvi
set region_cluster_id = coalesce(tc.region_cluster_id, tvi.region_cluster_id)
from tv_channels tc
where tc.id = tvi.channel_id
  and tvi.region_cluster_id is null;"
        };

        foreach (var statement in statements)
        {
            await connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: cancellationToken));
        }
    }

    private static IReadOnlyList<SabcRadioStationProfileSeed> GetSabcRadioStationProfileSeeds()
    {
        return new[]
        {
            new SabcRadioStationProfileSeed("Metro FM", "SABC", "national", "flagship", 4_629_000, null, "National urban music and lifestyle station with strong appeal among ambitious, upwardly mobile Black adults.", "urban mainstream", new[] { "music", "lifestyle", "business", "aspirational" }, "English", new[] { "25-49", "urban adults" }, 7, 10, "National footprint with strong urban metro influence and premium commercial appeal.", new[] { "Gauteng", "Western Cape", "KwaZulu-Natal", "Eastern Cape" }, new[] { "Johannesburg", "Pretoria", "Cape Town", "Durban" }, true, true, 9.8m, 9.7m, 9.6m, "metrofm.co.za"),
            new SabcRadioStationProfileSeed("Good Hope FM", "SABC", "regional", "commercial", 680_000, null, "Cape Town contemporary hit radio audience with lifestyle, music, and city culture appeal.", "lifestyle", new[] { "music", "retail", "tourism", "youthful adults" }, "English", new[] { "18-39", "urban adults" }, 6, 10, "Cape Town, surrounding towns, Overberg, and Plettenberg Bay lifestyle market.", new[] { "Western Cape" }, new[] { "Cape Town", "Bellville", "Wynberg", "Plettenberg Bay" }, false, false, 7.2m, 7.1m, 6.9m, "goodhopefm.co.za"),
            new SabcRadioStationProfileSeed("5FM", "SABC", "national", "flagship", 893_000, null, "National youth multimedia brand built around trendsetting music, entertainment, and youth culture.", "youth", new[] { "music", "trendsetters", "digital-first", "entertainment" }, "English", new[] { "18-34", "youth" }, 6, 10, "National youth reach across South Africa through on-air and digital media.", new[] { "Gauteng", "Western Cape", "KwaZulu-Natal", "Eastern Cape" }, new[] { "Johannesburg", "Cape Town", "Durban", "Pretoria" }, true, true, 8.6m, 8.8m, 7.8m, "5fm.co.za"),
            new SabcRadioStationProfileSeed("Ikwekwezi FM", "SABC", "regional", "public", 1_156_000, null, "IsiNdebele-led station focused on cultural identity, education, and community development.", "community", new[] { "culture", "talk", "community", "education" }, "isiNdebele", new[] { "25-54", "family audience" }, 3, 8, "Mpumalanga and isiNdebele-speaking communities with public-service programming.", new[] { "Mpumalanga" }, new[] { "Mbombela", "KwaMhlanga" }, false, false, 6.2m, 6.4m, 6.0m, "ikwekwezifm.co.za"),
            new SabcRadioStationProfileSeed("Lesedi FM", "SABC", "regional", "public", 3_529_000, null, "Largest Sesotho station with strong community trust, empowerment, and information-led programming.", "family", new[] { "community", "culture", "education", "current affairs" }, "Sesotho", new[] { "25-54", "mass market adults" }, 3, 8, "Free State core market with spillover into Lesotho and strong Sesotho-speaking reach.", new[] { "Free State" }, new[] { "Bloemfontein", "Welkom" }, false, false, 7.8m, 7.6m, 8.3m, "lesedifm.co.za"),
            new SabcRadioStationProfileSeed("Ligwalagwala FM", "SABC", "regional", "public", 1_277_000, null, "Upbeat SiSwati station for young, motivated, brand-conscious Black listeners.", "youth", new[] { "lifestyle", "music", "community", "news" }, "isiSwati", new[] { "18-39", "young adults" }, 4, 8, "Mpumalanga-focused SiSwati reach with youth and lifestyle appeal.", new[] { "Mpumalanga" }, new[] { "Mbombela", "Matsapha" }, false, false, 6.5m, 6.4m, 6.3m, "ligwalagwalafm.co.za"),
            new SabcRadioStationProfileSeed("Motsweding FM", "SABC", "regional", "public", 3_042_000, null, "Largest Setswana station with worldly, aspirational, and contemporary identity.", "aspirational", new[] { "community", "culture", "empowerment", "cosmopolitan" }, "Setswana", new[] { "25-54", "mass market adults" }, 3, 8, "North West heartland with strong Setswana-speaking audiences and Botswana spillover.", new[] { "North West" }, new[] { "Mahikeng", "Rustenburg" }, false, false, 7.4m, 7.3m, 8.0m, "motswedingfm.co.za"),
            new SabcRadioStationProfileSeed("Munghana Lonene FM", "SABC", "regional", "public", 1_424_000, null, "Xitsonga station blending information, education, entertainment, and family-oriented community relevance.", "family", new[] { "culture", "community", "family", "entertainment" }, "Xitsonga", new[] { "25-54", "family audience" }, 2, 7, "Limpopo core market with reach into Mpumalanga, Gauteng, and North West.", new[] { "Limpopo", "Mpumalanga", "Gauteng", "North West" }, new[] { "Polokwane", "Mbombela", "Johannesburg" }, false, false, 6.8m, 7.0m, 6.7m, "munghanalonenefm.co.za"),
            new SabcRadioStationProfileSeed("Phalaphala FM", "SABC", "regional", "public", 1_119_000, null, "Modern Tshivenda home station with balanced rural and urban appeal and a strong cultural connection.", "community", new[] { "culture", "family", "community", "heritage" }, "Tshivenda", new[] { "25-54", "family audience" }, 2, 7, "Limpopo core footprint with additional reach into Gauteng, North West, and Mpumalanga.", new[] { "Limpopo", "Gauteng", "North West", "Mpumalanga" }, new[] { "Polokwane", "Johannesburg", "Rustenburg" }, false, false, 6.5m, 6.8m, 6.2m, "phalaphalafm.co.za"),
            new SabcRadioStationProfileSeed("Thobela FM", "SABC", "regional", "public", 2_713_000, null, "Contemporary Sepedi voice with trusted messaging and top-ten radio station stature.", "community", new[] { "family", "community", "current affairs", "culture" }, "Sepedi", new[] { "25-54", "mass market adults" }, 3, 8, "Limpopo and Northern Sotho-speaking communities with strong mass-market reach.", new[] { "Limpopo" }, new[] { "Polokwane", "Burgersfort" }, false, false, 7.1m, 7.0m, 7.5m, "thobelafm.co.za"),
            new SabcRadioStationProfileSeed("Tru FM", "SABC", "regional", "public", 252_000, null, "Youth and youthful Eastern Cape station focused on self-development and socially conscious programming.", "youth", new[] { "youth", "education", "entertainment", "culture" }, "English / isiXhosa", new[] { "16-34", "youth" }, 4, 7, "Eastern Cape youth audience with English and isiXhosa programming.", new[] { "Eastern Cape" }, new[] { "East London", "Mthatha" }, false, false, 5.4m, 5.6m, 4.8m, "trufm.co.za"),
            new SabcRadioStationProfileSeed("Ukhozi FM", "SABC", "regional", "flagship", 7_546_000, null, "Leading African language station with massive IsiZulu audience reach, edutainment, and modern cultural identity.", "mass market", new[] { "youth", "culture", "music", "community" }, "isiZulu", new[] { "16-49", "mass market adults" }, 2, 8, "KwaZulu-Natal powerhouse with one of the biggest radio audiences in Africa.", new[] { "KwaZulu-Natal" }, new[] { "Durban", "Pietermaritzburg" }, true, true, 9.7m, 9.1m, 10.0m, "ukhozifm.co.za"),
            new SabcRadioStationProfileSeed("Umhlobo Wenene FM", "SABC", "regional", "flagship", 4_183_000, null, "Dominant IsiXhosa station across Eastern and Western Cape with major metro reach and deep cultural relevance.", "mass market", new[] { "culture", "community", "music", "family" }, "isiXhosa", new[] { "25-54", "mass market adults" }, 2, 8, "Dominant in Eastern and Western Cape and present across major South African metros.", new[] { "Eastern Cape", "Western Cape", "Gauteng" }, new[] { "Gqeberha", "Cape Town", "Johannesburg", "Mthatha" }, true, true, 8.9m, 8.7m, 9.1m, "umhlobowenenefm.co.za"),
            new SabcRadioStationProfileSeed("XK FM", "SABC", "regional", "public", 1_000, null, "Niche San community station preserving !Xun and Khwe culture through talk and traditional music.", "community", new[] { "culture", "heritage", "community" }, "!Xuntali / Khwedam / Afrikaans", new[] { "16-24", "25-34", "35-49", "50+" }, 1, 6, "Platfontein, Northern Cape community service for San audiences.", new[] { "Northern Cape" }, new[] { "Platfontein", "Kimberley" }, false, false, 2.5m, 2.2m, 1.2m, "xkfm.co.za"),
            new SabcRadioStationProfileSeed("Channel Africa", "SABC", "international", "public", null, null, "International multilingual radio service covering African and global developments with continent-wide perspective.", "current affairs", new[] { "news", "current affairs", "international", "business" }, "English / French / Portuguese / Chinyanja / Kiswahili", new[] { "25-64", "adult audience" }, null, null, "Pan-African and international satellite and digital footprint.", Array.Empty<string>(), Array.Empty<string>(), false, false, 6.0m, 8.4m, 5.8m, "channelafrica.co.za"),
            new SabcRadioStationProfileSeed("Lotus FM", "SABC", "regional", "premium", 237_000, null, "Affluent South African Indian audience with strong cultural, religious, and information needs.", "affluent cultural", new[] { "culture", "religion", "lifestyle", "news" }, "English / Indian language mix", new[] { "25-54", "affluent adults" }, 7, 10, "Regional South African Indian audience with strong cultural identity and quality talk affinity.", new[] { "KwaZulu-Natal", "Gauteng" }, new[] { "Durban", "Johannesburg" }, false, true, 6.8m, 5.9m, 5.4m, "lotusfm.co.za"),
            new SabcRadioStationProfileSeed("Radio 2000", "SABC", "national", "premium", 1_929_000, null, "Cosmopolitan music-driven national station with lifestyle, family, sport, business, and personal finance appeal.", "business", new[] { "lifestyle", "family", "business", "travel" }, "English", new[] { "25-54", "adult professionals" }, 7, 10, "National English-speaking audience with mature, informed, affluent leaning profile.", new[] { "Gauteng", "Western Cape", "KwaZulu-Natal", "Eastern Cape" }, new[] { "Johannesburg", "Pretoria", "Cape Town", "Durban" }, true, true, 8.4m, 8.6m, 8.0m, "radio2000.co.za"),
            new SabcRadioStationProfileSeed("RSG", "SABC", "national", "premium", 1_265_000, null, "Full-spectrum Afrikaans station with strong trust, broad genre mix, and deeply engaged listeners.", "family", new[] { "afrikaans", "news", "music", "culture" }, "Afrikaans", new[] { "25-64", "adult audience" }, 6, 10, "National Afrikaans reach with strong digital engagement and cultural identity.", new[] { "Western Cape", "Gauteng", "Free State" }, new[] { "Cape Town", "Johannesburg", "Bloemfontein" }, false, true, 8.0m, 8.2m, 7.2m, "rsg.co.za"),
            new SabcRadioStationProfileSeed("SAfm", "SABC", "national", "premium", 566_000, null, "National news and current affairs station for mature, sophisticated decision-makers seeking quality information.", "business", new[] { "news", "current affairs", "decision-makers", "public affairs" }, "English", new[] { "30-64", "decision-makers" }, 8, 10, "National current affairs and quality information audience.", new[] { "Gauteng", "Western Cape", "KwaZulu-Natal", "Eastern Cape" }, new[] { "Johannesburg", "Pretoria", "Cape Town", "Durban" }, true, true, 8.5m, 8.8m, 7.0m, "safm.co.za")
        };
    }

    private static string InferTvChannelName(string sourceFileName, string rawText, string? programmeName = null)
    {
        var combined = $"{sourceFileName} {rawText} {programmeName}";
        if (combined.Contains("Expresso", StringComparison.OrdinalIgnoreCase))
        {
            return "SABC 3";
        }

        if (combined.Contains("e.tv", StringComparison.OrdinalIgnoreCase))
        {
            return "e.tv";
        }

        if (combined.Contains("SABC Sport", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("Soccer", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("NBA", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("Boxing", StringComparison.OrdinalIgnoreCase))
        {
            return "SABC Sport";
        }

        if (combined.Contains("SABC News", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("News Package", StringComparison.OrdinalIgnoreCase))
        {
            return "SABC News Channel";
        }

        if (combined.Contains("SABC 1", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("YOTV", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("Uzalo", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("Skeem Saam", StringComparison.OrdinalIgnoreCase))
        {
            return "SABC 1";
        }

        if (combined.Contains("SABC 2", StringComparison.OrdinalIgnoreCase))
        {
            return "SABC 2";
        }

        return "SABC 3";
    }

    private static string InferTvPackageName(string rawText, string channelName)
    {
        static string Limit(string value) => value.Length > 180 ? value[..180].Trim() : value;

        if (rawText.Contains("Expresso", StringComparison.OrdinalIgnoreCase))
        {
            return Limit("Expresso");
        }

        if (rawText.Contains("News Package", StringComparison.OrdinalIgnoreCase))
        {
            return Limit("News packages");
        }

        if (rawText.Contains("LOTS OF SPOTS", StringComparison.OrdinalIgnoreCase))
        {
            return Limit("Lots of spots package");
        }

        if (rawText.Contains("SABC SPORT", StringComparison.OrdinalIgnoreCase))
        {
            return Limit("SABC Sport channel package");
        }

        if (rawText.Contains("SOCCER PACKAGE", StringComparison.OrdinalIgnoreCase))
        {
            return Limit("Soccer package");
        }

        return Limit($"{channelName} package");
    }

    private static string InferTvGenre(string programmeName, string rawText)
    {
        var combined = $"{programmeName} {rawText}".ToLowerInvariant();
        if (combined.Contains("news"))
        {
            return "news";
        }

        if (combined.Contains("sport") || combined.Contains("soccer") || combined.Contains("nba") || combined.Contains("boxing"))
        {
            return "sport";
        }

        if (combined.Contains("expresso") || combined.Contains("lifestyle") || combined.Contains("house of zwide"))
        {
            return "lifestyle";
        }

        if (combined.Contains("yo tv") || combined.Contains("kids"))
        {
            return "youth";
        }

        return "entertainment";
    }

    private static string InferTvDaypart(string programmeName, string rawText, string? slotTime = null)
    {
        var combined = $"{programmeName} {rawText}".ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(slotTime) && TimeSpan.TryParse(slotTime, out var parsed))
        {
            if (parsed >= new TimeSpan(5, 0, 0) && parsed < new TimeSpan(9, 0, 0))
            {
                return "breakfast";
            }

            if (parsed >= new TimeSpan(15, 0, 0) && parsed < new TimeSpan(21, 0, 0))
            {
                return "drive";
            }

            if (parsed >= new TimeSpan(9, 0, 0) && parsed < new TimeSpan(15, 0, 0))
            {
                return "midday";
            }
        }

        if (combined.Contains("morning") || combined.Contains("breakfast") || combined.Contains("expresso"))
        {
            return "breakfast";
        }

        if (combined.Contains("prime") || combined.Contains("evening") || combined.Contains("drive"))
        {
            return "drive";
        }

        return "midday";
    }

    private static string InferTvAudienceSummary(string channelName, string programmeName, string genre, string daypart, string rawText)
    {
        var combined = $"{channelName} {programmeName} {genre} {rawText}".ToLowerInvariant();
        if (combined.Contains("expresso"))
        {
            return "Morning lifestyle audience with adult household appeal";
        }

        if (combined.Contains("news"))
        {
            return "Adult news and current affairs audience";
        }

        if (combined.Contains("sport") || combined.Contains("soccer") || combined.Contains("nba") || combined.Contains("boxing"))
        {
            return "Sport-focused audience with live-event and highlights intent";
        }

        if (combined.Contains("yo tv") || combined.Contains("kids"))
        {
            return "Youth and family co-viewing audience";
        }

        if (genre == "lifestyle")
        {
            return "Lifestyle and entertainment audience with broad adult reach";
        }

        return daypart switch
        {
            "breakfast" => "Morning household and commuter audience",
            "drive" => "Evening and prime-time audience with mass-market appeal",
            _ => "Broad television audience with general entertainment reach"
        };
    }

    private static string InferTvLanguage(string channelName)
    {
        return channelName.Equals("e.tv", StringComparison.OrdinalIgnoreCase)
            ? "English-led television"
            : "Multilingual free-to-air television";
    }

    private static string InferTvSourceUrl(string channelName, string programmeName, string genre)
    {
        var combined = $"{channelName} {programmeName} {genre}".ToLowerInvariant();
        if (combined.Contains("expresso"))
        {
            return "https://www.sabc3.co.za/sabc/home/channel/tvshows/details?id=4af9a5e1-30f4-43e2-bf09-6124dd16be3c";
        }

        if (combined.Contains("news"))
        {
            return channelName.Equals("e.tv", StringComparison.OrdinalIgnoreCase)
                ? "https://www.etv.co.za/shows/etv-news"
                : "https://www.sabcplus.com/channels/sabc-news";
        }

        if (combined.Contains("sport") || combined.Contains("soccer") || combined.Contains("nba"))
        {
            return "https://www.sabcplus.com/channels/sabc-sport";
        }

        if (combined.Contains("yo tv"))
        {
            return "https://www.sabcplus.com/channels/sabc1";
        }

        if (combined.Contains("house of zwide"))
        {
            return "https://www.etv.co.za/shows/house-zwide";
        }

        return channelName.Equals("e.tv", StringComparison.OrdinalIgnoreCase)
            ? "https://www.etv.co.za/"
            : "https://www.sabcplus.com/";
    }

    private static string InferTvAgeGroupsJson(string programmeName, string genre, string daypart)
    {
        var ageGroups = genre switch
        {
            "sport" => new[] { "18-49", "adult sport viewers" },
            "news" => new[] { "25-64", "adult viewers" },
            "youth" => new[] { "13-24", "family co-viewing" },
            "lifestyle" => new[] { "25-54", "adult households" },
            _ => daypart == "breakfast"
                ? new[] { "25-54", "adult households" }
                : new[] { "18-49", "mass-market adults" }
        };

        return JsonSerializer.Serialize(ageGroups);
    }

    private static string CleanProgrammeName(string value)
    {
        var cleaned = value
            .Replace("\n", " ")
            .Replace("  ", " ")
            .Trim(' ', '-', ':');

        return cleaned.Length > 180
            ? cleaned[..180].Trim()
            : cleaned;
    }

    private static bool IsMeaningfulTvProgramme(string programmeName)
    {
        if (string.IsNullOrWhiteSpace(programmeName))
        {
            return false;
        }

        return !programmeName.Equals("Time", StringComparison.OrdinalIgnoreCase)
            && !programmeName.Equals("Slot", StringComparison.OrdinalIgnoreCase)
            && !programmeName.StartsWith("R", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal? ParseMoney(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var sanitized = rawValue
            .Replace(" ", string.Empty)
            .Replace(",", string.Empty)
            .Trim();

        return decimal.TryParse(sanitized, out var parsed)
            ? parsed
            : null;
    }

    private static int? ParseNullableInt(string? rawValue)
    {
        return int.TryParse(rawValue, out var parsed)
            ? parsed
            : null;
    }

    private static string ClassifyMediaChannel(string fileName)
    {
        var normalized = fileName.Trim().ToLowerInvariant();
        if (normalized.Contains("tv"))
        {
            return "tv";
        }

        if (normalized.Contains("brief"))
        {
            return "brief";
        }

        if (KnownRadioTokens.Any(token => normalized.Contains(token)))
        {
            return "radio";
        }

        return "document";
    }

    private static string? InferSupplier(string fileName)
    {
        var normalized = fileName.Trim();
        var matches = new[]
        {
            "Algoa FM",
            "Jozi FM",
            "Kaya",
            "Smile 90.4FM",
            "SABC",
            "YFM"
        };

        return matches.FirstOrDefault(match => normalized.Contains(match, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TvSourcePageRow
    {
        public Guid PageId { get; set; }
        public Guid SourceDocumentId { get; set; }
        public string SourceFileName { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public string RawText { get; set; } = string.Empty;
    }

    private sealed class TvInventorySeed
    {
        public string ChannelName { get; set; } = string.Empty;
        public string ProgrammeName { get; set; } = string.Empty;
        public Guid SourceDocumentId { get; set; }
        public string InventoryKind { get; set; } = string.Empty;
        public string InventoryName { get; set; } = string.Empty;
        public string Daypart { get; set; } = string.Empty;
        public string SlotType { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
        public decimal? RateZar { get; set; }
        public decimal? PackageCostZar { get; set; }
        public string Language { get; set; } = string.Empty;
        public string GeographyScope { get; set; } = string.Empty;
        public string AudienceSummary { get; set; } = string.Empty;
        public string AgeGroupsJson { get; set; } = "[]";
        public string SourceUrl { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";
    }

    private sealed record SabcRadioStationProfileSeed(
        string StationName,
        string StationGroup,
        string MarketScope,
        string MarketTier,
        int? MonthlyListenership,
        int? WeeklyListenership,
        string AudienceSummary,
        string PrimaryAudience,
        string[] SecondaryAudiences,
        string LanguageSummary,
        string[] AgeGroups,
        int? LsmMin,
        int? LsmMax,
        string CoverageSummary,
        string[] ProvinceCoverage,
        string[] CityCoverage,
        bool IsFlagshipStation,
        bool IsPremiumStation,
        decimal BrandStrengthScore,
        decimal CoverageScore,
        decimal AudiencePowerScore,
        string SourceUrl);

    private sealed record RegionClusterSeed(string Code, string Name, string Description);

    private sealed record RegionClusterMappingSeed(
        string ClusterCode,
        string? Province,
        string? City,
        string? StationOrChannelName,
        string MetadataJson);
}

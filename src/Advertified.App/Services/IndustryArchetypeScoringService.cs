using Advertified.App.Services.Abstractions;
using Dapper;
using Npgsql;

namespace Advertified.App.Services;

public sealed class IndustryArchetypeScoringService : IIndustryArchetypeScoringService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly object _syncRoot = new();
    private IReadOnlyDictionary<string, IndustryArchetypeScoringProfile>? _profilesByCode;

    public IndustryArchetypeScoringService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public IndustryArchetypeScoringProfile? Resolve(string? industryCode)
    {
        if (string.IsNullOrWhiteSpace(industryCode))
        {
            return null;
        }

        var profiles = GetProfiles();
        profiles.TryGetValue(Normalize(industryCode), out var profile);
        return profile;
    }

    public IReadOnlyCollection<string> GetSupportedIndustryCodes()
    {
        return GetProfiles().Keys.ToArray();
    }

    private IReadOnlyDictionary<string, IndustryArchetypeScoringProfile> GetProfiles()
    {
        if (_profilesByCode is not null)
        {
            return _profilesByCode;
        }

        lock (_syncRoot)
        {
            _profilesByCode ??= LoadProfiles();
            return _profilesByCode;
        }
    }

    private IReadOnlyDictionary<string, IndustryArchetypeScoringProfile> LoadProfiles()
    {
        try
        {
            using var connection = _dataSource.OpenConnection();
            var rows = connection.Query<IndustryArchetypeScoringRow>(
                @"select
                    mi.code as IndustryCode,
                    misp.metadata_tag_match_score as MetadataTagMatchScore,
                    mimfs.media_type as MediaType,
                    mimfs.score as MediaScore,
                    miahs.hint_token as HintToken,
                    miahs.score as HintScore
                  from master_industry_scoring_profiles misp
                  join master_industries mi on mi.id = misp.master_industry_id
                  left join master_industry_media_fit_scores mimfs on mimfs.master_industry_scoring_profile_id = misp.id
                  left join master_industry_audience_hint_scores miahs on miahs.master_industry_scoring_profile_id = misp.id
                  order by mi.code, mimfs.media_type, miahs.hint_token;");

            var profiles = rows
                .GroupBy(row => Normalize(row.IndustryCode))
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var first = group.First();
                        return new IndustryArchetypeScoringProfile
                        {
                            IndustryCode = first.IndustryCode,
                            MetadataTagMatchScore = first.MetadataTagMatchScore,
                            MediaTypeScores = group
                                .Where(row => !string.IsNullOrWhiteSpace(row.MediaType))
                                .GroupBy(row => Normalize(row.MediaType!))
                                .ToDictionary(
                                    mediaGroup => mediaGroup.Key,
                                    mediaGroup => mediaGroup.First().MediaScore,
                                    StringComparer.OrdinalIgnoreCase),
                            AudienceHintScores = group
                                .Where(row => !string.IsNullOrWhiteSpace(row.HintToken))
                                .GroupBy(row => Normalize(row.HintToken!))
                                .ToDictionary(
                                    hintGroup => hintGroup.Key,
                                    hintGroup => hintGroup.First().HintScore,
                                    StringComparer.OrdinalIgnoreCase)
                        };
                    },
                    StringComparer.OrdinalIgnoreCase);

            if (profiles.Count == 0)
            {
                throw new InvalidOperationException("Industry archetype scoring profiles are missing.");
            }

            return profiles;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Industry archetype scoring data could not be loaded. Ensure master industry scoring tables are initialized before startup.",
                ex);
        }
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private sealed class IndustryArchetypeScoringRow
    {
        public string IndustryCode { get; init; } = string.Empty;
        public decimal MetadataTagMatchScore { get; init; }
        public string? MediaType { get; init; }
        public decimal MediaScore { get; init; }
        public string? HintToken { get; init; }
        public decimal HintScore { get; init; }
    }
}

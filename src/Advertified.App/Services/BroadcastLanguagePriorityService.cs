using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Advertified.App.Services;

public sealed class BroadcastLanguagePriorityService : IBroadcastLanguagePriorityService
{
    private const string CacheKey = "broadcast_language_market_priority_v1";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly NpgsqlDataSource _dataSource;
    private readonly IBroadcastMasterDataService _broadcastMasterDataService;
    private readonly IMemoryCache _memoryCache;

    public BroadcastLanguagePriorityService(
        NpgsqlDataSource dataSource,
        IBroadcastMasterDataService broadcastMasterDataService,
        IMemoryCache memoryCache)
    {
        _dataSource = dataSource;
        _broadcastMasterDataService = broadcastMasterDataService;
        _memoryCache = memoryCache;
    }

    public async Task<IReadOnlyList<string>> OrderRequestedLanguagesAsync(IEnumerable<string> languages, CancellationToken cancellationToken)
    {
        var selected = BroadcastLanguageSupport.NormalizeRequestedLanguages(languages, _broadcastMasterDataService.NormalizeLanguageCode);
        if (selected.Count == 0)
        {
            return Array.Empty<string>();
        }

        var ranking = await GetRankingAsync(cancellationToken);
        return selected
            .OrderBy(language => ranking.TryGetValue(language, out var rank) ? rank : int.MaxValue)
            .ThenBy(language => language, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private Task<IReadOnlyDictionary<string, int>> GetRankingAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, int>? cached) && cached is not null)
        {
            return Task.FromResult(cached);
        }

        return LoadAndCacheAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, int>> LoadAndCacheAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<LanguagePriorityRow>(new CommandDefinition(
            @"
            select language_code as LanguageCode, market_rank as MarketRank
            from broadcast_language_market_priority
            where is_active = true
            order by market_rank, language_code;
            ",
            cancellationToken: cancellationToken))).ToList();

        var snapshot = rows.Count == 0
            ? BuildFallbackRanking()
            : rows.ToDictionary(
                row => _broadcastMasterDataService.NormalizeLanguageCode(row.LanguageCode),
                row => row.MarketRank,
                StringComparer.OrdinalIgnoreCase);

        _memoryCache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    private Dictionary<string, int> BuildFallbackRanking()
    {
        var languages = new[]
        {
            "english",
            "isizulu",
            "afrikaans",
            "isixhosa",
            "setswana",
            "sesotho",
            "sepedi",
            "xitsonga",
            "tshivenda",
            "siswati",
            "isindebele"
        };

        return languages
            .Select((language, index) => new { language, rank = index + 1 })
            .ToDictionary(
                item => _broadcastMasterDataService.NormalizeLanguageCode(item.language),
                item => item.rank,
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed class LanguagePriorityRow
    {
        public string LanguageCode { get; set; } = string.Empty;
        public int MarketRank { get; set; }
    }
}

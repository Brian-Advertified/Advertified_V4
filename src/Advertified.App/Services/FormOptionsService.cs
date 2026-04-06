using Advertified.App.Contracts.Public;
using Advertified.App.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Advertified.App.Support;

namespace Advertified.App.Services;

public sealed class FormOptionsService
{
    private const string CacheKey = "public-form-options:v1";
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public FormOptionsService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<PublicFormOptionsResponse> GetPublicOptionsAsync(CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(
                CacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

                    var items = await _db.FormOptionItems
                        .AsNoTracking()
                        .Where(x => x.IsActive)
                        .OrderBy(x => x.OptionSetKey)
                        .ThenBy(x => x.SortOrder)
                        .ThenBy(x => x.Label)
                        .Select(x => new FormOptionRow(x.OptionSetKey, x.Value, x.Label))
                        .ToListAsync(cancellationToken);

                    return BuildResponse(items);
                })
            ?? new PublicFormOptionsResponse();
    }

    public async Task<bool> IsAllowedValueAsync(string optionSetKey, string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var options = await GetPublicOptionsAsync(cancellationToken);
        var trimmedValue = value.Trim();
        return GetOptions(options, optionSetKey)
            .Any(option => option.Value.Equals(trimmedValue, StringComparison.OrdinalIgnoreCase));
    }

    private static PublicFormOptionsResponse BuildResponse(IReadOnlyList<FormOptionRow> items)
    {
        var grouped = items
            .GroupBy(x => x.OptionSetKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FormOptionResponse>)group
                    .Select(item => new FormOptionResponse
                    {
                        Value = item.Value,
                        Label = item.Label
                    })
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new PublicFormOptionsResponse
        {
            BusinessTypes = GetOptions(grouped, FormOptionSetKeys.BusinessTypes),
            Industries = GetOptions(grouped, FormOptionSetKeys.Industries),
            Provinces = GetOptions(grouped, FormOptionSetKeys.Provinces),
            RevenueBands = GetOptions(grouped, FormOptionSetKeys.RevenueBands),
            BusinessStages = GetOptions(grouped, FormOptionSetKeys.BusinessStages),
            MonthlyRevenueBands = GetOptions(grouped, FormOptionSetKeys.MonthlyRevenueBands),
            SalesModels = GetOptions(grouped, FormOptionSetKeys.SalesModels),
            CustomerTypes = GetOptions(grouped, FormOptionSetKeys.CustomerTypes),
            BuyingBehaviours = GetOptions(grouped, FormOptionSetKeys.BuyingBehaviours),
            DecisionCycles = GetOptions(grouped, FormOptionSetKeys.DecisionCycles),
            GrowthTargets = GetOptions(grouped, FormOptionSetKeys.GrowthTargets),
            PricePositioning = GetOptions(grouped, FormOptionSetKeys.PricePositioning),
            AverageCustomerSpendBands = GetOptions(grouped, FormOptionSetKeys.AverageCustomerSpendBands),
            UrgencyLevels = GetOptions(grouped, FormOptionSetKeys.UrgencyLevels),
            AudienceClarity = GetOptions(grouped, FormOptionSetKeys.AudienceClarity),
            ValuePropositionFocus = GetOptions(grouped, FormOptionSetKeys.ValuePropositionFocus)
        };
    }

    private static IReadOnlyList<FormOptionResponse> GetOptions(
        IReadOnlyDictionary<string, IReadOnlyList<FormOptionResponse>> grouped,
        string optionSetKey)
    {
        return grouped.TryGetValue(optionSetKey, out var options)
            ? options
            : Array.Empty<FormOptionResponse>();
    }

    private static IReadOnlyList<FormOptionResponse> GetOptions(PublicFormOptionsResponse options, string optionSetKey)
    {
        return optionSetKey switch
        {
            FormOptionSetKeys.BusinessTypes => options.BusinessTypes,
            FormOptionSetKeys.Industries => options.Industries,
            FormOptionSetKeys.Provinces => options.Provinces,
            FormOptionSetKeys.RevenueBands => options.RevenueBands,
            FormOptionSetKeys.BusinessStages => options.BusinessStages,
            FormOptionSetKeys.MonthlyRevenueBands => options.MonthlyRevenueBands,
            FormOptionSetKeys.SalesModels => options.SalesModels,
            FormOptionSetKeys.CustomerTypes => options.CustomerTypes,
            FormOptionSetKeys.BuyingBehaviours => options.BuyingBehaviours,
            FormOptionSetKeys.DecisionCycles => options.DecisionCycles,
            FormOptionSetKeys.GrowthTargets => options.GrowthTargets,
            FormOptionSetKeys.PricePositioning => options.PricePositioning,
            FormOptionSetKeys.AverageCustomerSpendBands => options.AverageCustomerSpendBands,
            FormOptionSetKeys.UrgencyLevels => options.UrgencyLevels,
            FormOptionSetKeys.AudienceClarity => options.AudienceClarity,
            FormOptionSetKeys.ValuePropositionFocus => options.ValuePropositionFocus,
            _ => Array.Empty<FormOptionResponse>()
        };
    }

    private sealed record FormOptionRow(string OptionSetKey, string Value, string Label);
}

using Advertified.App.Contracts.Public;
using Advertified.App.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Advertified.App.Support;
using System.Data;

namespace Advertified.App.Services;

public sealed class FormOptionsService
{
    private const string CacheKey = "public-form-options:v2";
    private const string AllItemsCacheKey = "form-options:all:v2";
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public FormOptionsService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<PublicFormOptionsResponse> GetPublicOptionsAsync(CancellationToken cancellationToken)
    {
        var items = await GetAllActiveRowsAsync(cancellationToken);
        return await _cache.GetOrCreateAsync(
                CacheKey,
                entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                    return Task.FromResult(BuildResponse(items));
                })
            ?? new PublicFormOptionsResponse();
    }

    public async Task<bool> IsAllowedValueAsync(string optionSetKey, string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmedValue = value.Trim();
        var options = await GetOptionsAsync(optionSetKey, cancellationToken);
        return options
            .Any(option => option.Value.Equals(trimmedValue, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<FormOptionResponse>> GetOptionsAsync(string optionSetKey, CancellationToken cancellationToken)
    {
        var items = await GetAllActiveRowsAsync(cancellationToken);
        return items
            .Where(item => item.OptionSetKey.Equals(optionSetKey, StringComparison.OrdinalIgnoreCase))
            .Select(item => new FormOptionResponse
            {
                Value = item.Value,
                Label = item.Label
            })
            .ToArray();
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

    private async Task<IReadOnlyList<FormOptionRow>> GetAllActiveRowsAsync(CancellationToken cancellationToken)
    {
        var items = await _cache.GetOrCreateAsync(
                   AllItemsCacheKey,
                   async entry =>
                   {
                       entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                       var baseItems = await _db.FormOptionItems
                           .AsNoTracking()
                           .Where(x => x.IsActive && x.OptionSetKey != FormOptionSetKeys.Industries)
                           .OrderBy(x => x.OptionSetKey)
                           .ThenBy(x => x.SortOrder)
                           .ThenBy(x => x.Label)
                           .Select(x => new FormOptionRow(x.OptionSetKey, x.Value, x.Label, x.SortOrder))
                           .ToListAsync(cancellationToken);

                       var industryItems = await GetCanonicalIndustryRowsAsync(cancellationToken);
                       return baseItems
                           .Concat(industryItems)
                           .OrderBy(x => x.OptionSetKey, StringComparer.OrdinalIgnoreCase)
                           .ThenBy(x => x.SortOrder)
                           .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                           .ToList();
                   })
               ?? new List<FormOptionRow>();

        return items;
    }

    private async Task<IReadOnlyList<FormOptionRow>> GetCanonicalIndustryRowsAsync(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                select code, label
                from master_industries
                order by label;";

            var rows = new List<FormOptionRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.GetString(0);
                var label = reader.GetString(1);
                rows.Add(new FormOptionRow(
                    FormOptionSetKeys.Industries,
                    label,
                    label,
                    ResolveIndustrySortOrder(code, label)));
            }

            return rows;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static int ResolveIndustrySortOrder(string code, string label)
    {
        return code.Trim().ToLowerInvariant() switch
        {
            LeadCanonicalValues.IndustryCodes.Retail => 10,
            LeadCanonicalValues.IndustryCodes.Finance => 20,
            LeadCanonicalValues.IndustryCodes.FoodHospitality => 30,
            LeadCanonicalValues.IndustryCodes.RealEstate => 40,
            LeadCanonicalValues.IndustryCodes.Automotive => 50,
            LeadCanonicalValues.IndustryCodes.Technology => 60,
            LeadCanonicalValues.IndustryCodes.Healthcare => 70,
            LeadCanonicalValues.IndustryCodes.Education => 80,
            LeadCanonicalValues.IndustryCodes.Travel => 90,
            LeadCanonicalValues.IndustryCodes.HomeServices => 100,
            LeadCanonicalValues.IndustryCodes.Beauty => 110,
            LeadCanonicalValues.IndustryCodes.Fitness => 120,
            LeadCanonicalValues.IndustryCodes.LegalServices => 130,
            LeadCanonicalValues.IndustryCodes.GeneralServices => 140,
            _ => 1000 + Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(label))
        };
    }

    private sealed record FormOptionRow(string OptionSetKey, string Value, string Label, int SortOrder);
}

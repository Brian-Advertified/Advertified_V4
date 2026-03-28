using System.Text.Json;
using Advertified.App.Contracts.Packages;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class PackageCatalogService : IPackageCatalogService
{
    private readonly AppDbContext _db;

    public PackageCatalogService(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyCollection<PackageBandDto> GetPackageBands()
    {
        var items = (from band in _db.PackageBands.AsNoTracking()
                     join profile in _db.PackageBandProfiles.AsNoTracking() on band.Id equals profile.PackageBandId into profileJoin
                     from profile in profileJoin.DefaultIfEmpty()
                     where band.IsActive
                     orderby band.SortOrder
                     select new
                     {
                         band.Id,
                         band.Code,
                         band.Name,
                         band.MinBudget,
                         band.MaxBudget,
                         band.SortOrder,
                         Description = profile != null ? profile.Description : string.Empty,
                         AudienceFit = profile != null ? profile.AudienceFit : string.Empty,
                         QuickBenefit = profile != null ? profile.QuickBenefit : string.Empty,
                         PackagePurpose = profile != null ? profile.PackagePurpose : string.Empty,
                         IncludeRadio = profile != null ? profile.IncludeRadio : "optional",
                         IncludeTv = profile != null ? profile.IncludeTv : "no",
                         LeadTimeLabel = profile != null ? profile.LeadTimeLabel : "7 business days",
                         RecommendedSpend = profile != null ? profile.RecommendedSpend : null,
                         IsRecommended = profile != null && profile.IsRecommended,
                         BenefitsJson = profile != null ? profile.BenefitsJson : "[]"
                     })
            .ToList();

        return items.Select(item => new PackageBandDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                MinBudget = item.MinBudget,
                MaxBudget = item.MaxBudget,
                SortOrder = item.SortOrder,
                Description = item.Description,
                AudienceFit = item.AudienceFit,
                QuickBenefit = item.QuickBenefit,
                PackagePurpose = item.PackagePurpose,
                IncludeRadio = item.IncludeRadio,
                IncludeTv = item.IncludeTv,
                LeadTime = item.LeadTimeLabel,
                RecommendedSpend = item.RecommendedSpend,
                IsRecommended = item.IsRecommended,
                Benefits = Deserialize(item.BenefitsJson)
            })
            .ToArray();
    }

    private static List<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }
}

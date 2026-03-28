using System.Text.Json;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Support;

internal static class PackageCatalogInitializer
{
    internal static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var packageBands = await db.PackageBands
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync();

        var seeds = PackageCatalogDefaults.CreateSeeds(packageBands);
        var now = DateTime.UtcNow;

        foreach (var seed in seeds)
        {
            var existingProfile = await db.PackageBandProfiles.FirstOrDefaultAsync(x => x.PackageBandId == seed.PackageBandId);
            if (existingProfile == null)
            {
                db.PackageBandProfiles.Add(new PackageBandProfile
                {
                    PackageBandId = seed.PackageBandId,
                    Description = seed.Description,
                    AudienceFit = seed.AudienceFit,
                    QuickBenefit = seed.QuickBenefit,
                    PackagePurpose = seed.PackagePurpose,
                    IncludeRadio = seed.IncludeRadio,
                    IncludeTv = seed.IncludeTv,
                    LeadTimeLabel = seed.LeadTimeLabel,
                    RecommendedSpend = seed.RecommendedSpend,
                    IsRecommended = seed.IsRecommended,
                    BenefitsJson = JsonSerializer.Serialize(seed.Benefits),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                existingProfile.Description = seed.Description;
                existingProfile.AudienceFit = seed.AudienceFit;
                existingProfile.QuickBenefit = seed.QuickBenefit;
                existingProfile.PackagePurpose = seed.PackagePurpose;
                existingProfile.IncludeRadio = seed.IncludeRadio;
                existingProfile.IncludeTv = seed.IncludeTv;
                existingProfile.LeadTimeLabel = seed.LeadTimeLabel;
                existingProfile.RecommendedSpend = seed.RecommendedSpend;
                existingProfile.IsRecommended = seed.IsRecommended;
                existingProfile.BenefitsJson = JsonSerializer.Serialize(seed.Benefits);
                existingProfile.UpdatedAt = now;
            }

            foreach (var tierSeed in seed.Tiers)
            {
                var existingTier = await db.PackageBandPreviewTiers
                    .FirstOrDefaultAsync(x => x.PackageBandId == seed.PackageBandId && x.TierCode == tierSeed.TierCode);

                if (existingTier == null)
                {
                    db.PackageBandPreviewTiers.Add(new PackageBandPreviewTier
                    {
                        Id = Guid.NewGuid(),
                        PackageBandId = seed.PackageBandId,
                        TierCode = tierSeed.TierCode,
                        TierLabel = tierSeed.TierLabel,
                        TypicalInclusionsJson = JsonSerializer.Serialize(tierSeed.TypicalInclusions),
                        IndicativeMixJson = JsonSerializer.Serialize(tierSeed.IndicativeMix),
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
                else
                {
                    existingTier.TierLabel = tierSeed.TierLabel;
                    existingTier.TypicalInclusionsJson = JsonSerializer.Serialize(tierSeed.TypicalInclusions);
                    existingTier.IndicativeMixJson = JsonSerializer.Serialize(tierSeed.IndicativeMix);
                    existingTier.UpdatedAt = now;
                }
            }
        }

        await db.SaveChangesAsync();
    }
}

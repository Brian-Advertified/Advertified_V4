using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class PricingSettingsProvider : IPricingSettingsProvider
{
    private readonly AppDbContext _db;

    public PricingSettingsProvider(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PricingSettingsSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var row = await _db.PricingSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PricingKey == "default", cancellationToken);

        return row is null
            ? new PricingSettingsSnapshot(0.10m, 0.05m, 0.10m, 0.10m)
            : new PricingSettingsSnapshot(
                row.AiStudioReservePercent,
                row.OohMarkupPercent,
                row.RadioMarkupPercent,
                row.TvMarkupPercent);
    }
}

using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class SignalCollectorService : ISignalCollectorService
{
    private readonly AppDbContext _db;
    private readonly IWebsiteSignalProvider _websiteSignalProvider;

    public SignalCollectorService(AppDbContext db, IWebsiteSignalProvider websiteSignalProvider)
    {
        _db = db;
        _websiteSignalProvider = websiteSignalProvider;
    }

    public async Task<Signal> CollectAsync(int leadId, CancellationToken cancellationToken)
    {
        var lead = await _db.Leads
            .FirstOrDefaultAsync(x => x.Id == leadId, cancellationToken)
            ?? throw new InvalidOperationException("Lead not found.");

        var websiteSignals = await _websiteSignalProvider.CollectAsync(lead.Website, cancellationToken);

        var signal = new Signal
        {
            LeadId = lead.Id,
            HasPromo = websiteSignals.HasPromo,
            HasMetaAds = websiteSignals.HasMetaAds,
            WebsiteUpdatedRecently = websiteSignals.WebsiteUpdatedRecently,
            CreatedAt = DateTime.UtcNow
        };

        _db.Signals.Add(signal);
        await _db.SaveChangesAsync(cancellationToken);

        return signal;
    }
}

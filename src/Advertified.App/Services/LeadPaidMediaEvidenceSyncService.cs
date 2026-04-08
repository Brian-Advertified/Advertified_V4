using Advertified.App.Configuration;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Advertified.App.Services;

public sealed class LeadPaidMediaEvidenceSyncService : ILeadPaidMediaEvidenceSyncService
{
    private readonly AppDbContext _db;
    private readonly ISignalCollectorService _signalCollectorService;
    private readonly LeadIntelligenceAutomationOptions _options;
    private readonly ILogger<LeadPaidMediaEvidenceSyncService> _logger;

    public LeadPaidMediaEvidenceSyncService(
        AppDbContext db,
        ISignalCollectorService signalCollectorService,
        IOptions<LeadIntelligenceAutomationOptions> options,
        ILogger<LeadPaidMediaEvidenceSyncService> logger)
    {
        _db = db;
        _signalCollectorService = signalCollectorService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> SyncBatchAsync(CancellationToken cancellationToken)
    {
        var leadIds = await _db.Leads
            .AsNoTracking()
            .Where(lead => lead.Website != null && lead.Website != string.Empty)
            .OrderBy(lead => lead.Id)
            .Take(Math.Max(1, _options.BatchSize))
            .Select(lead => lead.Id)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var leadId in leadIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _signalCollectorService.CollectAsync(leadId, cancellationToken);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Paid media evidence sync failed for lead {LeadId}.", leadId);
            }
        }

        return processed;
    }
}

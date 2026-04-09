namespace Advertified.App.Services.Abstractions;

public interface ILeadPaidMediaEvidenceSyncService
{
    Task<LeadPaidMediaSyncRunResult> SyncBatchAsync(CancellationToken cancellationToken);
}

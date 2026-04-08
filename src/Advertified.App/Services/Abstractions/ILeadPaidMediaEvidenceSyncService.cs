namespace Advertified.App.Services.Abstractions;

public interface ILeadPaidMediaEvidenceSyncService
{
    Task<int> SyncBatchAsync(CancellationToken cancellationToken);
}

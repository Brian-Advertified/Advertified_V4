namespace Advertified.App.Services.Abstractions;

public interface IBroadcastInventoryImportService
{
    Task SyncAsync(CancellationToken cancellationToken);
}

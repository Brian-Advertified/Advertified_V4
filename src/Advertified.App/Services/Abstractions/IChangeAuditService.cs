namespace Advertified.App.Services.Abstractions;

public interface IChangeAuditService
{
    Task WriteAsync(
        Guid? actorUserId,
        string scope,
        string action,
        string entityType,
        string entityId,
        string? entityLabel,
        string summary,
        object? metadata,
        CancellationToken cancellationToken);
}

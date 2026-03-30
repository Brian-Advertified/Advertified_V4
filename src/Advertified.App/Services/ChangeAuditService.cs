using System.Text.Json;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class ChangeAuditService : IChangeAuditService
{
    private readonly AppDbContext _db;

    public ChangeAuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        Guid? actorUserId,
        string scope,
        string action,
        string entityType,
        string entityId,
        string? entityLabel,
        string summary,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var normalizedScope = scope.Trim().ToLowerInvariant();
        var normalizedAction = action.Trim().ToLowerInvariant();
        var normalizedEntityType = entityType.Trim().ToLowerInvariant();
        var normalizedEntityId = entityId.Trim();

        if (string.IsNullOrWhiteSpace(normalizedScope) || string.IsNullOrWhiteSpace(normalizedAction) || string.IsNullOrWhiteSpace(normalizedEntityType) || string.IsNullOrWhiteSpace(normalizedEntityId))
        {
            throw new InvalidOperationException("Audit scope, action, entity type, and entity id are required.");
        }

        var actor = actorUserId.HasValue
            ? await _db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId.Value, cancellationToken)
            : null;

        var entry = new ChangeAuditLog
        {
            ActorUserId = actorUserId,
            ActorRole = actor?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            ActorName = actor?.FullName ?? string.Empty,
            ActorEmail = actor?.Email ?? string.Empty,
            Scope = normalizedScope,
            Action = normalizedAction,
            EntityType = normalizedEntityType,
            EntityId = normalizedEntityId,
            EntityLabel = string.IsNullOrWhiteSpace(entityLabel) ? null : entityLabel.Trim(),
            Summary = summary.Trim(),
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata),
            CreatedAt = DateTime.UtcNow
        };

        _db.ChangeAuditLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

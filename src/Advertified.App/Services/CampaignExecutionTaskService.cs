using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Services;

public sealed class CampaignExecutionTaskService : ICampaignExecutionTaskService
{
    private readonly AppDbContext _db;

    public CampaignExecutionTaskService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureApprovalTasksAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var existing = await _db.CampaignExecutionTasks
            .Where(item => item.CampaignId == campaignId)
            .ToDictionaryAsync(item => item.TaskKey, item => item, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var task in BuildDefaultTasks(campaignId, now))
        {
            if (existing.ContainsKey(task.TaskKey))
            {
                continue;
            }

            _db.CampaignExecutionTasks.Add(task);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CampaignExecutionTask>> GetTasksAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return await _db.CampaignExecutionTasks
            .AsNoTracking()
            .Where(item => item.CampaignId == campaignId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public async Task MarkTaskCompletedAsync(Guid campaignId, string taskKey, CancellationToken cancellationToken)
    {
        await UpdateTaskStatusAsync(campaignId, taskKey, "completed", cancellationToken);
    }

    public async Task MarkTaskOpenAsync(Guid campaignId, string taskKey, CancellationToken cancellationToken)
    {
        await UpdateTaskStatusAsync(campaignId, taskKey, "open", cancellationToken);
    }

    private static IEnumerable<CampaignExecutionTask> BuildDefaultTasks(Guid campaignId, DateTime now)
    {
        return new[]
        {
            CreateTask(campaignId, "booking_confirmation", "Confirm supplier bookings", "Lock final supplier slots and committed dates for each selected channel.", 10, now),
            CreateTask(campaignId, "creative_handoff", "Finalize creative handoff", "Confirm all approved creative assets are attached to supplier bookings.", 20, now),
            CreateTask(campaignId, "tracking_links", "Configure tracking links", "Set channel tracking links and campaign attribution tags before go-live.", 30, now),
            CreateTask(campaignId, "first_report_snapshot", "Capture first performance snapshot", "Record first live delivery metrics within 24 hours of launch.", 40, now)
        };
    }

    private static CampaignExecutionTask CreateTask(
        Guid campaignId,
        string taskKey,
        string title,
        string details,
        int sortOrder,
        DateTime now)
    {
        return new CampaignExecutionTask
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            TaskKey = taskKey,
            Title = title,
            Details = details,
            Status = "open",
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private async Task UpdateTaskStatusAsync(
        Guid campaignId,
        string taskKey,
        string status,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
        {
            return;
        }

        await EnsureApprovalTasksAsync(campaignId, cancellationToken);

        var normalizedKey = taskKey.Trim();
        var row = await _db.CampaignExecutionTasks
            .FirstOrDefaultAsync(
                item => item.CampaignId == campaignId
                    && item.TaskKey == normalizedKey,
                cancellationToken);

        if (row is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        row.Status = status;
        row.CompletedAt = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
            ? now
            : null;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

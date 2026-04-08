using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ICampaignExecutionTaskService
{
    Task EnsureApprovalTasksAsync(Guid campaignId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CampaignExecutionTask>> GetTasksAsync(Guid campaignId, CancellationToken cancellationToken);
    Task MarkTaskCompletedAsync(Guid campaignId, string taskKey, CancellationToken cancellationToken);
    Task MarkTaskOpenAsync(Guid campaignId, string taskKey, CancellationToken cancellationToken);
}

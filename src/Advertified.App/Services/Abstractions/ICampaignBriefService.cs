using Advertified.App.Contracts.Campaigns;
using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ICampaignBriefService
{
    Task SaveDraftAsync(Guid userId, Guid campaignId, SaveCampaignBriefRequest request, CancellationToken cancellationToken);
    Task SaveAgentManagedAsync(Campaign campaign, SaveCampaignBriefRequest request, string planningMode, string? campaignName, bool submitBrief, CancellationToken cancellationToken);
    Task SaveProspectSubmissionAsync(Campaign campaign, SaveCampaignBriefRequest request, DateTime submittedAt, CancellationToken cancellationToken);
    Task SubmitAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken);
    Task SetPlanningModeAsync(Guid userId, Guid campaignId, string planningMode, CancellationToken cancellationToken);
}

namespace Advertified.App.Services.Abstractions;

public interface ICampaignAccessService
{
    Task EnsureCanCreateOrderAsync(Guid userId, CancellationToken cancellationToken);
    Task EnsureCanEditBriefAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken);
    Task EnsureCanGeneratePlanAsync(Guid userId, Guid campaignId, CancellationToken cancellationToken);
}

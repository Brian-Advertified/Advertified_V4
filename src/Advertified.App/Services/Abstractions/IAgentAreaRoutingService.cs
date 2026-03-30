namespace Advertified.App.Services.Abstractions;

public interface IAgentAreaRoutingService
{
    Task TryAssignCampaignAsync(Guid campaignId, string trigger, CancellationToken cancellationToken);
}

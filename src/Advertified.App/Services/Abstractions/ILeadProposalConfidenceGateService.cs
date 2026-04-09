namespace Advertified.App.Services.Abstractions;

public interface ILeadProposalConfidenceGateService
{
    Task EnsureCampaignReadyAsync(Guid campaignId, CancellationToken cancellationToken);
}

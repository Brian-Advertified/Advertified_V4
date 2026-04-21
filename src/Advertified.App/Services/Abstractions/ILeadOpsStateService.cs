namespace Advertified.App.Services.Abstractions;

public interface ILeadOpsStateService
{
    Task RefreshLeadAsync(int leadId, CancellationToken cancellationToken);

    Task RefreshProspectAsync(Guid prospectLeadId, CancellationToken cancellationToken);

    Task RefreshCampaignAsync(Guid campaignId, CancellationToken cancellationToken);
}

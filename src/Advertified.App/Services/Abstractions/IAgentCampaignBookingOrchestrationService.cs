using Advertified.App.Contracts.Agent;

namespace Advertified.App.Services.Abstractions;

public interface IAgentCampaignBookingOrchestrationService
{
    Task<AgentCampaignActionResult> SaveSupplierBookingAsync(Guid id, SaveCampaignSupplierBookingRequest request, CancellationToken cancellationToken);

    Task<AgentCampaignActionResult> SaveDeliveryReportAsync(Guid id, SaveCampaignDeliveryReportRequest request, CancellationToken cancellationToken);
}

using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Contracts.Public;

namespace Advertified.App.Services.Abstractions;

public interface IAgentCampaignWorkflowOrchestrationService
{
    Task<AgentCampaignActionResult> AssignAsync(Guid id, AssignCampaignRequest request, CancellationToken cancellationToken);

    Task<AgentCampaignActionResult> UnassignAsync(Guid id, CancellationToken cancellationToken);

    Task<AgentCampaignActionResult> ConvertProspectToSaleAsync(Guid id, ConvertProspectToSaleRequest request, CancellationToken cancellationToken);

    Task<AgentCampaignActionResult> MarkLaunchedAsync(Guid id, CancellationToken cancellationToken);

    Task<AgentCampaignActionResult> SendToClientAsync(Guid id, SendToClientRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<FormOptionResponse>> GetProspectDispositionReasonsAsync(CancellationToken cancellationToken);

    Task<AgentCampaignActionResult> RequestRecommendationChangesAsync(Guid id, RequestRecommendationChangesRequest request, CancellationToken cancellationToken);

    Task<AgentCampaignActionResult> CloseProspectAsync(Guid id, CloseProspectCampaignRequest request, CancellationToken cancellationToken);

    Task<AgentCampaignActionResult> ReopenProspectAsync(Guid id, CancellationToken cancellationToken);

    Task<AgentCampaignActionResult> ResendProposalEmailAsync(Guid id, string toEmail, string? message, CancellationToken cancellationToken);
}

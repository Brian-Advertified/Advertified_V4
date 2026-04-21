using Advertified.App.Contracts.Agent;
using Advertified.App.Contracts.Campaigns;
using Advertified.App.Contracts.Public;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("agent/campaigns")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentCampaignWorkflowController : ControllerBase
{
    private readonly IAgentCampaignWorkflowOrchestrationService _workflowService;

    public AgentCampaignWorkflowController(IAgentCampaignWorkflowOrchestrationService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignCampaignRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.AssignAsync(id, request, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    [HttpPost("{id:guid}/unassign")]
    public async Task<IActionResult> Unassign(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workflowService.UnassignAsync(id, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    [HttpPost("{id:guid}/convert-to-sale")]
    public async Task<IActionResult> ConvertProspectToSale(Guid id, [FromBody] ConvertProspectToSaleRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.ConvertProspectToSaleAsync(id, request, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    [HttpPost("{id:guid}/mark-launched")]
    public async Task<IActionResult> MarkLaunched(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workflowService.MarkLaunchedAsync(id, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    [HttpPost("{id:guid}/send-to-client")]
    public async Task<IActionResult> SendToClient(Guid id, [FromBody] SendToClientRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.SendToClientAsync(id, request, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    [HttpGet("prospect-disposition-reasons")]
    public async Task<ActionResult<IReadOnlyList<FormOptionResponse>>> GetProspectDispositionReasons(CancellationToken cancellationToken)
    {
        var options = await _workflowService.GetProspectDispositionReasonsAsync(cancellationToken);
        return Ok(options);
    }

    [HttpPost("{id:guid}/request-recommendation-changes")]
    public async Task<IActionResult> RequestRecommendationChanges(Guid id, [FromBody] RequestRecommendationChangesRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.RequestRecommendationChangesAsync(id, request, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    [HttpPost("{id:guid}/close-prospect")]
    public async Task<IActionResult> CloseProspect(Guid id, [FromBody] CloseProspectCampaignRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.CloseProspectAsync(id, request, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    [HttpPost("{id:guid}/reopen-prospect")]
    public async Task<IActionResult> ReopenProspect(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workflowService.ReopenProspectAsync(id, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    [HttpPost("{id:guid}/resend-proposal-email")]
    public async Task<IActionResult> ResendProposalEmail(Guid id, [FromBody] ResendProposalEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await _workflowService.ResendProposalEmailAsync(id, request.ToEmail, request.Message, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    public sealed class ResendProposalEmailRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string? Message { get; set; }
    }
}

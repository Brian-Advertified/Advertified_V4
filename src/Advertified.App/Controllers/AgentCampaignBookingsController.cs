using Advertified.App.Contracts.Agent;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("agent/campaigns")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentCampaignBookingsController : ControllerBase
{
    private readonly IAgentCampaignBookingOrchestrationService _bookingService;

    public AgentCampaignBookingsController(IAgentCampaignBookingOrchestrationService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost("{id:guid}/supplier-bookings")]
    public async Task<IActionResult> SaveSupplierBooking(Guid id, [FromBody] SaveCampaignSupplierBookingRequest request, CancellationToken cancellationToken)
    {
        var result = await _bookingService.SaveSupplierBookingAsync(id, request, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }

    [HttpPost("{id:guid}/delivery-reports")]
    public async Task<IActionResult> SaveDeliveryReport(Guid id, [FromBody] SaveCampaignDeliveryReportRequest request, CancellationToken cancellationToken)
    {
        var result = await _bookingService.SaveDeliveryReportAsync(id, request, cancellationToken);
        return StatusCode(result.StatusCode, result.Payload);
    }
}

using Advertified.App.Contracts.Public;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Advertified.App.Controllers;

[ApiController]
[Route("partner-enquiry")]
[AllowAnonymous]
[EnableRateLimiting("public_general")]
public sealed class PartnerEnquiryController : ControllerBase
{
    private readonly ITemplatedEmailService _emailService;

    public PartnerEnquiryController(ITemplatedEmailService emailService)
    {
        _emailService = emailService;
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] PartnerEnquiryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.CompanyName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.PartnerType) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return Problem(
                title: "Please complete all required fields before sending your enquiry.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await _emailService.SendAsync(
            "partner-enquiry-notification",
            "ad@advertified.com",
            "support",
            new Dictionary<string, string?>
            {
                ["FullName"] = request.FullName.Trim(),
                ["CompanyName"] = request.CompanyName.Trim(),
                ["Email"] = request.Email.Trim(),
                ["Phone"] = string.IsNullOrWhiteSpace(request.Phone) ? "Not provided" : request.Phone.Trim(),
                ["PartnerType"] = request.PartnerType.Trim(),
                ["InventorySummary"] = string.IsNullOrWhiteSpace(request.InventorySummary) ? "Not provided" : request.InventorySummary.Trim(),
                ["Message"] = request.Message.Trim(),
            },
            attachments: null,
            cancellationToken);

        return Ok(new
        {
            message = "Partner enquiry sent successfully."
        });
    }
}

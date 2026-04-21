using Advertified.App.Contracts.Leads;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("integrations/google-sheets/leads")]
[Authorize(Roles = "Agent,Admin")]
public sealed class GoogleSheetsLeadIntegrationController : ControllerBase
{
    private readonly IGoogleSheetsLeadIntegrationService _service;

    public GoogleSheetsLeadIntegrationController(IGoogleSheetsLeadIntegrationService service)
    {
        _service = service;
    }

    [HttpGet("status")]
    public ActionResult<GoogleSheetsLeadIntegrationStatusDto> GetStatus()
    {
        return Ok(_service.GetStatus());
    }

    [HttpPost("import-now")]
    public async Task<ActionResult<GoogleSheetsLeadIntegrationRunDto>> ImportNow(CancellationToken cancellationToken)
    {
        return Ok(await _service.ImportConfiguredSourcesAsync(cancellationToken));
    }

    [HttpPost("export-now")]
    public async Task<ActionResult<GoogleSheetsLeadIntegrationRunDto>> ExportNow(CancellationToken cancellationToken)
    {
        return Ok(await _service.ExportLeadOpsSnapshotAsync(cancellationToken));
    }
}

using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/dashboard")]
public sealed class AdminDashboardController : BaseAdminController
{
    private readonly IAdminDashboardService _adminDashboardService;

    public AdminDashboardController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IAdminDashboardService adminDashboardService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _adminDashboardService = adminDashboardService;
    }

    [HttpGet("")]
    public async Task<ActionResult<AdminDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        return Ok(await _adminDashboardService.GetDashboardAsync(cancellationToken));
    }
}

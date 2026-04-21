using Advertified.App.Contracts.Agent;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("agent/lead-ops")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentLeadOpsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILeadOpsCoverageService _leadOpsCoverageService;
    private readonly ILeadOpsInboxService _leadOpsInboxService;

    public AgentLeadOpsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        ILeadOpsCoverageService leadOpsCoverageService,
        ILeadOpsInboxService leadOpsInboxService)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _leadOpsCoverageService = leadOpsCoverageService;
        _leadOpsInboxService = leadOpsInboxService;
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<LeadOpsInboxResponse>> GetInbox(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        return Ok(await _leadOpsInboxService.BuildAsync(currentUser, cancellationToken));
    }

    [HttpGet("coverage")]
    public async Task<ActionResult<LeadOpsCoverageResponse>> GetCoverage(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        return Ok(await _leadOpsCoverageService.BuildAsync(currentUser, cancellationToken));
    }

    private async Task<UserAccount> GetCurrentOperationsUserAsync(CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var currentUser = await _db.UserAccounts.FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken);
        if (currentUser is null)
        {
            throw new InvalidOperationException("Authenticated user account could not be found.");
        }

        if (currentUser.Role is not UserRole.Agent and not UserRole.Admin and not UserRole.CreativeDirector)
        {
            throw new ForbiddenException("Agent, creative director, or admin access is required.");
        }

        return currentUser;
    }
}

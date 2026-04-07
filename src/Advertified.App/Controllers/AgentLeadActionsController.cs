using Advertified.App.Contracts.Leads;
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
[Route("agent/lead-actions")]
[Authorize(Roles = "Agent,Admin")]
public sealed class AgentLeadActionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AgentLeadActionsController(AppDbContext db, ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("inbox")]
    public async Task<ActionResult<LeadActionInboxDto>> GetInbox(CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentOperationsUserAsync(cancellationToken);
        var currentUserId = currentUser.Id;

        var items = await _db.LeadActions
            .AsNoTracking()
            .Where(x => x.Status == "open")
            .OrderBy(x => x.AssignedAgentUserId == currentUserId ? 0 : x.AssignedAgentUserId == null ? 1 : 2)
            .ThenByDescending(x => x.Priority == "high")
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new LeadActionInboxItemDto
            {
                ActionId = x.Id,
                LeadId = x.LeadId,
                LeadName = x.Lead.Name,
                LeadLocation = x.Lead.Location,
                LeadCategory = x.Lead.Category,
                LeadSource = x.Lead.Source,
                Action = new LeadActionDto
                {
                    Id = x.Id,
                    LeadId = x.LeadId,
                    LeadInsightId = x.LeadInsightId,
                    ActionType = x.ActionType,
                    Title = x.Title,
                    Description = x.Description,
                    Status = x.Status,
                    Priority = x.Priority,
                    AssignedAgentUserId = x.AssignedAgentUserId,
                    AssignedAgentName = x.AssignedAgentUser != null ? x.AssignedAgentUser.FullName : null,
                    AssignedAt = x.AssignedAt,
                    IsAssignedToCurrentUser = x.AssignedAgentUserId == currentUserId,
                    IsUnassigned = x.AssignedAgentUserId == null,
                    CreatedAt = x.CreatedAt,
                    CompletedAt = x.CompletedAt,
                }
            })
            .Take(50)
            .ToListAsync(cancellationToken);

        return Ok(new LeadActionInboxDto
        {
            TotalOpenActions = items.Count,
            AssignedToMeCount = items.Count(x => x.Action.IsAssignedToCurrentUser),
            UnassignedCount = items.Count(x => x.Action.IsUnassigned),
            HighPriorityCount = items.Count(x => string.Equals(x.Action.Priority, "high", StringComparison.OrdinalIgnoreCase)),
            Items = items,
        });
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

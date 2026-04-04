using Advertified.App.Contracts.Admin;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("admin/users")]
public sealed class AdminUsersController : BaseAdminController
{
    private readonly IPasswordHashingService _passwordHashingService;

    public AdminUsersController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IChangeAuditService changeAuditService,
        IPasswordHashingService passwordHashingService)
        : base(db, currentUserAccessor, changeAuditService)
    {
        _passwordHashingService = passwordHashingService;
    }

    [HttpGet("")]
    public async Task<ActionResult<IReadOnlyCollection<AdminUserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var users = await Db.UserAccounts
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .ToArrayAsync(cancellationToken);

        var assignments = await Db.AgentAreaAssignments.AsNoTracking().ToArrayAsync(cancellationToken);
        var areaLabelsByCode = await GetAreaLabelsByCodeAsync(cancellationToken);

        return Ok(users.Select(user => MapAdminUser(user, assignments, areaLabelsByCode)).ToArray());
    }

    [HttpPost("")]
    public async Task<ActionResult<AdminUserResponse>> CreateUser([FromBody] CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            await using var transaction = await Db.Database.BeginTransactionAsync(cancellationToken);
            var user = await BuildUserAsync(request.FullName, request.Email, request.Phone, request.Password, request.Role, request.AccountStatus, request.IsSaCitizen, request.EmailVerified, request.PhoneVerified, cancellationToken);
            Db.UserAccounts.Add(user);
            await Db.SaveChangesAsync(cancellationToken);
            await SyncAgentAreaAssignmentsAsync(user.Id, user.Role, request.AssignedAreaCodes, cancellationToken);
            await WriteChangeAuditAsync("create", "user_account", user.Id.ToString(), user.FullName, $"Created user account {user.FullName}.", new { user.Email, request.Role, request.AccountStatus, request.AssignedAreaCodes }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(await BuildAdminUserResponseAsync(user, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminUserResponse>> UpdateUser(Guid id, [FromBody] UpdateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        try
        {
            var actorUserId = await CurrentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
            await using var transaction = await Db.Database.BeginTransactionAsync(cancellationToken);
            var user = await Db.UserAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (user is null)
            {
                return NotFound();
            }

            var normalizedEmail = NormalizeEmail(request.Email);
            var duplicateExists = await Db.UserAccounts.AnyAsync(x => x.Id != id && x.Email == normalizedEmail, cancellationToken);
            if (duplicateExists)
            {
                throw new InvalidOperationException("A user with this email address already exists.");
            }

            user.FullName = RequireValue(request.FullName, "Full name");
            user.Email = normalizedEmail;
            user.Phone = RequireValue(request.Phone, "Phone");
            user.Role = ParseUserRole(request.Role);
            user.AccountStatus = ParseAccountStatus(request.AccountStatus);
            user.IsSaCitizen = request.IsSaCitizen;
            user.EmailVerified = request.EmailVerified;
            user.PhoneVerified = request.PhoneVerified;
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.PasswordHash = _passwordHashingService.HashPassword(user, request.Password);
            }

            await Db.SaveChangesAsync(cancellationToken);
            await SyncAgentAreaAssignmentsAsync(user.Id, user.Role, request.AssignedAreaCodes, cancellationToken);
            await WriteChangeAuditAsync(actorUserId, "update", "user_account", user.Id.ToString(), user.FullName, $"Updated user account {user.FullName}.", new { user.Email, request.Role, request.AccountStatus, request.AssignedAreaCodes }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(await BuildAdminUserResponseAsync(user, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var gateResult = await EnsureAdminAsync(cancellationToken);
        if (gateResult is not null)
        {
            return gateResult;
        }

        var user = await Db.UserAccounts
            .Include(x => x.Campaigns)
            .Include(x => x.PackageOrders)
            .Include(x => x.BusinessProfile)
            .Include(x => x.IdentityProfile)
            .Include(x => x.EmailVerificationTokens)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var hasLinkedRecommendations = await Db.CampaignRecommendations.AnyAsync(x => x.CreatedByUserId == id, cancellationToken);
        if (user.Campaigns.Count > 0 || user.PackageOrders.Count > 0 || hasLinkedRecommendations)
        {
            return BadRequest(new { message = "This user already has linked campaigns, recommendations, or orders and cannot be deleted." });
        }

        Db.EmailVerificationTokens.RemoveRange(user.EmailVerificationTokens);
        if (user.BusinessProfile is not null)
        {
            Db.BusinessProfiles.Remove(user.BusinessProfile);
        }

        if (user.IdentityProfile is not null)
        {
            Db.IdentityProfiles.Remove(user.IdentityProfile);
        }

        Db.UserAccounts.Remove(user);
        await Db.SaveChangesAsync(cancellationToken);
        await WriteChangeAuditAsync("delete", "user_account", user.Id.ToString(), user.FullName, $"Deleted user account {user.FullName}.", new { user.Email }, cancellationToken);
        return NoContent();
    }

    private async Task<UserAccount> BuildUserAsync(
        string fullName,
        string email,
        string phone,
        string password,
        string role,
        string accountStatus,
        bool isSaCitizen,
        bool emailVerified,
        bool phoneVerified,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var duplicateExists = await Db.UserAccounts.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (duplicateExists)
        {
            throw new InvalidOperationException("A user with this email address already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            FullName = RequireValue(fullName, "Full name"),
            Email = normalizedEmail,
            Phone = RequireValue(phone, "Phone"),
            Role = ParseUserRole(role),
            AccountStatus = ParseAccountStatus(accountStatus),
            IsSaCitizen = isSaCitizen,
            EmailVerified = emailVerified,
            PhoneVerified = phoneVerified,
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = _passwordHashingService.HashPassword(user, password);
        return user;
    }

    private async Task<AdminUserResponse> BuildAdminUserResponseAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var assignments = await Db.AgentAreaAssignments
            .AsNoTracking()
            .Where(x => x.AgentUserId == user.Id)
            .ToArrayAsync(cancellationToken);
        var areaLabelsByCode = await GetAreaLabelsByCodeAsync(cancellationToken);
        return MapAdminUser(user, assignments, areaLabelsByCode);
    }
}

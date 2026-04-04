using Advertified.App.Contracts.Users;
using Advertified.App.Data;
using Advertified.App.Services.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("me")]
public sealed class MeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public MeController(AppDbContext db, ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<MeResponse>> Get(CancellationToken cancellationToken)
    {
        var userId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var user = await _db.UserAccounts
            .AsNoTracking()
            .Include(x => x.BusinessProfile)
            .Include(x => x.IdentityProfile)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User account not found.");

        return Ok(new MeResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = ToSnakeCase(user.Role.ToString()),
            AccountStatus = ToSnakeCase(user.AccountStatus.ToString()),
            EmailVerified = user.EmailVerified,
            RequiresPasswordSetup = user.Role == Data.Enums.UserRole.Client && user.BusinessProfile is null && user.IdentityProfile is null,
            IdentityComplete = user.IdentityProfile is not null,
            PhoneVerified = user.PhoneVerified,
            BusinessName = user.BusinessProfile?.BusinessName,
            RegistrationNumber = user.BusinessProfile?.RegistrationNumber,
            City = user.BusinessProfile?.City,
            Province = user.BusinessProfile?.Province
        });
    }

    private static string ToSnakeCase(string value)
    {
        return Regex.Replace(value, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
    }
}

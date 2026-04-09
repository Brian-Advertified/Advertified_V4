using Advertified.App.Contracts.Consent;
using Advertified.App.Data;
using Advertified.App.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Advertified.App.Controllers;

[ApiController]
[Route("consent")]
[AllowAnonymous]
[EnableRateLimiting("public_general")]
public sealed class ConsentController : ControllerBase
{
    private readonly AppDbContext _db;

    public ConsentController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<ConsentPreferenceResponse>> GetPreferences([FromQuery] string browserId, CancellationToken cancellationToken)
    {
        var normalizedBrowserId = browserId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBrowserId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Browser ID is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var currentUserId = TryGetCurrentUserId();
        var preference = await _db.ConsentPreferences
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(
                x => x.BrowserId == normalizedBrowserId || (currentUserId.HasValue && x.UserId == currentUserId.Value),
                cancellationToken);

        if (preference is null)
        {
            return Ok(new ConsentPreferenceResponse
            {
                BrowserId = normalizedBrowserId,
                NecessaryCookies = true,
                AnalyticsCookies = false,
                MarketingCookies = false,
                PrivacyAccepted = false,
                HasSavedPreferences = false
            });
        }

        return Ok(MapResponse(preference, true));
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<ConsentPreferenceResponse>> UpsertPreferences([FromBody] UpsertConsentPreferenceRequest request, CancellationToken cancellationToken)
    {
        var normalizedBrowserId = request.BrowserId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBrowserId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Browser ID is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var currentUserId = TryGetCurrentUserId();
        var preference = await _db.ConsentPreferences
            .FirstOrDefaultAsync(x => x.BrowserId == normalizedBrowserId, cancellationToken);

        var now = DateTime.UtcNow;
        if (preference is null)
        {
            preference = new ConsentPreference
            {
                Id = Guid.NewGuid(),
                BrowserId = normalizedBrowserId,
                CreatedAt = now
            };
            _db.ConsentPreferences.Add(preference);
        }

        preference.UserId = currentUserId ?? preference.UserId;
        preference.NecessaryCookies = true;
        preference.AnalyticsCookies = request.AnalyticsCookies;
        preference.MarketingCookies = request.MarketingCookies;
        preference.PrivacyAccepted = request.PrivacyAccepted;
        preference.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(MapResponse(preference, true));
    }

    private Guid? TryGetCurrentUserId()
    {
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim))
        {
            return null;
        }

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private static ConsentPreferenceResponse MapResponse(ConsentPreference preference, bool hasSavedPreferences)
    {
        return new ConsentPreferenceResponse
        {
            BrowserId = preference.BrowserId,
            NecessaryCookies = preference.NecessaryCookies,
            AnalyticsCookies = preference.AnalyticsCookies,
            MarketingCookies = preference.MarketingCookies,
            PrivacyAccepted = preference.PrivacyAccepted,
            HasSavedPreferences = hasSavedPreferences
        };
    }
}

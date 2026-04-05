using Advertified.App.Data;
using Advertified.App.Data.Enums;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Advertified.App.Controllers;

[ApiController]
[Route("campaign-assets")]
[Authorize]
public sealed class CampaignAssetsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IPublicAssetStorage _assetStorage;

    public CampaignAssetsController(
        AppDbContext db,
        ICurrentUserAccessor currentUserAccessor,
        IPublicAssetStorage assetStorage)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _assetStorage = assetStorage;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var currentUserId = await _currentUserAccessor.GetCurrentUserIdAsync(cancellationToken);
        var asset = await _db.CampaignAssets
            .AsNoTracking()
            .Include(x => x.Campaign)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (asset is null)
        {
            return NotFound();
        }

        var user = await _db.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentUserId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var canAccess = user.Role switch
        {
            UserRole.Admin => true,
            UserRole.CreativeDirector => true,
            UserRole.Agent => asset.Campaign.AssignedAgentUserId == currentUserId || asset.Campaign.AssignedAgentUserId is null,
            _ => asset.Campaign.UserId == currentUserId
        };

        if (!canAccess)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var bytes = await _assetStorage.GetBytesAsync(asset.StorageObjectKey, cancellationToken);
        return File(bytes, asset.ContentType ?? "application/octet-stream", asset.DisplayName);
    }
}

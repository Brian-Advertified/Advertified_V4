using Advertified.App.Contracts.Public;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Advertified.App.Controllers;

[ApiController]
[Route("public/locations")]
[AllowAnonymous]
[EnableRateLimiting("public_general")]
public sealed class PublicLocationsController : ControllerBase
{
    private readonly IPublicLocationSearchService _locationSearchService;

    public PublicLocationsController(IPublicLocationSearchService locationSearchService)
    {
        _locationSearchService = locationSearchService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PublicLocationSuggestionResponse>>> Search(
        [FromQuery] string query,
        [FromQuery] string? geographyScope,
        [FromQuery] string? city,
        [FromQuery] int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var results = await _locationSearchService.SearchAsync(query, geographyScope, city, limit, cancellationToken);
        return Ok(results);
    }
}

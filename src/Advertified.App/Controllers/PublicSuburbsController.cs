using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Controllers;

[ApiController]
[Route("public/suburbs")]
[AllowAnonymous]
[EnableRateLimiting("public_general")]
public sealed class PublicSuburbsController : ControllerBase
{
    private readonly IPublicLocationSearchService _locationSearchService;

    public PublicSuburbsController(IPublicLocationSearchService locationSearchService)
    {
        _locationSearchService = locationSearchService;
    }

    [HttpGet]
    public async Task<ActionResult<List<string>>> Get([FromQuery] string city, CancellationToken cancellationToken)
    {
        city = (city ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(city))
        {
            return Ok(new List<string>());
        }

        var rows = await _locationSearchService.ListSuburbsAsync(city, cancellationToken);
        return Ok(rows.ToList());
    }
}

using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("packages")]
public sealed class PackagesController : ControllerBase
{
    private readonly IPackageCatalogService _packageCatalogService;
    private readonly IPackageAreaService _packageAreaService;
    private readonly IPackagePreviewService _packagePreviewService;

    public PackagesController(
        IPackageCatalogService packageCatalogService,
        IPackageAreaService packageAreaService,
        IPackagePreviewService packagePreviewService)
    {
        _packageCatalogService = packageCatalogService;
        _packageAreaService = packageAreaService;
        _packagePreviewService = packagePreviewService;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(_packageCatalogService.GetPackageBands());
    }

    [HttpGet("areas")]
    public async Task<IActionResult> GetAreas(CancellationToken cancellationToken)
    {
        return Ok(await _packageAreaService.GetAreasAsync(cancellationToken));
    }

    [HttpGet("preview")]
    public async Task<IActionResult> GetPreview([FromQuery] Guid packageBandId, [FromQuery] decimal budget, [FromQuery] string? selectedArea, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _packagePreviewService.GeneratePreviewAsync(packageBandId, budget, selectedArea, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                title: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}

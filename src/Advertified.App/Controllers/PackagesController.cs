using Advertified.App.Services.Abstractions;
using Advertified.App.Support;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("packages")]
public sealed class PackagesController : ControllerBase
{
    private readonly IPackageCatalogService _packageCatalogService;
    private readonly IPackageAreaService _packageAreaService;
    private readonly IPackagePreviewService _packagePreviewService;
    private readonly IPricingSettingsProvider _pricingSettingsProvider;

    public PackagesController(
        IPackageCatalogService packageCatalogService,
        IPackageAreaService packageAreaService,
        IPackagePreviewService packagePreviewService,
        IPricingSettingsProvider pricingSettingsProvider)
    {
        _packageCatalogService = packageCatalogService;
        _packageAreaService = packageAreaService;
        _packagePreviewService = packagePreviewService;
        _pricingSettingsProvider = pricingSettingsProvider;
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

    [HttpGet("pricing-summary")]
    public async Task<IActionResult> GetPricingSummary([FromQuery] decimal selectedBudget, [FromQuery] Guid? packageBandId, CancellationToken cancellationToken)
    {
        var settings = await _pricingSettingsProvider.GetCurrentAsync(cancellationToken);
        var packageBand = packageBandId.HasValue
            ? _packageCatalogService.GetPackageBands().FirstOrDefault(x => x.Id == packageBandId.Value)
            : null;
        return Ok(new Contracts.Packages.PackagePricingSummaryResponse
        {
            SelectedBudget = selectedBudget,
            ChargedAmount = PricingPolicy.CalculateChargedAmount(selectedBudget, settings.AiStudioReservePercent),
            AiStudioReserveAmount = PricingPolicy.CalculateAiStudioReserveAmount(selectedBudget, settings.AiStudioReservePercent, packageBand?.Code, packageBand?.Name)
        });
    }
}

using Advertified.App.Contracts.Public;
using Advertified.App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("public/form-options")]
[AllowAnonymous]
public sealed class PublicFormOptionsController : ControllerBase
{
    private readonly FormOptionsService _formOptionsService;

    public PublicFormOptionsController(FormOptionsService formOptionsService)
    {
        _formOptionsService = formOptionsService;
    }

    [HttpGet]
    public async Task<ActionResult<PublicFormOptionsResponse>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _formOptionsService.GetPublicOptionsAsync(cancellationToken));
    }
}

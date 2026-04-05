using Advertified.App.Contracts.Creatives;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/creatives")]
[Authorize(Roles = "CreativeDirector")]
public sealed class CreativesController : ControllerBase
{
    // Legacy template-era API surface retained for backward compatibility.
    private readonly ICreativeGenerationOrchestrator _creativeGenerationOrchestrator;

    public CreativesController(ICreativeGenerationOrchestrator creativeGenerationOrchestrator)
    {
        _creativeGenerationOrchestrator = creativeGenerationOrchestrator;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<GenerateCreativesResponse>> Generate(
        [FromBody] GenerateCreativesRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _creativeGenerationOrchestrator.GenerateAsync(request, null, true, cancellationToken);
        return Ok(response);
    }

    [HttpPost("regenerate")]
    public async Task<ActionResult<GenerateCreativesResponse>> Regenerate(
        [FromBody] RegenerateCreativeRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CreativeId == Guid.Empty)
        {
            throw new InvalidOperationException("creativeId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Feedback))
        {
            throw new InvalidOperationException("feedback is required.");
        }

        var response = await _creativeGenerationOrchestrator.RegenerateAsync(request, cancellationToken);
        return Ok(response);
    }
}

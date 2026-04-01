using Advertified.AIPlatform.Api.Contracts;
using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.AIPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/creatives")]
public sealed class CreativesController : ControllerBase
{
    private readonly ICreativeGenerationService _creativeGenerationService;

    public CreativesController(ICreativeGenerationService creativeGenerationService)
    {
        _creativeGenerationService = creativeGenerationService;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<CreativeGenerationResult>> Generate(
        [FromBody] GenerateCreativesRequest request,
        CancellationToken cancellationToken)
    {
        var channels = request.Channels
            .Select(MapChannel)
            .ToArray();

        var brief = new CreativeBrief(
            request.CampaignId,
            request.Brand,
            request.Objective,
            request.Tone,
            request.Languages,
            channels,
            request.KeyMessage,
            request.CallToAction,
            request.AudienceInsights ?? Array.Empty<string>());

        var result = await _creativeGenerationService.GenerateAsync(brief, cancellationToken);
        return Ok(result);
    }

    private static AdvertisingChannel MapChannel(string value)
    {
        return Enum.TryParse<AdvertisingChannel>(value, true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unsupported channel '{value}'.", nameof(value));
    }
}

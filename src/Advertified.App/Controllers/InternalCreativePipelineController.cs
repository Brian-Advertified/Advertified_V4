using Advertified.App.Contracts.Creatives;
using Advertified.App.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("internal")]
public sealed class InternalCreativePipelineController : ControllerBase
{
    private readonly ICreativeGenerationOrchestrator _creativeGenerationOrchestrator;

    public InternalCreativePipelineController(ICreativeGenerationOrchestrator creativeGenerationOrchestrator)
    {
        _creativeGenerationOrchestrator = creativeGenerationOrchestrator;
    }

    [HttpPost("creative-brief")]
    public ActionResult<CreativeBriefResponse> BuildCreativeBrief([FromBody] GenerateCreativesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Business.Name))
        {
            throw new InvalidOperationException("Business name is required.");
        }

        var languages = request.Audience.Languages.Count > 0 ? request.Audience.Languages : new[] { "English" };
        return Ok(new CreativeBriefResponse
        {
            Brand = request.Business.Name.Trim(),
            Objective = string.IsNullOrWhiteSpace(request.Objective) ? "Awareness" : request.Objective.Trim(),
            Tone = string.IsNullOrWhiteSpace(request.Tone) ? "Balanced" : request.Tone.Trim(),
            KeyMessage = $"Fast, trusted {request.Business.Industry} service in {request.Business.Location}.",
            Cta = "Visit today",
            Languages = languages,
            AudienceInsights = new[]
            {
                $"LSM {request.Audience.Lsm}",
                $"Age {request.Audience.AgeRange}",
                request.Business.Location
            }
        });
    }

    [HttpPost("generators/radio")]
    public async Task<ActionResult<IReadOnlyList<RadioCreativeResponse>>> GenerateRadio(
        [FromBody] GenerateCreativesRequest request,
        CancellationToken cancellationToken)
    {
        var response = await GenerateSingleChannel(request, "radio", cancellationToken);
        return Ok(response.Creatives.Radio);
    }

    [HttpPost("generators/tv")]
    public async Task<ActionResult<IReadOnlyList<TvCreativeResponse>>> GenerateTv(
        [FromBody] GenerateCreativesRequest request,
        CancellationToken cancellationToken)
    {
        var response = await GenerateSingleChannel(request, "tv", cancellationToken);
        return Ok(response.Creatives.Tv);
    }

    [HttpPost("generators/billboard")]
    public async Task<ActionResult<IReadOnlyList<BillboardCreativeResponse>>> GenerateBillboard(
        [FromBody] GenerateCreativesRequest request,
        CancellationToken cancellationToken)
    {
        var response = await GenerateSingleChannel(request, "billboard", cancellationToken);
        return Ok(response.Creatives.Billboard);
    }

    [HttpPost("generators/newspaper")]
    public async Task<ActionResult<IReadOnlyList<NewspaperCreativeResponse>>> GenerateNewspaper(
        [FromBody] GenerateCreativesRequest request,
        CancellationToken cancellationToken)
    {
        var response = await GenerateSingleChannel(request, "newspaper", cancellationToken);
        return Ok(response.Creatives.Newspaper);
    }

    [HttpPost("generators/digital")]
    public async Task<ActionResult<IReadOnlyList<DigitalCreativeResponse>>> GenerateDigital(
        [FromBody] GenerateCreativesRequest request,
        CancellationToken cancellationToken)
    {
        var response = await GenerateSingleChannel(request, "digital", cancellationToken);
        return Ok(response.Creatives.Digital);
    }

    [HttpPost("creative-score")]
    public ActionResult<CreativeChannelScoreResponse> Score([FromBody] CreativeScoreRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Channel))
        {
            throw new InvalidOperationException("Channel is required.");
        }

        var metrics = request.Channel.Trim().ToLowerInvariant() switch
        {
            "radio" => new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["clarity"] = 8.5m,
                ["emotionalImpact"] = 7.8m,
                ["ctaStrength"] = 9.0m
            },
            "billboard" => new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["readability"] = 9.2m,
                ["attention"] = 8.9m,
                ["ctaStrength"] = 8.6m
            },
            _ => new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["clarity"] = 8.2m,
                ["brandFit"] = 8.3m,
                ["ctaStrength"] = 8.4m
            }
        };

        return Ok(new CreativeChannelScoreResponse
        {
            CreativeId = string.IsNullOrWhiteSpace(request.CreativeId) ? Guid.NewGuid().ToString("N") : request.CreativeId,
            Metrics = metrics
        });
    }

    [HttpPost("localisation")]
    public ActionResult<LocalisationResponse> Localize([FromBody] LocalisationRequest request)
    {
        var response = _creativeGenerationOrchestrator.Localize(request);
        return Ok(response);
    }

    private async Task<GenerateCreativesResponse> GenerateSingleChannel(
        GenerateCreativesRequest request,
        string channel,
        CancellationToken cancellationToken)
    {
        var singleRequest = new GenerateCreativesRequest
        {
            CampaignId = request.CampaignId,
            Business = request.Business,
            Objective = request.Objective,
            Budget = request.Budget,
            Audience = request.Audience,
            Channels = new[] { channel },
            Tone = request.Tone
        };

        return await _creativeGenerationOrchestrator.GenerateAsync(singleRequest, null, false, cancellationToken);
    }
}

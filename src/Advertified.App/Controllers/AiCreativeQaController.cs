using Advertified.App.AIPlatform.Api;
using Advertified.App.AIPlatform.Application;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/v2/ai-platform/qa")]
public sealed class AiCreativeQaController : ControllerBase
{
    private readonly ICreativeQaResultRepository _creativeQaResultRepository;

    public AiCreativeQaController(ICreativeQaResultRepository creativeQaResultRepository)
    {
        _creativeQaResultRepository = creativeQaResultRepository;
    }

    [HttpGet("campaigns/{campaignId:guid}")]
    public async Task<ActionResult<IReadOnlyList<CreativeQaResultResponse>>> GetCampaignQaResults(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var rows = await _creativeQaResultRepository.GetByCampaignAsync(campaignId, cancellationToken);
        var response = rows.Select(item => new CreativeQaResultResponse
        {
            CreativeId = item.CreativeId,
            CampaignId = item.CampaignId,
            Channel = item.Channel.ToString(),
            Language = item.Language,
            Clarity = item.Clarity,
            Attention = item.Attention,
            EmotionalImpact = item.EmotionalImpact,
            CtaStrength = item.CtaStrength,
            BrandFit = item.BrandFit,
            ChannelFit = item.ChannelFit,
            FinalScore = item.FinalScore,
            Status = item.Status,
            RiskLevel = item.RiskLevel,
            Issues = item.Issues,
            Suggestions = item.Suggestions,
            RiskFlags = item.RiskFlags,
            ImprovedPayloadJson = item.ImprovedPayloadJson,
            CreatedAt = item.CreatedAt
        }).ToArray();

        return Ok(response);
    }
}

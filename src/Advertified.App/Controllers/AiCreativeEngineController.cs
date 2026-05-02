using Advertified.App.AIPlatform.Api;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/v2/ai-platform/creative-engine")]
public sealed class AiCreativeEngineController : ControllerBase
{
    private readonly ICreativeCampaignOrchestrator _creativeCampaignOrchestrator;
    private readonly ICreativeGenerationEngine _creativeGenerationEngine;

    public AiCreativeEngineController(
        ICreativeCampaignOrchestrator creativeCampaignOrchestrator,
        ICreativeGenerationEngine creativeGenerationEngine)
    {
        _creativeCampaignOrchestrator = creativeCampaignOrchestrator;
        _creativeGenerationEngine = creativeGenerationEngine;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<CreativeEngineGenerateResponse>> Generate(
        [FromBody] CreativeEngineGenerateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CampaignId == Guid.Empty)
        {
            throw new InvalidOperationException("campaignId is required.");
        }

        var result = await _creativeCampaignOrchestrator.GenerateAsync(new GenerateCampaignCreativesCommand(
            request.CampaignId,
            request.PromptOverride,
            request.PersistOutputs,
            request.VoicePackId,
            request.IdempotencyKey), cancellationToken);

        return Ok(Map(result));
    }

    [HttpPost("generate-from-brief")]
    public async Task<ActionResult<CreativeEngineGenerateResponse>> GenerateFromBrief(
        [FromBody] CreativeEngineGenerateFromBriefRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Brief.CampaignId == Guid.Empty)
        {
            throw new InvalidOperationException("brief.campaignId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Brief.Brand))
        {
            throw new InvalidOperationException("brief.brand is required.");
        }

        var brief = new CreativeBrief(
            request.Brief.CampaignId,
            request.Brief.Budget > 0m ? request.Brief.Budget : 50000m,
            request.Brief.Brand.Trim(),
            string.IsNullOrWhiteSpace(request.Brief.Objective) ? "Awareness" : request.Brief.Objective.Trim(),
            string.IsNullOrWhiteSpace(request.Brief.Tone) ? "Balanced" : request.Brief.Tone.Trim(),
            string.IsNullOrWhiteSpace(request.Brief.KeyMessage)
                ? $"High-impact campaign for {request.Brief.Brand.Trim()}."
                : request.Brief.KeyMessage.Trim(),
            string.IsNullOrWhiteSpace(request.Brief.CallToAction) ? "Get started today" : request.Brief.CallToAction.Trim(),
            request.Brief.AudienceInsights.Count > 0 ? request.Brief.AudienceInsights : new List<string> { "South African audience" },
            request.Brief.Languages.Count > 0 ? request.Brief.Languages : new List<string> { "English" },
            request.Brief.Channels.Count > 0 ? request.Brief.Channels : new List<AdvertisingChannel> { AdvertisingChannel.Digital },
            request.Brief.PromptVersion <= 0 ? 1 : request.Brief.PromptVersion,
            request.Brief.MaxVariantsPerChannel <= 0 ? 1 : request.Brief.MaxVariantsPerChannel);

        var creatives = await _creativeGenerationEngine.GenerateAsync(brief, cancellationToken);
        return Ok(new CreativeEngineGenerateResponse
        {
            CampaignId = brief.CampaignId,
            JobId = Guid.NewGuid(),
            CompletedAt = DateTimeOffset.UtcNow,
            Creatives = creatives.Select(item => new CreativeEngineCreativeItemResponse
            {
                CreativeId = item.CreativeId,
                Channel = item.Channel.ToString(),
                Language = item.Language,
                PayloadJson = item.PayloadJson
            }).ToList()
        });
    }

    private static CreativeEngineGenerateResponse Map(GenerateCampaignCreativesResult result)
    {
        return new CreativeEngineGenerateResponse
        {
            CampaignId = result.CampaignId,
            JobId = result.JobId,
            CompletedAt = result.CompletedAt,
            Creatives = result.Creatives.Select(item => new CreativeEngineCreativeItemResponse
            {
                CreativeId = item.CreativeId,
                Channel = item.Channel.ToString(),
                Language = item.Language,
                PayloadJson = item.PayloadJson
            }).ToList()
        };
    }
}

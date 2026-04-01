using Advertified.App.AIPlatform.Api;
using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Advertified.App.Controllers;

[ApiController]
[Route("api/v2/ai-platform/voice-templates")]
public sealed class AiVoiceTemplatesController : ControllerBase
{
    private readonly IVoiceTemplateSelectionService _voiceTemplateSelectionService;

    public AiVoiceTemplatesController(IVoiceTemplateSelectionService voiceTemplateSelectionService)
    {
        _voiceTemplateSelectionService = voiceTemplateSelectionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VoiceTemplateResponse>>> Get(CancellationToken cancellationToken)
    {
        var rows = await _voiceTemplateSelectionService.ListAsync(cancellationToken);
        return Ok(rows.Select(item => new VoiceTemplateResponse
        {
            Id = item.Id,
            TemplateNumber = item.TemplateNumber,
            Category = item.Category,
            Name = item.Name,
            PromptTemplate = item.PromptTemplate,
            PrimaryVoicePackName = item.PrimaryVoicePackName,
            FallbackVoicePackNames = item.FallbackVoicePackNames
        }).ToArray());
    }

    [HttpPost("select")]
    public async Task<ActionResult<SelectVoiceTemplateResponse>> Select(
        [FromBody] SelectVoiceTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _voiceTemplateSelectionService.SelectAsync(
            new VoiceTemplateSelectionInput(
                request.CampaignId,
                request.Product,
                request.Industry,
                request.Audience,
                request.Goal,
                request.BudgetTier,
                request.Language,
                request.Platform,
                request.Objective,
                request.Brand,
                request.Business,
                request.EventName,
                request.Offer),
            cancellationToken);

        return Ok(new SelectVoiceTemplateResponse
        {
            TemplateNumber = result.TemplateNumber,
            TemplateName = result.TemplateName,
            PromptTemplate = result.PromptTemplate,
            FinalPrompt = result.FinalPrompt,
            PrimaryVoicePackName = result.PrimaryVoicePackName,
            PrimaryVoicePackId = result.PrimaryVoicePackId,
            FallbackVoicePackNames = result.FallbackVoicePackNames,
            FallbackVoicePackIds = result.FallbackVoicePackIds
        });
    }
}

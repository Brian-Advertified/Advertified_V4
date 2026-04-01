using System.Text.Json;
using Advertified.AIPlatform.Application.Abstractions;
using Advertified.AIPlatform.Domain.Models;

namespace Advertified.AIPlatform.Application.Services;

public sealed class CreativeGenerationService : ICreativeGenerationService
{
    private readonly IMultiAiOrchestrator _orchestrator;

    public CreativeGenerationService(IMultiAiOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<CreativeGenerationResult> GenerateAsync(CreativeBrief brief, CancellationToken cancellationToken)
    {
        var outputs = new List<ChannelCreativeOutput>();

        // Fan-out per channel/language keeps creative generation deterministic and debuggable.
        foreach (var channel in brief.Channels)
        {
            foreach (var language in brief.Languages)
            {
                var inputPayload = JsonSerializer.Serialize(new
                {
                    brief.CampaignId,
                    brief.Brand,
                    brief.Objective,
                    brief.Tone,
                    brief.KeyMessage,
                    brief.CallToAction,
                    brief.AudienceInsights,
                    Channel = channel.ToString(),
                    Language = language
                });

                var payloadJson = await _orchestrator.ExecuteAsync(
                    channel,
                    operation: "creative-generate",
                    inputPayload,
                    cancellationToken);

                outputs.Add(new ChannelCreativeOutput(channel, language, payloadJson));
            }
        }

        return new CreativeGenerationResult(
            brief.CampaignId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            outputs);
    }
}

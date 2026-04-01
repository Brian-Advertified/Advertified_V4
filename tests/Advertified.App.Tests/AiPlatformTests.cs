using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.AIPlatform.Infrastructure;
using FluentAssertions;

namespace Advertified.App.Tests;

public class AiPlatformPromptLibraryTests
{
    [Fact]
    public async Task GetLatestAsync_ReturnsLatestTemplateVersion()
    {
        var service = new InMemoryPromptLibraryService();

        var template = await service.GetLatestAsync("creative-brief-default", CancellationToken.None);

        template.Version.Should().BeGreaterThan(0);
        template.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }
}

public class MultiAiProviderOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_UsesMatchingProviderStrategy()
    {
        var orchestrator = new MultiAiProviderOrchestrator(new IAiProviderStrategy[]
        {
            new OpenAiProviderStrategy(),
            new ElevenLabsProviderStrategy(),
            new RunwayProviderStrategy(),
            new ImageApiProviderStrategy()
        });

        var radioAsset = await orchestrator.ExecuteAsync(
            AdvertisingChannel.Radio,
            "asset-voice",
            "{\"message\":\"test\"}",
            CancellationToken.None);

        radioAsset.Should().Contain("radio-voice.mp3");
    }
}

public class CreativeCampaignOrchestratorTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsCreativesScoresAndAssets()
    {
        var orchestrator = new CreativeCampaignOrchestrator(
            new StubMediaPlanningIntegrationService(),
            new InMemoryPromptLibraryService(),
            new StrategyCreativeGenerationEngine(new MultiAiProviderOrchestrator(new IAiProviderStrategy[]
            {
                new OpenAiProviderStrategy(),
                new ElevenLabsProviderStrategy(),
                new RunwayProviderStrategy(),
                new ImageApiProviderStrategy()
            })),
            new OpenAiCreativeQaService(new MultiAiProviderOrchestrator(new IAiProviderStrategy[]
            {
                new OpenAiProviderStrategy()
            })),
            new StrategyAssetGenerationPipeline(new MultiAiProviderOrchestrator(new IAiProviderStrategy[]
            {
                new OpenAiProviderStrategy(),
                new ElevenLabsProviderStrategy(),
                new RunwayProviderStrategy(),
                new ImageApiProviderStrategy()
            })),
            new InMemoryCreativeJobQueue());

        var result = await orchestrator.GenerateAsync(
            new GenerateCampaignCreativesCommand(Guid.NewGuid(), null, true),
            CancellationToken.None);

        result.Creatives.Should().NotBeEmpty();
        result.Scores.Should().HaveCount(result.Creatives.Count);
        result.Assets.Should().HaveCount(result.Creatives.Count);
    }

    private sealed class StubMediaPlanningIntegrationService : IMediaPlanningIntegrationService
    {
        public Task<MediaPlanningContext> BuildContextAsync(Guid campaignId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new MediaPlanningContext(
                campaignId,
                "Brian's Car Wash",
                "Automotive",
                "Johannesburg",
                "FootTraffic",
                50000m,
                "Energetic",
                "5-8",
                "25-45",
                new[] { "English", "Zulu" },
                new[] { AdvertisingChannel.Radio, AdvertisingChannel.Billboard, AdvertisingChannel.Digital }));
        }
    }
}

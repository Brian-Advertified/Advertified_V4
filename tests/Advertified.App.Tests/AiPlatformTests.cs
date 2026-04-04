using Advertified.App.AIPlatform.Application;
using Advertified.App.AIPlatform.Domain;
using Advertified.App.AIPlatform.Infrastructure;
using Advertified.App.Configuration;
using Advertified.App.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        var expectedPayload = "{\"assetUrl\":\"https://assets.example.com/audio/radio-voice.mp3\"}";
        var factory = new AiProviderStrategyFactory(new IAiProviderStrategy[]
        {
            new StubProviderStrategy("openai", canHandle: false, expectedPayload),
            new StubProviderStrategy("voice", canHandle: true, expectedPayload),
        });
        var orchestrator = new MultiAiProviderOrchestrator(factory);

        var radioAsset = await orchestrator.ExecuteAsync(
            AdvertisingChannel.Radio,
            "asset-voice",
            "{\"message\":\"test\"}",
            CancellationToken.None);

        radioAsset.Should().Be(expectedPayload);
    }

    private sealed class StubProviderStrategy : IAiProviderStrategy
    {
        private readonly bool _canHandle;
        private readonly string _payload;

        public StubProviderStrategy(string providerName, bool canHandle, string payload)
        {
            ProviderName = providerName;
            _canHandle = canHandle;
            _payload = payload;
        }

        public string ProviderName { get; }

        public bool CanHandle(AdvertisingChannel channel, string operation) => _canHandle;

        public Task<string> ExecuteAsync(string inputJson, CancellationToken cancellationToken)
            => Task.FromResult(_payload);
    }
}

public class CreativeCampaignOrchestratorTests
{
    [Fact]
    public async Task GenerateAsync_ReturnsCreativesScoresAndAssets()
    {
        await using var db = BuildDbContext();
        var orchestrator = new CreativeCampaignOrchestrator(
            new StubMediaPlanningIntegrationService(),
            new StubCreativeGenerationEngine(),
            new StubCreativeQaService(),
            new StubAssetGenerationPipeline(),
            new InMemoryCreativeJobQueue(),
            new StubAiIdempotencyService(),
            new StubAiCostEstimator(),
            new StubAiCostControlService(),
            db);

        var result = await orchestrator.GenerateAsync(
            new GenerateCampaignCreativesCommand(Guid.NewGuid(), null, true),
            CancellationToken.None);

        result.Creatives.Should().NotBeEmpty();
        result.Scores.Should().HaveCount(result.Creatives.Count);
        result.Assets.Should().HaveCount(result.Creatives.Count);
    }

    private static AppDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ai-platform-tests-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
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
                new[] { AdvertisingChannel.Radio }));
        }
    }

    private sealed class StubCreativeGenerationEngine : ICreativeGenerationEngine
    {
        public Task<IReadOnlyList<CreativeVariant>> GenerateAsync(CreativeBrief brief, CancellationToken cancellationToken)
        {
            var payload = "{\"content\":{\"script\":\"Advertified ad script\",\"cta\":\"Book now\",\"duration\":30,\"structure\":[\"hook\",\"message\",\"cta\"]}}";
            IReadOnlyList<CreativeVariant> output =
                new[]
                {
                    new CreativeVariant(
                        Guid.NewGuid(),
                        brief.CampaignId,
                        AdvertisingChannel.Radio,
                        "English",
                        payload,
                        DateTimeOffset.UtcNow)
                };
            return Task.FromResult(output);
        }
    }

    private sealed class StubCreativeQaService : ICreativeQaService
    {
        public Task<IReadOnlyList<CreativeQualityScore>> ScoreAsync(
            CreativeBrief brief,
            IReadOnlyList<CreativeVariant> creatives,
            CancellationToken cancellationToken)
        {
            var scores = creatives
                .Select(creative => new CreativeQualityScore(
                    creative.CreativeId,
                    creative.Channel,
                    new Dictionary<string, decimal>
                    {
                        ["clarity"] = 9m,
                        ["attention"] = 9m,
                        ["emotionalImpact"] = 8m,
                        ["ctaStrength"] = 9m,
                        ["brandFit"] = 9m,
                        ["channelFit"] = 9m
                    },
                    8.8m))
                .ToArray();

            return Task.FromResult<IReadOnlyList<CreativeQualityScore>>(scores);
        }
    }

    private sealed class StubAssetGenerationPipeline : IAssetGenerationPipeline
    {
        public Task<IReadOnlyList<AssetGenerationResult>> GenerateAssetsAsync(
            IReadOnlyList<AssetGenerationRequest> requests,
            CancellationToken cancellationToken)
        {
            var assets = requests.Select(request => new AssetGenerationResult(
                request.CreativeId,
                request.Channel,
                "voice",
                "https://assets.example.com/audio/test.mp3",
                DateTimeOffset.UtcNow)).ToArray();

            return Task.FromResult<IReadOnlyList<AssetGenerationResult>>(assets);
        }
    }

    private sealed class StubAiIdempotencyService : IAiIdempotencyService
    {
        public Task<Guid?> GetJobIdAsync(string scope, string key, CancellationToken cancellationToken)
            => Task.FromResult<Guid?>(null);

        public Task SaveJobIdAsync(string scope, string key, Guid jobId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class StubAiCostEstimator : IAiCostEstimator
    {
        public decimal CalculateMaxAiCost(decimal campaignBudget) => campaignBudget * 0.05m;
        public int ResolveVariantCount(decimal campaignBudget) => 1;
        public bool AllowVideoGeneration(decimal campaignBudget) => true;
        public decimal EstimateTextGenerationCost(int variantCount) => 1m;
        public decimal EstimateQaCost(int variantCount) => 1m;
        public decimal EstimateAssetCost(string assetKind, int units = 1) => 1m;
    }

    private sealed class StubAiCostControlService : IAiCostControlService
    {
        public Task<AiCostGuardDecision> GuardAsync(AiCostGuardRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AiCostGuardDecision(
                Allowed: true,
                UsageLogId: Guid.NewGuid(),
                CampaignBudgetZar: request.CampaignBudgetZar ?? 50000m,
                MaxAllowedCostZar: 5000m,
                CurrentCommittedCostZar: 0m,
                ProjectedCommittedCostZar: Math.Max(0m, request.EstimatedCostZar),
                Message: "ok"));
        }

        public Task CompleteAsync(Guid usageLogId, decimal? actualCostZar, string? details, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task FailAsync(Guid usageLogId, string? details, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<AiCampaignCostSummary> GetSummaryAsync(Guid campaignId, decimal? campaignBudgetZar, CancellationToken cancellationToken)
        {
            var budget = campaignBudgetZar ?? 0m;
            return Task.FromResult(new AiCampaignCostSummary(campaignId, budget, budget * 0.05m, 0m, budget * 0.05m, 0m));
        }
    }
}

public class AdPlatformPublisherContractTests
{
    [Fact]
    public async Task MetaPublisher_DryRun_ReturnsDeterministicPublishAndMetrics()
    {
        var options = Options.Create(new AdPlatformOptions
        {
            DryRunMode = true
        });
        var publisher = new MetaAdPlatformPublisher(
            options,
            new StubHttpClientFactory(),
            NullLogger<MetaAdPlatformPublisher>.Instance);

        var variantId = Guid.NewGuid();
        var variant = new AdVariantSummary(
            variantId,
            Guid.NewGuid(),
            null,
            "Meta",
            "Digital",
            "English",
            null,
            null,
            null,
            "Script",
            null,
            null,
            "draft",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);

        var platformAdId = await publisher.PublishAsync(variant, CancellationToken.None);
        var metrics = await publisher.GetMetricsAsync(platformAdId, CancellationToken.None);

        platformAdId.Should().StartWith("meta-");
        metrics.Impressions.Should().BeGreaterThan(0);
        metrics.Clicks.Should().BeGreaterThan(0);
        metrics.Conversions.Should().BeGreaterThan(0);
        metrics.CostZar.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GooglePublisher_DryRun_ReturnsDeterministicPublishAndMetrics()
    {
        var options = Options.Create(new AdPlatformOptions
        {
            DryRunMode = true
        });
        var publisher = new GoogleAdsPlatformPublisher(
            options,
            new StubHttpClientFactory(),
            NullLogger<GoogleAdsPlatformPublisher>.Instance);

        var variantId = Guid.NewGuid();
        var variant = new AdVariantSummary(
            variantId,
            Guid.NewGuid(),
            null,
            "GoogleAds",
            "Digital",
            "English",
            null,
            null,
            null,
            "Script",
            null,
            null,
            "draft",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);

        var platformAdId = await publisher.PublishAsync(variant, CancellationToken.None);
        var metrics = await publisher.GetMetricsAsync(platformAdId, CancellationToken.None);

        platformAdId.Should().StartWith("gads-");
        metrics.Impressions.Should().BeGreaterThan(0);
        metrics.Clicks.Should().BeGreaterThan(0);
        metrics.Conversions.Should().BeGreaterThan(0);
        metrics.CostZar.Should().BeGreaterThan(0);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

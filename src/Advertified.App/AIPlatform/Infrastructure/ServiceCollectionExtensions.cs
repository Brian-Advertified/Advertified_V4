using Advertified.App.AIPlatform.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Advertified.App.AIPlatform.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiAdvertisingPlatform(this IServiceCollection services)
    {
        services.AddSingleton<ICreativeJobQueue, ServiceBusCreativeJobQueue>();
        services.AddScoped<IMediaPlanningIntegrationService, DbMediaPlanningIntegrationService>();
        services.AddScoped<IPromptTemplateRepository, DbPromptTemplateRepository>();
        services.AddScoped<IPromptLibraryService, PromptLibraryService>();
        services.AddScoped<IPromptInputBuilder, PromptInputBuilder>();
        services.AddScoped<IAiIdempotencyService, DbAiIdempotencyService>();
        services.AddScoped<ICreativeGenerationEngine, StrategyCreativeGenerationEngine>();
        services.AddScoped<ICreativeValidationService, RuleBasedCreativeValidationService>();
        services.AddScoped<ICreativeScoringService, HybridCreativeScoringService>();
        services.AddScoped<ICreativeRiskService, ComplianceCreativeRiskService>();
        services.AddScoped<ICreativeImprovementService, CreativeImprovementService>();
        services.AddScoped<ICreativeDecisionService, ThresholdCreativeDecisionService>();
        services.AddScoped<ICreativeQaResultRepository, DbCreativeQaResultRepository>();
        services.AddScoped<ICreativeQaService, PipelineCreativeQaService>();
        services.AddScoped<IAiCostEstimator, AiCostEstimator>();
        services.AddScoped<IAiCostControlService, AiCostControlService>();
        services.AddSingleton<IAssetJobQueue, ServiceBusAssetJobQueue>();
        services.AddScoped<IAssetJobRepository, DbAssetJobRepository>();
        services.AddScoped<IAssetJobService, DbAssetJobRepository>();
        services.AddScoped<IVoiceAssetGenerationService, VoiceAssetGenerationService>();
        services.AddScoped<IImageAssetGenerationService, ImageAssetGenerationService>();
        services.AddScoped<IVideoAssetGenerationService, VideoAssetGenerationService>();
        services.AddScoped<IAssetGenerationPipeline, StrategyAssetGenerationPipeline>();
        services.AddScoped<ICreativeCampaignOrchestrator, CreativeCampaignOrchestrator>();
        services.AddScoped<ICreativeFeedbackRegenerationService, CreativeFeedbackRegenerationService>();
        services.AddScoped<IMultiAiProviderOrchestrator, MultiAiProviderOrchestrator>();
        services.AddScoped<IAiProviderStrategyFactory, AiProviderStrategyFactory>();
        services.AddScoped<IVoicePackPolicyService, VoicePackPolicyService>();
        services.AddScoped<IVoiceTemplateSelectionService, VoiceTemplateSelectionService>();
        services.AddScoped<IAdVariantService, DbAdVariantService>();
        services.AddScoped<IAdPlatformPublisherFactory, AdPlatformPublisherFactory>();
        services.AddScoped<IAdPlatformPublisher, MetaAdPlatformPublisher>();
        services.AddScoped<IAdPlatformPublisher, GoogleAdsPlatformPublisher>();

        services.AddScoped<IAiProviderStrategy, OpenAiProviderStrategy>();
        services.AddScoped<IAiProviderStrategy, ElevenLabsProviderStrategy>();
        services.AddScoped<IAiProviderStrategy, RunwayProviderStrategy>();
        services.AddScoped<IAiProviderStrategy, ImageApiProviderStrategy>();

        services.AddHostedService<CreativeJobWorker>();
        services.AddHostedService<AssetJobWorker>();
        services.AddHostedService<AdMetricsSyncWorker>();
        return services;
    }
}

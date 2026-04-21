namespace Advertified.App.Configuration;

public sealed class AiPlatformOptions
{
    public const string SectionName = "AiPlatform";

    public string ServiceBusConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "ai-creative-jobs";
    public string AssetQueueName { get; set; } = "ai-asset-jobs";
    public bool UseInMemoryFallback { get; set; } = false;
    public int MaxWorkerRetries { get; set; } = 3;
    public int BaseRetryDelaySeconds { get; set; } = 2;
    public string DashboardUrl { get; set; } = string.Empty;
    public string TracesUrl { get; set; } = string.Empty;

    // Cost optimization controls.
    public decimal MaxAiCostPercentOfCampaignBudget { get; set; } = 0.05m;
    public decimal MaxAiCostHardCapZar { get; set; } = 25000m;
    public decimal SpendSafetyFactorOfAiReserve { get; set; } = 0.40m;
    public decimal TextGenerationCostZar { get; set; } = 0.15m;
    public decimal QaScoringCostZar { get; set; } = 0.02m;
    public decimal ImageGenerationCostZar { get; set; } = 5m;
    public decimal VoiceGenerationCostZar { get; set; } = 3m;
    public decimal VideoGenerationCostZar { get; set; } = 50m;
    public decimal VideoMinCampaignBudgetZar { get; set; } = 10000m;
    public int FreeRegenerationLimit { get; set; } = 1;
    public int PaidRegenerationLimit { get; set; } = 3;
}

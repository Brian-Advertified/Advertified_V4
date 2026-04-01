namespace Advertified.App.AIPlatform.Api;

public sealed class SubmitCreativeGenerationRequest
{
    public Guid CampaignId { get; set; }
    public string? PromptOverride { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class SubmitCreativeGenerationResponse
{
    public Guid JobId { get; set; }
    public Guid CampaignId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset QueuedAt { get; set; }
}

public sealed class CreativeJobStatusResponse
{
    public Guid JobId { get; set; }
    public Guid CampaignId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int RetryAttemptCount { get; set; }
    public string? LastFailure { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RegenerateCreativeRequestDto
{
    public Guid CreativeId { get; set; }
    public Guid CampaignId { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

public sealed class AiPlatformCampaignCreativeItemResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AiPlatformCampaignCostSummaryResponse
{
    public Guid CampaignId { get; set; }
    public decimal CampaignBudgetZar { get; set; }
    public decimal MaxAllowedCostZar { get; set; }
    public decimal CommittedCostZar { get; set; }
    public decimal RemainingBudgetZar { get; set; }
    public decimal UtilizationPercent { get; set; }
}

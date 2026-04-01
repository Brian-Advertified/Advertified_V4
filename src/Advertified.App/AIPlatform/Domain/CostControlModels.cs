namespace Advertified.App.AIPlatform.Domain;

public sealed record AiCostGuardRequest(
    Guid CampaignId,
    string Operation,
    string? Provider,
    decimal EstimatedCostZar,
    decimal? CampaignBudgetZar = null,
    Guid? CreativeId = null,
    Guid? JobId = null,
    string? Details = null);

public sealed record AiCostGuardDecision(
    bool Allowed,
    Guid? UsageLogId,
    decimal CampaignBudgetZar,
    decimal MaxAllowedCostZar,
    decimal CurrentCommittedCostZar,
    decimal ProjectedCommittedCostZar,
    string Message);

public sealed record AiCampaignCostSummary(
    Guid CampaignId,
    decimal CampaignBudgetZar,
    decimal MaxAllowedCostZar,
    decimal CommittedCostZar,
    decimal RemainingBudgetZar,
    decimal UtilizationPercent);


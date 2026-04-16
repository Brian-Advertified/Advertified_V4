using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface IRecommendationApprovalWorkflowService
{
    Task<RecommendationWorkflowResult> ApproveAsync(Guid campaignId, Guid? recommendationId, CancellationToken cancellationToken);
    Task<RecommendationWorkflowResult> RequestChangesAsync(Guid campaignId, string? notes, CancellationToken cancellationToken);
    Task<RecommendationWorkflowResult> RejectAllAsync(Guid campaignId, string? notes, CancellationToken cancellationToken);
}

public sealed class RecommendationWorkflowResult
{
    public Guid CampaignId { get; init; }
    public Guid RecommendationId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

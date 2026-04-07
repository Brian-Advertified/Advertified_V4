using Advertified.App.Data.Entities;

namespace Advertified.App.Services.Abstractions;

public interface ILeadActionRecommendationService
{
    IReadOnlyList<LeadAction> BuildRecommendedActions(
        Lead lead,
        LeadScoreResult score,
        LeadTrendAnalysisResult trend,
        LeadInsight insight);
}

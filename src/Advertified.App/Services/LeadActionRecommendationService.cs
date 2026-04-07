using Advertified.App.Data.Entities;
using Advertified.App.Services.Abstractions;

namespace Advertified.App.Services;

public sealed class LeadActionRecommendationService : ILeadActionRecommendationService
{
    public IReadOnlyList<LeadAction> BuildRecommendedActions(
        Lead lead,
        LeadScoreResult score,
        LeadTrendAnalysisResult trend,
        LeadInsight insight)
    {
        var actions = new List<LeadAction>();

        if (trend.CampaignStartedRecently)
        {
            actions.Add(new LeadAction
            {
                LeadId = lead.Id,
                LeadInsightId = insight.Id,
                ActionType = "outreach",
                Title = "Contact lead during fresh campaign activity",
                Description = $"{lead.Name} appears to have launched new activity recently. Reach out while campaign momentum is visible.",
                Priority = "high",
                Status = "open",
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (score.IntentLevel.Equals("High", StringComparison.OrdinalIgnoreCase))
        {
            actions.Add(new LeadAction
            {
                LeadId = lead.Id,
                LeadInsightId = insight.Id,
                ActionType = "campaign_suggestion",
                Title = "Prepare campaign recommendation",
                Description = $"High-intent signals suggest {lead.Name} is a strong candidate for an outbound media recommendation.",
                Priority = "high",
                Status = "open",
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (trend.ActivityIncreased && !trend.CampaignStartedRecently)
        {
            actions.Add(new LeadAction
            {
                LeadId = lead.Id,
                LeadInsightId = insight.Id,
                ActionType = "monitor",
                Title = "Monitor lead for follow-up activity",
                Description = $"{lead.Name} is showing rising activity. Keep it on a short watchlist and recheck after the next refresh.",
                Priority = "medium",
                Status = "open",
                CreatedAt = DateTime.UtcNow,
            });
        }

        return actions;
    }
}

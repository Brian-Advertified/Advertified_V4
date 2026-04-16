using Advertified.App.Data.Entities;
using Advertified.App.Support;

namespace Advertified.App.Tests;

public sealed class CampaignWorkflowPolicyTests
{
    [Fact]
    public void BuildClientWorkflow_ReturnsPaymentRequired_WhenRecommendationAwaitsDecisionButPaymentIsOutstanding()
    {
        var campaign = new Campaign
        {
            Status = CampaignStatuses.ReviewReady,
            AiUnlocked = true,
            PackageOrder = new PackageOrder
            {
                PaymentProvider = "vodapay",
                PaymentStatus = "pending"
            },
            CampaignRecommendations = new[]
            {
                new CampaignRecommendation
                {
                    Status = RecommendationStatuses.SentToClient,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        var workflow = CampaignWorkflowPolicy.BuildClientWorkflow(campaign);

        Assert.Equal("payment_required", workflow.CurrentStateKey);
        Assert.True(workflow.PaymentRequiredBeforeApproval);
        Assert.False(workflow.RecommendationApprovalCompleted);
    }

    [Fact]
    public void BuildClientWorkflow_ReturnsManualReview_WhenLulaPaymentIsPending()
    {
        var campaign = new Campaign
        {
            Status = CampaignStatuses.ReviewReady,
            AiUnlocked = true,
            PackageOrder = new PackageOrder
            {
                PaymentProvider = "lula",
                PaymentStatus = "pending"
            },
            CampaignRecommendations = new[]
            {
                new CampaignRecommendation
                {
                    Status = RecommendationStatuses.SentToClient,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        var workflow = CampaignWorkflowPolicy.BuildClientWorkflow(campaign);

        Assert.Equal("payment_under_review", workflow.CurrentStateKey);
        Assert.True(workflow.PaymentAwaitingManualReview);
        Assert.False(workflow.RequiresClientAction);
    }

    [Fact]
    public void TryAdvanceToBookingInProgress_RequiresCreativeApproved()
    {
        var readyCampaign = new Campaign { Status = CampaignStatuses.CreativeApproved };
        var pendingCampaign = new Campaign { Status = CampaignStatuses.CreativeSentToClientForApproval };

        var readyResult = CampaignStatusTransitionPolicy.TryAdvanceToBookingInProgress(readyCampaign);
        var pendingResult = CampaignStatusTransitionPolicy.TryAdvanceToBookingInProgress(pendingCampaign);

        Assert.True(readyResult);
        Assert.Equal(CampaignStatuses.BookingInProgress, readyCampaign.Status);
        Assert.False(pendingResult);
        Assert.Equal(CampaignStatuses.CreativeSentToClientForApproval, pendingCampaign.Status);
    }
}

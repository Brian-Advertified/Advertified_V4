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

    [Fact]
    public void ResolveAgentQueueStage_ReturnsClosedProspect_WhenProspectDispositionIsClosed()
    {
        var campaign = new Campaign
        {
            Status = CampaignStatuses.ReviewReady,
            ProspectDispositionStatus = ProspectDispositionStatuses.Closed,
            PackageOrder = new PackageOrder
            {
                PaymentProvider = "prospect",
                PaymentStatus = "pending"
            }
        };

        var stage = CampaignWorkflowPolicy.ResolveAgentQueueStage(campaign);

        Assert.Equal(QueueStages.ClosedProspect, stage);
    }

    [Fact]
    public void GetAgentNextAction_ReturnsClosedProspectGuidance_WhenProspectIsClosed()
    {
        var currentUserId = Guid.NewGuid();
        var campaign = new Campaign
        {
            Status = CampaignStatuses.ReviewReady,
            AssignedAgentUserId = currentUserId,
            ProspectDispositionStatus = ProspectDispositionStatuses.Closed,
            PackageBand = new PackageBand
            {
                Name = "Growth"
            },
            PackageOrder = new PackageOrder
            {
                PaymentProvider = "prospect",
                PaymentStatus = "pending",
                Amount = 50000m
            }
        };

        var nextAction = CampaignWorkflowPolicy.GetAgentNextAction(campaign, QueueStages.ClosedProspect, currentUserId);

        Assert.Contains("Prospect is closed", nextAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildClientWorkflow_AllowsProspectProposalReviewBeforePayment()
    {
        var campaign = CreateProspectCampaign();
        campaign.CampaignRecommendations = new[]
        {
            new CampaignRecommendation
            {
                Status = RecommendationStatuses.SentToClient,
                RecommendationType = "mix:balanced",
                RevisionNumber = 1,
                SentToClientAt = DateTime.UtcNow.AddMinutes(-5),
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        var workflow = CampaignWorkflowPolicy.BuildClientWorkflow(campaign);

        Assert.Equal("recommendation_ready", workflow.CurrentStateKey);
        Assert.False(workflow.PaymentRequiredBeforeApproval);
        Assert.True(workflow.RecommendationAwaitingDecision);
        Assert.Equal("Review proposal", workflow.ActionLabel);
    }

    [Fact]
    public void BuildTimeline_OrdersProspectProposalReviewBeforePayment()
    {
        var campaign = CreateProspectCampaign();
        campaign.CampaignRecommendations = new[]
        {
            new CampaignRecommendation
            {
                Status = RecommendationStatuses.SentToClient,
                RecommendationType = "mix:balanced",
                RevisionNumber = 1,
                SentToClientAt = DateTime.UtcNow.AddMinutes(-5),
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        var timeline = CampaignWorkflowPolicy.BuildTimeline(campaign);

        Assert.Equal(7, timeline.Count);
        Assert.Equal("proposal", timeline[0].Key);
        Assert.Equal(TimelineStates.Complete, timeline[0].State);
        Assert.Equal("review", timeline[1].Key);
        Assert.Equal(TimelineStates.Current, timeline[1].State);
        Assert.Equal("payment", timeline[2].Key);
        Assert.Equal(TimelineStates.Upcoming, timeline[2].State);
    }

    [Fact]
    public void ResolveAgentQueueStage_UsesWaitingOnClientForSentProspectProposal()
    {
        var currentUserId = Guid.NewGuid();
        var campaign = CreateProspectCampaign();
        campaign.AssignedAgentUserId = currentUserId;
        campaign.CampaignRecommendations = new[]
        {
            new CampaignRecommendation
            {
                Status = RecommendationStatuses.SentToClient,
                RecommendationType = "mix:balanced",
                RevisionNumber = 1,
                SentToClientAt = DateTime.UtcNow.AddMinutes(-5),
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        var stage = CampaignWorkflowPolicy.ResolveAgentQueueStage(campaign);
        var nextAction = CampaignWorkflowPolicy.GetAgentNextAction(campaign, stage, currentUserId);

        Assert.Equal(QueueStages.WaitingOnClient, stage);
        Assert.Contains("proposal feedback", nextAction, StringComparison.OrdinalIgnoreCase);
    }

    private static Campaign CreateProspectCampaign()
    {
        return new Campaign
        {
            Status = CampaignStatuses.AwaitingPurchase,
            PackageBand = new PackageBand
            {
                Name = "Scale"
            },
            PackageOrder = new PackageOrder
            {
                PaymentProvider = "prospect",
                PaymentStatus = "pending",
                Amount = 185000m,
                SelectedBudget = 185000m
            },
            ProspectLead = new ProspectLead
            {
                FullName = "Brian Prospect",
                Email = "prospect@example.com",
                Phone = "0821234567",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
    }
}

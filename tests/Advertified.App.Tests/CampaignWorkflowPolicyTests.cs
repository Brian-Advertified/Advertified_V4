using Advertified.App.Data.Entities;
using Advertified.App.Support;

namespace Advertified.App.Tests;

public sealed class CampaignWorkflowPolicyTests
{
    [Fact]
    public void CampaignLifecycleSupport_ReturnsPaymentPending_WhenRecommendationAwaitsDecisionButPaymentIsOutstanding()
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

        var lifecycle = CampaignLifecycleSupport.Build(campaign);

        Assert.Equal("payment_pending", lifecycle.CurrentState);
        Assert.Equal("payment_pending", lifecycle.PaymentState);
        Assert.Equal("sent", lifecycle.ProposalState);
    }

    [Fact]
    public void CampaignLifecycleSupport_ReturnsUnderReview_WhenLulaPaymentIsPending()
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

        var lifecycle = CampaignLifecycleSupport.Build(campaign);

        Assert.Equal("payment_pending", lifecycle.CurrentState);
        Assert.Equal("under_review", lifecycle.PaymentState);
        Assert.Equal("sent", lifecycle.ProposalState);
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
                OrderIntent = OrderIntentValues.Prospect,
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
                OrderIntent = OrderIntentValues.Prospect,
                PaymentStatus = "pending",
                Amount = 50000m
            }
        };

        var nextAction = CampaignWorkflowPolicy.GetAgentNextAction(campaign, QueueStages.ClosedProspect, currentUserId);

        Assert.Contains("Prospect is closed", nextAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CampaignLifecycleSupport_AllowsProspectProposalReviewBeforePayment()
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

        var lifecycle = CampaignLifecycleSupport.Build(campaign);

        Assert.Equal("sent", lifecycle.CurrentState);
        Assert.Equal("not_started", lifecycle.PaymentState);
        Assert.Equal("proposal_sent", lifecycle.CommercialState);
        Assert.Equal("sent", lifecycle.ProposalState);
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
                OrderIntent = OrderIntentValues.Prospect,
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

    [Fact]
    public void ProspectCampaignPolicy_UsesOrderIntentAsPrimarySignal()
    {
        var campaign = new Campaign
        {
            Status = CampaignStatuses.AwaitingPurchase,
            PackageOrder = new PackageOrder
            {
                OrderIntent = OrderIntentValues.Prospect,
                PaymentProvider = "vodapay",
                PaymentStatus = "pending"
            }
        };

        var result = ProspectCampaignPolicy.IsProspectiveCampaign(campaign);

        Assert.True(result);
    }

    [Fact]
    public void CampaignSendValidationSupport_RequiresThreeOohBackedRecommendations()
    {
        var campaign = new Campaign
        {
            Status = CampaignStatuses.PlanningInProgress,
            ProspectLead = new ProspectLead
            {
                FullName = "Brian Prospect",
                Email = "prospect@example.com"
            },
            CampaignRecommendations = new[]
            {
                CreateRecommendation("hybrid:balanced", includeOoh: true),
                CreateRecommendation("hybrid:ooh_focus", includeOoh: false)
            }
        };

        var validation = CampaignSendValidationSupport.Build(campaign);

        Assert.False(validation.CanSendRecommendation);
        Assert.Contains(validation.Reasons, reason => reason.Contains("Three proposal options", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(validation.Reasons, reason => reason.Contains("Proposal 2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CampaignSendValidationSupport_RequiresValidContactEmail()
    {
        var campaign = new Campaign
        {
            Status = CampaignStatuses.PlanningInProgress,
            ProspectLead = new ProspectLead
            {
                FullName = "Brian Prospect",
                Email = "not-an-email"
            },
            CampaignRecommendations = new[]
            {
                CreateRecommendation("hybrid:balanced", includeOoh: true),
                CreateRecommendation("hybrid:ooh_focus", includeOoh: true),
                CreateRecommendation("hybrid:radio_focus", includeOoh: true)
            }
        };

        var validation = CampaignSendValidationSupport.Build(campaign);

        Assert.False(validation.CanSendRecommendation);
        Assert.Contains(validation.Reasons, reason => reason.Contains("email address", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CampaignLifecycleSupport_UsesCanonicalCurrentStateForPaidApprovedCampaign()
    {
        var campaign = new Campaign
        {
            Status = CampaignStatuses.Approved,
            PackageOrder = new PackageOrder
            {
                OrderIntent = OrderIntentValues.Sale,
                PaymentStatus = "paid",
                UpdatedAt = DateTime.UtcNow
            },
            CampaignRecommendations = new[]
            {
                new CampaignRecommendation
                {
                    Status = RecommendationStatuses.Approved,
                    RecommendationType = "hybrid:balanced",
                    RevisionNumber = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            }
        };

        var lifecycle = CampaignLifecycleSupport.Build(campaign);

        Assert.Equal("activation_ready", lifecycle.CurrentState);
        Assert.Equal("paid", lifecycle.PaymentState);
        Assert.Equal("converted", lifecycle.CommercialState);
    }

    private static CampaignRecommendation CreateRecommendation(string recommendationType, bool includeOoh)
    {
        return new CampaignRecommendation
        {
            Id = Guid.NewGuid(),
            RecommendationType = recommendationType,
            Status = RecommendationStatuses.Draft,
            RevisionNumber = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RecommendationItems = includeOoh
                ? new[]
                {
                    new RecommendationItem
                    {
                        Id = Guid.NewGuid(),
                        InventoryType = "billboard",
                        DisplayName = "OOH line"
                    }
                }
                : new[]
                {
                    new RecommendationItem
                    {
                        Id = Guid.NewGuid(),
                        InventoryType = "radio",
                        DisplayName = "Radio line"
                    }
                }
        };
    }
}

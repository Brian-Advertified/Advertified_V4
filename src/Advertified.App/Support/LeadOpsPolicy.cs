using Advertified.App.Contracts.Leads;
using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

public static class LeadProspectQualificationReasons
{
    public const string RealContact = "real_contact";
    public const string HumanEngagement = "human_engagement";
    public const string AgentDecision = "agent_decision";
}

public static class UnifiedLifecycleStages
{
    public const string New = "new";
    public const string Qualified = "qualified";
    public const string ActionRequired = "action_required";
    public const string Engaged = "engaged";
    public const string ProspectCreated = "prospect_created";
    public const string ProposalInProgress = "proposal_in_progress";
    public const string AwaitingClient = "awaiting_client";
    public const string Won = "won";
    public const string Lost = "lost";
    public const string Dormant = "dormant";
}

public static class LeadOpsPolicy
{
    private static readonly HashSet<string> HumanEngagementTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "reply",
        "email_reply",
        "whatsapp_reply",
        "phone_call",
        "call",
        "meeting",
        "demo"
    };

    public static bool HasHumanEngagement(IEnumerable<LeadInteraction> interactions)
    {
        return interactions.Any(interaction => HumanEngagementTypes.Contains(interaction.InteractionType ?? string.Empty));
    }

    public static bool HasRealContact(string? email, string? phone)
    {
        return !string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(phone);
    }

    public static void ValidateConversionRequest(
        ConvertLeadToProspectRequest request,
        IReadOnlyCollection<LeadInteraction> interactions)
    {
        var reason = (request.QualificationReason ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("A qualification reason is required to convert this lead to a prospect.");
        }

        if (string.Equals(reason, LeadProspectQualificationReasons.RealContact, StringComparison.OrdinalIgnoreCase)
            && !HasRealContact(request.Email, request.Phone))
        {
            throw new InvalidOperationException("Real contact conversion requires an email address or phone number.");
        }

        if (string.Equals(reason, LeadProspectQualificationReasons.HumanEngagement, StringComparison.OrdinalIgnoreCase)
            && !HasHumanEngagement(interactions))
        {
            throw new InvalidOperationException("Human engagement conversion requires a recorded reply, call, meeting, or similar interaction.");
        }

        if (!string.Equals(reason, LeadProspectQualificationReasons.RealContact, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(reason, LeadProspectQualificationReasons.HumanEngagement, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(reason, LeadProspectQualificationReasons.AgentDecision, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Qualification reason must be real_contact, human_engagement, or agent_decision.");
        }
    }

    public static string ResolveUnifiedLifecycleStage(Campaign? campaign, bool hasProspect, bool hasHumanEngagement, bool hasOpenActions)
    {
        if (campaign is not null)
        {
            if (ProspectCampaignPolicy.IsClosed(campaign))
            {
                return UnifiedLifecycleStages.Lost;
            }

            if (string.Equals(campaign.PackageOrder?.PaymentStatus, CampaignStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            {
                return UnifiedLifecycleStages.Won;
            }

            if (campaign.Status is CampaignStatuses.PlanningInProgress or CampaignStatuses.ReviewReady or CampaignStatuses.Approved)
            {
                return UnifiedLifecycleStages.ProposalInProgress;
            }

            if (campaign.Status is CampaignStatuses.CreativeSentToClientForApproval or CampaignStatuses.CreativeChangesRequested)
            {
                return UnifiedLifecycleStages.AwaitingClient;
            }

            return UnifiedLifecycleStages.ProspectCreated;
        }

        if (hasProspect)
        {
            return UnifiedLifecycleStages.ProspectCreated;
        }

        if (hasHumanEngagement)
        {
            return UnifiedLifecycleStages.Engaged;
        }

        if (hasOpenActions)
        {
            return UnifiedLifecycleStages.ActionRequired;
        }

        return UnifiedLifecycleStages.Qualified;
    }
}

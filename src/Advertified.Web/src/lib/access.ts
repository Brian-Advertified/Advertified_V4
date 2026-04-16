import type { Campaign, SessionUser } from '../types/domain';
import {
  CAMPAIGN_STATUSES_AFTER_PAYMENT,
  CAMPAIGN_STATUSES_AFTER_RECOMMENDATION_APPROVAL,
  CAMPAIGN_STATUSES_WITH_BRIEF_ACCESS,
  CAMPAIGN_STATUSES_WITH_PLANNING_ACCESS,
  CAMPAIGN_STATUSES_WITH_RECOMMENDATION_WORKSPACE,
  getPrimaryRecommendation,
  hasRecommendationApprovalCompleted,
  isCampaignInSet,
} from './campaignStatus';

export type PackagePurchaseRestriction = 'none' | 'email_unverified' | 'identity_incomplete';
export type ClientCampaignStateKey =
  | 'payment_required'
  | 'payment_under_review'
  | 'brief_in_progress'
  | 'planning_in_progress'
  | 'recommendation_ready'
  | 'recommendation_approved'
  | 'creative_review'
  | 'creative_revision'
  | 'booking_in_progress'
  | 'live';

export type ClientCampaignState = {
  key: ClientCampaignStateKey;
  statusLabel: string;
  headline: string;
  description: string;
  nextStep: string;
  requiresClientAction: boolean;
  actionLabel: string;
};

type CampaignPaymentState = 'cleared' | 'manual_review' | 'payment_required';

export function isAgent(user: SessionUser | null) {
  return user?.role === 'agent';
}

export function isCreativeDirector(user: SessionUser | null) {
  return user?.role === 'creative_director';
}

export function isAdmin(user: SessionUser | null) {
  return user?.role === 'admin';
}

export function canAccessOperations(user: SessionUser | null) {
  return isAgent(user) || isAdmin(user);
}

export function canAccessCreativeStudio(user: SessionUser | null) {
  return isCreativeDirector(user) || isAdmin(user);
}

export function canBuyPackage(user: SessionUser | null) {
  return getPackagePurchaseRestriction(user) === 'none';
}

export function getPackagePurchaseRestriction(user: SessionUser | null): PackagePurchaseRestriction {
  if (!user) {
    return 'email_unverified';
  }

  if (isAdmin(user) || isAgent(user) || isCreativeDirector(user)) {
    return 'none';
  }

  if (!user.emailVerified) {
    return 'email_unverified';
  }

  if (!user.identityComplete) {
    return 'identity_incomplete';
  }

  return 'none';
}

export function canOpenBrief(campaign?: Campaign | null) {
  return Boolean(campaign?.workflow?.canOpenBrief ?? (
    campaign &&
      isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_WITH_BRIEF_ACCESS)
  ));
}

export function canOpenPlanning(campaign?: Campaign | null) {
  return Boolean(campaign?.workflow?.canOpenPlanning ?? (
    campaign &&
      campaign.aiUnlocked &&
      isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_WITH_PLANNING_ACCESS)
  ));
}

export function isPaymentAwaitingManualReview(
  paymentProvider?: string | null,
  paymentStatus?: string | null,
) {
  return String(paymentProvider ?? '').toLowerCase() === 'lula'
    && String(paymentStatus ?? '').toLowerCase() === 'pending';
}

export function hasCampaignClearedPayment(campaign?: Campaign | null) {
  if (campaign?.workflow) {
    return campaign.workflow.hasClearedPayment;
  }

  return Boolean(
    campaign
      && (
        campaign.paymentStatus === 'paid'
        || isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_AFTER_PAYMENT)
      ),
  );
}

export function getCampaignPaymentState(campaign?: Campaign | null): CampaignPaymentState {
  if (!campaign) {
    return 'payment_required';
  }

  if (campaign.workflow?.paymentState === 'manual_review') {
    return 'manual_review';
  }

  if (campaign.workflow?.paymentState === 'payment_required') {
    return 'payment_required';
  }

  if (campaign.workflow?.paymentState === 'cleared') {
    return 'cleared';
  }

  if (hasCampaignClearedPayment(campaign)) {
    return 'cleared';
  }

  if (isPaymentAwaitingManualReview(campaign.paymentProvider, campaign.paymentStatus)) {
    return 'manual_review';
  }

  return 'payment_required';
}

export function campaignNeedsCheckout(campaign?: Campaign | null) {
  return getCampaignPaymentState(campaign) === 'payment_required';
}

export function getClientCampaignState(campaign: Campaign): ClientCampaignState {
  if (campaign.workflow) {
    return {
      key: campaign.workflow.currentStateKey as ClientCampaignStateKey,
      statusLabel: campaign.workflow.statusLabel,
      headline: campaign.workflow.headline,
      description: campaign.workflow.description,
      nextStep: campaign.workflow.nextStep,
      requiresClientAction: campaign.workflow.requiresClientAction,
      actionLabel: campaign.workflow.actionLabel,
    };
  }

  const recommendation = getPrimaryRecommendation(campaign);
  const recommendationApproved = hasRecommendationApprovalCompleted(campaign.status, recommendation, campaign.workflow);
  const recommendationAwaitingDecision = recommendation?.status === 'sent_to_client';
  const paymentState = getCampaignPaymentState(campaign);
  const paymentReview = paymentState === 'manual_review';
  const paymentRequired = paymentState === 'payment_required' && !recommendationApproved;

  if (paymentReview) {
    return {
      key: 'payment_under_review',
      statusLabel: 'Pay Later under review',
      headline: 'Your Pay Later application is under review',
      description: 'Your Finance Partner application has already been submitted. You do not need to pay again or approve anything while this review is pending.',
      nextStep: 'We will update this workspace once the review outcome is confirmed.',
      requiresClientAction: false,
      actionLabel: 'View status',
    };
  }

  if (paymentRequired) {
    return {
      key: 'payment_required',
      statusLabel: 'Payment required',
      headline: 'Payment is still required',
      description: recommendationAwaitingDecision
        ? 'Your recommendation is ready, but payment still needs to be completed before approval can continue.'
        : 'Complete payment to unlock the next step for this campaign.',
      nextStep: 'Finish payment to continue into recommendation review.',
      requiresClientAction: true,
      actionLabel: 'Complete payment',
    };
  }

  if (campaign.status === 'paid' || campaign.status === 'brief_in_progress') {
    return {
      key: 'brief_in_progress',
      statusLabel: 'Brief in progress',
      headline: 'Your campaign brief is the next step',
      description: 'Your package is paid and the campaign is ready for detail capture or review.',
      nextStep: 'Open the campaign workspace to continue the brief.',
      requiresClientAction: true,
      actionLabel: 'Open campaign workspace',
    };
  }

  if (campaign.status === 'brief_submitted' || (campaign.status === 'planning_in_progress' && !recommendationAwaitingDecision)) {
    return {
      key: 'planning_in_progress',
      statusLabel: 'Planning in progress',
      headline: 'We are preparing your recommendation',
      description: 'Advertified is reviewing your brief and shaping the best route forward.',
      nextStep: 'We will bring the recommendation here once it is ready.',
      requiresClientAction: false,
      actionLabel: 'View campaign status',
    };
  }

  if ((campaign.status === 'review_ready' || recommendationAwaitingDecision) && !recommendationApproved) {
    return {
      key: 'recommendation_ready',
      statusLabel: 'Needs approval',
      headline: 'Your recommendation is ready to review',
      description: 'Review the recommended media plan and approve it so Advertified can continue.',
      nextStep: 'Approve the recommendation or send it back with notes.',
      requiresClientAction: true,
      actionLabel: 'Review recommendation',
    };
  }

  if (campaign.status === 'creative_sent_to_client_for_approval') {
    return {
      key: 'creative_review',
      statusLabel: 'Content approval needed',
      headline: 'Approve your campaign content',
      description: 'Your campaign content is ready. Approve it so booking can begin, or send it back with revision notes.',
      nextStep: 'Approve the content or request changes.',
      requiresClientAction: true,
      actionLabel: 'Approve content',
    };
  }

  if (campaign.status === 'creative_changes_requested') {
    return {
      key: 'creative_revision',
      statusLabel: 'Revision in progress',
      headline: 'Creative revisions are in progress',
      description: 'Your feedback has been sent back to the team and the revised creative handoff is being prepared.',
      nextStep: 'We will return the next approval here once the revision is ready.',
      requiresClientAction: false,
      actionLabel: 'View campaign status',
    };
  }

  if (campaign.status === 'booking_in_progress' || campaign.status === 'creative_approved') {
    return {
      key: 'booking_in_progress',
      statusLabel: campaign.status === 'creative_approved' ? 'Approved for booking' : 'Booking in progress',
      headline: campaign.status === 'creative_approved'
        ? 'Creative approval is complete'
        : 'We are booking your campaign now',
      description: campaign.status === 'creative_approved'
        ? 'Your final content is approved and our team is moving into booking and launch preparation.'
        : 'Placements, live dates, and supplier readiness are being confirmed before launch.',
      nextStep: 'There is nothing you need to do right now.',
      requiresClientAction: false,
      actionLabel: 'View campaign progress',
    };
  }

  if (campaign.status === 'launched') {
    return {
      key: 'live',
      statusLabel: 'Campaign live',
      headline: 'Your campaign is now live',
      description: 'Operations has activated the campaign and it is now running.',
      nextStep: 'Use this workspace for updates, reports, and support.',
      requiresClientAction: false,
      actionLabel: 'Review live status',
    };
  }

  return {
    key: 'recommendation_approved',
    statusLabel: 'All set for now',
    headline: 'Your campaign is moving forward',
    description: 'Your recommendation has been approved and Advertified is handling the next production step.',
    nextStep: campaign.nextAction,
    requiresClientAction: false,
    actionLabel: 'View campaign progress',
  };
}

export function getCampaignPrimaryAction(campaign: Campaign) {
  const clientState = getClientCampaignState(campaign);
  const primaryRecommendation = getPrimaryRecommendation(campaign);
  const hasRecommendation = Boolean(primaryRecommendation);
  const selectedRecommendationId = primaryRecommendation?.id;
  const paymentRequiredBeforeApproval = campaign.workflow?.paymentRequiredBeforeApproval
    ?? (getCampaignPaymentState(campaign) === 'payment_required'
      && (campaign.status === 'review_ready' || campaign.status === 'planning_in_progress'));

  if (campaign.status === 'paid' || campaign.status === 'brief_in_progress') {
    return {
      href: `/campaigns/${campaign.id}`,
      label: clientState.actionLabel,
      description: clientState.description,
      stepLabel: 'Open workspace',
    };
  }

  if (campaign.status === 'brief_submitted' || (campaign.status === 'planning_in_progress' && !campaign.planningMode)) {
    return {
      href: `/campaigns/${campaign.id}`,
      label: clientState.actionLabel,
      description: clientState.description,
      stepLabel: 'Open workspace',
    };
  }

  if (isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_WITH_RECOMMENDATION_WORKSPACE) && hasRecommendation) {
    const recommendationApproved = isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_AFTER_RECOMMENDATION_APPROVAL);
    const href = paymentRequiredBeforeApproval
      ? `/checkout/payment?orderId=${encodeURIComponent(campaign.packageOrderId)}&campaignId=${encodeURIComponent(campaign.id)}${selectedRecommendationId ? `&recommendationId=${encodeURIComponent(selectedRecommendationId)}` : ''}`
      : `/campaigns/${campaign.id}`;

    return {
      href,
      label: paymentRequiredBeforeApproval ? 'Complete payment' : clientState.actionLabel,
      description: clientState.description,
      stepLabel: paymentRequiredBeforeApproval
        ? 'Payment required'
        : recommendationApproved
          ? 'Open workspace'
          : 'Needs action',
    };
  }

  return {
    href: `/campaigns/${campaign.id}`,
    label: 'Open campaign',
    description: 'Review your campaign progress and next action.',
    stepLabel: 'Continue',
  };
}

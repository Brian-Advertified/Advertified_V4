import type { Campaign, SessionUser } from '../types/domain';
import {
  CAMPAIGN_STATUSES_AFTER_PAYMENT,
  CAMPAIGN_STATUSES_AFTER_RECOMMENDATION_APPROVAL,
  CAMPAIGN_STATUSES_WITH_BRIEF_ACCESS,
  CAMPAIGN_STATUSES_WITH_PLANNING_ACCESS,
  CAMPAIGN_STATUSES_WITH_RECOMMENDATION_WORKSPACE,
  getPrimaryRecommendation,
  isCampaignInSet,
} from './campaignStatus';

export type PackagePurchaseRestriction = 'none' | 'email_unverified' | 'identity_incomplete';

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
  return Boolean(
    campaign &&
      isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_WITH_BRIEF_ACCESS),
  );
}

export function canOpenPlanning(campaign?: Campaign | null) {
  return Boolean(
      campaign &&
      campaign.aiUnlocked &&
      isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_WITH_PLANNING_ACCESS),
  );
}

export function isPaymentAwaitingManualReview(
  paymentProvider?: string | null,
  paymentStatus?: string | null,
) {
  return String(paymentProvider ?? '').toLowerCase() === 'lula'
    && String(paymentStatus ?? '').toLowerCase() === 'pending';
}

export function hasCampaignClearedPayment(campaign?: Campaign | null) {
  return Boolean(
    campaign
      && (
        campaign.paymentStatus === 'paid'
        || isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_AFTER_PAYMENT)
      ),
  );
}

export function campaignNeedsCheckout(campaign?: Campaign | null) {
  return Boolean(
    campaign
    && !hasCampaignClearedPayment(campaign)
    && !isPaymentAwaitingManualReview(campaign.paymentProvider, campaign.paymentStatus),
  );
}

export function getCampaignPrimaryAction(campaign: Campaign) {
  const primaryRecommendation = getPrimaryRecommendation(campaign);
  const hasRecommendation = Boolean(primaryRecommendation);
  const selectedRecommendationId = primaryRecommendation?.id;
  const paymentRequiredBeforeApproval =
    campaignNeedsCheckout(campaign)
    && (campaign.status === 'review_ready' || campaign.status === 'planning_in_progress');

  if (campaign.status === 'paid' || campaign.status === 'brief_in_progress') {
    return {
      href: `/campaigns/${campaign.id}`,
      label: 'Open campaign workspace',
      description: 'Your package is paid and the campaign workspace shows the current next step.',
      stepLabel: 'Open workspace',
    };
  }

  if (campaign.status === 'brief_submitted' || (campaign.status === 'planning_in_progress' && !campaign.planningMode)) {
    return {
      href: `/campaigns/${campaign.id}`,
      label: 'Open campaign workspace',
      description: 'The campaign workspace now keeps the client focused on the current approval state, not setup screens.',
      stepLabel: 'Open workspace',
    };
  }

  if (isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_WITH_RECOMMENDATION_WORKSPACE) && hasRecommendation) {
    const recommendationApproved = isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_AFTER_RECOMMENDATION_APPROVAL);
    const href = paymentRequiredBeforeApproval
      ? `/checkout/payment?orderId=${encodeURIComponent(campaign.packageOrderId)}&campaignId=${encodeURIComponent(campaign.id)}${selectedRecommendationId ? `&recommendationId=${encodeURIComponent(selectedRecommendationId)}` : ''}`
      : `/campaigns/${campaign.id}`;
    const label = paymentRequiredBeforeApproval
      ? 'Complete payment'
      : recommendationApproved
        ? 'Open campaign workspace'
        : 'Review recommendation';
    const description = paymentRequiredBeforeApproval
      ? 'Payment is still required before you can approve this recommendation and move into production.'
      : recommendationApproved
        ? 'See the approved recommendation and current campaign approval state in one workspace.'
        : 'Review the draft recommendation, then approve it or request changes from the same workspace.';
    const stepLabel = paymentRequiredBeforeApproval
      ? 'Payment required'
      : recommendationApproved
        ? 'Open workspace'
        : 'Needs action';

    return {
      href,
      label,
      description,
      stepLabel,
    };
  }

  return {
    href: `/campaigns/${campaign.id}`,
    label: 'Open campaign',
    description: 'Review your campaign progress and next action.',
    stepLabel: 'Continue',
  };
}

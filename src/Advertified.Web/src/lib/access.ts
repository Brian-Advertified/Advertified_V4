import type { Campaign, SessionUser } from '../types/domain';

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
      [
        'paid',
        'brief_in_progress',
        'brief_submitted',
        'planning_in_progress',
        'review_ready',
        'approved',
        'creative_sent_to_client_for_approval',
        'creative_changes_requested',
        'creative_approved',
        'booking_in_progress',
        'launched',
      ].includes(campaign.status),
  );
}

export function canOpenPlanning(campaign?: Campaign | null) {
  return Boolean(
      campaign &&
      campaign.aiUnlocked &&
      ['brief_submitted', 'planning_in_progress', 'review_ready', 'approved', 'creative_sent_to_client_for_approval', 'creative_changes_requested', 'creative_approved', 'booking_in_progress', 'launched'].includes(campaign.status),
  );
}

export function getCampaignPrimaryAction(campaign: Campaign) {
  const hasRecommendation = Boolean(campaign.recommendation);
  const selectedRecommendationId = campaign.recommendations.find((item) => item.status === 'approved')?.id
    ?? campaign.recommendations.find((item) => item.status === 'sent_to_client')?.id
    ?? campaign.recommendation?.id;
  const paymentRequiredBeforeApproval =
    campaign.paymentStatus !== 'paid'
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

  if ((campaign.status === 'planning_in_progress' || campaign.status === 'review_ready' || campaign.status === 'approved' || campaign.status === 'creative_sent_to_client_for_approval' || campaign.status === 'creative_changes_requested' || campaign.status === 'creative_approved' || campaign.status === 'booking_in_progress' || campaign.status === 'launched') && hasRecommendation) {
    return {
      href: paymentRequiredBeforeApproval
        ? `/checkout/payment?orderId=${encodeURIComponent(campaign.packageOrderId)}&campaignId=${encodeURIComponent(campaign.id)}${selectedRecommendationId ? `&recommendationId=${encodeURIComponent(selectedRecommendationId)}` : ''}`
        : `/campaigns/${campaign.id}`,
      label: paymentRequiredBeforeApproval
        ? 'Complete payment'
        : campaign.status === 'approved' || campaign.status === 'creative_sent_to_client_for_approval' || campaign.status === 'creative_changes_requested' || campaign.status === 'creative_approved' || campaign.status === 'booking_in_progress' || campaign.status === 'launched'
          ? 'Open campaign workspace'
          : 'Review recommendation',
      description: paymentRequiredBeforeApproval
        ? 'Payment is still required before you can approve this recommendation and move into production.'
        : campaign.status === 'approved' || campaign.status === 'creative_sent_to_client_for_approval' || campaign.status === 'creative_changes_requested' || campaign.status === 'creative_approved' || campaign.status === 'booking_in_progress' || campaign.status === 'launched'
        ? 'See the approved recommendation and current campaign approval state in one workspace.'
        : 'Review the draft recommendation, then approve it or request changes from the same workspace.',
      stepLabel: paymentRequiredBeforeApproval
        ? 'Payment required'
        : campaign.status === 'approved' || campaign.status === 'creative_sent_to_client_for_approval' || campaign.status === 'creative_changes_requested' || campaign.status === 'creative_approved' || campaign.status === 'booking_in_progress' || campaign.status === 'launched'
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

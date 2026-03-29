import type { Campaign, SessionUser } from '../types/domain';

export function isAgent(user: SessionUser | null) {
  return user?.role === 'agent';
}

export function isAdmin(user: SessionUser | null) {
  return user?.role === 'admin';
}

export function canAccessOperations(user: SessionUser | null) {
  return isAgent(user) || isAdmin(user);
}

export function canBuyPackage(user: SessionUser | null) {
  return Boolean(user && user.emailVerified);
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
      ].includes(campaign.status),
  );
}

export function canOpenPlanning(campaign?: Campaign | null) {
  return Boolean(
    campaign &&
      campaign.aiUnlocked &&
      ['brief_submitted', 'planning_in_progress', 'review_ready', 'approved'].includes(campaign.status),
  );
}

export function getCampaignPrimaryAction(campaign: Campaign) {
  const hasRecommendation = Boolean(campaign.recommendation);

  if (campaign.status === 'paid' || campaign.status === 'brief_in_progress') {
    return {
      href: `/campaigns/${campaign.id}/brief`,
      label: 'Complete campaign brief',
      description: 'Tell us about your audience, geography, and channels so planning can begin.',
      stepLabel: 'Step 1 of 3',
    };
  }

  if (campaign.status === 'brief_submitted' || (campaign.status === 'planning_in_progress' && !campaign.planningMode)) {
    return {
      href: `/campaigns/${campaign.id}/planning`,
      label: 'Choose planning mode',
      description: 'Pick AI-assisted, agent-assisted, or hybrid planning for this campaign.',
      stepLabel: 'Step 2 of 3',
    };
  }

  if ((campaign.status === 'planning_in_progress' || campaign.status === 'review_ready' || campaign.status === 'approved') && hasRecommendation) {
    return {
      href: `/campaigns/${campaign.id}/review`,
      label: campaign.status === 'approved' ? 'View approved recommendation' : 'Review recommendation',
      description: campaign.status === 'approved'
        ? 'See the approved recommendation and the next activation-ready details.'
        : 'Review the draft recommendation, then approve it or request changes.',
      stepLabel: campaign.status === 'approved' ? 'Completed' : 'Step 3 of 3',
    };
  }

  return {
    href: `/campaigns/${campaign.id}`,
    label: 'Open campaign',
    description: 'Review your campaign progress and next action.',
    stepLabel: 'Continue',
  };
}

import type { Campaign, SessionUser } from '../types/domain';

export function isAgent(user: SessionUser | null) {
  return user?.role === 'agent';
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

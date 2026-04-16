import type { Campaign, CampaignRecommendation, CampaignStatus } from '../types/domain';

export const CAMPAIGN_STATUSES_AFTER_PAYMENT: readonly CampaignStatus[] = [
  'approved',
  'creative_sent_to_client_for_approval',
  'creative_changes_requested',
  'creative_approved',
  'booking_in_progress',
  'launched',
];

export const CAMPAIGN_STATUSES_WITH_RECOMMENDATION_WORKSPACE: readonly CampaignStatus[] = [
  'planning_in_progress',
  'review_ready',
  'approved',
  'creative_sent_to_client_for_approval',
  'creative_changes_requested',
  'creative_approved',
  'booking_in_progress',
  'launched',
];

export const CAMPAIGN_STATUSES_WITH_PLANNING_ACCESS: readonly CampaignStatus[] = [
  'brief_submitted',
  'planning_in_progress',
  'review_ready',
  'approved',
  'creative_sent_to_client_for_approval',
  'creative_changes_requested',
  'creative_approved',
  'booking_in_progress',
  'launched',
];

export const CAMPAIGN_STATUSES_WITH_BRIEF_ACCESS: readonly CampaignStatus[] = [
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
];

export const CAMPAIGN_STATUSES_AFTER_RECOMMENDATION_APPROVAL = CAMPAIGN_STATUSES_AFTER_PAYMENT;

export const AI_STUDIO_READY_STATUSES = CAMPAIGN_STATUSES_AFTER_RECOMMENDATION_APPROVAL;

export const CAMPAIGN_STATUSES_AFTER_CREATIVE_APPROVAL: readonly CampaignStatus[] = [
  'creative_approved',
  'booking_in_progress',
  'launched',
];

export function isCampaignInSet(
  status: CampaignStatus | null | undefined,
  statuses: readonly CampaignStatus[],
): boolean {
  return Boolean(status && statuses.includes(status));
}

export function isRecommendationApproved(recommendation?: CampaignRecommendation | null): boolean {
  return recommendation?.status === 'approved';
}

export function hasRecommendationApprovalCompleted(
  campaignStatus: CampaignStatus | null | undefined,
  recommendation?: CampaignRecommendation | null,
  workflow?: Campaign['workflow'],
): boolean {
  if (workflow) {
    return workflow.recommendationApprovalCompleted;
  }

  return isRecommendationApproved(recommendation)
    || isCampaignInSet(campaignStatus, CAMPAIGN_STATUSES_AFTER_RECOMMENDATION_APPROVAL);
}

export function getPrimaryRecommendation(campaign: Campaign): CampaignRecommendation | undefined {
  return campaign.recommendations.find((item) => item.status === 'approved')
    ?? campaign.recommendations.find((item) => item.status === 'sent_to_client')
    ?? campaign.recommendations[0]
    ?? campaign.recommendation;
}

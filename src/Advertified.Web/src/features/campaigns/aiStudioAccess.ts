import type { CampaignStatus } from '../../types/domain';

const AI_STUDIO_READY_STATUSES: CampaignStatus[] = [
  'approved',
  'creative_changes_requested',
  'creative_sent_to_client_for_approval',
  'creative_approved',
  'booking_in_progress',
  'launched',
];

export function canAccessAiStudioForStatus(status: string | null | undefined): boolean {
  if (!status) {
    return false;
  }

  return AI_STUDIO_READY_STATUSES.includes(status as CampaignStatus);
}

export function getAiStudioAccessMessage(status: string | null | undefined): string {
  if (canAccessAiStudioForStatus(status)) {
    return 'AI Studio is available for this campaign.';
  }

  return 'AI Studio becomes available once the recommendation is approved and the campaign moves into content production.';
}

import type { CampaignStatus } from '../../types/domain';

const AI_STUDIO_READY_STATUSES: CampaignStatus[] = ['creative_approved', 'launched'];

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

  return 'AI Studio becomes available only after a purchased campaign is complete and ready to go live.';
}

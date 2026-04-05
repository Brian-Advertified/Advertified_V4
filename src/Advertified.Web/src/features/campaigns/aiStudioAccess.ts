import type { CampaignStatus } from '../../types/domain';
import { AI_STUDIO_READY_STATUSES, isCampaignInSet } from '../../lib/campaignStatus';

export function canAccessAiStudioForStatus(status: string | null | undefined): boolean {
  return isCampaignInSet(status as CampaignStatus | null | undefined, AI_STUDIO_READY_STATUSES);
}

export function getAiStudioAccessMessage(status: string | null | undefined): string {
  if (canAccessAiStudioForStatus(status)) {
    return 'AI Studio is available for this campaign.';
  }

  return 'AI Studio becomes available once the recommendation is approved and the campaign moves into content production.';
}

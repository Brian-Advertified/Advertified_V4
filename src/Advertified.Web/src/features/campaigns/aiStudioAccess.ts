import type { Campaign, CampaignStatus } from '../../types/domain';
import { AI_STUDIO_READY_STATUSES, isCampaignInSet } from '../../lib/campaignStatus';

export function canAccessAiStudioForStatus(status: string | null | undefined): boolean {
  return isCampaignInSet(status as CampaignStatus | null | undefined, AI_STUDIO_READY_STATUSES);
}

export function canAccessAiStudio(campaign?: Pick<Campaign, 'status' | 'lifecycle'> | null): boolean {
  if (campaign?.lifecycle) {
    return campaign.lifecycle.aiStudioAccessState !== 'locked';
  }

  return canAccessAiStudioForStatus(campaign?.status);
}

export function getAiStudioAccessMessage(campaign?: Pick<Campaign, 'status' | 'lifecycle'> | null): string {
  if (canAccessAiStudio(campaign)) {
    return 'AI Studio is available for this campaign.';
  }

  return 'AI Studio becomes available once the recommendation is approved and the campaign moves into content production.';
}

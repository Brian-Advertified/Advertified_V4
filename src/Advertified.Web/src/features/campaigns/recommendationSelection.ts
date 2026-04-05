import type { Campaign, CampaignRecommendation } from '../../types/domain';

type RecommendationCarrier = Pick<Campaign, 'recommendations' | 'recommendation'>;

export function getCampaignRecommendations(campaign?: RecommendationCarrier | null): CampaignRecommendation[] {
  if (!campaign) {
    return [];
  }

  return campaign.recommendations.length > 0
    ? campaign.recommendations
    : (campaign.recommendation ? [campaign.recommendation] : []);
}

export function resolveRecommendationId(
  recommendations: CampaignRecommendation[],
  options: {
    currentSelectionId?: string;
    requestedRecommendationId?: string;
    preferredRecommendationId?: string;
  } = {},
): string {
  const { currentSelectionId = '', requestedRecommendationId = '', preferredRecommendationId = '' } = options;

  if (recommendations.some((item) => item.id === currentSelectionId)) {
    return currentSelectionId;
  }

  if (recommendations.some((item) => item.id === preferredRecommendationId)) {
    return preferredRecommendationId;
  }

  if (recommendations.some((item) => item.id === requestedRecommendationId)) {
    return requestedRecommendationId;
  }

  return recommendations.find((item) => item.status === 'sent_to_client')?.id ?? recommendations[0]?.id ?? '';
}

export function selectRecommendation(
  recommendations: CampaignRecommendation[],
  options: Parameters<typeof resolveRecommendationId>[1] = {},
): CampaignRecommendation | undefined {
  const resolvedId = resolveRecommendationId(recommendations, options);
  return recommendations.find((item) => item.id === resolvedId) ?? recommendations[0];
}

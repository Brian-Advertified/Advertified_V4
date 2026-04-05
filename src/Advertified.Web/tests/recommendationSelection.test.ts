import { describe, expect, it } from 'vitest';
import type { CampaignRecommendation } from '../src/types/domain';
import { getCampaignRecommendations, resolveRecommendationId, selectRecommendation } from '../src/features/campaigns/recommendationSelection';

function buildRecommendation(id: string, status: CampaignRecommendation['status']): CampaignRecommendation {
  return {
    id,
    campaignId: 'campaign-1',
    summary: `Recommendation ${id}`,
    rationale: 'Rationale',
    manualReviewRequired: false,
    fallbackFlags: [],
    status,
    totalCost: 250000,
    items: [],
  };
}

describe('recommendation selection helpers', () => {
  it('normalizes singular recommendation payloads into an array', () => {
    const recommendation = buildRecommendation('rec-1', 'draft');

    expect(getCampaignRecommendations({ recommendations: [], recommendation })).toEqual([recommendation]);
  });

  it('prefers current selection, then preferred, then requested, then sent-to-client', () => {
    const draft = buildRecommendation('draft', 'draft');
    const sent = buildRecommendation('sent', 'sent_to_client');
    const approved = buildRecommendation('approved', 'approved');
    const recommendations = [draft, sent, approved];

    expect(resolveRecommendationId(recommendations, { currentSelectionId: 'sent', preferredRecommendationId: 'approved' })).toBe('sent');
    expect(resolveRecommendationId(recommendations, { preferredRecommendationId: 'approved', requestedRecommendationId: 'draft' })).toBe('approved');
    expect(resolveRecommendationId(recommendations, { requestedRecommendationId: 'draft' })).toBe('draft');
    expect(resolveRecommendationId(recommendations)).toBe('sent');
  });

  it('returns the selected recommendation object using the shared fallback order', () => {
    const draft = buildRecommendation('draft', 'draft');
    const sent = buildRecommendation('sent', 'sent_to_client');

    expect(selectRecommendation([draft, sent])?.id).toBe('sent');
  });
});

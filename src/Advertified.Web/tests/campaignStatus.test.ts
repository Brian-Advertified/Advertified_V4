import { describe, expect, it } from 'vitest';
import type { Campaign, CampaignRecommendation } from '../src/types/domain';
import { getPrimaryRecommendation, hasRecommendationApprovalCompleted } from '../src/lib/campaignStatus';

function buildRecommendation(
  id: string,
  status: CampaignRecommendation['status'],
): CampaignRecommendation {
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

function buildCampaign(status: Campaign['status'], recommendations: CampaignRecommendation[] = []): Campaign {
  return {
    id: 'campaign-1',
    userId: 'user-1',
    packageOrderId: 'order-1',
    packageBandId: 'band-1',
    packageBandName: 'Scale',
    selectedBudget: 250000,
    paymentStatus: 'paid',
    status,
    aiUnlocked: true,
    agentAssistanceRequested: false,
    campaignName: 'Retail Launch',
    nextAction: 'Next step',
    timeline: [],
    recommendations,
    recommendation: recommendations[0],
    creativeSystems: [],
    assets: [],
    supplierBookings: [],
    deliveryReports: [],
    createdAt: '2026-03-30T00:00:00Z',
  };
}

describe('campaign status helpers', () => {
  it('prefers approved recommendations over sent and fallback recommendations', () => {
    const sent = buildRecommendation('rec-sent', 'sent_to_client');
    const approved = buildRecommendation('rec-approved', 'approved');
    const draft = buildRecommendation('rec-draft', 'draft');
    const campaign = buildCampaign('approved', [draft, sent, approved]);

    expect(getPrimaryRecommendation(campaign)?.id).toBe('rec-approved');
  });

  it('treats creative-stage campaign statuses as completed recommendation approval', () => {
    expect(hasRecommendationApprovalCompleted('creative_sent_to_client_for_approval')).toBe(true);
    expect(hasRecommendationApprovalCompleted('booking_in_progress')).toBe(true);
    expect(hasRecommendationApprovalCompleted('review_ready')).toBe(false);
  });

  it('treats an approved recommendation as completed even before campaign status advances', () => {
    const approved = buildRecommendation('rec-approved', 'approved');

    expect(hasRecommendationApprovalCompleted('review_ready', approved)).toBe(true);
  });
});

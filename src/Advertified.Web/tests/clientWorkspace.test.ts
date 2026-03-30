import { describe, expect, it } from 'vitest';
import type { Campaign } from '../src/types/domain';
import { getCampaignProgressPercent, getCampaignQuickSteps } from '../src/pages/client/clientWorkspace';

function buildCampaign(status: Campaign['status']): Campaign {
  return {
    id: 'campaign-1',
    userId: 'user-1',
    packageOrderId: 'order-1',
    packageBandId: 'band-1',
    packageBandName: 'Scale',
    selectedBudget: 250000,
    status,
    aiUnlocked: true,
    agentAssistanceRequested: false,
    campaignName: 'Retail Launch',
    nextAction: 'Next step',
    timeline: [],
    recommendations: [],
    assets: [],
    supplierBookings: [],
    deliveryReports: [],
    createdAt: '2026-03-30T00:00:00Z',
  };
}

describe('client workspace progress helpers', () => {
  it('keeps creative production upcoming until recommendation approval exists', () => {
    const campaign = {
      ...buildCampaign('review_ready'),
      recommendation: {
        id: 'rec-1',
        campaignId: 'campaign-1',
        summary: 'Review ready',
        rationale: 'Rationale',
        manualReviewRequired: false,
        fallbackFlags: [],
        status: 'sent_to_client' as const,
        totalCost: 250000,
        items: [],
      },
    };

    const steps = getCampaignQuickSteps(campaign);

    expect(steps.find((step) => step.key === 'recommendation')?.state).toBe('current');
    expect(steps.find((step) => step.key === 'creative-production')?.state).toBe('upcoming');
    expect(steps.find((step) => step.key === 'creative-review')?.state).toBe('upcoming');
  });

  it('marks launch readiness only after final creative approval', () => {
    const campaign = buildCampaign('creative_approved');

    const steps = getCampaignQuickSteps(campaign);

    expect(steps.find((step) => step.key === 'creative-review')?.state).toBe('complete');
    expect(steps.find((step) => step.key === 'launch')?.state).toBe('current');
    expect(steps.find((step) => step.key === 'live')?.state).toBe('upcoming');
    expect(getCampaignProgressPercent(campaign)).toBe(98);
  });
});

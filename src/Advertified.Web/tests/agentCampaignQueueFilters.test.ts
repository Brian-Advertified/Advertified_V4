import { describe, expect, it } from 'vitest';
import type { AgentInboxItem } from '../src/types/domain';
import {
  buildAgentCampaignQueueHref,
  buildAgentMessagesHref,
  buildQueueFiltersForInboxItem,
  matchesCampaignOwnership,
  matchesCampaignQueueFocus,
  matchesCampaignQueueStage,
  parseAgentCampaignQueueFilters,
} from '../src/pages/agent/agentCampaignQueueFilters';

function createInboxItem(overrides: Partial<AgentInboxItem> = {}): AgentInboxItem {
  return {
    id: 'campaign-1',
    campaignName: 'Launch campaign',
    clientName: 'Northwind',
    clientEmail: 'hello@northwind.test',
    packageBandId: 'pkg-1',
    packageBandName: 'Growth',
    selectedBudget: 15000,
    paymentStatus: 'paid',
    status: 'paid',
    queueStage: 'planning_ready',
    queueLabel: 'Planning ready',
    isAssignedToCurrentUser: true,
    isUnassigned: false,
    nextAction: 'Build the recommendation.',
    manualReviewRequired: false,
    isOverBudget: false,
    isStale: false,
    isUrgent: false,
    ageInDays: 2,
    hasBrief: true,
    hasRecommendation: false,
    createdAt: '2026-04-18T08:00:00Z',
    updatedAt: '2026-04-18T09:00:00Z',
    ...overrides,
  };
}

describe('agentCampaignQueueFilters', () => {
  it('parses valid filters and falls back for invalid values', () => {
    const filters = parseAgentCampaignQueueFilters(new URLSearchParams('stage=waiting_on_client&ownership=all&focus=budget_issues&q= Acme '));
    const fallbackFilters = parseAgentCampaignQueueFilters(new URLSearchParams('stage=unknown&ownership=oops&focus=bad'));

    expect(filters).toEqual({
      search: 'Acme',
      stage: 'waiting_on_client',
      ownership: 'all',
      focus: 'budget_issues',
    });
    expect(fallbackFilters).toEqual({
      search: '',
      stage: 'all',
      ownership: 'all',
      focus: 'all',
    });
  });

  it('builds compact queue and message hrefs', () => {
    expect(buildAgentCampaignQueueHref()).toBe('/agent/campaigns');
    expect(buildAgentCampaignQueueHref({ stage: 'all', ownership: 'all', focus: 'urgent' })).toBe('/agent/campaigns?focus=urgent');
    expect(buildAgentMessagesHref('campaign-42')).toBe('/agent/messages?campaignId=campaign-42');
  });

  it('matches queue filters without duplicating queue logic in components', () => {
    const urgentReviewItem = createInboxItem({ queueStage: 'agent_review', manualReviewRequired: true, isUrgent: true, isOverBudget: true });
    const waitingItem = createInboxItem({ queueStage: 'waiting_on_client', status: 'review_ready', isAssignedToCurrentUser: false, isUnassigned: true });

    expect(matchesCampaignQueueStage(urgentReviewItem, 'ready_to_work')).toBe(true);
    expect(matchesCampaignQueueStage(waitingItem, 'waiting_on_client')).toBe(true);
    expect(matchesCampaignQueueFocus(urgentReviewItem, 'needs_review')).toBe(true);
    expect(matchesCampaignQueueFocus(urgentReviewItem, 'budget_issues')).toBe(true);
    expect(matchesCampaignOwnership(waitingItem, 'unassigned')).toBe(true);
  });

  it('maps inbox items back to stable queue filters', () => {
    expect(buildQueueFiltersForInboxItem(createInboxItem({ queueStage: 'newly_paid' }))).toEqual({
      stage: 'ready_to_work',
      ownership: 'all',
      focus: 'newly_paid',
    });
    expect(buildQueueFiltersForInboxItem(createInboxItem({ status: 'awaiting_purchase', queueStage: 'watching' }))).toEqual({
      stage: 'prospects',
    });
  });
});

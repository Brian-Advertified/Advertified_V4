import type { AgentInboxItem } from '../../types/domain';

export type CampaignQueueStageFilter = 'all' | 'ready_to_work' | 'waiting_on_client' | 'prospects' | 'completed';
export type CampaignOwnershipFilter = 'all' | 'assigned_to_me' | 'unassigned';
export type CampaignQueueFocusFilter =
  | 'all'
  | 'urgent'
  | 'newly_paid'
  | 'brief_waiting'
  | 'planning_ready'
  | 'needs_review'
  | 'budget_issues';

export type AgentCampaignQueueFilters = {
  search: string;
  stage: CampaignQueueStageFilter;
  ownership: CampaignOwnershipFilter;
  focus: CampaignQueueFocusFilter;
};

const campaignQueueStageFilters = new Set<CampaignQueueStageFilter>(['all', 'ready_to_work', 'waiting_on_client', 'prospects', 'completed']);
const campaignOwnershipFilters = new Set<CampaignOwnershipFilter>(['all', 'assigned_to_me', 'unassigned']);
const campaignQueueFocusFilters = new Set<CampaignQueueFocusFilter>(['all', 'urgent', 'newly_paid', 'brief_waiting', 'planning_ready', 'needs_review', 'budget_issues']);

export const defaultAgentCampaignQueueFilters: AgentCampaignQueueFilters = {
  search: '',
  stage: 'ready_to_work',
  ownership: 'assigned_to_me',
  focus: 'all',
};

function readFilterValue<T extends string>(value: string | null, allowed: Set<T>, fallback: T): T {
  return value && allowed.has(value as T) ? value as T : fallback;
}

export function parseAgentCampaignQueueFilters(searchParams: URLSearchParams): AgentCampaignQueueFilters {
  return {
    search: searchParams.get('q')?.trim() ?? defaultAgentCampaignQueueFilters.search,
    stage: readFilterValue(searchParams.get('stage'), campaignQueueStageFilters, defaultAgentCampaignQueueFilters.stage),
    ownership: readFilterValue(searchParams.get('ownership'), campaignOwnershipFilters, defaultAgentCampaignQueueFilters.ownership),
    focus: readFilterValue(searchParams.get('focus'), campaignQueueFocusFilters, defaultAgentCampaignQueueFilters.focus),
  };
}

export function toAgentCampaignQueueSearchParams(filters: AgentCampaignQueueFilters): URLSearchParams {
  const searchParams = new URLSearchParams();

  if (filters.search) {
    searchParams.set('q', filters.search);
  }

  if (filters.stage !== defaultAgentCampaignQueueFilters.stage) {
    searchParams.set('stage', filters.stage);
  }

  if (filters.ownership !== defaultAgentCampaignQueueFilters.ownership) {
    searchParams.set('ownership', filters.ownership);
  }

  if (filters.focus !== defaultAgentCampaignQueueFilters.focus) {
    searchParams.set('focus', filters.focus);
  }

  return searchParams;
}

export function buildAgentCampaignQueueHref(overrides: Partial<AgentCampaignQueueFilters> = {}): string {
  const filters: AgentCampaignQueueFilters = {
    ...defaultAgentCampaignQueueFilters,
    ...overrides,
  };
  const searchParams = toAgentCampaignQueueSearchParams(filters);
  const query = searchParams.toString();
  return query.length > 0 ? `/agent/campaigns?${query}` : '/agent/campaigns';
}

export function matchesCampaignQueueStage(item: AgentInboxItem, stage: CampaignQueueStageFilter) {
  switch (stage) {
    case 'all':
      return true;
    case 'ready_to_work':
      return ['newly_paid', 'brief_waiting', 'planning_ready', 'agent_review'].includes(item.queueStage);
    case 'prospects':
      return item.status === 'awaiting_purchase';
    case 'waiting_on_client':
      return item.queueStage === 'waiting_on_client';
    case 'completed':
      return item.queueStage === 'completed';
    default:
      return true;
  }
}

export function matchesCampaignOwnership(item: AgentInboxItem, ownership: CampaignOwnershipFilter) {
  switch (ownership) {
    case 'all':
      return true;
    case 'assigned_to_me':
      return item.isAssignedToCurrentUser;
    case 'unassigned':
      return item.isUnassigned;
    default:
      return true;
  }
}

export function matchesCampaignQueueFocus(item: AgentInboxItem, focus: CampaignQueueFocusFilter) {
  switch (focus) {
    case 'all':
      return true;
    case 'urgent':
      return item.isUrgent;
    case 'newly_paid':
      return item.queueStage === 'newly_paid';
    case 'brief_waiting':
      return item.queueStage === 'brief_waiting';
    case 'planning_ready':
      return item.queueStage === 'planning_ready';
    case 'needs_review':
      return item.queueStage === 'agent_review' || item.manualReviewRequired;
    case 'budget_issues':
      return item.isOverBudget;
    default:
      return true;
  }
}

export function buildAgentMessagesHref(campaignId?: string) {
  if (!campaignId) {
    return '/agent/messages';
  }

  const searchParams = new URLSearchParams({ campaignId });
  return `/agent/messages?${searchParams.toString()}`;
}

export function buildQueueFiltersForInboxItem(item: Pick<AgentInboxItem, 'status' | 'queueStage'>): Partial<AgentCampaignQueueFilters> {
  if (item.status === 'awaiting_purchase') {
    return { stage: 'prospects' };
  }

  switch (item.queueStage) {
    case 'newly_paid':
      return { stage: 'ready_to_work', ownership: 'all', focus: 'newly_paid' };
    case 'brief_waiting':
      return { stage: 'ready_to_work', ownership: 'all', focus: 'brief_waiting' };
    case 'planning_ready':
      return { stage: 'ready_to_work', ownership: 'all', focus: 'planning_ready' };
    case 'agent_review':
      return { stage: 'ready_to_work', ownership: 'all', focus: 'needs_review' };
    case 'waiting_on_client':
      return { stage: 'waiting_on_client', ownership: 'all' };
    case 'completed':
      return { stage: 'completed', ownership: 'all' };
    default:
      return { stage: 'all', ownership: 'all' };
  }
}

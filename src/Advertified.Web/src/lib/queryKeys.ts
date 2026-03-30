import type { QueryClient } from '@tanstack/react-query';

export const queryKeys = {
  packages: {
    all: ['packages'] as const,
    areas: ['package-areas'] as const,
    pricingSummary: (selectedBudget: number) => ['package-pricing-summary', selectedBudget] as const,
    preview: (packageBandId: string | undefined, budget: number, selectedArea: string) =>
      ['package-preview', packageBandId ?? 'none', budget, selectedArea] as const,
  },
  orders: {
    list: (userId?: string) => ['orders', userId ?? 'anonymous'] as const,
    detail: (orderId: string, userId?: string) => ['package-order', orderId, userId ?? 'anonymous'] as const,
  },
  campaigns: {
    list: (userId?: string) => ['campaigns', userId ?? 'anonymous'] as const,
    detail: (campaignId: string) => ['campaign', campaignId] as const,
    messages: (campaignId: string) => ['campaign-messages', campaignId] as const,
  },
  creative: {
    inbox: ['creative-director-inbox'] as const,
    campaign: (campaignId: string) => ['creative-campaign', campaignId] as const,
    preview: (campaignId: string, surface: 'ops' | 'client') => ['creative-studio-preview', campaignId, surface] as const,
  },
  agent: {
    inbox: ['agent-inbox'] as const,
    campaigns: ['agent-campaigns'] as const,
    campaign: (campaignId: string) => ['agent-campaign', campaignId] as const,
    inventory: (campaignId?: string) => ['inventory', campaignId ?? 'all'] as const,
    messages: {
      inbox: ['agent-message-inbox'] as const,
      thread: (campaignId: string) => ['agent-message-thread', campaignId] as const,
    },
  },
  admin: {
    dashboard: ['admin-dashboard'] as const,
    campaignOperations: ['admin-campaign-operations'] as const,
    outlet: (outletCode: string) => ['admin-outlet', outletCode] as const,
    outletPricing: (outletCode: string) => ['admin-outlet-pricing', outletCode] as const,
    geography: (areaCode: string) => ['admin-geography', areaCode] as const,
    notifications: ['admin-dashboard-notifications'] as const,
  },
  consent: {
    preferences: (browserId: string) => ['consent-preferences', browserId] as const,
  },
  notifications: {
    summary: (role?: string) => ['notifications-summary', role ?? 'guest'] as const,
  },
} as const;

export async function invalidateClientCampaignQueries(queryClient: QueryClient, campaignId: string, userId?: string, includeMessages = false) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.campaigns.detail(campaignId) }),
    queryClient.invalidateQueries({ queryKey: queryKeys.campaigns.list(userId) }),
    includeMessages ? queryClient.invalidateQueries({ queryKey: queryKeys.campaigns.messages(campaignId) }) : Promise.resolve(),
    queryClient.invalidateQueries({ queryKey: queryKeys.notifications.summary('client') }),
  ]);
}

export async function invalidateAgentCampaignQueries(queryClient: QueryClient, campaignId: string, includeClientList = true) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.agent.campaign(campaignId) }),
    queryClient.invalidateQueries({ queryKey: queryKeys.agent.inbox }),
    queryClient.invalidateQueries({ queryKey: queryKeys.agent.campaigns }),
    includeClientList ? queryClient.invalidateQueries({ queryKey: queryKeys.campaigns.detail(campaignId) }) : Promise.resolve(),
    queryClient.invalidateQueries({ queryKey: queryKeys.notifications.summary('agent') }),
  ]);
}

export async function invalidateCreativeCampaignQueries(queryClient: QueryClient, campaignId: string) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.creative.inbox }),
    queryClient.invalidateQueries({ queryKey: queryKeys.creative.campaign(campaignId) }),
    queryClient.invalidateQueries({ queryKey: queryKeys.agent.campaign(campaignId) }),
    queryClient.invalidateQueries({ queryKey: queryKeys.campaigns.detail(campaignId) }),
    queryClient.invalidateQueries({ queryKey: queryKeys.notifications.summary('creative_director') }),
  ]);
}

export async function invalidateAdminOperationsQueries(queryClient: QueryClient) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.admin.campaignOperations }),
    queryClient.invalidateQueries({ queryKey: queryKeys.notifications.summary('admin') }),
  ]);
}

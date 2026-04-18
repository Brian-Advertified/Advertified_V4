import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { useToast } from '../../components/ui/toast';
import { AgentCampaignQueueCard } from '../../features/agent/components/AgentCampaignQueueCard';
import { AgentCampaignQueueFiltersPanel } from '../../features/agent/components/AgentCampaignQueueFiltersPanel';
import { AgentCampaignQueueSidebar } from '../../features/agent/components/AgentCampaignQueueSidebar';
import { advertifiedApi } from '../../services/advertifiedApi';
import {
  AgentPageShell,
  AgentQueryBoundary,
  useAgentInboxQuery,
} from './agentWorkspace';
import {
  AgentPageLead,
  AgentSummaryCard,
} from './agentSectionShared';
import {
  buildAgentCampaignQueueHref,
  matchesCampaignOwnership,
  matchesCampaignQueueFocus,
  matchesCampaignQueueStage,
  parseAgentCampaignQueueFilters,
  toAgentCampaignQueueSearchParams,
  type AgentCampaignQueueFilters,
  type CampaignOwnershipFilter,
  type CampaignQueueFocusFilter,
  type CampaignQueueStageFilter,
} from './agentCampaignQueueFilters';
import { pushAgentMutationError } from './agentMutationToast';

export function AgentCampaignsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const inboxQuery = useAgentInboxQuery();
  const queueFilters = parseAgentCampaignQueueFilters(searchParams);
  const { search, stage: stageFilter, ownership: ownershipFilter, focus: focusFilter } = queueFilters;
  const assignMutation = useMutation({
    mutationFn: (campaignId: string) => advertifiedApi.assignCampaignToMe(campaignId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['agent-inbox'] });
      pushToast({ title: 'Campaign assigned.', description: 'This campaign is now in your active queue.' });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not assign campaign.', error),
  });
  const unassignMutation = useMutation({
    mutationFn: (campaignId: string) => advertifiedApi.unassignCampaign(campaignId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['agent-inbox'] });
      pushToast({ title: 'Campaign unassigned.', description: 'This campaign was returned to the shared queue.' }, 'info');
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not unassign campaign.', error),
  });
  const convertSaleMutation = useMutation({
    mutationFn: ({ campaignId, paymentReference }: { campaignId: string; paymentReference?: string }) => (
      advertifiedApi.convertProspectToSale(campaignId, { paymentReference })
    ),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-sales'] }),
      ]);
      pushToast({ title: 'Campaign converted to sale.', description: 'Payment is marked as paid and this sale is now tracked in My Sales.' });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not convert to sale.', error),
  });

  const handleConvertToSale = (campaignId: string) => {
    const paymentReference = window.prompt('Enter payment reference (optional)')?.trim();
    if (paymentReference === undefined) {
      return;
    }

    convertSaleMutation.mutate({
      campaignId,
      paymentReference: paymentReference.length > 0 ? paymentReference : undefined,
    });
  };

  const updateQueueFilters = (overrides: Partial<AgentCampaignQueueFilters>) => {
    const nextFilters: AgentCampaignQueueFilters = {
      ...queueFilters,
      ...overrides,
    };

    setSearchParams(toAgentCampaignQueueSearchParams(nextFilters), { replace: true });
  };

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading agent inbox...">
      <AgentPageShell title="Campaigns" description="Use the main queue to find the next campaign, understand the next step, and act quickly.">
        {(() => {
          const inbox = inboxQuery.data;
          if (!inbox) {
            return null;
          }

          const queueTabs = [
            { id: 'ready_to_work' as const satisfies CampaignQueueStageFilter, label: 'Ready to work', count: inbox.newlyPaidCount + inbox.briefWaitingCount + inbox.planningReadyCount + inbox.agentReviewCount },
            { id: 'waiting_on_client' as const satisfies CampaignQueueStageFilter, label: 'Waiting on client', count: inbox.waitingOnClientCount },
            { id: 'prospects' as const satisfies CampaignQueueStageFilter, label: 'Prospects', count: inbox.items.filter((item) => item.status === 'awaiting_purchase').length },
            { id: 'completed' as const satisfies CampaignQueueStageFilter, label: 'Completed', count: inbox.completedCount },
            { id: 'all' as const satisfies CampaignQueueStageFilter, label: 'All', count: inbox.totalCampaigns },
          ];
          const focusTabs = [
            { id: 'all' as const satisfies CampaignQueueFocusFilter, label: 'All actions', count: inbox.totalCampaigns },
            { id: 'urgent' as const satisfies CampaignQueueFocusFilter, label: 'Urgent', count: inbox.urgentCount },
            { id: 'newly_paid' as const satisfies CampaignQueueFocusFilter, label: 'New leads', count: inbox.newlyPaidCount },
            { id: 'brief_waiting' as const satisfies CampaignQueueFocusFilter, label: 'Need brief', count: inbox.briefWaitingCount },
            { id: 'planning_ready' as const satisfies CampaignQueueFocusFilter, label: 'Planning ready', count: inbox.planningReadyCount },
            { id: 'needs_review' as const satisfies CampaignQueueFocusFilter, label: 'Needs review', count: inbox.agentReviewCount + inbox.manualReviewCount },
            { id: 'budget_issues' as const satisfies CampaignQueueFocusFilter, label: 'Budget issues', count: inbox.overBudgetCount },
          ];
          const ownershipTabs = [
            { label: 'Mine', count: inbox.assignedToMeCount, id: 'assigned_to_me' as const satisfies CampaignOwnershipFilter },
            { label: 'Unassigned', count: inbox.unassignedCount, id: 'unassigned' as const satisfies CampaignOwnershipFilter },
            { label: 'All', count: inbox.totalCampaigns, id: 'all' as const satisfies CampaignOwnershipFilter },
          ];
          const visibleQueueTabs = queueTabs.filter((tab) => tab.id === 'all' || tab.id === stageFilter || tab.count > 0);
          const visibleFocusTabs = focusTabs.filter((tab) => tab.id === 'all' || tab.id === focusFilter || tab.count > 0);
          const visibleOwnershipTabs = ownershipTabs.filter((tab) => tab.id === 'all' || tab.id === ownershipFilter || tab.count > 0);

          const campaigns = inbox.items.filter((item) => {
            const matchesSearch = `${item.campaignName} ${item.packageBandName} ${item.clientName} ${item.clientEmail}`
              .toLowerCase()
              .includes(search.toLowerCase());
            return matchesSearch
              && matchesCampaignQueueStage(item, stageFilter)
              && matchesCampaignOwnership(item, ownershipFilter)
              && matchesCampaignQueueFocus(item, focusFilter);
          });

          return (
            <section className="space-y-6">
              <AgentPageLead
                eyebrow="Campaigns"
                title="Focused queue with direct action."
                description="Keep this page as the front door to campaign operations: filter the live queue, confirm the next step, then move work forward without extra navigation."
                aside={(
                  <label className="relative block">
                    <Search className="absolute left-4 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
                    <input
                      value={search}
                      onChange={(event) => updateQueueFilters({ search: event.target.value })}
                      className="input-base min-w-[280px] pl-11"
                      placeholder="Search campaign, client, or package"
                    />
                  </label>
                )}
              />

              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <AgentSummaryCard
                  label="Ready to work"
                  value={inbox.newlyPaidCount + inbox.briefWaitingCount + inbox.planningReadyCount + inbox.agentReviewCount}
                  helper="Items that can move today."
                  href={buildAgentCampaignQueueHref({ stage: 'ready_to_work', ownership: 'all' })}
                  actionLabel="Open ready queue"
                />
                <AgentSummaryCard
                  label="Needs review"
                  value={inbox.agentReviewCount + inbox.manualReviewCount}
                  helper="Recommendation or operator review required."
                  href={buildAgentCampaignQueueHref({ stage: 'ready_to_work', ownership: 'all', focus: 'needs_review' })}
                  actionLabel="Open reviews"
                />
                <AgentSummaryCard
                  label="Waiting on client"
                  value={inbox.waitingOnClientCount}
                  helper="Sent work with no final answer yet."
                  href={buildAgentCampaignQueueHref({ stage: 'waiting_on_client', ownership: 'all' })}
                  actionLabel="Open client replies"
                />
                <AgentSummaryCard
                  label="All campaigns"
                  value={inbox.totalCampaigns}
                  helper="Every campaign in the live queue."
                  href={buildAgentCampaignQueueHref({ stage: 'all', ownership: 'all' })}
                  actionLabel="Open full queue"
                />
              </div>

              <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
                <div className="space-y-6">
                  <AgentCampaignQueueFiltersPanel
                    stageFilter={stageFilter}
                    focusFilter={focusFilter}
                    ownershipFilter={ownershipFilter}
                    queueTabs={visibleQueueTabs}
                    focusTabs={visibleFocusTabs}
                    ownershipTabs={visibleOwnershipTabs}
                    visibleCampaignCount={campaigns.length}
                    onStageChange={(value) => updateQueueFilters({ stage: value })}
                    onFocusChange={(value) => updateQueueFilters({ focus: value })}
                    onOwnershipChange={(value) => updateQueueFilters({ ownership: value })}
                  />

                  <div className="space-y-4">
                    {campaigns.length > 0 ? campaigns.map((campaign) => (
                      <AgentCampaignQueueCard
                        key={campaign.id}
                        campaign={campaign}
                        onAssign={(campaignId) => assignMutation.mutate(campaignId)}
                        onUnassign={(campaignId) => unassignMutation.mutate(campaignId)}
                        onConvertToSale={handleConvertToSale}
                        assignDisabled={assignMutation.isPending}
                        unassignDisabled={unassignMutation.isPending}
                        convertDisabled={convertSaleMutation.isPending}
                      />
                    )) : (
                      <div className="rounded-[24px] border border-dashed border-line bg-white px-5 py-10 text-sm text-ink-soft">
                        No campaigns match this queue view right now.
                      </div>
                    )}
                  </div>
                </div>

                <AgentCampaignQueueSidebar inbox={inbox} visibleCampaignCount={campaigns.length} />
              </div>
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

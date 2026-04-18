import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, Eye, Pencil, Search, UserPlus2, UserX2, WalletCards } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  queueTone,
  titleize,
  useAgentInboxQuery,
} from './agentWorkspace';
import {
  ActionIconButton,
  AgentPageLead,
  AgentSectionIntro,
  AgentSummaryCard,
} from './agentSectionShared';
import {
  buildAgentCampaignQueueHref,
  buildAgentMessagesHref,
  buildQueueFiltersForInboxItem,
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

function formatBuildSource(planningMode?: 'ai_assisted' | 'agent_assisted' | 'hybrid') {
  switch (planningMode) {
    case 'ai_assisted':
      return 'AI draft';
    case 'agent_assisted':
      return 'Agent built';
    case 'hybrid':
      return 'Hybrid';
    default:
      return 'Not selected';
  }
}

function getPrimaryAction(campaign: Awaited<ReturnType<typeof advertifiedApi.getAgentInbox>>['items'][number]) {
  if (campaign.status === 'awaiting_purchase') {
    return {
      label: 'Open prospect',
      href: `/agent/campaigns/${campaign.id}`,
    };
  }

  if (campaign.queueStage === 'waiting_on_client') {
    return {
      label: 'View client reply',
      href: `/agent/campaigns/${campaign.id}`,
    };
  }

  if (campaign.queueStage === 'brief_waiting') {
    return {
      label: 'Check brief',
      href: `/agent/campaigns/${campaign.id}`,
    };
  }

  if (campaign.queueStage === 'planning_ready' || campaign.queueStage === 'agent_review') {
    return {
      label: 'Work on recommendation',
      href: `/agent/recommendations/new?campaignId=${campaign.id}`,
    };
  }

  return {
    label: 'Open campaign',
    href: `/agent/campaigns/${campaign.id}`,
  };
}

function getOwnershipLabel(campaign: Awaited<ReturnType<typeof advertifiedApi.getAgentInbox>>['items'][number]) {
  if (campaign.isAssignedToCurrentUser) {
    return 'Assigned to me';
  }

  if (campaign.isUnassigned) {
    return 'Unassigned';
  }

  return `Assigned to ${campaign.assignedAgentName ?? 'another agent'}`;
}

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
                title="Table-first queue with direct actions."
                description="Filter the queue, scan the campaign, then take the next step without opening extra workflow pages first."
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
                <div className="panel px-6 py-6">
                  <AgentSectionIntro
                    title="Campaign queue"
                    description="Choose the view you need, then work from top to bottom."
                  />

                  <div className="mt-5 space-y-4">
                    <div>
                      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Queue view</p>
                      <div className="mt-3 flex flex-wrap gap-2">
                        {queueTabs.map((tab) => (
                          <button
                            key={tab.id}
                            type="button"
                            onClick={() => updateQueueFilters({ stage: tab.id })}
                            className={`rounded-full border px-4 py-2 text-sm font-semibold transition ${
                              stageFilter === tab.id
                                ? 'border-brand bg-brand text-white'
                                : 'border-line bg-white text-ink-soft'
                            }`}
                          >
                            {tab.label} ({tab.count})
                          </button>
                        ))}
                      </div>
                    </div>

                    <div>
                      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Action focus</p>
                      <div className="mt-3 flex flex-wrap gap-2">
                        {focusTabs.map((tab) => (
                          <button
                            key={tab.id}
                            type="button"
                            onClick={() => updateQueueFilters({ focus: tab.id })}
                            className={`rounded-full border px-4 py-2 text-sm font-semibold transition ${
                              focusFilter === tab.id
                                ? 'border-brand bg-brand text-white'
                                : 'border-line bg-white text-ink-soft'
                            }`}
                          >
                            {tab.label} ({tab.count})
                          </button>
                        ))}
                      </div>
                    </div>

                    <div>
                      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Ownership</p>
                      <div className="mt-3 flex flex-wrap gap-2">
                        {[
                          { label: `Mine (${inbox.assignedToMeCount})`, id: 'assigned_to_me' as const satisfies CampaignOwnershipFilter },
                          { label: `Unassigned (${inbox.unassignedCount})`, id: 'unassigned' as const satisfies CampaignOwnershipFilter },
                          { label: `All (${inbox.totalCampaigns})`, id: 'all' as const satisfies CampaignOwnershipFilter },
                        ].map((filter) => (
                          <button
                            key={filter.id}
                            type="button"
                            onClick={() => updateQueueFilters({ ownership: filter.id })}
                            className={`rounded-full border px-4 py-2 text-sm font-semibold transition ${
                              ownershipFilter === filter.id
                                ? 'border-brand bg-brand text-white'
                                : 'border-line bg-white text-ink-soft'
                            }`}
                          >
                            {filter.label}
                          </button>
                        ))}
                      </div>
                    </div>

                    <div className="space-y-3">
                      {campaigns.length > 0 ? campaigns.map((campaign) => {
                        const primaryAction = getPrimaryAction(campaign);

                        return (
                          <article key={campaign.id} className="rounded-[20px] border border-line bg-slate-50/70 p-4">
                            <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                              <div className="min-w-0 flex-1">
                                <div className="flex flex-wrap items-center gap-2">
                                  <Link
                                    to={buildAgentCampaignQueueHref(buildQueueFiltersForInboxItem(campaign))}
                                    className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold transition hover:-translate-y-0.5 ${queueTone(campaign.queueStage)}`}
                                  >
                                    {campaign.queueLabel}
                                  </Link>
                                  <span className="pill bg-white text-ink-soft">{getOwnershipLabel(campaign)}</span>
                                  {campaign.isUrgent ? (
                                    <Link to={buildAgentCampaignQueueHref({ stage: 'all', ownership: 'all', focus: 'urgent' })} className="pill border-rose-200 bg-rose-50 text-rose-700 transition hover:border-rose-300 hover:bg-rose-100">
                                      Urgent
                                    </Link>
                                  ) : null}
                                  {campaign.manualReviewRequired ? (
                                    <Link to={buildAgentCampaignQueueHref({ stage: 'ready_to_work', ownership: 'all', focus: 'needs_review' })} className="pill border-amber-200 bg-amber-50 text-amber-700 transition hover:border-amber-300 hover:bg-amber-100">
                                      Manual review
                                    </Link>
                                  ) : null}
                                  {campaign.isOverBudget ? (
                                    <Link to={buildAgentCampaignQueueHref({ stage: 'all', ownership: 'all', focus: 'budget_issues' })} className="pill border-rose-200 bg-rose-50 text-rose-700 transition hover:border-rose-300 hover:bg-rose-100">
                                      Over budget
                                    </Link>
                                  ) : null}
                                </div>

                                <h3 className="mt-3 text-lg font-semibold text-ink">{campaign.campaignName}</h3>
                                <p className="mt-1 text-sm text-ink-soft">{campaign.clientName} | {campaign.clientEmail}</p>

                                <div className="mt-4 grid gap-3 md:grid-cols-[minmax(0,1fr)_220px]">
                                  <div className="rounded-[18px] border border-line bg-white px-4 py-4">
                                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Next step</p>
                                    <p className="mt-2 text-sm text-ink">{campaign.nextAction}</p>
                                    <p className="mt-3 text-xs text-ink-soft">Status: {titleize(campaign.status)} | Build style: {formatBuildSource(campaign.planningMode)}</p>
                                  </div>

                                  <div className="rounded-[18px] border border-line bg-white px-4 py-4">
                                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Campaign</p>
                                    <p className="mt-2 text-sm font-semibold text-ink">{campaign.packageBandName}</p>
                                    <p className="mt-1 text-sm text-ink-soft">{fmtCurrency(campaign.selectedBudget)}</p>
                                    {campaign.isStale ? <p className="mt-3 text-xs text-amber-700">Stale for {campaign.ageInDays} days</p> : null}
                                  </div>
                                </div>
                              </div>

                              <div className="flex w-full shrink-0 flex-col gap-3 xl:w-[220px]">
                                <Link to={primaryAction.href} className="button-primary inline-flex items-center justify-center gap-2 px-4 py-3 text-sm font-semibold">
                                  {primaryAction.label}
                                  <ArrowRight className="size-4" />
                                </Link>
                                <Link to={`/agent/campaigns/${campaign.id}`} className="button-secondary inline-flex items-center justify-center gap-2 px-4 py-3 text-sm font-semibold">
                                  View campaign
                                  <Eye className="size-4" />
                                </Link>
                                <Link to={buildAgentMessagesHref(campaign.id)} className="button-secondary inline-flex items-center justify-center gap-2 px-4 py-3 text-sm font-semibold">
                                  Message client
                                </Link>
                                <div className="flex flex-wrap gap-2 xl:justify-end">
                                  <Link to={`/agent/recommendations/new?campaignId=${campaign.id}`} className="button-secondary p-2" title={`Edit ${campaign.campaignName}`}>
                                    <Pencil className="size-4" />
                                  </Link>
                                  {campaign.status === 'awaiting_purchase' && campaign.isAssignedToCurrentUser ? (
                                    <ActionIconButton title={`Convert ${campaign.campaignName} to sale`} onClick={() => handleConvertToSale(campaign.id)} disabled={convertSaleMutation.isPending}>
                                      <WalletCards className="size-4" />
                                    </ActionIconButton>
                                  ) : null}
                                  {campaign.isAssignedToCurrentUser ? (
                                    <ActionIconButton title={`Unassign ${campaign.campaignName}`} onClick={() => unassignMutation.mutate(campaign.id)} disabled={unassignMutation.isPending}>
                                      <UserX2 className="size-4" />
                                    </ActionIconButton>
                                  ) : (
                                    <ActionIconButton title={`Assign ${campaign.campaignName}`} onClick={() => assignMutation.mutate(campaign.id)} disabled={assignMutation.isPending}>
                                      <UserPlus2 className="size-4" />
                                    </ActionIconButton>
                                  )}
                                </div>
                              </div>
                            </div>
                          </article>
                        );
                      }) : (
                        <div className="rounded-[20px] border border-line bg-slate-50/70 px-4 py-8 text-sm text-ink-soft">
                          No campaigns match this view right now.
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                <div className="panel px-6 py-6">
                  <AgentSectionIntro
                    title="Review guide"
                    description="Minimal operator support."
                  />

                  <div className="mt-5 space-y-3">
                    {[
                      'Urgent, over-budget, and manual-review items first.',
                      'Check the next step before opening deeper workflow screens.',
                      'Assign, unassign, or escalate ownership from the queue when possible.',
                      'Move to the campaign page only when you need full context.',
                    ].map((line, index) => (
                      <div key={line} className="rounded-[20px] border border-line bg-slate-50/70 p-4">
                        <p className="text-sm font-semibold text-ink">{index + 1}. Step</p>
                        <p className="mt-2 text-sm text-ink-soft">{line}</p>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

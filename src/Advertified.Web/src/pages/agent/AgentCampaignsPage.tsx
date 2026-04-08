import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, Eye, Pencil, Search, UserPlus2, UserX2, WalletCards } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
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
import { pushAgentMutationError } from './agentMutationToast';

type QueueStageFilter = 'all' | 'ready_to_work' | 'waiting_on_client' | 'prospects' | 'completed';
type OwnershipFilter = 'all' | 'assigned_to_me' | 'unassigned';

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

  if (campaign.queueStage === 'ready_to_send') {
    return {
      label: 'Send to client',
      href: '/agent/review-send',
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
  const [search, setSearch] = useState('');
  const [stageFilter, setStageFilter] = useState<QueueStageFilter>('ready_to_work');
  const [ownershipFilter, setOwnershipFilter] = useState<OwnershipFilter>('assigned_to_me');
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const inboxQuery = useAgentInboxQuery();
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

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading agent inbox...">
      <AgentPageShell title="Campaigns" description="Use the main queue to find the next campaign, understand the next step, and act quickly.">
        {(() => {
          const inbox = inboxQuery.data;
          if (!inbox) {
            return null;
          }

          const queueTabs = [
            { id: 'ready_to_work' as const, label: 'Ready to work', count: inbox.newlyPaidCount + inbox.briefWaitingCount + inbox.planningReadyCount + inbox.agentReviewCount + inbox.readyToSendCount },
            { id: 'waiting_on_client' as const, label: 'Waiting on client', count: inbox.waitingOnClientCount },
            { id: 'prospects' as const, label: 'Prospects', count: inbox.items.filter((item) => item.status === 'awaiting_purchase').length },
            { id: 'completed' as const, label: 'Completed', count: inbox.completedCount },
            { id: 'all' as const, label: 'All', count: inbox.totalCampaigns },
          ];

          const campaigns = inbox.items.filter((item) => {
            const matchesSearch = `${item.campaignName} ${item.packageBandName} ${item.clientName} ${item.clientEmail}`
              .toLowerCase()
              .includes(search.toLowerCase());
            const matchesStage = stageFilter === 'all'
              || (stageFilter === 'ready_to_work' && ['newly_paid', 'brief_waiting', 'planning_ready', 'agent_review', 'ready_to_send'].includes(item.queueStage))
              || (stageFilter === 'prospects' && item.status === 'awaiting_purchase')
              || item.queueStage === stageFilter;
            const matchesOwnership = ownershipFilter === 'all'
              || (ownershipFilter === 'assigned_to_me' && item.isAssignedToCurrentUser)
              || (ownershipFilter === 'unassigned' && item.isUnassigned);

            return matchesSearch && matchesStage && matchesOwnership;
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
                      onChange={(event) => setSearch(event.target.value)}
                      className="input-base min-w-[280px] pl-11"
                      placeholder="Search campaign, client, or package"
                    />
                  </label>
                )}
              />

              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <AgentSummaryCard label="Ready to work" value={inbox.newlyPaidCount + inbox.briefWaitingCount + inbox.planningReadyCount + inbox.agentReviewCount + inbox.readyToSendCount} helper="Items that can move today." />
                <AgentSummaryCard label="Needs review" value={inbox.agentReviewCount + inbox.manualReviewCount} helper="Recommendation or operator review required." />
                <AgentSummaryCard label="Waiting on client" value={inbox.waitingOnClientCount} helper="Sent work with no final answer yet." />
                <AgentSummaryCard label="All campaigns" value={inbox.totalCampaigns} helper="Every campaign in the live queue." />
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
                            onClick={() => setStageFilter(tab.id)}
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
                      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Ownership</p>
                      <div className="mt-3 flex flex-wrap gap-2">
                        {[
                          { label: `Mine (${inbox.assignedToMeCount})`, id: 'assigned_to_me' as const },
                          { label: `Unassigned (${inbox.unassignedCount})`, id: 'unassigned' as const },
                          { label: `All (${inbox.totalCampaigns})`, id: 'all' as const },
                        ].map((filter) => (
                          <button
                            key={filter.id}
                            type="button"
                            onClick={() => setOwnershipFilter(filter.id)}
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
                                  <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(campaign.queueStage)}`}>{campaign.queueLabel}</span>
                                  <span className="pill bg-white text-ink-soft">{getOwnershipLabel(campaign)}</span>
                                  {campaign.isUrgent ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Urgent</span> : null}
                                  {campaign.manualReviewRequired ? <span className="pill border-amber-200 bg-amber-50 text-amber-700">Manual review</span> : null}
                                  {campaign.isOverBudget ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Over budget</span> : null}
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

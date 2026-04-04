import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ArrowRight, Eye, Pencil, Search, UserPlus2, UserX2, WalletCards } from 'lucide-react';
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
import { ActionIconButton } from './agentSectionShared';
import { pushAgentMutationError } from './agentMutationToast';

type QueueStageFilter = 'all' | 'ready_to_work' | 'waiting_on_client' | 'prospects' | 'completed';
type OwnershipFilter = 'all' | 'assigned_to_me' | 'unassigned';

function formatBuildSource(planningMode?: 'ai_assisted' | 'agent_assisted' | 'hybrid') {
  switch (planningMode) {
    case 'ai_assisted':
      return 'AI draft';
    case 'agent_assisted':
      return 'Agent-built';
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

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading agent inbox...">
      <AgentPageShell title="Campaigns" description="Use this as the main working queue for campaigns, with simple stage filters and direct actions.">
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
          const highlightedCounts = [
            { label: 'Need action now', value: inbox.urgentCount + inbox.manualReviewCount, tone: 'border-amber-200 bg-amber-50 text-amber-700' },
            { label: 'Ready to work', value: inbox.newlyPaidCount + inbox.briefWaitingCount + inbox.planningReadyCount + inbox.agentReviewCount + inbox.readyToSendCount, tone: 'border-brand/20 bg-brand-soft text-brand' },
            { label: 'Waiting on client', value: inbox.waitingOnClientCount, tone: 'border-sky-200 bg-sky-50 text-sky-700' },
          ];

          const campaigns = inbox.items.filter((item) => {
            const matchesSearch = `${item.campaignName} ${item.packageBandName} ${item.clientName} ${item.clientEmail}`.toLowerCase().includes(search.toLowerCase());
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
              <div className="panel overflow-hidden border-brand/10 bg-[radial-gradient(circle_at_top_left,_rgba(15,118,110,0.12),_transparent_28%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(242,248,246,0.95))] px-6 py-6">
                <div className="flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
                  <div className="space-y-3">
                    <div className="inline-flex items-center gap-2 rounded-full border border-brand/15 bg-white/80 px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.26em] text-brand">
                      Main queue
                    </div>
                    <div>
                      <h3 className="text-2xl font-semibold text-ink">Start here when you want to see what to work on next.</h3>
                      <p className="mt-2 max-w-3xl text-sm leading-6 text-ink-soft">
                        This page keeps the campaign flow simple: find the campaign, see what needs to happen next, and open the right action.
                      </p>
                    </div>
                  </div>
                  <label className="relative w-full max-w-xl">
                    <Search className="absolute left-4 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
                    <input value={search} onChange={(event) => setSearch(event.target.value)} className="input-base pl-11" placeholder="Search by campaign, client, or package" />
                  </label>
                </div>
                <div className="mt-5 flex flex-wrap gap-3">
                  {highlightedCounts.map((item) => (
                    <span key={item.label} className={`pill ${item.tone}`}>
                      {item.label}: {item.value}
                    </span>
                  ))}
                </div>
              </div>

              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Show me</p>
                <div className="mt-3 flex flex-wrap gap-3">
                {queueTabs.map((tab) => (
                  <button
                    key={tab.id}
                    type="button"
                    onClick={() => setStageFilter(tab.id)}
                    className={`pill transition ${stageFilter === tab.id ? 'border-brand bg-brand text-white' : 'bg-white text-ink-soft'}`}
                  >
                    {tab.label} ({tab.count})
                  </button>
                ))}
                </div>
              </div>

              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Ownership</p>
                <div className="mt-3 flex flex-wrap gap-3">
                {[
                  { label: `Mine (${inbox.assignedToMeCount})`, id: 'assigned_to_me' as const },
                  { label: `Unassigned (${inbox.unassignedCount})`, id: 'unassigned' as const },
                  { label: `All campaigns (${inbox.totalCampaigns})`, id: 'all' as const },
                ].map((filter) => (
                  <button
                    key={filter.id}
                    type="button"
                    onClick={() => setOwnershipFilter(filter.id)}
                    className={`pill transition ${ownershipFilter === filter.id ? 'border-brand bg-brand text-white' : 'bg-white text-ink-soft'}`}
                  >
                    {filter.label}
                  </button>
                ))}
                </div>
              </div>

              <div className="grid gap-4">
                {campaigns.map((campaign) => {
                  const primaryAction = getPrimaryAction(campaign);

                  return (
                    <article key={campaign.id} className="rounded-[28px] border border-line bg-white p-6 shadow-[0_12px_34px_rgba(15,23,42,0.05)]">
                      <div className="flex flex-col gap-5 xl:flex-row xl:items-start xl:justify-between">
                        <div className="space-y-4">
                          <div>
                            <div className="flex flex-wrap items-center gap-2">
                              <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(campaign.queueStage)}`}>{campaign.queueLabel}</span>
                              <span className="pill bg-white text-ink-soft">{getOwnershipLabel(campaign)}</span>
                              {campaign.isUrgent ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Urgent</span> : null}
                              {campaign.manualReviewRequired ? <span className="pill border-amber-200 bg-amber-50 text-amber-700">Manual review</span> : null}
                              {campaign.isOverBudget ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Over budget</span> : null}
                              {campaign.isStale ? <span className="pill border-slate-200 bg-slate-100 text-ink-soft">Stale {campaign.ageInDays}d</span> : null}
                            </div>
                            <h3 className="mt-4 text-xl font-semibold text-ink">{campaign.campaignName}</h3>
                            <p className="mt-2 text-sm text-ink-soft">{campaign.clientName} | {campaign.clientEmail}</p>
                          </div>

                          <div className="grid gap-3 md:grid-cols-3">
                            <div className="rounded-2xl border border-line px-4 py-3">
                              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Package</p>
                              <p className="mt-2 text-sm font-semibold text-ink">{campaign.packageBandName}</p>
                              <p className="mt-1 text-xs text-ink-soft">{fmtCurrency(campaign.selectedBudget)}</p>
                            </div>
                            <div className="rounded-2xl border border-line px-4 py-3">
                              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">What to do next</p>
                              <p className="mt-2 text-sm text-ink">{campaign.nextAction}</p>
                            </div>
                            <div className="rounded-2xl border border-line px-4 py-3">
                              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Details</p>
                              <p className="mt-2 text-sm text-ink">Build style: {formatBuildSource(campaign.planningMode)}</p>
                              <p className="mt-1 text-xs text-ink-soft">Status: {titleize(campaign.status)}</p>
                            </div>
                          </div>
                        </div>

                        <div className="flex w-full flex-col gap-3 xl:w-auto xl:min-w-[240px]">
                          <Link to={primaryAction.href} className="button-primary inline-flex items-center justify-center gap-2 rounded-full px-4 py-3 text-sm font-semibold">
                            {primaryAction.label}
                            <ArrowRight className="size-4" />
                          </Link>
                          <Link to={`/agent/campaigns/${campaign.id}`} className="button-secondary inline-flex items-center justify-center gap-2 rounded-full px-4 py-3 text-sm font-semibold">
                            View campaign
                            <Eye className="size-4" />
                          </Link>
                          <div className="flex flex-wrap justify-center gap-2 xl:justify-end">
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
                })}
                {campaigns.length === 0 ? (
                  <div className="rounded-[28px] border border-line bg-white px-6 py-10 text-center text-sm text-ink-soft">
                    No campaigns match this view right now.
                  </div>
                ) : null}
              </div>
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

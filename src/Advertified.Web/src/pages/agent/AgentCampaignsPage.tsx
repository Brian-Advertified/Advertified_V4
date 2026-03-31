import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Eye, Pencil, Search, UserPlus2, UserX2, WalletCards } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  queueTone,
  useAgentInboxQuery,
} from './agentWorkspace';
import { ActionIconButton } from './agentSectionShared';

type QueueStageFilter = 'all' | 'prospects' | 'newly_paid' | 'brief_waiting' | 'planning_ready' | 'agent_review' | 'ready_to_send' | 'waiting_on_client' | 'completed';
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

export function AgentCampaignsPage() {
  const [search, setSearch] = useState('');
  const [stageFilter, setStageFilter] = useState<QueueStageFilter>('all');
  const [ownershipFilter, setOwnershipFilter] = useState<OwnershipFilter>('all');
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const inboxQuery = useAgentInboxQuery();
  const assignMutation = useMutation({
    mutationFn: (campaignId: string) => advertifiedApi.assignCampaignToMe(campaignId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['agent-inbox'] });
      pushToast({ title: 'Campaign assigned.', description: 'This campaign is now in your active queue.' });
    },
    onError: (error) => pushToast({ title: 'Could not assign campaign.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });
  const unassignMutation = useMutation({
    mutationFn: (campaignId: string) => advertifiedApi.unassignCampaign(campaignId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['agent-inbox'] });
      pushToast({ title: 'Campaign unassigned.', description: 'This campaign was returned to the shared queue.' }, 'info');
    },
    onError: (error) => pushToast({ title: 'Could not unassign campaign.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
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
    onError: (error) => pushToast({ title: 'Could not convert to sale.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading agent inbox...">
      <AgentPageShell title="Campaign pipeline" description="Manage campaigns from purchase to planning, review, approval, and launch with a live queue view and direct campaign links.">
        {(() => {
          const inbox = inboxQuery.data;
          if (!inbox) {
            return null;
          }
          const queueTabs = [
            { id: 'all' as const, label: 'All', count: inbox.totalCampaigns },
            { id: 'prospects' as const, label: 'Prospects', count: inbox.items.filter((item) => item.status === 'awaiting_purchase').length },
            { id: 'newly_paid' as const, label: 'Newly paid', count: inbox.newlyPaidCount },
            { id: 'brief_waiting' as const, label: 'Brief waiting', count: inbox.briefWaitingCount },
            { id: 'planning_ready' as const, label: 'Needs planning', count: inbox.planningReadyCount },
            { id: 'agent_review' as const, label: 'Needs agent review', count: inbox.agentReviewCount },
            { id: 'ready_to_send' as const, label: 'Ready to send', count: inbox.readyToSendCount },
            { id: 'waiting_on_client' as const, label: 'Waiting on client', count: inbox.waitingOnClientCount },
            { id: 'completed' as const, label: 'Completed', count: inbox.completedCount },
          ];

          const campaigns = inbox.items.filter((item) => {
            const matchesSearch = `${item.campaignName} ${item.packageBandName} ${item.clientName} ${item.clientEmail}`.toLowerCase().includes(search.toLowerCase());
            const matchesStage = stageFilter === 'all'
              || (stageFilter === 'prospects' && item.status === 'awaiting_purchase')
              || item.queueStage === stageFilter;
            const matchesOwnership = ownershipFilter === 'all'
              || (ownershipFilter === 'assigned_to_me' && item.isAssignedToCurrentUser)
              || (ownershipFilter === 'unassigned' && item.isUnassigned);
            return matchesSearch && matchesStage && matchesOwnership;
          });

          return (
            <section className="space-y-6">
              <div className="panel flex flex-col gap-4 px-6 py-6 lg:flex-row lg:items-center">
                <label className="relative flex-1">
                  <Search className="absolute left-4 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
                  <input value={search} onChange={(event) => setSearch(event.target.value)} className="input-base pl-11" placeholder="Search campaigns or clients" />
                </label>
                <div className="rounded-2xl bg-brand-soft px-4 py-3 text-sm text-brand">Filter the live queue by campaign stage, ownership, and client search.</div>
              </div>

              <div className="flex flex-wrap gap-3">
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

              <div className="flex flex-wrap gap-3">
                {[
                  { label: `All ownership (${inbox.totalCampaigns})`, id: 'all' as const },
                  { label: `Assigned to me (${inbox.assignedToMeCount})`, id: 'assigned_to_me' as const },
                  { label: `Unassigned (${inbox.unassignedCount})`, id: 'unassigned' as const },
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

              <div className="flex flex-wrap gap-3">
                {[
                  { label: `Urgent (${inbox.urgentCount})`, tone: 'border-rose-200 bg-rose-50 text-rose-700' },
                  { label: `Manual review (${inbox.manualReviewCount})`, tone: 'border-amber-200 bg-amber-50 text-amber-700' },
                  { label: `Over budget (${inbox.overBudgetCount})`, tone: 'border-rose-200 bg-rose-50 text-rose-700' },
                  { label: `Stale (${inbox.staleCount})`, tone: 'border-slate-200 bg-slate-100 text-ink-soft' },
                ].map((chip) => (
                  <span key={chip.label} className={`pill ${chip.tone}`}>
                    {chip.label}
                  </span>
                ))}
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Campaign</th>
                      <th className="px-4 py-4">Queue</th>
                      <th className="px-4 py-4">Ownership</th>
                      <th className="px-4 py-4">Build source</th>
                      <th className="px-4 py-4">Next action</th>
                      <th className="px-4 py-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {campaigns.map((campaign) => (
                      <tr key={campaign.id} className="border-t border-line">
                        <td className="px-4 py-4">
                          <p className="font-semibold text-ink">{campaign.campaignName}</p>
                          <p className="text-xs text-ink-soft">{campaign.clientName} | {campaign.packageBandName}</p>
                          <p className="mt-1 text-xs text-ink-soft">{fmtCurrency(campaign.selectedBudget)}</p>
                        </td>
                        <td className="px-4 py-4">
                          <div className="flex flex-wrap gap-2">
                            <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(campaign.queueStage)}`}>{campaign.queueLabel}</span>
                            {campaign.isUrgent ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Urgent</span> : null}
                            {campaign.manualReviewRequired ? <span className="pill border-amber-200 bg-amber-50 text-amber-700">Manual review</span> : null}
                            {campaign.isOverBudget ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Over budget</span> : null}
                            {campaign.isStale ? <span className="pill border-slate-200 bg-slate-100 text-ink-soft">Stale {campaign.ageInDays}d</span> : null}
                          </div>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          {campaign.isAssignedToCurrentUser ? 'Assigned to me' : campaign.isUnassigned ? 'Unassigned' : `Owned by ${campaign.assignedAgentName ?? 'another agent'}`}
                        </td>
                        <td className="px-4 py-4 text-ink-soft">{formatBuildSource(campaign.planningMode)}</td>
                        <td className="px-4 py-4 text-ink-soft">{campaign.nextAction}</td>
                        <td className="px-4 py-4">
                          <div className="flex justify-end gap-2">
                            <Link to={`/agent/campaigns/${campaign.id}`} className="button-secondary p-2" title={`View ${campaign.campaignName}`}>
                              <Eye className="size-4" />
                            </Link>
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
                        </td>
                      </tr>
                    ))}
                    {campaigns.length === 0 ? (
                      <tr><td colSpan={6} className="px-4 py-8 text-center text-sm text-ink-soft">No campaigns match this inbox view yet.</td></tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

import { useQuery } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageHero } from '../../components/marketing/PageHero';
import { StatusBadge } from '../../components/ui/StatusBadge';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';

type QueueStageFilter = 'all' | 'newly_paid' | 'brief_waiting' | 'planning_ready' | 'agent_review' | 'ready_to_send' | 'waiting_on_client' | 'completed';
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
  const inboxQuery = useQuery({ queryKey: ['agent-inbox'], queryFn: advertifiedApi.getAgentInbox });

  if (inboxQuery.isLoading) {
    return <LoadingState label="Loading agent inbox..." />;
  }

  const inbox = inboxQuery.data;
  const queueTabs = useMemo(() => ([
    { id: 'all' as const, label: 'All', count: inbox?.totalCampaigns ?? 0 },
    { id: 'newly_paid' as const, label: 'Newly paid', count: inbox?.newlyPaidCount ?? 0 },
    { id: 'brief_waiting' as const, label: 'Brief waiting', count: inbox?.briefWaitingCount ?? 0 },
    { id: 'planning_ready' as const, label: 'Needs planning', count: inbox?.planningReadyCount ?? 0 },
    { id: 'agent_review' as const, label: 'Needs agent review', count: inbox?.agentReviewCount ?? 0 },
    { id: 'ready_to_send' as const, label: 'Ready to send', count: inbox?.readyToSendCount ?? 0 },
    { id: 'waiting_on_client' as const, label: 'Waiting on client', count: inbox?.waitingOnClientCount ?? 0 },
    { id: 'completed' as const, label: 'Completed', count: inbox?.completedCount ?? 0 },
  ]), [inbox]);

  const campaigns = (inbox?.items ?? []).filter((item) => {
    const matchesSearch = `${item.campaignName} ${item.packageBandName} ${item.clientName} ${item.clientEmail}`.toLowerCase().includes(search.toLowerCase());
    const matchesStage = stageFilter === 'all' || item.queueStage === stageFilter;
    const matchesOwnership = ownershipFilter === 'all'
      || (ownershipFilter === 'assigned_to_me' && item.isAssignedToCurrentUser)
      || (ownershipFilter === 'unassigned' && item.isUnassigned);
    return matchesSearch && matchesStage && matchesOwnership;
  });

  return (
    <section className="page-shell space-y-6">
      <PageHero
        kicker="Agent inbox"
        title="Campaign queue"
        description="Use the inbox to work the newest paid campaigns first, monitor brief progress, and separate active work from campaigns waiting on client decisions."
        aside={(
          <label className="relative block">
            <Search className="absolute left-4 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
            <input value={search} onChange={(event) => setSearch(event.target.value)} className="input-base pl-11" placeholder="Search campaigns or clients" />
          </label>
        )}
      />

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
          { label: `Urgent (${inbox?.urgentCount ?? 0})`, tone: 'border-rose-200 bg-rose-50 text-rose-700' },
          { label: `Manual review (${inbox?.manualReviewCount ?? 0})`, tone: 'border-amber-200 bg-amber-50 text-amber-700' },
          { label: `Over budget (${inbox?.overBudgetCount ?? 0})`, tone: 'border-rose-200 bg-rose-50 text-rose-700' },
          { label: `Stale (${inbox?.staleCount ?? 0})`, tone: 'border-slate-200 bg-slate-100 text-ink-soft' },
        ].map((chip) => (
          <span key={chip.label} className={`pill ${chip.tone}`}>
            {chip.label}
          </span>
        ))}
      </div>

      <div className="flex flex-wrap gap-3">
        {[
          { id: 'all' as const, label: `All ownership (${inbox?.totalCampaigns ?? 0})` },
          { id: 'assigned_to_me' as const, label: `Assigned to me (${inbox?.assignedToMeCount ?? 0})` },
          { id: 'unassigned' as const, label: `Unassigned (${inbox?.unassignedCount ?? 0})` },
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

      <div className="grid gap-4">
        {campaigns.length === 0 ? (
          <div className="panel px-6 py-8 text-sm text-ink-soft">
            No campaigns match this inbox view yet.
          </div>
        ) : null}

        {campaigns.map((campaign) => (
          <Link key={campaign.id} to={`/agent/campaigns/${campaign.id}`} className="panel flex flex-col gap-4 px-6 py-6 transition hover:border-brand/30 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex flex-wrap items-center gap-3">
                <p className="text-lg font-semibold text-ink">{campaign.campaignName}</p>
                <span className="pill border-line bg-slate-50 text-ink-soft">{campaign.queueLabel}</span>
                <span className={`pill ${campaign.isAssignedToCurrentUser ? 'border-brand bg-brand-soft text-brand' : campaign.isUnassigned ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-line bg-slate-50 text-ink-soft'}`}>
                  {campaign.isAssignedToCurrentUser ? 'Assigned to me' : campaign.isUnassigned ? 'Unassigned' : `Owned by ${campaign.assignedAgentName ?? 'another agent'}`}
                </span>
                {campaign.isUrgent ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Urgent</span> : null}
                {campaign.manualReviewRequired ? <span className="pill border-amber-200 bg-amber-50 text-amber-700">Manual review</span> : null}
                {campaign.isOverBudget ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Over budget</span> : null}
                {campaign.isStale ? <span className="pill border-slate-200 bg-slate-100 text-ink-soft">Stale {campaign.ageInDays}d</span> : null}
              </div>
              <p className="mt-2 text-sm text-ink-soft">{campaign.clientName} | {campaign.packageBandName}</p>
              <p className="mt-2 text-sm text-ink-soft">Paid budget: {formatCurrency(campaign.selectedBudget)}</p>
              <p className="mt-2 text-sm text-ink-soft">Build source: {formatBuildSource(campaign.planningMode)}</p>
              <p className="mt-2 text-sm text-ink-soft">{campaign.nextAction}</p>
            </div>
            <StatusBadge status={campaign.status} />
          </Link>
        ))}
      </div>
    </section>
  );
}

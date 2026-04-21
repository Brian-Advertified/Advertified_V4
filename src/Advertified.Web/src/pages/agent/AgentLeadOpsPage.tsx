import { useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Search } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { LeadOpsCoverageItem, LeadOpsInboxItem, LeadOpsItemType } from '../../types/domain';
import { AgentPageShell, AgentQueryBoundary, fmtDate, titleize, useLeadOpsCoverageQuery, useLeadOpsInboxQuery } from './agentWorkspace';
import { AgentPageLead, AgentSummaryCard } from './agentSectionShared';
import { pushAgentMutationError } from './agentMutationToast';

type OwnershipFilter = 'all' | 'assigned_to_me' | 'unassigned';
type FocusFilter = 'all' | LeadOpsItemType | 'urgent';

function matchesOwnership(item: LeadOpsInboxItem, filter: OwnershipFilter) {
  if (filter === 'assigned_to_me') {
    return item.isAssignedToCurrentUser;
  }

  if (filter === 'unassigned') {
    return item.isUnassigned;
  }

  return true;
}

function matchesFocus(item: LeadOpsInboxItem, filter: FocusFilter) {
  if (filter === 'all') {
    return true;
  }

  if (filter === 'urgent') {
    return item.isUrgent;
  }

  return item.itemType === filter;
}

function itemTone(item: LeadOpsInboxItem) {
  if (item.itemType === 'overdue_follow_up' || item.itemType === 'prospect_no_recent_activity') {
    return 'border-rose-200 bg-rose-50 text-rose-700';
  }

  if (item.itemType === 'awaiting_client_response') {
    return 'border-amber-200 bg-amber-50 text-amber-700';
  }

  if (item.itemType === 'open_lead_action') {
    return 'border-sky-200 bg-sky-50 text-sky-700';
  }

  return 'border-brand/20 bg-brand-soft text-brand';
}

function coverageOwnerLabel(item: LeadOpsCoverageItem) {
  if (item.ownerAgentName) {
    return item.ownerAgentName;
  }

  if (item.ownerResolution === 'multiple_action_owners') {
    return 'Multiple owners';
  }

  return 'Unassigned';
}

export function AgentLeadOpsPage() {
  const inboxQuery = useLeadOpsInboxQuery();
  const coverageQuery = useLeadOpsCoverageQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [search, setSearch] = useState('');
  const [ownershipFilter, setOwnershipFilter] = useState<OwnershipFilter>('all');
  const [focusFilter, setFocusFilter] = useState<FocusFilter>('all');

  const assignCampaignMutation = useMutation({
    mutationFn: (campaignId: string) => advertifiedApi.assignCampaignToMe(campaignId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['lead-ops-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['lead-ops-coverage'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
      ]);
      pushToast({ title: 'Prospect assigned.', description: 'This item is now owned by you in Lead Ops.' });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not assign prospect.', error),
  });

  const assignLeadActionMutation = useMutation({
    mutationFn: ({ leadId, actionId }: { leadId: number; actionId: number }) =>
      advertifiedApi.assignLeadActionToMe(leadId, actionId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['lead-ops-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['lead-ops-coverage'] }),
        queryClient.invalidateQueries({ queryKey: ['lead-intelligence-inbox'] }),
      ]);
      pushToast({ title: 'Lead action assigned.', description: 'The action is now in your owned queue.' });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not assign lead action.', error),
  });

  const visibleItems = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    return (inboxQuery.data?.items ?? []).filter((item) => {
      const matchesSearch = normalizedSearch.length === 0
        || `${item.title} ${item.subtitle} ${item.description} ${item.itemLabel} ${item.unifiedStatus}`
          .toLowerCase()
          .includes(normalizedSearch);

      return matchesSearch && matchesOwnership(item, ownershipFilter) && matchesFocus(item, focusFilter);
    });
  }, [focusFilter, inboxQuery.data?.items, ownershipFilter, search]);

  const visibleCoverageItems = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    return (coverageQuery.data?.items ?? []).filter((item) => {
      if (normalizedSearch.length === 0) {
        return true;
      }

      return `${item.leadName} ${item.location} ${item.category} ${item.source} ${item.nextAction} ${item.unifiedStatus} ${item.ownerAgentName ?? ''}`
        .toLowerCase()
        .includes(normalizedSearch);
    });
  }, [coverageQuery.data?.items, search]);

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading lead ops inbox...">
      <AgentPageShell title="Lead Ops" description="Run inbound prospects, outbound follow-ups, and client waits from one operating queue.">
        {(() => {
          const inbox = inboxQuery.data;
          if (!inbox) {
            return null;
          }

          const focusTabs: Array<{ id: FocusFilter; label: string; count: number }> = [
            { id: 'all', label: 'All', count: inbox.totalItems },
            { id: 'urgent', label: 'Urgent', count: inbox.urgentCount },
            { id: 'new_inbound_prospect', label: 'New inbound', count: inbox.newInboundProspectsCount },
            { id: 'unassigned_prospect', label: 'Unassigned', count: inbox.unassignedProspectsCount },
            { id: 'open_lead_action', label: 'Lead actions', count: inbox.openLeadActionsCount },
            { id: 'awaiting_client_response', label: 'Awaiting client', count: inbox.awaitingClientResponsesCount },
            { id: 'prospect_no_recent_activity', label: 'No activity', count: inbox.noRecentActivityCount },
            { id: 'overdue_follow_up', label: 'Overdue', count: inbox.overdueFollowUpsCount },
          ];

          const ownershipTabs: Array<{ id: OwnershipFilter; label: string; count: number }> = [
            { id: 'assigned_to_me', label: 'Mine', count: inbox.assignedToMeCount },
            { id: 'unassigned', label: 'Unassigned', count: inbox.unassignedCount },
            { id: 'all', label: 'All', count: inbox.totalItems },
          ];

          const coverage = coverageQuery.data;

          return (
            <section className="space-y-6">
              <AgentPageLead
                eyebrow="Lead Ops"
                title="One queue for the real work."
                description="This merges new prospects, claimable work, outbound lead actions, stale prospects, overdue follow-ups, and client waits into one operating system."
                aside={(
                  <label className="relative block">
                    <Search className="absolute left-4 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
                    <input
                      value={search}
                      onChange={(event) => setSearch(event.target.value)}
                      className="input-base min-w-[280px] pl-11"
                      placeholder="Search lead, contact, or status"
                    />
                  </label>
                )}
              />

              {coverage ? (
                <section className="space-y-4 rounded-[28px] border border-line bg-white p-5">
                  <AgentPageLead
                    eyebrow="Operating Answers"
                    title="The business questions should answer themselves."
                    description="This coverage view resolves who owns each visible lead, which leads have never been contacted, the next action, source attribution, and lead-to-sale conversion."
                  />

                  <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
                    <AgentSummaryCard
                      label="Owned leads"
                      value={`${coverage.ownedLeadCount}/${coverage.totalLeadCount}`}
                      helper={coverage.ambiguousOwnerCount > 0
                        ? `${coverage.ambiguousOwnerCount} need owner cleanup.`
                        : `${coverage.unownedLeadCount} are still unowned.`}
                    />
                    <AgentSummaryCard
                      label="Not contacted"
                      value={coverage.uncontactedLeadCount}
                      helper="No interaction or prospect contact logged yet."
                    />
                    <AgentSummaryCard
                      label="Next action"
                      value={`${coverage.leadsWithNextActionCount}/${coverage.totalLeadCount}`}
                      helper="Leads with a resolved next step."
                    />
                    <AgentSummaryCard
                      label="Top source"
                      value={coverage.sources[0]?.source ? titleize(coverage.sources[0].source) : 'None'}
                      helper={coverage.sources[0]
                        ? `${coverage.sources[0].leadCount} visible leads from this source.`
                        : 'No source data available.'}
                    />
                    <AgentSummaryCard
                      label="Lead to sale"
                      value={`${coverage.leadToSaleRatePercent}%`}
                      helper={`${coverage.wonLeadCount} of ${coverage.totalLeadCount} visible leads reached paid sale.`}
                    />
                  </div>

                  <div className="grid gap-4 xl:grid-cols-[1.1fr_1.9fr]">
                    <div className="rounded-[24px] border border-line bg-slate-50 p-4">
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <h3 className="text-sm font-semibold text-ink">Source mix</h3>
                          <p className="mt-1 text-xs text-ink-soft">Lead intake by source with conversion visibility.</p>
                        </div>
                        <span className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">
                          Prospect {coverage.leadToProspectRatePercent}% | Sale {coverage.leadToSaleRatePercent}%
                        </span>
                      </div>
                      <div className="mt-4 space-y-3">
                        {coverage.sources.slice(0, 6).map((source) => (
                          <div key={source.source} className="rounded-2xl border border-line bg-white px-4 py-3">
                            <div className="flex items-center justify-between gap-3">
                              <p className="font-semibold text-ink">{titleize(source.source)}</p>
                              <p className="text-sm text-ink-soft">{source.leadCount} leads</p>
                            </div>
                            <p className="mt-1 text-xs text-ink-soft">
                              {source.prospectCount} prospects | {source.wonCount} won
                            </p>
                          </div>
                        ))}
                      </div>
                    </div>

                    <div className="rounded-[24px] border border-line bg-slate-50 p-4">
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <h3 className="text-sm font-semibold text-ink">Lead coverage</h3>
                          <p className="mt-1 text-xs text-ink-soft">Ownership, contact state, and next action in one list.</p>
                        </div>
                        <span className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">
                          {visibleCoverageItems.length} visible
                        </span>
                      </div>
                      <div className="mt-4 space-y-3">
                        {visibleCoverageItems.length > 0 ? visibleCoverageItems.slice(0, 12).map((item) => (
                          <LeadCoverageCard key={item.leadId} item={item} />
                        )) : (
                          <div className="rounded-2xl border border-dashed border-line bg-white px-4 py-6 text-sm text-ink-soft">
                            No leads match this search.
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                </section>
              ) : coverageQuery.isError ? (
                <section className="rounded-[28px] border border-amber-200 bg-amber-50 p-5 text-sm text-amber-800">
                  Lead coverage could not be loaded right now. The queue is still available below.
                </section>
              ) : null}

              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <AgentSummaryCard label="Urgent" value={inbox.urgentCount} helper="Needs attention first." />
                <AgentSummaryCard label="New inbound" value={inbox.newInboundProspectsCount} helper="Fresh prospects that just landed." />
                <AgentSummaryCard label="Overdue follow-up" value={inbox.overdueFollowUpsCount} helper="Past the promised next touch." />
                <AgentSummaryCard label="Lead actions" value={inbox.openLeadActionsCount} helper="Outbound tasks still open." />
              </div>

              <div className="rounded-[24px] border border-line bg-white p-5">
                <div className="flex flex-col gap-4">
                  <div className="flex flex-wrap gap-2">
                    {focusTabs.filter((tab) => tab.id === focusFilter || tab.count > 0 || tab.id === 'all').map((tab) => (
                      <button
                        key={tab.id}
                        type="button"
                        onClick={() => setFocusFilter(tab.id)}
                        className={focusFilter === tab.id ? 'button-primary px-4 py-2 text-sm' : 'button-secondary px-4 py-2 text-sm'}
                      >
                        {tab.label} ({tab.count})
                      </button>
                    ))}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {ownershipTabs.filter((tab) => tab.id === ownershipFilter || tab.count > 0 || tab.id === 'all').map((tab) => (
                      <button
                        key={tab.id}
                        type="button"
                        onClick={() => setOwnershipFilter(tab.id)}
                        className={ownershipFilter === tab.id ? 'button-primary px-4 py-2 text-sm' : 'button-secondary px-4 py-2 text-sm'}
                      >
                        {tab.label} ({tab.count})
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                {visibleItems.length > 0 ? visibleItems.map((item) => (
                  <article key={item.id} className="rounded-[24px] border border-line bg-white p-5">
                    <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                      <div className="space-y-3">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${itemTone(item)}`}>
                            {item.itemLabel}
                          </span>
                          <span className="inline-flex rounded-full border border-line bg-slate-50 px-3 py-1 text-xs font-semibold text-ink-soft">
                            {titleize(item.unifiedStatus)}
                          </span>
                          {item.isUrgent ? (
                            <span className="inline-flex rounded-full border border-rose-200 bg-rose-50 px-3 py-1 text-xs font-semibold text-rose-700">
                              Urgent
                            </span>
                          ) : null}
                        </div>
                        <div>
                          <h3 className="text-lg font-semibold text-ink">{item.title}</h3>
                          <p className="mt-1 text-sm text-ink-soft">{item.subtitle}</p>
                        </div>
                        <p className="text-sm text-ink-soft">{item.description}</p>
                        <div className="grid gap-3 text-sm md:grid-cols-2 xl:grid-cols-4">
                          <div>
                            <p className="text-ink-soft">Owner</p>
                            <p className="font-medium text-ink">{item.assignedAgentName ?? (item.isUnassigned ? 'Unassigned' : 'Not set')}</p>
                          </div>
                          <div>
                            <p className="text-ink-soft">Updated</p>
                            <p className="font-medium text-ink">{fmtDate(item.updatedAt)}</p>
                          </div>
                          <div>
                            <p className="text-ink-soft">Created</p>
                            <p className="font-medium text-ink">{fmtDate(item.createdAt)}</p>
                          </div>
                          <div>
                            <p className="text-ink-soft">Due</p>
                            <p className="font-medium text-ink">{item.dueAt ? fmtDate(item.dueAt) : 'Not set'}</p>
                          </div>
                        </div>
                      </div>

                      <div className="flex flex-wrap gap-3 xl:justify-end">
                        {item.isUnassigned && item.campaignId ? (
                          <button
                            type="button"
                            onClick={() => assignCampaignMutation.mutate(item.campaignId!)}
                            disabled={assignCampaignMutation.isPending}
                            className="button-secondary px-4 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60"
                          >
                            Assign to me
                          </button>
                        ) : null}
                        {item.isUnassigned && item.leadId && item.leadActionId ? (
                          <button
                            type="button"
                            onClick={() => assignLeadActionMutation.mutate({ leadId: item.leadId!, actionId: item.leadActionId! })}
                            disabled={assignLeadActionMutation.isPending}
                            className="button-secondary px-4 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-60"
                          >
                            Assign action
                          </button>
                        ) : null}
                        <Link to={item.routePath} className="button-primary px-4 py-2 text-sm">
                          {item.routeLabel}
                        </Link>
                      </div>
                    </div>
                  </article>
                )) : (
                  <div className="rounded-[24px] border border-dashed border-line bg-white px-5 py-10 text-sm text-ink-soft">
                    No Lead Ops items match this view right now.
                  </div>
                )}
              </div>
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

function LeadCoverageCard({ item }: { item: LeadOpsCoverageItem }) {
  return (
    <article className="rounded-2xl border border-line bg-white p-4">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <span className="inline-flex rounded-full border border-line bg-slate-50 px-3 py-1 text-xs font-semibold text-ink-soft">
              {titleize(item.unifiedStatus)}
            </span>
            <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${item.hasBeenContacted ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : 'border-amber-200 bg-amber-50 text-amber-700'}`}>
              {item.hasBeenContacted ? 'Contact started' : 'No contact yet'}
            </span>
            {item.convertedToSale ? (
              <span className="inline-flex rounded-full border border-brand/20 bg-brand-soft px-3 py-1 text-xs font-semibold text-brand">
                Won
              </span>
            ) : item.hasProspect ? (
              <span className="inline-flex rounded-full border border-sky-200 bg-sky-50 px-3 py-1 text-xs font-semibold text-sky-700">
                Prospect
              </span>
            ) : null}
          </div>
          <div>
            <h3 className="text-base font-semibold text-ink">{item.leadName}</h3>
            <p className="mt-1 text-sm text-ink-soft">{item.location} | {item.category} | {titleize(item.source)}</p>
          </div>
          <div className="grid gap-3 text-sm md:grid-cols-2 xl:grid-cols-4">
            <div>
              <p className="text-ink-soft">Owner</p>
              <p className="font-medium text-ink">{coverageOwnerLabel(item)}</p>
            </div>
            <div>
              <p className="text-ink-soft">Last contacted</p>
              <p className="font-medium text-ink">{item.lastContactedAt ? fmtDate(item.lastContactedAt) : 'Not yet contacted'}</p>
            </div>
            <div>
              <p className="text-ink-soft">Next action</p>
              <p className="font-medium text-ink">{item.nextAction}</p>
            </div>
            <div>
              <p className="text-ink-soft">Due</p>
              <p className="font-medium text-ink">{item.nextActionDueAt ? fmtDate(item.nextActionDueAt) : 'Not set'}</p>
            </div>
          </div>
        </div>

        <div className="flex flex-wrap gap-3 xl:justify-end">
          <Link to={item.routePath} className="button-primary px-4 py-2 text-sm">
            {item.activeCampaignId ? 'Open deal' : 'Open lead'}
          </Link>
        </div>
      </div>
    </article>
  );
}

export default AgentLeadOpsPage;

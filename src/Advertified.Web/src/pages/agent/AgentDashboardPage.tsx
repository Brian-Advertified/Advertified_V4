import { FolderKanban, MessageSquareText, UserRoundSearch } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  queueTone,
  useAgentInboxQuery,
} from './agentWorkspace';
import {
  AgentSectionIntro,
  AgentSummaryCard,
  buildTasks,
} from './agentSectionShared';
import {
  buildAgentCampaignQueueHref,
  buildAgentMessagesHref,
  buildQueueFiltersForInboxItem,
} from './agentCampaignQueueFilters';

function shorten(value: string, maxLength: number) {
  const trimmed = value.trim();
  return trimmed.length <= maxLength ? trimmed : `${trimmed.slice(0, maxLength - 1).trimEnd()}...`;
}

export function AgentDashboardPage() {
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading agent dashboard...">
      <AgentPageShell title="Dashboard" description="Start with the queue, see the few numbers that matter, and jump straight into work.">
        {(() => {
          const inbox = inboxQuery.data;
          if (!inbox) {
            return null;
          }

          const tasks = buildTasks(inbox);
          const topQueue = [...tasks.urgent, ...tasks.review.filter((item) => !tasks.urgent.some((urgent) => urgent.id === item.id))]
            .slice(0, 4);
          const recentItems = inbox.items.slice(0, 5);
          const todayList = [
            {
              label: 'New leads',
              value: inbox.newlyPaidCount,
              helper: 'Campaigns that need first contact.',
              href: buildAgentCampaignQueueHref({ stage: 'ready_to_work', ownership: 'all', focus: 'newly_paid' }),
              actionLabel: 'Open new leads',
            },
            {
              label: 'Needs review',
              value: inbox.agentReviewCount + inbox.manualReviewCount,
              helper: 'Recommendation work waiting on you.',
              href: buildAgentCampaignQueueHref({ stage: 'ready_to_work', ownership: 'all', focus: 'needs_review' }),
              actionLabel: 'Open review queue',
            },
            {
              label: 'Waiting on client',
              value: inbox.waitingOnClientCount,
              helper: 'Sent work with no final client response yet.',
              href: buildAgentCampaignQueueHref({ stage: 'waiting_on_client', ownership: 'all' }),
              actionLabel: 'Open client replies',
            },
            {
              label: 'Budget issues',
              value: inbox.overBudgetCount,
              helper: 'Campaigns that need setup changes before progress.',
              href: buildAgentCampaignQueueHref({ stage: 'all', ownership: 'all', focus: 'budget_issues' }),
              actionLabel: 'Open budget issues',
            },
          ];
          const supportLinks = [
            { label: 'Campaign queue', href: '/agent/campaigns', helper: 'Open the full work queue.', icon: FolderKanban },
            { label: 'Clients', href: '/agent/leads', helper: 'See each client and their latest campaign.', icon: UserRoundSearch },
            { label: 'Messages', href: '/agent/messages', helper: 'Reply to unread client conversations.', icon: MessageSquareText },
          ];

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                {todayList.map((item) => (
                  <AgentSummaryCard
                    key={item.label}
                    label={item.label}
                    value={item.value}
                    helper={item.helper}
                    href={item.href}
                    actionLabel={item.actionLabel}
                  />
                ))}
              </div>

              <div className="grid gap-6 xl:grid-cols-[minmax(0,1.15fr)_320px]">
                <div className="panel px-6 py-6">
                  <AgentSectionIntro
                    title="Work queue"
                    description="Most important items first."
                    action={<Link to="/agent/campaigns" className="button-secondary px-4 py-2">View all</Link>}
                  />

                  <div className="mt-5 space-y-3">
                    {topQueue.length > 0 ? topQueue.map((item) => (
                      <article key={item.id} className="rounded-[20px] border border-line bg-slate-50/70 p-4">
                        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                          <div className="min-w-0">
                            <div className="flex flex-wrap items-center gap-2">
                              {item.isUrgent ? (
                                <Link to={buildAgentCampaignQueueHref({ stage: 'all', ownership: 'all', focus: 'urgent' })} className="pill border-rose-200 bg-rose-50 text-rose-700 transition hover:border-rose-300 hover:bg-rose-100">
                                  Urgent
                                </Link>
                              ) : null}
                              {item.manualReviewRequired ? (
                                <Link to={buildAgentCampaignQueueHref({ stage: 'ready_to_work', ownership: 'all', focus: 'needs_review' })} className="pill border-amber-200 bg-amber-50 text-amber-700 transition hover:border-amber-300 hover:bg-amber-100">
                                  Needs review
                                </Link>
                              ) : null}
                              {item.isOverBudget ? (
                                <Link to={buildAgentCampaignQueueHref({ stage: 'all', ownership: 'all', focus: 'budget_issues' })} className="pill border-rose-200 bg-rose-50 text-rose-700 transition hover:border-rose-300 hover:bg-rose-100">
                                  Over budget
                                </Link>
                              ) : null}
                              <Link
                                to={buildAgentCampaignQueueHref(buildQueueFiltersForInboxItem(item))}
                                className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold transition hover:-translate-y-0.5 ${queueTone(item.queueStage)}`}
                              >
                                {item.queueLabel}
                              </Link>
                            </div>
                            <h3 className="mt-3 text-base font-semibold text-ink">{item.campaignName}</h3>
                            <p className="mt-1 text-sm text-ink-soft">{item.clientName} | {fmtCurrency(item.selectedBudget)}</p>
                            <p className="mt-3 text-sm text-ink">{shorten(item.nextAction, 140)}</p>
                          </div>

                          <div className="flex shrink-0 flex-wrap gap-2">
                            <Link to={`/agent/campaigns/${item.id}`} className="button-primary px-4 py-2 text-sm font-semibold">
                              Open
                            </Link>
                            <Link to={buildAgentMessagesHref(item.id)} className="button-secondary px-4 py-2 text-sm font-semibold">
                              Message
                            </Link>
                          </div>
                        </div>
                      </article>
                    )) : (
                      <div className="rounded-[20px] border border-line bg-slate-50/70 px-4 py-6 text-sm text-ink-soft">
                        No urgent or review-heavy work right now.
                      </div>
                    )}
                  </div>
                </div>

                <div className="panel px-6 py-6">
                  <AgentSectionIntro
                    title="Today"
                    description="Small support rail only."
                  />

                  <div className="mt-5 space-y-3">
                    <div className="rounded-[20px] border border-line bg-slate-50/70 p-4">
                      <p className="text-sm font-semibold text-ink">Top priority</p>
                      <p className="mt-2 text-sm text-ink-soft">Work items blocking launch or client movement first.</p>
                    </div>

                    {supportLinks.map((item) => {
                      const Icon = item.icon;

                      return (
                        <Link key={item.href} to={item.href} className="flex items-start gap-3 rounded-[20px] border border-line bg-slate-50/70 p-4 transition hover:border-brand/30 hover:bg-brand-soft/20">
                          <div className="rounded-2xl bg-white p-3 text-brand">
                            <Icon className="size-4" />
                          </div>
                          <div>
                            <p className="text-sm font-semibold text-ink">{item.label}</p>
                            <p className="mt-1 text-sm text-ink-soft">{item.helper}</p>
                          </div>
                        </Link>
                      );
                    })}

                    <div className="rounded-[20px] border border-line bg-slate-50/70 p-4">
                      <p className="text-sm font-semibold text-ink">Recent work</p>
                      <div className="mt-3 space-y-3">
                        {recentItems.length > 0 ? recentItems.map((item) => (
                          <Link key={item.id} to={`/agent/campaigns/${item.id}`} className="flex items-start justify-between gap-3 rounded-2xl border border-line bg-white px-3 py-3 transition hover:border-brand/30 hover:bg-brand-soft/10">
                            <div className="min-w-0">
                              <p className="truncate text-sm font-medium text-ink">{item.campaignName}</p>
                              <p className="mt-1 text-xs text-ink-soft">{item.clientName}</p>
                            </div>
                            <span className={`inline-flex rounded-full border px-3 py-1 text-[11px] font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span>
                          </Link>
                        )) : (
                          <p className="text-sm text-ink-soft">No recent campaign work yet.</p>
                        )}
                      </div>
                    </div>
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

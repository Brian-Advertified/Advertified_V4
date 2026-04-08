import { ArrowRight, FolderKanban, MessageSquareText, UserRoundSearch } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  queueTone,
  useAgentInboxQuery,
} from './agentWorkspace';
import {
  AgentPageLead,
  AgentSectionIntro,
  AgentSummaryCard,
  buildTasks,
} from './agentSectionShared';

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
            { label: 'New leads', value: inbox.newlyPaidCount, helper: 'Campaigns that need first contact.' },
            { label: 'Needs review', value: inbox.agentReviewCount + inbox.readyToSendCount, helper: 'Recommendation work waiting on you.' },
            { label: 'Waiting on client', value: inbox.waitingOnClientCount, helper: 'Sent work with no final client response yet.' },
            { label: 'Budget issues', value: inbox.overBudgetCount, helper: 'Campaigns that need setup changes before progress.' },
          ];
          const supportLinks = [
            { label: 'Campaign queue', href: '/agent/campaigns', helper: 'Open the full work queue.', icon: FolderKanban },
            { label: 'Clients', href: '/agent/leads', helper: 'See each client and their latest campaign.', icon: UserRoundSearch },
            { label: 'Messages', href: '/agent/messages', helper: 'Reply to unread client conversations.', icon: MessageSquareText },
          ];

          return (
            <section className="space-y-6">
              <AgentPageLead
                eyebrow="Dashboard"
                title="One summary strip, one work queue, one support panel."
                description="This page is the simple starting point for the day. Check what needs action, open the next campaign, and move on without hunting through extra screens."
                aside={(
                  <Link to="/agent/campaigns" className="button-primary inline-flex items-center gap-2 rounded-full px-4 py-3 text-sm font-semibold">
                    Open campaign queue
                    <ArrowRight className="size-4" />
                  </Link>
                )}
              />

              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                {todayList.map((item) => (
                  <AgentSummaryCard key={item.label} label={item.label} value={item.value} helper={item.helper} />
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
                              {item.isUrgent ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Urgent</span> : null}
                              {item.manualReviewRequired ? <span className="pill border-amber-200 bg-amber-50 text-amber-700">Needs review</span> : null}
                              {item.isOverBudget ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Over budget</span> : null}
                              <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span>
                            </div>
                            <h3 className="mt-3 text-base font-semibold text-ink">{item.campaignName}</h3>
                            <p className="mt-1 text-sm text-ink-soft">{item.clientName} | {fmtCurrency(item.selectedBudget)}</p>
                            <p className="mt-3 text-sm text-ink">{shorten(item.nextAction, 140)}</p>
                          </div>

                          <div className="flex shrink-0 flex-wrap gap-2">
                            <Link to={`/agent/campaigns/${item.id}`} className="button-primary px-4 py-2 text-sm font-semibold">
                              Open
                            </Link>
                            <Link to="/agent/messages" className="button-secondary px-4 py-2 text-sm font-semibold">
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
                          <div key={item.id} className="flex items-start justify-between gap-3 rounded-2xl border border-line bg-white px-3 py-3">
                            <div className="min-w-0">
                              <p className="truncate text-sm font-medium text-ink">{item.campaignName}</p>
                              <p className="mt-1 text-xs text-ink-soft">{item.clientName}</p>
                            </div>
                            <span className={`inline-flex rounded-full border px-3 py-1 text-[11px] font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span>
                          </div>
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

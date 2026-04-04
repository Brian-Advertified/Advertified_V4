import { ArrowRight, CircleAlert, CircleDollarSign, Clock3, FolderKanban, MessageSquareText, Send, Sparkles, UserRoundSearch, UsersRound } from 'lucide-react';
import { Link } from 'react-router-dom';
import { canAccessAiStudioForStatus } from '../../features/campaigns/aiStudioAccess';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  queueTone,
  useAgentInboxQuery,
} from './agentWorkspace';
import { buildTasks } from './agentSectionShared';

function shorten(value: string, maxLength: number) {
  const trimmed = value.trim();
  return trimmed.length <= maxLength ? trimmed : `${trimmed.slice(0, maxLength - 1).trimEnd()}…`;
}

export function AgentDashboardPage() {
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading agent dashboard...">
      <AgentPageShell title="Dashboard" description="See what needs attention now, which clients need a response, and which campaigns should move next.">
        {(() => {
          const inbox = inboxQuery.data;
          if (!inbox) {
            return null;
          }
          const stageColumns = [
            { label: 'Lead', items: inbox.items.filter((item) => item.queueStage === 'newly_paid').slice(0, 3) },
            { label: 'Purchased', items: inbox.items.filter((item) => item.queueStage === 'brief_waiting').slice(0, 3) },
            { label: 'Planning', items: inbox.items.filter((item) => item.queueStage === 'planning_ready' || item.queueStage === 'agent_review').slice(0, 3) },
            { label: 'Approval', items: inbox.items.filter((item) => item.queueStage === 'ready_to_send' || item.queueStage === 'waiting_on_client').slice(0, 3) },
          ];
          const tasks = buildTasks(inbox);
          const focusTasks = [...tasks.urgent, ...tasks.review.filter((item) => !tasks.urgent.some((urgent) => urgent.id === item.id))]
            .slice(0, 4);
          const quickLinks = [
            { label: 'Open campaign queue', href: '/agent/campaigns', icon: FolderKanban, helper: 'Use the main queue to move campaigns forward.' },
            { label: 'View clients', href: '/agent/leads', icon: UserRoundSearch, helper: 'See each client and open their latest campaign.' },
            { label: 'Reply to messages', href: '/agent/messages', icon: MessageSquareText, helper: 'Check unread conversations and send replies.' },
            { label: 'Review sales', href: '/agent/sales', icon: CircleDollarSign, helper: 'See paid sales and converted prospects in the live queue.' },
          ];
          const recentItems = inbox.items.slice(0, 6);

          return (
            <section className="space-y-6">
              <div className="panel overflow-hidden border-brand/10 bg-[radial-gradient(circle_at_top_left,_rgba(15,118,110,0.14),_transparent_30%),radial-gradient(circle_at_bottom_right,_rgba(245,158,11,0.14),_transparent_24%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(242,248,246,0.96))] p-6">
                <div className="flex flex-col gap-5 xl:flex-row xl:items-center xl:justify-between">
                  <div className="space-y-3">
                    <div className="inline-flex items-center gap-2 rounded-full border border-brand/15 bg-white/80 px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.26em] text-brand">
                      Agent overview
                    </div>
                    <div>
                      <h3 className="text-2xl font-semibold text-ink">Start here to see what needs action today.</h3>
                      <p className="mt-2 max-w-3xl text-sm leading-6 text-ink-soft">
                        This page gives you one clear starting point, then sends you into campaigns, clients, messages, or sales without needing to learn extra workflow screens first.
                      </p>
                    </div>
                  </div>
                  <div className="grid gap-3 sm:grid-cols-3">
                    <div className="rounded-2xl border border-white/80 bg-white/80 px-4 py-3">
                      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Clients</p>
                      <p className="mt-2 text-sm text-ink">Open your client list and recent account activity</p>
                    </div>
                    <Link to="/agent/campaigns" className="rounded-2xl border border-white/80 bg-white/80 px-4 py-3 transition hover:border-brand/30 hover:bg-brand-soft/30">
                      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Campaigns</p>
                      <p className="mt-2 text-sm font-semibold text-ink">{tasks.urgent.length + tasks.review.length + tasks.waiting.length} active follow-ups</p>
                    </Link>
                    <Link to="/agent/messages" className="rounded-2xl border border-white/80 bg-white/80 px-4 py-3 transition hover:border-brand/30 hover:bg-brand-soft/30">
                      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Messages</p>
                      <p className="mt-2 text-sm font-semibold text-ink">Jump into unread client conversations</p>
                    </Link>
                  </div>
                </div>
              </div>

              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                {[
                  { label: 'New leads', value: inbox.newlyPaidCount, helper: 'Paid campaigns that still need first contact.', icon: UsersRound },
                  { label: 'Pending briefs', value: inbox.briefWaitingCount, helper: 'Clients who paid but still owe planning input.', icon: Clock3 },
                  { label: 'Recommendations due', value: inbox.planningReadyCount + inbox.agentReviewCount, helper: 'Campaigns waiting for build or strategist review.', icon: Sparkles },
                  { label: 'Waiting on client', value: inbox.waitingOnClientCount, helper: 'Campaigns that still need a client reply.', icon: Send },
                ].map((stat) => {
                  const Icon = stat.icon;
                  return (
                    <div key={stat.label} className="rounded-[28px] border border-line bg-white px-5 py-5 shadow-[0_12px_34px_rgba(15,23,42,0.05)]">
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">{stat.label}</p>
                          <p className="mt-4 text-3xl font-semibold text-ink">{stat.value}</p>
                          <p className="mt-2 text-sm text-ink-soft">{stat.helper}</p>
                        </div>
                        <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                          <Icon className="size-5" />
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
                <div className="rounded-[30px] border border-line bg-white p-6 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <h3 className="text-xl font-semibold text-ink">Today&apos;s pipeline</h3>
                      <p className="mt-2 text-sm text-ink-soft">A simple view of what has come in, what is waiting, and what needs a client decision.</p>
                    </div>
                    <Link to="/agent/campaigns" className="button-secondary px-4 py-2">Open campaigns</Link>
                  </div>
                  <div className="mt-5 grid gap-4 xl:grid-cols-4">
                    {stageColumns.map((column) => (
                      <div key={column.label} className="rounded-[22px] border border-line bg-slate-50/80 p-4">
                        <div>
                          <div className="flex items-center justify-between gap-3">
                            <h4 className="text-sm font-semibold text-ink">{column.label}</h4>
                            <span className="pill">{column.items.length}</span>
                          </div>
                          <div className="mt-4 space-y-3">
                            {column.items.length > 0 ? column.items.map((item) => (
                              <Link key={item.id} to={`/agent/campaigns/${item.id}`} className="block rounded-2xl border border-line bg-white p-3 text-sm transition hover:border-brand/30 hover:bg-brand-soft/20">
                                <p className="font-semibold text-ink">{item.clientName}</p>
                                <p className="mt-1 text-xs text-ink-soft">{shorten(item.campaignName, 32)}</p>
                                <p className="mt-2 text-xs text-ink-soft">{shorten(item.nextAction, 72)}</p>
                              </Link>
                            )) : <p className="text-sm text-ink-soft">No campaigns here.</p>}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="rounded-[30px] border border-line bg-white p-6 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
                    <h3 className="text-lg font-semibold text-ink">My priorities</h3>
                    <p className="mt-2 text-sm text-ink-soft">Pull your next actions from the live queue and jump directly into campaign work.</p>
                    <div className="mt-4 space-y-4">
                      {focusTasks.length > 0 ? focusTasks.map((item) => (
                        <div key={item.id} className="rounded-2xl border border-line bg-slate-50/70 p-4">
                          <div className="flex items-start justify-between gap-4">
                            <div>
                              <p className="font-semibold text-ink">{item.clientName}</p>
                              <p className="mt-1 text-xs text-ink-soft">{item.campaignName}</p>
                            </div>
                            <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span>
                          </div>
                          <p className="mt-3 text-sm text-ink-soft">{item.nextAction}</p>
                        </div>
                      )) : <p className="text-sm text-ink-soft">Nothing urgent or review-heavy right now.</p>}
                    </div>
                    <div className="mt-4">
                      <Link to="/agent/campaigns" className="button-secondary px-4 py-2">Open campaign queue</Link>
                    </div>
                  </div>

                  <div className="rounded-[30px] border border-line bg-white p-6 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
                    <h3 className="text-lg font-semibold text-ink">Quick actions</h3>
                    <div className="mt-4 grid gap-3">
                      {quickLinks.map((item) => {
                        const Icon = item.icon;
                        return (
                          <Link key={item.href} to={item.href} className="flex items-center justify-between gap-4 rounded-2xl border border-line bg-slate-50/70 px-4 py-4 transition hover:border-brand/30 hover:bg-brand-soft/20">
                            <div className="flex items-center gap-3">
                              <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                                <Icon className="size-5" />
                              </div>
                              <div>
                                <p className="font-semibold text-ink">{item.label}</p>
                                <p className="mt-1 text-sm text-ink-soft">{item.helper}</p>
                              </div>
                            </div>
                            <ArrowRight className="size-4 text-ink-soft" />
                          </Link>
                        );
                      })}
                    </div>
                  </div>
                </div>
              </div>

              <div className="grid gap-6 xl:grid-cols-[1.15fr_0.85fr]">
                <div className="rounded-[30px] border border-line bg-white p-6 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <h3 className="text-lg font-semibold text-ink">Recent campaign work</h3>
                      <p className="mt-2 text-sm text-ink-soft">Open the latest campaigns directly from here when you want to continue work where you left off.</p>
                    </div>
                    <Link to="/agent/campaigns" className="button-secondary px-4 py-2">Open campaigns</Link>
                  </div>
                  <div className="mt-5 overflow-hidden rounded-[22px] border border-line">
                    <div className="hidden grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)_140px_120px] bg-slate-50 px-4 py-3 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft md:grid">
                      <div>Campaign</div>
                      <div>Client / Budget</div>
                      <div>Status</div>
                      <div>Action</div>
                    </div>
                    <div className="divide-y divide-line">
                      {recentItems.length > 0 ? recentItems.map((item) => (
                        <div key={item.id} className="px-4 py-4 text-sm">
                          <div className="space-y-3 md:hidden">
                            <div>
                              <p className="font-semibold text-ink">{item.campaignName}</p>
                              <p className="mt-1 text-xs text-ink-soft">{item.packageBandName}</p>
                            </div>
                            <p className="text-xs text-ink-soft">{item.nextAction}</p>
                            <div className="flex items-center justify-between gap-3">
                              <div>
                                <p className="font-medium text-ink">{item.clientName}</p>
                                <p className="mt-1 text-xs text-ink-soft">{fmtCurrency(item.selectedBudget)}</p>
                              </div>
                              <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span>
                            </div>
                            <Link to={`/agent/campaigns/${item.id}`} className="button-secondary inline-flex px-3 py-2">Open campaign</Link>
                            {canAccessAiStudioForStatus(item.status) ? (
                              <Link to={`/ai-studio?campaignId=${item.id}`} className="button-secondary inline-flex px-3 py-2">AI Studio</Link>
                            ) : null}
                          </div>

                          <div className="hidden grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)_140px_120px] items-start gap-4 md:grid">
                            <div>
                              <p className="font-semibold text-ink">{item.campaignName}</p>
                              <p className="mt-1 text-xs text-ink-soft">{item.packageBandName}</p>
                              <p className="mt-2 text-xs text-ink-soft">{item.nextAction}</p>
                            </div>
                            <div>
                              <p className="font-medium text-ink">{item.clientName}</p>
                              <p className="mt-1 text-xs text-ink-soft">{fmtCurrency(item.selectedBudget)}</p>
                            </div>
                            <div>
                              <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span>
                            </div>
                            <div>
                              <Link to={`/agent/campaigns/${item.id}`} className="button-secondary px-3 py-2">Open</Link>
                              {canAccessAiStudioForStatus(item.status) ? (
                                <Link to={`/ai-studio?campaignId=${item.id}`} className="button-secondary mt-2 inline-flex px-3 py-2">AI</Link>
                              ) : null}
                            </div>
                          </div>
                        </div>
                      )) : (
                        <div className="px-4 py-6 text-sm text-ink-soft">No recent campaign activity yet.</div>
                      )}
                    </div>
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="rounded-[30px] border border-line bg-white p-6 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
                    <div className="flex items-center gap-3">
                      <CircleAlert className="size-5 text-amber-600" />
                      <h3 className="text-lg font-semibold text-ink">Operations watchlist</h3>
                    </div>
                    <div className="mt-4 grid gap-3 sm:grid-cols-2">
                      {[
                        { label: 'Manual review', value: inbox.manualReviewCount },
                        { label: 'Over budget', value: inbox.overBudgetCount },
                        { label: 'Stale work', value: inbox.staleCount },
                        { label: 'Unassigned', value: inbox.unassignedCount },
                      ].map((item) => (
                        <div key={item.label} className="rounded-2xl border border-line bg-slate-50 px-4 py-3">
                          <p className="text-xs font-semibold uppercase tracking-[0.14em] text-ink-soft">{item.label}</p>
                          <p className="mt-2 text-2xl font-semibold text-ink">{item.value}</p>
                        </div>
                      ))}
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

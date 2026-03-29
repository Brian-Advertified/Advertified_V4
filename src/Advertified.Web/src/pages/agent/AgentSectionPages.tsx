import { useState } from 'react';
import { Link } from 'react-router-dom';
import { BriefcaseBusiness, CircleAlert, CircleCheckBig, Clock3, FolderKanban, MessageSquareText, Search, Send, Sparkles, TrendingUp, UsersRound } from 'lucide-react';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  fmtDate,
  queueTone,
  titleize,
  useAgentCampaignsQuery,
  useAgentInboxQuery,
  usePackagesQuery,
} from './agentWorkspace';

function buildClientRows(campaigns: Awaited<ReturnType<typeof useAgentCampaignsQuery>['data']>, search: string) {
  const grouped = new Map<string, {
    userId: string;
    clientName: string;
    clientEmail: string;
    campaignCount: number;
    activeCount: number;
    awaitingApprovalCount: number;
    latestActivityAt?: string;
    latestActivity: string;
    topRegion: string;
    topPackage: string;
  }>();

  for (const campaign of campaigns ?? []) {
    const current = grouped.get(campaign.userId) ?? {
      userId: campaign.userId,
      clientName: campaign.clientName ?? campaign.businessName ?? 'Client account',
      clientEmail: campaign.clientEmail ?? 'No email captured',
      campaignCount: 0,
      activeCount: 0,
      awaitingApprovalCount: 0,
      latestActivityAt: campaign.createdAt,
      latestActivity: campaign.nextAction,
      topRegion: campaign.brief?.provinces?.[0] ?? campaign.brief?.areas?.[0] ?? 'Not set',
      topPackage: campaign.packageBandName,
    };

    current.campaignCount += 1;
    if (campaign.status !== 'approved') current.activeCount += 1;
    if (campaign.recommendations.some((item) => item.status === 'sent_to_client')) current.awaitingApprovalCount += 1;
    if (!current.latestActivityAt || new Date(campaign.createdAt).getTime() > new Date(current.latestActivityAt).getTime()) {
      current.latestActivityAt = campaign.createdAt;
      current.latestActivity = campaign.nextAction;
      current.topRegion = campaign.brief?.provinces?.[0] ?? campaign.brief?.areas?.[0] ?? current.topRegion;
      current.topPackage = campaign.packageBandName;
    }

    grouped.set(campaign.userId, current);
  }

  return Array.from(grouped.values())
    .filter((item) => `${item.clientName} ${item.clientEmail} ${item.topRegion} ${item.topPackage}`.toLowerCase().includes(search.toLowerCase()))
    .sort((left, right) => (right.awaitingApprovalCount - left.awaitingApprovalCount) || (right.activeCount - left.activeCount) || left.clientName.localeCompare(right.clientName));
}

function getAverageTurnaroundDays(campaigns: Awaited<ReturnType<typeof useAgentCampaignsQuery>['data']>) {
  const completed = (campaigns ?? []).filter((campaign) => campaign.recommendations.some((item) => item.status === 'approved'));
  if (completed.length === 0) return null;
  const totalDays = completed.reduce((sum, campaign) => {
    const createdAt = new Date(campaign.createdAt).getTime();
    const approvedAt = campaign.timeline.find((step) => step.key === 'approval' && step.state === 'complete') ? Date.now() : createdAt;
    return sum + Math.max(0, (approvedAt - createdAt) / (1000 * 60 * 60 * 24));
  }, 0);
  return (totalDays / completed.length).toFixed(1);
}

function buildTasks(inbox: NonNullable<ReturnType<typeof useAgentInboxQuery>['data']>) {
  return {
    urgent: inbox.items.filter((item) => item.isUrgent).slice(0, 5),
    review: inbox.items.filter((item) => item.queueStage === 'agent_review' || item.queueStage === 'ready_to_send').slice(0, 5),
    waiting: inbox.items.filter((item) => item.queueStage === 'waiting_on_client').slice(0, 5),
  };
}

export function AgentDashboardPage() {
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading agent dashboard...">
      <AgentPageShell title="Agent dashboard" description="Daily sales activity, planning queue, approvals, and the client work that needs attention next.">
        {(() => {
          const inbox = inboxQuery.data!;
          const stageColumns = [
            { label: 'Lead', items: inbox.items.filter((item) => item.queueStage === 'newly_paid').slice(0, 3) },
            { label: 'Purchased', items: inbox.items.filter((item) => item.queueStage === 'brief_waiting').slice(0, 3) },
            { label: 'Planning', items: inbox.items.filter((item) => item.queueStage === 'planning_ready' || item.queueStage === 'agent_review').slice(0, 3) },
            { label: 'Approval', items: inbox.items.filter((item) => item.queueStage === 'ready_to_send' || item.queueStage === 'waiting_on_client').slice(0, 3) },
          ];
          const tasks = buildTasks(inbox);

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                {[
                  { label: 'New leads', value: inbox.newlyPaidCount, helper: 'Paid campaigns that still need first contact.', icon: UsersRound },
                  { label: 'Pending briefs', value: inbox.briefWaitingCount, helper: 'Clients who paid but still owe planning input.', icon: Clock3 },
                  { label: 'Recommendations due', value: inbox.planningReadyCount + inbox.agentReviewCount, helper: 'Campaigns waiting for build or strategist review.', icon: Sparkles },
                  { label: 'Awaiting approval', value: inbox.waitingOnClientCount, helper: 'Sent to client and waiting for response.', icon: Send },
                ].map((stat) => {
                  const Icon = stat.icon;
                  return (
                    <div key={stat.label} className="panel bg-white/90 px-5 py-5">
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">{stat.label}</p>
                          <p className="mt-3 text-3xl font-semibold tracking-tight text-ink">{stat.value}</p>
                          <p className="mt-2 text-sm leading-6 text-ink-soft">{stat.helper}</p>
                        </div>
                        <div className="rounded-2xl bg-brand-soft p-3 text-brand"><Icon className="size-5" /></div>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="grid gap-6 xl:grid-cols-[1.35fr_0.9fr]">
                <div className="panel bg-white/92 p-6">
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <h3 className="text-lg font-semibold text-ink">Today&apos;s pipeline</h3>
                      <p className="mt-2 text-sm text-ink-soft">Live campaign movement across newly paid, briefing, planning, and approval stages.</p>
                    </div>
                    <Link to="/agent/campaigns" className="button-secondary px-4 py-2">Open pipeline</Link>
                  </div>
                  <div className="mt-5 grid gap-4 xl:grid-cols-4">
                    {stageColumns.map((column) => (
                      <div key={column.label} className="rounded-[22px] border border-line bg-slate-50 p-4">
                        <p className="text-sm font-semibold text-ink">{column.label}</p>
                        <div className="mt-3 space-y-3">
                          {column.items.length > 0 ? column.items.map((item) => (
                            <Link key={item.id} to={`/agent/campaigns/${item.id}`} className="block rounded-2xl border border-line bg-white p-3 text-sm transition hover:border-brand/30">
                              <p className="font-semibold text-ink">{item.clientName}</p>
                              <p className="mt-1 text-xs text-ink-soft">{item.packageBandName} | {fmtCurrency(item.selectedBudget)}</p>
                              <p className="mt-2 text-xs text-ink-soft">{item.nextAction}</p>
                            </Link>
                          )) : <p className="text-sm text-ink-soft">No campaigns in this stage.</p>}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="space-y-4">
                  <div className="panel bg-white/92 p-6">
                    <h3 className="text-lg font-semibold text-ink">My tasks</h3>
                    <div className="mt-4 space-y-3">
                      {tasks.urgent.length > 0 ? tasks.urgent.map((item) => (
                        <Link key={item.id} to={`/agent/campaigns/${item.id}`} className="block rounded-2xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">
                          <p className="font-semibold">{item.clientName}</p>
                          <p className="mt-1">{item.nextAction}</p>
                        </Link>
                      )) : <p className="text-sm text-ink-soft">No urgent work right now.</p>}
                    </div>
                  </div>
                  <div className="panel bg-white/92 p-6">
                    <h3 className="text-lg font-semibold text-ink">Quick actions</h3>
                    <div className="mt-4 flex flex-wrap gap-3">
                      <Link to="/agent/leads" className="button-secondary px-4 py-2">Open clients</Link>
                      <Link to="/agent/recommendation-builder" className="button-secondary px-4 py-2">Build recommendation</Link>
                      <Link to="/agent/review-send" className="button-secondary px-4 py-2">Review & send</Link>
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

export function AgentLeadsClientsPage() {
  const campaignsQuery = useAgentCampaignsQuery();
  const [search, setSearch] = useState('');

  return (
    <AgentQueryBoundary query={campaignsQuery} loadingLabel="Loading leads and clients...">
      <AgentPageShell title="Leads and clients" description="Track active client accounts, current campaign load, and the latest commercial activity tied to each account.">
        {(() => {
          const rows = buildClientRows(campaignsQuery.data, search);
          return (
            <section className="space-y-6">
              <div className="panel flex flex-col gap-4 px-6 py-6 lg:flex-row lg:items-center">
                <label className="relative flex-1">
                  <Search className="absolute left-4 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
                  <input value={search} onChange={(event) => setSearch(event.target.value)} className="input-base pl-11" placeholder="Search client, email, region, or package" />
                </label>
                <div className="rounded-2xl bg-brand-soft px-4 py-3 text-sm text-brand">Live client rows built from agent campaign ownership and recommendation activity.</div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Client</th>
                      <th className="px-4 py-4">Portfolio</th>
                      <th className="px-4 py-4">Current focus</th>
                      <th className="px-4 py-4">Latest activity</th>
                      <th className="px-4 py-4 text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row) => (
                      <tr key={row.userId} className="border-t border-line">
                        <td className="px-4 py-4">
                          <p className="font-semibold text-ink">{row.clientName}</p>
                          <p className="text-xs text-ink-soft">{row.clientEmail}</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          <p>{row.campaignCount} campaign(s)</p>
                          <p className="text-xs">{row.activeCount} active | {row.awaitingApprovalCount} awaiting approval</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          <p>{row.topPackage}</p>
                          <p className="text-xs">{titleize(row.topRegion)}</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          <p>{row.latestActivity}</p>
                          <p className="text-xs">{fmtDate(row.latestActivityAt)}</p>
                        </td>
                        <td className="px-4 py-4">
                          <div className="flex justify-end">
                            <Link to="/agent/campaigns" className="button-secondary px-4 py-2">Open campaigns</Link>
                          </div>
                        </td>
                      </tr>
                    ))}
                    {rows.length === 0 ? (
                      <tr><td colSpan={5} className="px-4 py-8 text-center text-sm text-ink-soft">No client records match this search yet.</td></tr>
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

export function AgentPackageSelectionPage() {
  const packagesQuery = usePackagesQuery();
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={packagesQuery} loadingLabel="Loading package selection...">
      <AgentPageShell title="Package selection" description="Guide clients through the live package bands, the budgets they support, and the planning signals each band unlocks.">
        {(() => {
          const packageBands = packagesQuery.data ?? [];
          const inbox = inboxQuery.data;
          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="panel p-6">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Active package bands</p>
                  <p className="mt-4 text-3xl font-semibold text-ink">{packageBands.length}</p>
                </div>
                <div className="panel p-6">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Newly paid today</p>
                  <p className="mt-4 text-3xl font-semibold text-ink">{inbox?.newlyPaidCount ?? 0}</p>
                </div>
                <div className="panel p-6">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Planning ready</p>
                  <p className="mt-4 text-3xl font-semibold text-ink">{inbox?.planningReadyCount ?? 0}</p>
                </div>
              </div>

              <div className="grid gap-4 xl:grid-cols-[1.25fr_0.85fr]">
                <div className="space-y-4">
                  {packageBands.map((pkg) => (
                    <div key={pkg.id} className="panel p-6">
                      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">{pkg.code}</p>
                          <h3 className="mt-2 text-xl font-semibold text-ink">{pkg.name}</h3>
                          <p className="mt-2 text-sm text-ink-soft">{pkg.description}</p>
                        </div>
                        <span className="pill border-brand/15 bg-brand-soft text-brand">{fmtCurrency(pkg.minBudget)} - {fmtCurrency(pkg.maxBudget)}</span>
                      </div>
                      <div className="mt-4 grid gap-3 md:grid-cols-2">
                        <div className="rounded-2xl border border-line bg-slate-50 p-4 text-sm text-ink-soft">
                          <p><span className="font-semibold text-ink">Audience fit:</span> {pkg.audienceFit}</p>
                          <p className="mt-2"><span className="font-semibold text-ink">Lead time:</span> {pkg.leadTime}</p>
                          <p className="mt-2"><span className="font-semibold text-ink">Radio:</span> {titleize(pkg.includeRadio)}</p>
                          <p className="mt-2"><span className="font-semibold text-ink">TV:</span> {titleize(pkg.includeTv)}</p>
                        </div>
                        <div className="rounded-2xl border border-line bg-white p-4 text-sm text-ink-soft">
                          <p className="font-semibold text-ink">Benefits</p>
                          <ul className="mt-2 space-y-1">
                            {pkg.benefits.map((benefit) => <li key={benefit}>• {benefit}</li>)}
                          </ul>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>

                <div className="space-y-4">
                  <div className="panel p-6">
                    <h3 className="text-lg font-semibold text-ink">Package explorer</h3>
                    <p className="mt-2 text-sm text-ink-soft">Use the public package experience when you want to walk a client through indicative examples before a purchase.</p>
                    <Link to="/packages" className="button-primary mt-4 inline-flex px-4 py-2">Open package page</Link>
                  </div>
                  <div className="panel p-6">
                    <h3 className="text-lg font-semibold text-ink">Sales guidance</h3>
                    <p className="mt-2 text-sm text-ink-soft">The package layer is live and rule-backed. Geography and channel mix still become campaign-specific inside the recommendation workflow.</p>
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

export function AgentCheckoutStatusPage() {
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading checkout status...">
      <AgentPageShell title="Purchase and checkout status" description="Monitor paid package movement, brief follow-up, and which paid orders still need agent action to become active campaigns.">
        {(() => {
          const rows = (inboxQuery.data?.items ?? [])
            .filter((item) => item.queueStage === 'newly_paid' || item.queueStage === 'brief_waiting')
            .sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime());

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Newly paid</p><p className="mt-4 text-3xl font-semibold text-ink">{inboxQuery.data?.newlyPaidCount ?? 0}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Brief waiting</p><p className="mt-4 text-3xl font-semibold text-ink">{inboxQuery.data?.briefWaitingCount ?? 0}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Unassigned</p><p className="mt-4 text-3xl font-semibold text-ink">{inboxQuery.data?.unassignedCount ?? 0}</p></div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Client</th>
                      <th className="px-4 py-4">Package</th>
                      <th className="px-4 py-4">Amount</th>
                      <th className="px-4 py-4">Status</th>
                      <th className="px-4 py-4">Date</th>
                      <th className="px-4 py-4 text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((item) => (
                      <tr key={item.id} className="border-t border-line">
                        <td className="px-4 py-4 font-semibold text-ink">{item.clientName}</td>
                        <td className="px-4 py-4 text-ink-soft">{item.packageBandName}</td>
                        <td className="px-4 py-4 text-ink-soft">{fmtCurrency(item.selectedBudget)}</td>
                        <td className="px-4 py-4"><span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span></td>
                        <td className="px-4 py-4 text-ink-soft">{fmtDate(item.createdAt)}</td>
                        <td className="px-4 py-4">
                          <div className="flex justify-end">
                            <Link to={`/agent/campaigns/${item.id}`} className="button-secondary px-4 py-2">{item.queueStage === 'newly_paid' ? 'Open order' : 'Open brief'}</Link>
                          </div>
                        </td>
                      </tr>
                    ))}
                    {rows.length === 0 ? (
                      <tr><td colSpan={6} className="px-4 py-8 text-center text-sm text-ink-soft">No purchase or checkout follow-up is waiting right now.</td></tr>
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

export function AgentCampaignsPage() {
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading campaign pipeline...">
      <AgentPageShell title="Campaign pipeline" description="Manage campaigns from purchase to planning, review, approval, and launch with a live queue view and direct campaign links.">
        {(() => {
          const inbox = inboxQuery.data!;
          const columns = [
            { label: 'Purchased', stages: ['newly_paid', 'brief_waiting'] },
            { label: 'Brief complete', stages: ['planning_ready'] },
            { label: 'Recommendation sent', stages: ['agent_review', 'ready_to_send', 'waiting_on_client'] },
            { label: 'Approved', stages: ['completed'] },
          ];

          return (
            <section className="space-y-6">
              <div className="grid gap-4 xl:grid-cols-4">
                {columns.map((column) => {
                  const items = inbox.items.filter((item) => column.stages.includes(item.queueStage)).slice(0, 5);
                  return (
                    <div key={column.label} className="rounded-[24px] border border-line bg-white p-5">
                      <div className="flex items-center justify-between gap-3">
                        <h3 className="text-base font-semibold text-ink">{column.label}</h3>
                        <span className="pill">{items.length}</span>
                      </div>
                      <div className="mt-4 space-y-3">
                        {items.length > 0 ? items.map((item) => (
                          <Link key={item.id} to={`/agent/campaigns/${item.id}`} className="block rounded-2xl border border-line bg-slate-50 p-3 text-sm transition hover:border-brand/30">
                            <p className="font-semibold text-ink">{item.clientName}</p>
                            <p className="mt-1 text-xs text-ink-soft">{item.campaignName}</p>
                            <p className="mt-2 text-xs text-ink-soft">{item.nextAction}</p>
                          </Link>
                        )) : <p className="text-sm text-ink-soft">No campaigns here.</p>}
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Campaign</th>
                      <th className="px-4 py-4">Client</th>
                      <th className="px-4 py-4">Queue</th>
                      <th className="px-4 py-4">Signals</th>
                      <th className="px-4 py-4">Next action</th>
                      <th className="px-4 py-4 text-right">Open</th>
                    </tr>
                  </thead>
                  <tbody>
                    {inbox.items.map((item) => (
                      <tr key={item.id} className="border-t border-line">
                        <td className="px-4 py-4">
                          <p className="font-semibold text-ink">{item.campaignName}</p>
                          <p className="text-xs text-ink-soft">{item.packageBandName} | {fmtCurrency(item.selectedBudget)}</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">{item.clientName}</td>
                        <td className="px-4 py-4"><span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span></td>
                        <td className="px-4 py-4 text-ink-soft">
                          <div className="flex flex-wrap gap-2">
                            {item.isUrgent ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Urgent</span> : null}
                            {item.manualReviewRequired ? <span className="pill border-amber-200 bg-amber-50 text-amber-700">Manual review</span> : null}
                            {item.isOverBudget ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Over budget</span> : null}
                            {item.isStale ? <span className="pill border-line bg-slate-100 text-ink-soft">Stale {item.ageInDays}d</span> : null}
                          </div>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">{item.nextAction}</td>
                        <td className="px-4 py-4">
                          <div className="flex justify-end">
                            <Link to={`/agent/campaigns/${item.id}`} className="button-secondary px-4 py-2">Open</Link>
                          </div>
                        </td>
                      </tr>
                    ))}
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

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, ArrowRight, CheckCircle2, Clock3, LogOut, Sparkles, UsersRound } from 'lucide-react';
import { Link } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../features/auth/auth-context';
import { advertifiedApi } from '../../services/advertifiedApi';

function formatCurrency(amount: number) {
  return new Intl.NumberFormat('en-ZA', {
    style: 'currency',
    currency: 'ZAR',
    maximumFractionDigits: 0,
  }).format(amount);
}

function statusTone(status: string) {
  switch (status) {
    case 'completed':
      return 'bg-emerald-50 text-emerald-700 border-emerald-100';
    case 'waiting_on_client':
      return 'bg-amber-50 text-amber-700 border-amber-100';
    case 'agent_review':
      return 'bg-violet-50 text-violet-700 border-violet-100';
    case 'ready_to_send':
      return 'bg-sky-50 text-sky-700 border-sky-100';
    default:
      return 'bg-brand-soft text-brand border-brand/10';
  }
}

export function AgentDashboardPage() {
  const { user, logout } = useAuth();
  const inboxQuery = useQuery({ queryKey: ['agent-inbox'], queryFn: advertifiedApi.getAgentInbox });

  if (inboxQuery.isLoading) {
    return <LoadingState label="Loading agent dashboard..." />;
  }

  const inbox = inboxQuery.data;
  const recentItems = (inbox?.items ?? []).slice(0, 3);
  const pendingCount = (inbox?.newlyPaidCount ?? 0) + (inbox?.planningReadyCount ?? 0) + (inbox?.briefWaitingCount ?? 0);
  const urgentCount = inbox?.urgentCount ?? 0;
  const inReviewCount = (inbox?.agentReviewCount ?? 0) + (inbox?.readyToSendCount ?? 0) + (inbox?.waitingOnClientCount ?? 0);
  const approvedCount = inbox?.completedCount ?? 0;
  const liveCampaignCount = (inbox?.items ?? []).filter((item) => item.status === 'approved').length;
  const firstName = user?.fullName?.split(' ')[0] ?? 'Agent';
  const createRecommendationHref = '/agent/recommendations/new';

  const stats = [
    {
      label: 'Pending',
      value: String(pendingCount),
      helper: 'Campaigns that still need planning attention.',
      icon: <Clock3 className="size-5" />,
    },
    {
      label: 'Urgent',
      value: String(urgentCount),
      helper: 'Manual review, over-budget, or aging work that needs attention now.',
      icon: <AlertTriangle className="size-5" />,
    },
    {
      label: 'In Review',
      value: String(inReviewCount),
      helper: 'Recommendations moving between agent and client review.',
      icon: <Sparkles className="size-5" />,
    },
    {
      label: 'Approved',
      value: String(approvedCount),
      helper: 'Campaigns signed off and ready for the next stage.',
      icon: <CheckCircle2 className="size-5" />,
    },
  ];

  const opsHighlights = [
    { label: 'Manual review', value: inbox?.manualReviewCount ?? 0 },
    { label: 'Over budget', value: inbox?.overBudgetCount ?? 0 },
    { label: 'Stale work', value: inbox?.staleCount ?? 0 },
    { label: 'Live campaigns', value: liveCampaignCount },
  ];

  return (
    <section className="page-shell space-y-8">
      <div className="panel border-brand/10 bg-white/85 px-6 py-5">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.22em] text-brand">Advertified Agent</p>
            <h1 className="mt-3 text-3xl font-semibold tracking-tight text-ink">Dashboard</h1>
            <p className="mt-2 text-sm text-ink-soft">Manage recommendations, active client work, and the next campaigns that need your attention.</p>
          </div>
          <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center">
            <p className="text-sm text-ink-soft">Welcome back, {firstName}</p>
            <div className="flex gap-3">
              <Link to={createRecommendationHref} className="inline-flex items-center gap-2 rounded-full bg-brand px-5 py-3 text-sm font-semibold text-white shadow-[0_14px_35px_rgba(18,58,51,0.18)] transition hover:bg-brand-dark">
                Create Recommendation
              </Link>
              <button type="button" onClick={() => logout('manual')} className="button-secondary inline-flex items-center gap-2 px-4 py-3">
                <LogOut className="size-4" />
                Logout
              </button>
            </div>
          </div>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {stats.map((stat) => (
          <div key={stat.label} className="panel border-brand/5 bg-white/90 px-5 py-5">
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">{stat.label}</p>
                <p className="mt-3 text-3xl font-semibold tracking-tight text-ink">{stat.value}</p>
                <p className="mt-2 text-sm leading-6 text-ink-soft">{stat.helper}</p>
              </div>
              <div className="rounded-2xl bg-brand-soft p-3 text-brand">{stat.icon}</div>
            </div>
          </div>
        ))}
      </div>

      <div className="grid gap-8 xl:grid-cols-[minmax(0,1.4fr)_minmax(320px,0.9fr)]">
        <div className="space-y-4">
          <div className="flex items-center justify-between gap-4">
            <div>
              <h2 className="text-xl font-semibold text-ink">Recent Recommendations</h2>
              <p className="mt-1 text-sm text-ink-soft">The latest campaign work across draft, review, and approved stages.</p>
            </div>
            <Link to="/agent/campaigns" className="button-secondary px-4 py-2">
              Open Full Queue
            </Link>
          </div>

          <div className="space-y-4">
            {recentItems.length > 0 ? recentItems.map((item) => (
              <div key={item.id} className="panel border-brand/5 bg-white/92 px-5 py-5">
                <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                  <div>
                    <p className="text-lg font-semibold text-ink">{item.campaignName}</p>
                    <p className="mt-1 text-sm text-ink-soft">
                      {item.packageBandName} · {item.clientName} · {formatCurrency(item.selectedBudget)}
                    </p>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {item.isUrgent ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Urgent</span> : null}
                      {item.manualReviewRequired ? <span className="pill border-amber-200 bg-amber-50 text-amber-700">Manual review</span> : null}
                      {item.isOverBudget ? <span className="pill border-rose-200 bg-rose-50 text-rose-700">Over budget</span> : null}
                      {item.isStale ? <span className="pill border-slate-200 bg-slate-100 text-ink-soft">Stale {item.ageInDays}d</span> : null}
                    </div>
                    <p className="mt-2 text-sm text-ink-soft">{item.nextAction}</p>
                  </div>

                  <div className="flex flex-wrap items-center gap-3">
                    <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${statusTone(item.queueStage)}`}>
                      {item.queueLabel}
                    </span>
                    <Link to={`/agent/campaigns/${item.id}`} className="button-secondary inline-flex items-center gap-2 px-4 py-2">
                      Open
                      <ArrowRight className="size-4" />
                    </Link>
                  </div>
                </div>
              </div>
            )) : (
              <div className="panel px-6 py-8 text-sm text-ink-soft">
                No recommendations are waiting right now. New client work will appear here as soon as it enters the queue.
              </div>
            )}
          </div>
        </div>

        <div className="space-y-4">
          <div>
            <h2 className="text-xl font-semibold text-ink">Quick Actions</h2>
            <p className="mt-1 text-sm text-ink-soft">Jump straight into the parts of the workflow agents use most.</p>
          </div>

          <div className="space-y-4">
            <div className="panel border-brand/5 bg-white/92 px-6 py-6">
              <div className="flex items-center justify-between gap-4">
                <div>
                  <h3 className="text-lg font-semibold text-ink">Operations snapshot</h3>
                  <p className="mt-2 text-sm leading-6 text-ink-soft">
                    Use these signals to spot risky work before it slips.
                  </p>
                </div>
                <UsersRound className="size-5 text-brand" />
              </div>
              <div className="mt-4 grid gap-3 sm:grid-cols-2">
                {opsHighlights.map((item) => (
                  <div key={item.label} className="rounded-2xl border border-line bg-slate-50 px-4 py-3">
                    <p className="text-xs font-semibold uppercase tracking-[0.14em] text-ink-soft">{item.label}</p>
                    <p className="mt-2 text-2xl font-semibold text-ink">{item.value}</p>
                  </div>
                ))}
              </div>
            </div>

            <div className="panel border-brand/5 bg-white/92 px-6 py-6">
              <h3 className="text-lg font-semibold text-ink">Create Recommendation</h3>
              <p className="mt-2 text-sm leading-6 text-ink-soft">
                Start with the next campaign that needs planning and shape the recommendation with AI-assisted inputs.
              </p>
              <Link to={createRecommendationHref} className="mt-4 inline-flex rounded-full bg-brand px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-dark">
                Start
              </Link>
            </div>

            <div className="panel border-brand/5 bg-white/92 px-6 py-6">
              <h3 className="text-lg font-semibold text-ink">Review Pending</h3>
              <p className="mt-2 text-sm leading-6 text-ink-soft">
                Open recommendations waiting for agent review or client feedback and move them forward quickly.
              </p>
              <Link to="/agent/campaigns" className="button-secondary mt-4 inline-flex px-4 py-2">
                View
              </Link>
            </div>

            <div className="panel border-brand/5 bg-white/92 px-6 py-6">
              <h3 className="text-lg font-semibold text-ink">View Live Campaigns</h3>
              <p className="mt-2 text-sm leading-6 text-ink-soft">
                Check approved work, client ownership, and the campaigns already moving through activation.
              </p>
              <Link to="/agent/campaigns" className="button-secondary mt-4 inline-flex px-4 py-2">
                Open
              </Link>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

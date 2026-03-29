import { useQuery } from '@tanstack/react-query';
import { ArrowRight, ClipboardList, LogOut, Settings2, ShieldCheck, UsersRound } from 'lucide-react';
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

export function AdminDashboardPage() {
  const { user, logout } = useAuth();
  const inboxQuery = useQuery({ queryKey: ['agent-inbox'], queryFn: advertifiedApi.getAgentInbox });

  if (inboxQuery.isLoading) {
    return <LoadingState label="Loading admin workspace..." />;
  }

  const inbox = inboxQuery.data;
  const firstName = user?.fullName?.split(' ')[0] ?? 'Admin';
  const totalBudgetInQueue = (inbox?.items ?? []).reduce((sum, item) => sum + item.selectedBudget, 0);
  const needsAssignment = inbox?.unassignedCount ?? 0;
  const inFlight = (inbox?.agentReviewCount ?? 0) + (inbox?.waitingOnClientCount ?? 0);
  const completed = inbox?.completedCount ?? 0;
  const recentItems = (inbox?.items ?? []).slice(0, 4);

  const stats = [
    {
      label: 'Needs assignment',
      value: String(needsAssignment),
      helper: 'Campaigns not yet owned by an agent.',
      icon: <UsersRound className="size-5" />,
    },
    {
      label: 'In flight',
      value: String(inFlight),
      helper: 'Recommendations being reviewed by agents or clients.',
      icon: <ClipboardList className="size-5" />,
    },
    {
      label: 'Completed',
      value: String(completed),
      helper: 'Campaigns approved and moved past review.',
      icon: <ShieldCheck className="size-5" />,
    },
    {
      label: 'Queue value',
      value: formatCurrency(totalBudgetInQueue),
      helper: 'Combined selected budget across the active queue.',
      icon: <Settings2 className="size-5" />,
    },
  ];

  return (
    <section className="page-shell space-y-8">
      <div className="panel border-brand/10 bg-white/85 px-6 py-5">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.22em] text-brand">Advertified Admin</p>
            <h1 className="mt-3 text-3xl font-semibold tracking-tight text-ink">Operations dashboard</h1>
            <p className="mt-2 text-sm text-ink-soft">
              Oversee queue health, assignment pressure, and the campaigns currently moving through strategy and client review.
            </p>
          </div>
          <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center">
            <p className="text-sm text-ink-soft">Welcome back, {firstName}</p>
            <div className="flex gap-3">
              <Link to="/agent/campaigns" className="inline-flex items-center gap-2 rounded-full bg-brand px-5 py-3 text-sm font-semibold text-white shadow-[0_14px_35px_rgba(18,58,51,0.18)] transition hover:bg-brand-dark">
                Open operations queue
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
              <h2 className="text-xl font-semibold text-ink">Recent queue activity</h2>
              <p className="mt-1 text-sm text-ink-soft">The latest campaign work entering or moving through the operations queue.</p>
            </div>
            <Link to="/agent/campaigns" className="button-secondary px-4 py-2">
              Open full queue
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
                    <p className="mt-2 text-sm text-ink-soft">{item.nextAction}</p>
                  </div>

                  <div className="flex flex-wrap items-center gap-3">
                    <span className="inline-flex rounded-full border border-brand/10 bg-brand-soft px-3 py-1 text-xs font-semibold text-brand">
                      {item.queueLabel}
                    </span>
                    <Link to={`/agent/campaigns/${item.id}`} className="button-secondary inline-flex items-center gap-2 px-4 py-2">
                      Inspect
                      <ArrowRight className="size-4" />
                    </Link>
                  </div>
                </div>
              </div>
            )) : (
              <div className="panel px-6 py-8 text-sm text-ink-soft">
                No campaigns are active right now. New paid orders and review work will show up here automatically.
              </div>
            )}
          </div>
        </div>

        <div className="space-y-4">
          <div>
            <h2 className="text-xl font-semibold text-ink">Admin actions</h2>
            <p className="mt-1 text-sm text-ink-soft">Jump into the operational surfaces admins use most.</p>
          </div>

          <div className="space-y-4">
            <div className="panel border-brand/5 bg-white/92 px-6 py-6">
              <h3 className="text-lg font-semibold text-ink">Review unassigned work</h3>
              <p className="mt-2 text-sm leading-6 text-ink-soft">
                Check newly paid and unowned campaigns so the right strategist can pick them up quickly.
              </p>
              <Link to="/agent/campaigns" className="button-secondary mt-4 inline-flex px-4 py-2">
                Open queue
              </Link>
            </div>

            <div className="panel border-brand/5 bg-white/92 px-6 py-6">
              <h3 className="text-lg font-semibold text-ink">Create recommendation</h3>
              <p className="mt-2 text-sm leading-6 text-ink-soft">
                Enter the guided setup flow directly when you need to seed a recommendation yourself.
              </p>
              <Link to="/agent/recommendations/new" className="mt-4 inline-flex rounded-full bg-brand px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-dark">
                Start setup
              </Link>
            </div>

            <div className="panel border-brand/5 bg-white/92 px-6 py-6">
              <h3 className="text-lg font-semibold text-ink">Inspect inventory tools</h3>
              <p className="mt-2 text-sm leading-6 text-ink-soft">
                Open the shared inventory workspace without dropping into the agent landing page first.
              </p>
              <Link to="/agent/inventory" className="button-secondary mt-4 inline-flex px-4 py-2">
                Open inventory
              </Link>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

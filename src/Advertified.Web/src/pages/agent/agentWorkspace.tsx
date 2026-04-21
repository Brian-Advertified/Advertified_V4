import { type ReactNode } from 'react';
import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { NavLink } from 'react-router-dom';
import {
  CircleDollarSign,
  FolderKanban,
  Inbox,
  LayoutDashboard,
  MessageSquareText,
  Radar,
  UserRoundSearch,
} from 'lucide-react';
import { PageHero } from '../../components/marketing/PageHero';
import { QueryStateBoundary } from '../../components/ui/QueryStateBoundary';
import { useAuth } from '../../features/auth/auth-context';
import { catalogQueryOptions } from '../../lib/catalogQueryOptions';
import { shouldPollWhenVisible } from '../../lib/queryPolling';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AgentInbox, Campaign, LeadOpsCoverage, LeadOpsInbox, PackageBand } from '../../types/domain';

type AgentNavItem = {
  path: string;
  label: string;
  icon: typeof LayoutDashboard;
  end?: boolean;
};

type AgentNavSection = {
  title: string;
  items: AgentNavItem[];
};

export const agentNavSections: AgentNavSection[] = [
  {
    title: 'Overview',
    items: [
      { path: '/agent', label: 'Dashboard', icon: LayoutDashboard, end: true },
    ],
  },
  {
    title: 'Work',
    items: [
      { path: '/agent/lead-ops', label: 'Lead Ops', icon: Inbox },
      { path: '/agent/lead-intelligence', label: 'Lead Intelligence', icon: Radar },
      { path: '/agent/leads', label: 'Clients', icon: UserRoundSearch },
      { path: '/agent/campaigns', label: 'Campaigns', icon: FolderKanban, end: true },
      { path: '/agent/messages', label: 'Messages', icon: MessageSquareText },
      { path: '/agent/sales', label: 'Sales', icon: CircleDollarSign },
    ],
  },
];

export const fmtCurrency = (amount?: number) => amount == null ? 'N/A' : new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', maximumFractionDigits: 0 }).format(amount);
export const fmtDate = (value?: string) => value ? new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not yet available';
export const titleize = (value?: string) => value ? value.replace(/_/g, ' ').replace(/\b\w/g, (char) => char.toUpperCase()) : 'Not set';

export function queueTone(queueStage: string) {
  switch (queueStage) {
    case 'completed':
      return 'border-brand/20 bg-brand-soft text-brand';
    case 'waiting_on_client':
      return 'border-amber-200 bg-amber-50 text-amber-700';
    case 'agent_review':
      return 'border-brand/20 bg-brand-soft text-brand';
    case 'planning_ready':
      return 'border-brand/20 bg-brand-soft text-brand';
    case 'newly_paid':
    case 'brief_waiting':
      return 'border-brand/20 bg-brand-soft text-brand';
    default:
      return 'border-line bg-slate-50 text-ink-soft';
  }
}

export function useAgentInboxQuery(): UseQueryResult<AgentInbox> {
  return useQuery<AgentInbox>({
    queryKey: ['agent-inbox'],
    queryFn: advertifiedApi.getAgentInbox,
    refetchInterval: () => shouldPollWhenVisible() ? 15_000 : false,
  });
}

export function useLeadOpsInboxQuery(): UseQueryResult<LeadOpsInbox> {
  return useQuery<LeadOpsInbox>({
    queryKey: ['lead-ops-inbox'],
    queryFn: advertifiedApi.getLeadOpsInbox,
    refetchInterval: () => shouldPollWhenVisible() ? 15_000 : false,
  });
}

export function useLeadOpsCoverageQuery(): UseQueryResult<LeadOpsCoverage> {
  return useQuery<LeadOpsCoverage>({
    queryKey: ['lead-ops-coverage'],
    queryFn: advertifiedApi.getLeadOpsCoverage,
    refetchInterval: () => shouldPollWhenVisible() ? 30_000 : false,
  });
}

export function useAgentCampaignsQuery(): UseQueryResult<Campaign[]> {
  return useQuery<Campaign[]>({
    queryKey: ['agent-campaigns'],
    queryFn: advertifiedApi.getAgentCampaigns,
    refetchInterval: () => shouldPollWhenVisible() ? 15_000 : false,
  });
}

export function usePackagesQuery(): UseQueryResult<PackageBand[]> {
  return useQuery<PackageBand[]>({ queryKey: ['packages'], queryFn: advertifiedApi.getPackages, ...catalogQueryOptions });
}

type AgentPageShellProps = {
  title: string;
  description: string;
  children: ReactNode;
};

export function AgentPageShell({ title, description, children }: AgentPageShellProps) {
  const { user, logout } = useAuth();
  const mobileNavItems = agentNavSections.flatMap((section) => section.items);

  return (
    <section className="page-shell ops-workspace space-y-5 sm:space-y-8 lg:space-y-10">
      <div className="lg:hidden">
        <div className="panel overflow-hidden p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Agent navigation</p>
          <div className="ops-mobile-nav mt-3">
            {mobileNavItems.map((item) => (
              <NavLink
                key={item.path}
                to={item.path}
                end={item.end}
                className={({ isActive }) => `ops-mobile-nav-item px-4 py-2 text-xs font-semibold text-center ${isActive ? 'button-primary' : 'button-secondary'}`}
              >
                {item.label}
              </NavLink>
            ))}
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[280px_1fr] lg:gap-8">
        <aside className="hidden h-fit rounded-[28px] border border-line bg-white p-6 shadow-[0_18px_60px_rgba(17,24,39,0.04)] lg:sticky lg:top-24 lg:block">
          <div className="space-y-6">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Workspace</p>
              <h2 className="mt-4 text-2xl font-semibold text-ink">Agent workspace</h2>
              <p className="mt-2 text-sm leading-6 text-ink-soft">One place to check the queue, open a campaign, and move the next task forward.</p>
            </div>
            <div className="space-y-5">
              {agentNavSections.map((section) => (
                <div key={section.title} className="space-y-2">
                  <p className="text-xs font-semibold uppercase tracking-[0.22em] text-ink-soft">{section.title}</p>
                  <div className="space-y-2">
                    {section.items.map((item) => {
                      const Icon = item.icon;
                      return (
                        <NavLink
                          key={item.path}
                          to={item.path}
                          end={item.end}
                          className={({ isActive }) => `flex w-full items-center gap-3 rounded-2xl px-4 py-3 text-sm font-semibold ${isActive ? 'button-primary' : 'button-secondary text-ink'}`}
                        >
                          <Icon className="size-4" />
                          {item.label}
                        </NavLink>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
            <div className="rounded-3xl border border-line bg-slate-50 p-4 text-sm">
              <p className="font-semibold text-ink">How to use this</p>
              <p className="mt-2 text-ink-soft">Start with the queue, open the campaign that needs action, then come back here when you need the next item.</p>
            </div>
          </div>
        </aside>

        <main className="min-w-0 space-y-6 sm:space-y-8 lg:space-y-10">
          <PageHero
            kicker="Advertified agent"
            title={title}
            description={description}
            aside={(
              <div className="space-y-4">
                <p className="text-sm text-ink-soft">Working as {user?.fullName?.split(' ')[0] ?? 'Agent'}</p>
                <div className="flex flex-wrap gap-3">
                  <button type="button" onClick={() => logout('manual')} className="button-secondary rounded-full font-semibold">Logout</button>
                  <NavLink to="/agent/recommendations/new" className="button-primary inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold">
                    Start recommendation
                  </NavLink>
                </div>
              </div>
            )}
          />
          {children}
        </main>
      </div>
    </section>
  );
}

type AgentQueryBoundaryProps = {
  query: UseQueryResult<unknown>;
  loadingLabel: string;
  children: ReactNode;
};

export function AgentQueryBoundary({ query, loadingLabel, children }: AgentQueryBoundaryProps) {
  return (
    <QueryStateBoundary
      query={query}
      loadingLabel={loadingLabel}
      errorTitle="Agent workspace unavailable"
      errorDescription="The agent workspace could not be loaded."
    >
      {() => children}
    </QueryStateBoundary>
  );
}

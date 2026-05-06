import { type ReactNode } from 'react';
import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { NavLink } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { QueryStateBoundary } from '../../components/ui/QueryStateBoundary';
import { useAuth } from '../../features/auth/auth-context';
import { formatCurrency, formatDateTime } from '../../lib/utils';
import { PageHero } from '../../components/marketing/PageHero';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AdminDashboard } from '../../types/domain';

type AdminNavItem = {
  path: string;
  label: string;
  end?: boolean;
};

type AdminNavSection = {
  title: string;
  items: AdminNavItem[];
};

export const adminNavSections: AdminNavSection[] = [
  {
    title: 'Overview',
    items: [
      { path: '/admin', label: 'Dashboard', end: true },
    ],
  },
  {
    title: 'Daily work',
    items: [
      { path: '/admin/package-orders', label: 'Payments' },
      { path: '/admin/campaign-operations', label: 'Campaigns' },
      { path: '/admin/users', label: 'Users' },
    ],
  },
  {
    title: 'Catalog',
    items: [
      { path: '/admin/stations', label: 'Outlets' },
      { path: '/admin/pricing', label: 'Pricing' },
      { path: '/admin/imports', label: 'Imports' },
      { path: '/admin/health', label: 'Catalog Health' },
      { path: '/admin/geography', label: 'Geography' },
    ],
  },
  {
    title: 'Settings',
    items: [
      { path: '/admin/engine', label: 'Planning Rules' },
      { path: '/admin/industry-policies', label: 'Industry Policies' },
      { path: '/admin/preview-rules', label: 'Preview Rules' },
      { path: '/admin/integrations', label: 'Integrations' },
      { path: '/admin/monitoring', label: 'Monitoring' },
      { path: '/admin/audit', label: 'Audit Log' },
    ],
  },
  {
    title: 'AI tools',
    items: [
      { path: '/admin/ai-voices', label: 'Voice Library' },
      { path: '/admin/ai-voice-packs', label: 'Voice Packs' },
      { path: '/admin/ai-voice-templates', label: 'Voice Templates' },
      { path: '/admin/ai-ad-ops', label: 'Ad Operations' },
    ],
  },
] as const;

export const fmtCurrency = (amount?: number) => amount == null ? 'N/A' : formatCurrency(amount);
export const fmtDate = (value?: string) => value ? formatDateTime(value) : 'Not yet available';
export const titleize = (value: string) => value.replace(/_/g, ' ').replace(/\b\w/g, (char) => char.toUpperCase());
export const tone = (value: string) => {
  const normalized = value.toLowerCase();
  if (normalized.includes('critical') || normalized.includes('weak') || normalized.includes('missing')) return 'border-rose-200 bg-rose-50 text-rose-700';
  if (normalized.includes('warning') || normalized.includes('mixed')) return 'border-amber-200 bg-amber-50 text-amber-700';
  return 'border-brand/20 bg-brand-soft text-brand';
};
export const splitList = (value: string) => value.split(',').map((item) => item.trim()).filter(Boolean);

export function useAdminDashboardQuery(): UseQueryResult<AdminDashboard> {
  return useQuery<AdminDashboard>({ queryKey: ['admin-dashboard'], queryFn: advertifiedApi.getAdminDashboard });
}

type AdminPageShellProps = {
  title: string;
  description: string;
  children: ReactNode;
};

export function AdminPageShell({ title, description, children }: AdminPageShellProps) {
  const { user, logout } = useAuth();
  const mobileNavItems = adminNavSections.flatMap((section) => section.items);

  return (
    <section className="page-shell ops-workspace space-y-5 sm:space-y-8 lg:space-y-10">
      <div className="lg:hidden">
        <div className="panel overflow-hidden p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Admin navigation</p>
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

      <div className="grid gap-6 lg:grid-cols-[260px_1fr] lg:gap-8">
        <aside className="hidden h-fit rounded-[28px] border border-line bg-white p-6 shadow-[0_18px_60px_rgba(17,24,39,0.04)] lg:sticky lg:top-24 lg:block">
          <div className="space-y-6">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Workspace</p>
              <h2 className="mt-4 text-2xl font-semibold text-ink">Admin workspace</h2>
              <p className="mt-2 text-sm leading-6 text-ink-soft">Handle payments, keep campaigns on track, maintain the catalog, and manage platform settings from one place.</p>
            </div>
            <div className="space-y-5">
              {adminNavSections.map((section) => (
                <div key={section.title} className="space-y-2">
                  <p className="text-xs font-semibold uppercase tracking-[0.22em] text-ink-soft">{section.title}</p>
                  <div className="space-y-2">
                    {section.items.map((item) => (
                      <NavLink
                        key={item.path}
                        to={item.path}
                        end={item.end}
                        className={({ isActive }) => `block w-full rounded-2xl px-4 py-3 text-sm font-semibold ${isActive ? 'button-primary' : 'button-secondary text-ink'}`}
                      >
                        {item.label}
                      </NavLink>
                    ))}
                  </div>
                </div>
              ))}
            </div>
            <div className="rounded-3xl bg-brand-soft p-4 text-sm text-brand">
              <p className="font-semibold">Live operational data</p>
              <p className="mt-2 text-ink-soft">The numbers and actions on these pages come from the live payment queue, campaign records, outlet catalog, and platform settings.</p>
            </div>
          </div>
        </aside>

        <main className="min-w-0 space-y-6 sm:space-y-8 lg:space-y-10">
          <PageHero
            kicker="Advertified admin"
            title={title}
            description={description}
            aside={(
              <div className="space-y-4">
                <p className="text-sm text-ink-soft">Welcome back, {user?.fullName?.split(' ')[0] ?? 'Admin'}</p>
                <div className="flex flex-wrap gap-3">
                  <button type="button" onClick={() => logout('manual')} className="button-secondary rounded-full font-semibold">Logout</button>
                  <NavLink to="/admin" end className="button-primary inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold">
                    Dashboard
                    <ArrowRight className="size-4" />
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

type AdminQueryBoundaryProps = {
  query: UseQueryResult<AdminDashboard>;
  children: (dashboard: AdminDashboard) => ReactNode;
};

export function AdminQueryBoundary({ query, children }: AdminQueryBoundaryProps) {
  return (
    <QueryStateBoundary
      query={query}
      loadingLabel="Loading admin workspace..."
      errorTitle="Admin access required"
      errorDescription="The admin workspace could not be loaded."
      emptyTitle="Admin dashboard data is unavailable."
      emptyDescription="The admin dashboard returned no data for this session."
    >
      {(dashboard) => children(dashboard)}
    </QueryStateBoundary>
  );
}

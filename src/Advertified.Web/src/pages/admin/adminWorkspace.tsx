import { type ReactNode } from 'react';
import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { NavLink } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../features/auth/auth-context';
import { PageHero } from '../../components/marketing/PageHero';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AdminDashboard } from '../../types/domain';

export const adminNavItems = [
  { path: '/admin', label: 'Dashboard' },
  { path: '/admin/campaign-operations', label: 'Campaign Controls' },
  { path: '/admin/stations', label: 'Stations & Channels' },
  { path: '/admin/pricing', label: 'Pricing & Packages' },
  { path: '/admin/imports', label: 'Imports & Rate Cards' },
  { path: '/admin/health', label: 'Data Quality & Health' },
  { path: '/admin/geography', label: 'Geography Mapping' },
  { path: '/admin/engine', label: 'Engine Settings' },
  { path: '/admin/preview-rules', label: 'Preview Rules' },
  { path: '/admin/monitoring', label: 'Monitoring' },
  { path: '/admin/users', label: 'Users & Roles' },
  { path: '/admin/audit', label: 'Audit Log' },
  { path: '/admin/integrations', label: 'Integrations' },
] as const;

export const fmtCurrency = (amount?: number) => amount == null ? 'N/A' : new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', maximumFractionDigits: 0 }).format(amount);
export const fmtDate = (value?: string) => value ? new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Not yet available';
export const titleize = (value: string) => value.replace(/_/g, ' ').replace(/\b\w/g, (char) => char.toUpperCase());
export const tone = (value: string) => {
  const normalized = value.toLowerCase();
  if (normalized.includes('critical') || normalized.includes('weak') || normalized.includes('missing')) return 'border-rose-200 bg-rose-50 text-rose-700';
  if (normalized.includes('warning') || normalized.includes('mixed')) return 'border-amber-200 bg-amber-50 text-amber-700';
  return 'border-emerald-200 bg-emerald-50 text-emerald-700';
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

  return (
    <section className="page-shell space-y-10">
      <div className="grid gap-8 xl:grid-cols-[260px_1fr]">
        <aside className="sticky top-24 h-fit rounded-[28px] border border-line bg-white p-6 shadow-[0_18px_60px_rgba(17,24,39,0.04)]">
          <div className="space-y-6">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Workspace</p>
              <h2 className="mt-4 text-2xl font-semibold text-ink">Admin system</h2>
              <p className="mt-2 text-sm leading-6 text-ink-soft">Live operational surfaces for inventory, rules, imports, health, and integrations.</p>
            </div>
            <div className="space-y-2">
              {adminNavItems.map((item) => (
                <NavLink
                  key={item.path}
                  to={item.path}
                  end={item.path === '/admin'}
                  className={({ isActive }) => `block rounded-2xl px-4 py-3 text-sm font-semibold transition ${isActive ? 'bg-brand text-white' : 'text-ink hover:bg-brand-soft hover:text-brand'}`}
                >
                  {item.label}
                </NavLink>
              ))}
            </div>
            <div className="rounded-3xl bg-brand-soft p-4 text-sm text-brand">
              <p className="font-semibold">Live data only</p>
              <p className="mt-2 text-ink-soft">This workspace is sourced from the admin dashboard API, broadcast catalog, import tables, package catalog, planning policy config, and payment audit logs.</p>
            </div>
          </div>
        </aside>

        <main className="space-y-10">
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
  if (query.isLoading) {
    return <LoadingState label="Loading admin workspace..." />;
  }

  if (query.isError) {
    return (
      <section className="page-shell">
        <div className="panel mx-auto max-w-3xl p-8">
          <h1 className="text-2xl font-semibold text-ink">Admin access required</h1>
          <p className="mt-3 text-sm leading-6 text-ink-soft">{query.error instanceof Error ? query.error.message : 'The admin workspace could not be loaded.'}</p>
        </div>
      </section>
    );
  }

  if (!query.data) {
    return <LoadingState label="Admin dashboard data is unavailable." />;
  }

  return <>{children(query.data)}</>;
}

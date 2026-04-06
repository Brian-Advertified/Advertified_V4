import { ArrowRight, ShieldCheck } from 'lucide-react';
import { Link } from 'react-router-dom';
import { AdminPageShell, AdminQueryBoundary, tone, useAdminDashboardQuery } from './adminWorkspace';

export function AdminDashboardPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const topHealthIssues = dashboard.healthIssues.slice(0, 8);
        const priorityActions = [
          {
            title: 'Review payments',
            helper: 'Open Finance Partner and invoice follow-ups first.',
            href: '/admin/package-orders',
          },
          {
            title: 'Manage campaigns',
            helper: 'Pause, resume, or refund live campaign work.',
            href: '/admin/campaign-operations',
          },
          {
            title: 'Fix catalog issues',
            helper: 'Resolve missing pricing and weak outlet records.',
            href: '/admin/health',
          },
          {
            title: 'Update pricing',
            helper: 'Maintain package bands and outlet pricing rows.',
            href: '/admin/pricing',
          },
          {
            title: 'Upload imports',
            helper: 'Add new rate cards and source files.',
            href: '/admin/imports',
          },
          {
            title: 'Manage outlets',
            helper: 'Edit outlet details, geography, and availability.',
            href: '/admin/stations',
          },
        ];

        return (
          <AdminPageShell title="Dashboard" description="Start here to see what needs attention in payments, campaigns, catalog health, and platform setup.">
            <section className="space-y-6">
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {[
                  ['Active outlets', dashboard.summary.activeOutlets, 'Outlets currently available in the live catalog.'],
                  ['Needs attention', dashboard.summary.weakOutlets, 'Outlets with missing pricing, inventory, or important details.'],
                  ['Imported files', dashboard.summary.sourceDocuments, 'Source files already stored for pricing and planning.'],
                  ['Fallback rate', `${dashboard.summary.fallbackRatePercent}%`, 'Recommendations still relying on fallback values.'],
                ].map(([label, value, note]) => (
                  <div key={String(label)} className="panel p-6">
                    <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{label}</p>
                    <p className="mt-4 text-4xl font-semibold text-ink">{value}</p>
                    <p className="mt-3 text-sm leading-6 text-ink-soft">{note}</p>
                  </div>
                ))}
              </div>

              <div className="grid gap-6 xl:grid-cols-[1.7fr_1fr]">
                <div className="panel p-6">
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <h2 className="text-xl font-semibold text-ink">Alerts</h2>
                      <p className="mt-2 text-sm text-ink-soft">The most important live issues are surfaced here first.</p>
                    </div>
                    <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                      <ShieldCheck className="size-5" />
                    </div>
                  </div>
                  <div className="mt-6 space-y-4">
                    {dashboard.alerts.length > 0 ? dashboard.alerts.map((alert) => (
                      <div key={`${alert.title}-${alert.context}`} className="rounded-3xl border border-line p-4">
                        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                          <div>
                            <p className="text-base font-semibold text-ink">{alert.title}</p>
                            <p className="text-sm text-ink-soft">{alert.context}</p>
                          </div>
                          <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${tone(alert.severity)}`}>{alert.severity}</span>
                        </div>
                      </div>
                    )) : <p className="text-sm text-ink-soft">No active alerts right now.</p>}
                  </div>
                </div>

                <div className="space-y-6">
                  <div className="panel p-6">
                    <h3 className="text-lg font-semibold text-ink">Recommendation status</h3>
                    <div className="mt-5 space-y-3 text-sm text-ink-soft">
                      {[
                        ['Total recommendations', dashboard.monitoring.recommendationCount],
                        ['Planning ready', dashboard.monitoring.planningReadyCount],
                        ['Waiting on client', dashboard.monitoring.waitingOnClientCount],
                      ].map(([label, value]) => (
                        <div key={String(label)} className="flex items-center justify-between rounded-2xl border border-line px-4 py-3">
                          <span>{label}</span>
                          <span className="font-semibold text-ink">{value}</span>
                        </div>
                      ))}
                    </div>
                  </div>

                  <div className="panel p-6">
                    <h3 className="text-lg font-semibold text-ink">Start with one of these</h3>
                    <p className="mt-2 text-sm text-ink-soft">These are the main jobs admins usually need to handle during the day.</p>
                    <div className="mt-6 grid gap-3">
                      {priorityActions.map((item) => (
                        <Link key={item.href} to={item.href} className="button-secondary inline-flex items-center justify-between gap-2 px-4 py-3">
                          <span>
                            <span className="block font-semibold text-ink">{item.title}</span>
                            <span className="mt-1 block text-sm font-normal text-ink-soft">{item.helper}</span>
                          </span>
                          <ArrowRight className="size-4" />
                        </Link>
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              <div className="rounded-[28px] border border-line bg-white p-6">
                <h3 className="text-lg font-semibold text-ink">Catalog fixes to handle next</h3>
                <p className="mt-2 text-sm text-ink-soft">The most urgent catalog problems stay visible here, with the full list on the catalog health page.</p>
                <div className="mt-4 overflow-hidden rounded-[24px] border border-line">
                  <table className="w-full border-collapse text-sm">
                    <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                      <tr>
                        <th className="px-4 py-4">Outlet</th>
                        <th className="px-4 py-4">Issue</th>
                        <th className="px-4 py-4">Impact</th>
                        <th className="px-4 py-4">Suggested fix</th>
                      </tr>
                    </thead>
                    <tbody>
                      {topHealthIssues.map((item) => (
                        <tr key={`${item.outletName}-${item.issue}`} className="border-t border-line">
                          <td className="px-4 py-4 font-semibold text-ink">{item.outletName}</td>
                          <td className="px-4 py-4 text-ink-soft">{item.issue}</td>
                          <td className="px-4 py-4 text-ink-soft">{item.impact}</td>
                          <td className="px-4 py-4 text-ink-soft">{item.suggestedFix}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </section>
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

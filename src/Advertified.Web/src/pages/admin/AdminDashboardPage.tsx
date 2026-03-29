import { ArrowRight, ShieldCheck } from 'lucide-react';
import { Link } from 'react-router-dom';
import { AdminPageShell, AdminQueryBoundary, tone, useAdminDashboardQuery } from './adminWorkspace';

export function AdminDashboardPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const topHealthIssues = dashboard.healthIssues.slice(0, 8);

        return (
          <AdminPageShell title="Operational overview and inventory controls" description="Manage stations, pricing, imports, health, geography, rules, monitoring, and platform integrations from live system data.">
            <section className="space-y-6">
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {[
                  ['Active outlets', dashboard.summary.activeOutlets, 'Broadcast outlets available in the normalized catalog.'],
                  ['Weak outlets', dashboard.summary.weakOutlets, 'Outlets with weak health, missing pricing, or incomplete metadata.'],
                  ['Imported source docs', dashboard.summary.sourceDocuments, 'Rate cards and source documents recorded in the import manifest.'],
                  ['Fallback rate', `${dashboard.summary.fallbackRatePercent}%`, 'Latest recommendation revisions containing fallback flags.'],
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
                      <h2 className="text-xl font-semibold text-ink">System alerts</h2>
                      <p className="mt-2 text-sm text-ink-soft">Top current issues surfaced from live catalog health signals.</p>
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
                    )) : <p className="text-sm text-ink-soft">No active admin alerts are being raised right now.</p>}
                  </div>
                </div>

                <div className="space-y-6">
                  <div className="panel p-6">
                    <h3 className="text-lg font-semibold text-ink">Recommendation health</h3>
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
                    <h3 className="text-lg font-semibold text-ink">Quick actions</h3>
                    <p className="mt-2 text-sm text-ink-soft">Open a focused admin page or jump into operational workflows.</p>
                    <div className="mt-6 grid gap-3">
                      <Link to="/admin/stations" className="button-secondary inline-flex items-center justify-between gap-2 px-4 py-3">Manage outlets <ArrowRight className="size-4" /></Link>
                      <Link to="/admin/imports" className="button-secondary inline-flex items-center justify-between gap-2 px-4 py-3">Upload rate cards <ArrowRight className="size-4" /></Link>
                      <Link to="/admin/preview-rules" className="button-secondary inline-flex items-center justify-between gap-2 px-4 py-3">Edit preview rules <ArrowRight className="size-4" /></Link>
                      <Link to="/agent/campaigns" className="button-secondary inline-flex items-center justify-between gap-2 px-4 py-3">Review queue <ArrowRight className="size-4" /></Link>
                      <Link to="/agent/recommendations/new" className="button-secondary inline-flex items-center justify-between gap-2 px-4 py-3">Create recommendation <ArrowRight className="size-4" /></Link>
                      <Link to="/agent/inventory" className="button-secondary inline-flex items-center justify-between gap-2 px-4 py-3">Inspect inventory <ArrowRight className="size-4" /></Link>
                    </div>
                  </div>
                </div>
              </div>

              <div className="rounded-[28px] border border-line bg-white p-6">
                <h3 className="text-lg font-semibold text-ink">Priority fix queue</h3>
                <p className="mt-2 text-sm text-ink-soft">The most urgent catalog issues stay visible here, with the full live queue available on the dedicated health page.</p>
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

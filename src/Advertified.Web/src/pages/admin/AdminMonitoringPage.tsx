import { ReadOnlyNotice } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, useAdminDashboardQuery } from './adminWorkspace';

export function AdminMonitoringPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => (
        <AdminPageShell title="Monitoring" description="Track live operational metrics for campaigns, recommendations, areas, and inventory coverage in one place.">
          <ReadOnlyNotice label="Monitoring is a live operational snapshot. These metrics are observational and are not edited from the admin UI." />
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {[['Total campaigns', dashboard.monitoring.totalCampaigns], ['Planning ready', dashboard.monitoring.planningReadyCount], ['Waiting on client', dashboard.monitoring.waitingOnClientCount], ['Inventory rows', dashboard.monitoring.inventoryRows], ['Active areas', dashboard.monitoring.activeAreaCount], ['Recommendation sets', dashboard.monitoring.recommendationCount]].map(([label, value]) => <div key={String(label)} className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{label}</p><p className="mt-4 text-4xl font-semibold text-ink">{value}</p></div>)}
          </div>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}

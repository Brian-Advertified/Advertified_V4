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
            {[
              ['Total campaigns', dashboard.monitoring.totalCampaigns],
              ['Planning ready', dashboard.monitoring.planningReadyCount],
              ['Waiting on client', dashboard.monitoring.waitingOnClientCount],
              ['Inventory rows', dashboard.monitoring.inventoryRows],
              ['Active areas', dashboard.monitoring.activeAreaCount],
              ['Recommendation sets', dashboard.monitoring.recommendationCount],
              ['AI job alerts', dashboard.monitoring.aiJobAlertCount],
              ['Creative alerts', dashboard.monitoring.aiCreativeJobAlertCount],
              ['Asset alerts', dashboard.monitoring.aiAssetJobAlertCount],
              ['Cost-cap rejections', dashboard.monitoring.aiCostCapRejectionCount],
            ].map(([label, value]) => <div key={String(label)} className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{label}</p><p className="mt-4 text-4xl font-semibold text-ink">{value}</p></div>)}
          </div>
          <div className="panel mt-6 p-6">
            <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">
              AI queue alert threshold: status failed and retries at or above {dashboard.monitoring.retryAlertThreshold}
            </p>
            {dashboard.monitoring.aiJobAlerts.length > 0 ? (
              <div className="mt-4 overflow-hidden rounded-[24px] border border-line">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Pipeline</th>
                      <th className="px-4 py-4">Campaign</th>
                      <th className="px-4 py-4">Retries</th>
                      <th className="px-4 py-4">Last failure</th>
                      <th className="px-4 py-4">Updated</th>
                    </tr>
                  </thead>
                  <tbody>
                    {dashboard.monitoring.aiJobAlerts.map((item) => (
                      <tr key={item.jobId} className="border-t border-line">
                        <td className="px-4 py-4 font-semibold text-ink">{item.pipeline}</td>
                        <td className="px-4 py-4 text-ink-soft">{item.campaignId}</td>
                        <td className="px-4 py-4 text-ink-soft">{item.retryAttemptCount}</td>
                        <td className="px-4 py-4 text-ink-soft">{item.lastFailure ?? '-'}</td>
                        <td className="px-4 py-4 text-ink-soft">{new Date(item.updatedAt).toLocaleString()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="mt-4 text-sm text-ink-soft">No AI queue alerts above threshold.</p>
            )}
          </div>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}

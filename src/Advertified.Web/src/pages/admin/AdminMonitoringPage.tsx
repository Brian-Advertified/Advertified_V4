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
              ['Creative queue backlog', dashboard.monitoring.creativeQueueBacklogCount],
              ['Asset queue backlog', dashboard.monitoring.assetQueueBacklogCount],
              ['Creative dead-letter', dashboard.monitoring.creativeDeadLetterCount],
              ['Publish success', dashboard.monitoring.publishSuccessCount],
              ['Publish failures', dashboard.monitoring.publishFailureCount],
              ['Metrics sync lag (min)', dashboard.monitoring.metricsSyncLagMinutes],
              ['Unpaid orders backlog', dashboard.monitoring.unpaidOrderBacklogCount],
              ['Unsent proposals', dashboard.monitoring.unsentProposalBacklogCount],
              ['Unopened proposals', dashboard.monitoring.unopenedProposalBacklogCount],
              ['Paid activation backlog', dashboard.monitoring.paidActivationBacklogCount],
              ['Stale prospects', dashboard.monitoring.staleProspectBacklogCount],
            ].map(([label, value]) => <div key={String(label)} className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{label}</p><p className="mt-4 text-4xl font-semibold text-ink">{value}</p></div>)}
          </div>
          <div className="panel mt-6 p-6">
            <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Lifecycle Queues</p>
            <div className="mt-4 grid gap-4 xl:grid-cols-2">
              {dashboard.monitoring.lifecycleQueues.map((queue) => (
                <div key={queue.queueKey} className="rounded-[24px] border border-line bg-white p-5">
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <p className="text-sm font-semibold text-ink">{queue.label}</p>
                      <p className="mt-1 text-sm text-ink-soft">{queue.description}</p>
                    </div>
                    <div className="text-2xl font-semibold text-ink">{queue.count}</div>
                  </div>
                  {queue.items.length > 0 ? (
                    <div className="mt-4 overflow-hidden rounded-[20px] border border-line">
                      <table className="w-full border-collapse text-sm">
                        <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                          <tr>
                            <th className="px-4 py-3">Campaign</th>
                            <th className="px-4 py-3">State</th>
                            <th className="px-4 py-3">Age</th>
                          </tr>
                        </thead>
                        <tbody>
                          {queue.items.map((item) => (
                            <tr key={item.campaignId} className="border-t border-line">
                              <td className="px-4 py-3">
                                <p className="font-semibold text-ink">{item.campaignName}</p>
                                <p className="text-xs text-ink-soft">{item.clientName}</p>
                              </td>
                              <td className="px-4 py-3 text-ink-soft">
                                <p>{item.currentState}</p>
                                <p className="text-xs">{item.paymentState} / {item.communicationState}</p>
                              </td>
                              <td className="px-4 py-3 text-ink-soft">
                                <p>{item.ageDays} day{item.ageDays === 1 ? '' : 's'}</p>
                                <p className="text-xs">{new Date(item.lastActivityAt).toLocaleString()}</p>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : (
                    <p className="mt-4 text-sm text-ink-soft">No campaigns in this queue.</p>
                  )}
                </div>
              ))}
            </div>
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

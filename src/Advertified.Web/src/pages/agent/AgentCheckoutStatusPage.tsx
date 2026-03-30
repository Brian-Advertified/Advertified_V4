import { Eye, Pencil } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  fmtDate,
  queueTone,
  useAgentInboxQuery,
} from './agentWorkspace';

export function AgentCheckoutStatusPage() {
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading checkout status...">
      <AgentPageShell title="Purchase and checkout status" description="Monitor paid package movement, brief follow-up, and which paid orders still need agent action to become active campaigns.">
        {(() => {
          const rows = (inboxQuery.data?.items ?? [])
            .filter((item) => item.queueStage === 'newly_paid' || item.queueStage === 'brief_waiting')
            .sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime());

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Newly paid</p><p className="mt-4 text-3xl font-semibold text-ink">{inboxQuery.data?.newlyPaidCount ?? 0}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Brief waiting</p><p className="mt-4 text-3xl font-semibold text-ink">{inboxQuery.data?.briefWaitingCount ?? 0}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Unassigned</p><p className="mt-4 text-3xl font-semibold text-ink">{inboxQuery.data?.unassignedCount ?? 0}</p></div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Client</th>
                      <th className="px-4 py-4">Package</th>
                      <th className="px-4 py-4">Amount</th>
                      <th className="px-4 py-4">Status</th>
                      <th className="px-4 py-4">Date</th>
                      <th className="px-4 py-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((item) => (
                      <tr key={item.id} className="border-t border-line">
                        <td className="px-4 py-4 font-semibold text-ink">{item.clientName}</td>
                        <td className="px-4 py-4 text-ink-soft">{item.packageBandName}</td>
                        <td className="px-4 py-4 text-ink-soft">{fmtCurrency(item.selectedBudget)}</td>
                        <td className="px-4 py-4"><span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span></td>
                        <td className="px-4 py-4 text-ink-soft">{fmtDate(item.createdAt)}</td>
                        <td className="px-4 py-4">
                          <div className="flex justify-end gap-2">
                            <Link to={`/agent/campaigns/${item.id}`} className="button-secondary p-2" title={`View ${item.campaignName}`}>
                              <Eye className="size-4" />
                            </Link>
                            <Link to={`/agent/recommendations/new?campaignId=${item.id}`} className="button-secondary p-2" title={`Edit ${item.campaignName}`}>
                              <Pencil className="size-4" />
                            </Link>
                          </div>
                        </td>
                      </tr>
                    ))}
                    {rows.length === 0 ? (
                      <tr><td colSpan={6} className="px-4 py-8 text-center text-sm text-ink-soft">No purchase or checkout follow-up is waiting right now.</td></tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

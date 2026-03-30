import { Eye, Pencil } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  AgentPageShell,
  AgentQueryBoundary,
  queueTone,
  titleize,
  useAgentCampaignsQuery,
} from './agentWorkspace';

export function AgentApprovalsPage() {
  const campaignsQuery = useAgentCampaignsQuery();

  return (
    <AgentQueryBoundary query={campaignsQuery} loadingLabel="Loading approvals...">
      <AgentPageShell title="Approvals and change requests" description="Track client responses, recommendation approvals, and the campaigns that came back with revision requests.">
        {(() => {
          const rows = (campaignsQuery.data ?? [])
            .filter((campaign) => campaign.recommendations.some((item) => item.status === 'sent_to_client' || item.status === 'approved' || item.clientFeedbackNotes))
            .sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime());

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Pending response</p><p className="mt-4 text-3xl font-semibold text-ink">{rows.filter((item) => item.recommendations.some((rec) => rec.status === 'sent_to_client')).length}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Approved</p><p className="mt-4 text-3xl font-semibold text-ink">{rows.filter((item) => item.recommendations.some((rec) => rec.status === 'approved')).length}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Changes requested</p><p className="mt-4 text-3xl font-semibold text-ink">{rows.filter((item) => item.recommendations.some((rec) => Boolean(rec.clientFeedbackNotes))).length}</p></div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Client</th>
                      <th className="px-4 py-4">Campaign</th>
                      <th className="px-4 py-4">Status</th>
                      <th className="px-4 py-4">Client feedback</th>
                      <th className="px-4 py-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((campaign) => {
                      const active = campaign.recommendations[0] ?? campaign.recommendation;
                      const status = active?.clientFeedbackNotes ? 'Changes requested' : titleize(active?.status ?? campaign.status);
                      const statusClass = active?.clientFeedbackNotes ? 'border-amber-200 bg-amber-50 text-amber-700' : queueTone(active?.status === 'approved' ? 'completed' : 'waiting_on_client');
                      return (
                        <tr key={campaign.id} className="border-t border-line">
                          <td className="px-4 py-4 font-semibold text-ink">{campaign.clientName ?? campaign.businessName ?? 'Client account'}</td>
                          <td className="px-4 py-4 text-ink-soft">{campaign.campaignName}</td>
                          <td className="px-4 py-4"><span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${statusClass}`}>{status}</span></td>
                          <td className="px-4 py-4 text-ink-soft">{active?.clientFeedbackNotes ?? 'Waiting for client response.'}</td>
                          <td className="px-4 py-4">
                            <div className="flex justify-end gap-2">
                              <Link to={`/agent/campaigns/${campaign.id}`} className="button-secondary p-2" title={`View ${campaign.campaignName}`}>
                                <Eye className="size-4" />
                              </Link>
                              <Link to={`/agent/recommendations/new?campaignId=${campaign.id}`} className="button-secondary p-2" title={`Edit ${campaign.campaignName}`}>
                                <Pencil className="size-4" />
                              </Link>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
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

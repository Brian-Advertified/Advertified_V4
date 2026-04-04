import { BriefcaseBusiness, Eye, Pencil } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  queueTone,
  titleize,
  useAgentInboxQuery,
} from './agentWorkspace';

export function AgentRecommendationBuilderPage() {
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading recommendation builder...">
      <AgentPageShell title="Build Recommendations" description="Open campaigns that are ready for planning and start or refine recommendation drafts.">
        {(() => {
          const rows = (inboxQuery.data?.items ?? [])
            .filter((item) => item.queueStage === 'planning_ready' || item.queueStage === 'agent_review')
            .sort((left, right) => Number(right.isUrgent) - Number(left.isUrgent) || new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime());

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Needs planning</p><p className="mt-4 text-3xl font-semibold text-ink">{inboxQuery.data?.planningReadyCount ?? 0}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Agent review</p><p className="mt-4 text-3xl font-semibold text-ink">{inboxQuery.data?.agentReviewCount ?? 0}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Manual review</p><p className="mt-4 text-3xl font-semibold text-ink">{inboxQuery.data?.manualReviewCount ?? 0}</p></div>
              </div>

              <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
                <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                  <table className="w-full border-collapse text-sm">
                    <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                      <tr>
                        <th className="px-4 py-4">Campaign</th>
                        <th className="px-4 py-4">Build source</th>
                        <th className="px-4 py-4">Queue</th>
                        <th className="px-4 py-4">Next action</th>
                        <th className="px-4 py-4 text-right">Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rows.map((item) => (
                        <tr key={item.id} className="border-t border-line">
                          <td className="px-4 py-4">
                            <p className="font-semibold text-ink">{item.campaignName}</p>
                            <p className="text-xs text-ink-soft">{item.clientName} | {fmtCurrency(item.selectedBudget)}</p>
                          </td>
                          <td className="px-4 py-4 text-ink-soft">{titleize(item.planningMode ?? 'hybrid')}</td>
                          <td className="px-4 py-4"><span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${queueTone(item.queueStage)}`}>{item.queueLabel}</span></td>
                          <td className="px-4 py-4 text-ink-soft">{item.nextAction}</td>
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
                        <tr><td colSpan={5} className="px-4 py-8 text-center text-sm text-ink-soft">No campaigns are waiting for recommendation build right now.</td></tr>
                      ) : null}
                    </tbody>
                  </table>
                </div>

                <div className="space-y-4">
                  <div className="panel p-6">
                    <div className="flex items-center gap-3 text-brand">
                      <BriefcaseBusiness className="size-5" />
                      <h3 className="text-lg font-semibold text-ink">Builder workspace</h3>
                    </div>
                    <p className="mt-3 text-sm text-ink-soft">Open the builder to structure the brief, run AI interpretation, and generate the first recommendation draft.</p>
                    <Link to="/agent/recommendations/new" className="button-primary mt-4 inline-flex px-4 py-2">Start recommendation</Link>
                  </div>
                  <div className="panel p-6">
                    <h3 className="text-lg font-semibold text-ink">When to use this page</h3>
                    <p className="mt-3 text-sm text-ink-soft">Use this when you only want campaigns that are ready for recommendation work, without the rest of the queue getting in the way.</p>
                  </div>
                </div>
              </div>
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

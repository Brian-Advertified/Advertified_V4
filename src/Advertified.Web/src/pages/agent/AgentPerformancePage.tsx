import { CircleAlert, CircleCheckBig, Clock3, FolderKanban, Send, Sparkles, TrendingUp } from 'lucide-react';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  useAgentCampaignsQuery,
  useAgentInboxQuery,
} from './agentWorkspace';
import { getAverageTurnaroundDays } from './agentSectionShared';

export function AgentPerformancePage() {
  const inboxQuery = useAgentInboxQuery();
  const campaignsQuery = useAgentCampaignsQuery();

  return (
    <AgentQueryBoundary query={campaignsQuery} loadingLabel="Loading performance metrics...">
      <AgentPageShell title="Agent performance" description="Track throughput, approval outcomes, value handled, and the speed of moving campaigns from brief to client-ready recommendation.">
        {(() => {
          const campaigns = campaignsQuery.data ?? [];
          const inbox = inboxQuery.data;
          const totalManagedValue = campaigns.reduce((sum, campaign) => sum + campaign.selectedBudget, 0);
          const recommendationsSent = campaigns.filter((campaign) => campaign.recommendations.some((item) => item.status === 'sent_to_client' || item.status === 'approved')).length;
          const approved = campaigns.filter((campaign) => campaign.recommendations.some((item) => item.status === 'approved')).length;
          const approvalRate = recommendationsSent > 0 ? Math.round((approved / recommendationsSent) * 100) : 0;
          const averageTurnaround = getAverageTurnaroundDays(campaigns);

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                {[
                  { label: 'Managed value', value: fmtCurrency(totalManagedValue), helper: 'Total budget currently sitting in your visible campaign workload.', icon: TrendingUp },
                  { label: 'Recommendations sent', value: String(recommendationsSent), helper: 'Campaigns that reached the client-facing recommendation stage.', icon: Send },
                  { label: 'Approval rate', value: `${approvalRate}%`, helper: 'Approved campaigns out of all campaigns sent to clients.', icon: CircleCheckBig },
                  { label: 'Average turnaround', value: averageTurnaround ? `${averageTurnaround}d` : 'N/A', helper: 'Approximate brief-to-approval speed from live campaign history.', icon: Sparkles },
                ].map((card) => {
                  const Icon = card.icon;
                  return (
                    <div key={card.label} className="panel p-6">
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">{card.label}</p>
                          <p className="mt-4 text-3xl font-semibold text-ink">{card.value}</p>
                          <p className="mt-2 text-sm text-ink-soft">{card.helper}</p>
                        </div>
                        <div className="rounded-2xl bg-brand-soft p-3 text-brand"><Icon className="size-5" /></div>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div className="grid gap-4 xl:grid-cols-3">
                <div className="panel p-6">
                  <div className="flex items-center gap-3">
                    <CircleAlert className="size-5 text-amber-600" />
                    <h3 className="text-lg font-semibold text-ink">Watchlist</h3>
                  </div>
                  <p className="mt-3 text-sm text-ink-soft">Urgent {inbox?.urgentCount ?? 0} | Manual review {inbox?.manualReviewCount ?? 0} | Stale {inbox?.staleCount ?? 0}</p>
                </div>
                <div className="panel p-6">
                  <div className="flex items-center gap-3">
                    <FolderKanban className="size-5 text-brand" />
                    <h3 className="text-lg font-semibold text-ink">Pipeline load</h3>
                  </div>
                  <p className="mt-3 text-sm text-ink-soft">Planning ready {inbox?.planningReadyCount ?? 0} | Agent review {inbox?.agentReviewCount ?? 0} | Waiting on client {inbox?.waitingOnClientCount ?? 0}</p>
                </div>
                <div className="panel p-6">
                  <div className="flex items-center gap-3">
                    <Clock3 className="size-5 text-brand" />
                    <h3 className="text-lg font-semibold text-ink">Current queue</h3>
                  </div>
                  <p className="mt-3 text-sm text-ink-soft">Assigned to me {inbox?.assignedToMeCount ?? 0} | Unassigned {inbox?.unassignedCount ?? 0} | Completed {inbox?.completedCount ?? 0}</p>
                </div>
              </div>
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

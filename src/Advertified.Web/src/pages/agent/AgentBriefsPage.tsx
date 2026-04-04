import { Eye, Pencil } from 'lucide-react';
import { Link } from 'react-router-dom';
import { canAccessAiStudioForStatus } from '../../features/campaigns/aiStudioAccess';
import {
  AgentPageShell,
  AgentQueryBoundary,
  titleize,
  useAgentCampaignsQuery,
} from './agentWorkspace';

function formatChannelLabel(value: string) {
  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}

export function AgentBriefsPage() {
  const campaignsQuery = useAgentCampaignsQuery();

  return (
    <AgentQueryBoundary query={campaignsQuery} loadingLabel="Loading campaign briefs...">
      <AgentPageShell title="Needs Brief" description="See which campaigns are still waiting for planning details and which ones are ready to move into recommendation work.">
        {(() => {
          const rows = (campaignsQuery.data ?? [])
            .filter((campaign) => campaign.status !== 'approved')
            .sort((left, right) => Number(Boolean(right.brief)) - Number(Boolean(left.brief)) || new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime());

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Brief missing</p><p className="mt-4 text-3xl font-semibold text-ink">{rows.filter((item) => !item.brief).length}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Brief captured</p><p className="mt-4 text-3xl font-semibold text-ink">{rows.filter((item) => item.brief).length}</p></div>
                <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Ready for planning</p><p className="mt-4 text-3xl font-semibold text-ink">{rows.filter((item) => item.brief && item.recommendations.length === 0).length}</p></div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Campaign</th>
                      <th className="px-4 py-4">Brief status</th>
                      <th className="px-4 py-4">Captured inputs</th>
                      <th className="px-4 py-4">Next action</th>
                      <th className="px-4 py-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((campaign) => (
                      <tr key={campaign.id} className="border-t border-line">
                        <td className="px-4 py-4">
                          <p className="font-semibold text-ink">{campaign.campaignName}</p>
                          <p className="text-xs text-ink-soft">{campaign.clientName ?? campaign.businessName ?? 'Client account'}</p>
                        </td>
                        <td className="px-4 py-4">
                          <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${campaign.brief ? 'border-brand/20 bg-brand-soft text-brand' : 'border-amber-200 bg-amber-50 text-amber-700'}`}>
                            {campaign.brief ? 'Brief complete' : 'Brief missing'}
                          </span>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          {campaign.brief
                            ? `${titleize(campaign.brief.objective)} | ${titleize(campaign.brief.geographyScope)} | ${(campaign.brief.preferredMediaTypes ?? []).map(formatChannelLabel).join(', ') || 'No channels set'}`
                            : 'No planning inputs captured yet.'}
                        </td>
                        <td className="px-4 py-4 text-ink-soft">{campaign.nextAction}</td>
                        <td className="px-4 py-4">
                          <div className="flex justify-end gap-2">
                            <Link to={`/agent/campaigns/${campaign.id}`} className="button-secondary p-2" title={`View ${campaign.campaignName}`}>
                              <Eye className="size-4" />
                            </Link>
                            {canAccessAiStudioForStatus(campaign.status) ? (
                              <Link to={`/ai-studio?campaignId=${campaign.id}`} className="button-secondary p-2" title={`Open AI Studio for ${campaign.campaignName}`}>
                                AI
                              </Link>
                            ) : null}
                            <Link to={`/agent/recommendations/new?campaignId=${campaign.id}`} className="button-secondary p-2" title={`Edit ${campaign.campaignName}`}>
                              <Pencil className="size-4" />
                            </Link>
                          </div>
                        </td>
                      </tr>
                    ))}
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


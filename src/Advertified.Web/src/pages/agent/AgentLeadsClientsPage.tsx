import { Search, Eye, Pencil, ArrowRightCircle } from 'lucide-react';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtDate,
  titleize,
  useAgentCampaignsQuery,
} from './agentWorkspace';
import { buildClientRows } from './agentSectionShared';

export function AgentLeadsClientsPage() {
  const campaignsQuery = useAgentCampaignsQuery();
  const [search, setSearch] = useState('');
  const navigate = useNavigate();

  function inferSuggestedCampaignType(input: { topPackage: string; latestActivity: string }): 'awareness' | 'launch' | 'promotion' | 'brand_presence' | 'leads' {
    const combined = `${input.topPackage} ${input.latestActivity}`.toLowerCase();
    if (combined.includes('lead')) return 'leads';
    if (combined.includes('launch')) return 'launch';
    if (combined.includes('promo')) return 'promotion';
    if (combined.includes('presence')) return 'brand_presence';
    return 'awareness';
  }

  function handleConvertToCampaign(row: {
    latestCampaignId?: string;
    clientName: string;
    topRegion: string;
    topPackage: string;
    latestActivity: string;
  }) {
    const suggestedCampaignType = inferSuggestedCampaignType(row);
    const targetPath = row.latestCampaignId
      ? `/agent/recommendations/new?campaignId=${encodeURIComponent(row.latestCampaignId)}`
      : '/agent/recommendation-builder';

    navigate(targetPath, {
      state: {
        convertToCampaign: {
          businessName: row.clientName,
          location: row.topRegion === 'Not set' ? '' : row.topRegion,
          suggestedCampaignType,
        },
      },
    });
  }

  return (
    <AgentQueryBoundary query={campaignsQuery} loadingLabel="Loading leads and clients...">
      <AgentPageShell title="Clients" description="See each client, what stage they are in, and the latest update without opening every campaign one by one.">
        {(() => {
          const rows = buildClientRows(campaignsQuery.data, search);
          return (
            <section className="space-y-6">
              <div className="panel flex flex-col gap-4 px-6 py-6 lg:flex-row lg:items-center">
                <label className="relative flex-1">
                  <Search className="absolute left-4 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
                  <input value={search} onChange={(event) => setSearch(event.target.value)} className="input-base pl-11" placeholder="Search client, email, region, or package" />
                </label>
                <div className="rounded-2xl bg-brand-soft px-4 py-3 text-sm text-brand">Each row summarizes the client from their live campaign activity.</div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Client</th>
                      <th className="px-4 py-4">Portfolio</th>
                      <th className="px-4 py-4">Current focus</th>
                      <th className="px-4 py-4">Latest activity</th>
                      <th className="px-4 py-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row) => (
                      <tr key={row.userId} className="border-t border-line">
                        <td className="px-4 py-4">
                          <p className="font-semibold text-ink">{row.clientName}</p>
                          <p className="text-xs text-ink-soft">{row.clientEmail}</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          <p>{row.campaignCount} campaign(s)</p>
                          <p className="text-xs">{row.activeCount} in progress | {row.awaitingApprovalCount} waiting on client</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          <p>{row.topPackage}</p>
                          <p className="text-xs">{titleize(row.topRegion)}</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          <p>{row.latestActivity}</p>
                          <p className="text-xs">{fmtDate(row.latestActivityAt)}</p>
                        </td>
                        <td className="px-4 py-4">
                          <div className="flex justify-end gap-2">
                            <button
                              type="button"
                              onClick={() => handleConvertToCampaign(row)}
                              className="button-secondary p-2"
                              title={`Convert ${row.clientName} to campaign`}
                            >
                              <ArrowRightCircle className="size-4" />
                            </button>
                            <Link to={row.latestCampaignId ? `/agent/campaigns/${row.latestCampaignId}` : '/agent/campaigns'} className="button-secondary p-2" title={`View campaigns for ${row.clientName}`}>
                              <Eye className="size-4" />
                            </Link>
                            <Link to={row.latestCampaignId ? `/agent/recommendations/new?campaignId=${row.latestCampaignId}` : '/agent/recommendation-builder'} className="button-secondary p-2" title={`Build recommendation for ${row.clientName}`}>
                              <Pencil className="size-4" />
                            </Link>
                          </div>
                        </td>
                      </tr>
                    ))}
                    {rows.length === 0 ? (
                      <tr><td colSpan={5} className="px-4 py-8 text-center text-sm text-ink-soft">No client records match this search yet.</td></tr>
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

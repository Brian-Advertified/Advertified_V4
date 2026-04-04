import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { AgentPageShell, AgentQueryBoundary, fmtCurrency, fmtDate } from './agentWorkspace';
import { advertifiedApi } from '../../services/advertifiedApi';

export function AgentSalesPage() {
  const salesQuery = useQuery({ queryKey: ['agent-sales'], queryFn: advertifiedApi.getAgentSales });

  return (
    <AgentQueryBoundary query={salesQuery} loadingLabel="Loading sales...">
      <AgentPageShell title="Sales" description="See paid sales linked to you, including campaigns converted from prospects.">
        {(() => {
          const sales = salesQuery.data;
          if (!sales) {
            return null;
          }

          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="rounded-[24px] border border-line bg-white px-5 py-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.14em] text-ink-soft">Total sales</p>
                  <p className="mt-2 text-2xl font-semibold text-ink">{sales.totalSalesCount}</p>
                </div>
                <div className="rounded-[24px] border border-line bg-white px-5 py-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.14em] text-ink-soft">Prospect conversions</p>
                  <p className="mt-2 text-2xl font-semibold text-ink">{sales.convertedProspectSalesCount}</p>
                </div>
                <div className="rounded-[24px] border border-line bg-white px-5 py-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.14em] text-ink-soft">Charged total</p>
                  <p className="mt-2 text-2xl font-semibold text-ink">{fmtCurrency(sales.totalChargedAmount)}</p>
                </div>
                <div className="rounded-[24px] border border-line bg-white px-5 py-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.14em] text-ink-soft">Media budget total</p>
                  <p className="mt-2 text-2xl font-semibold text-ink">{fmtCurrency(sales.totalSelectedBudget)}</p>
                </div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line bg-white">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Client</th>
                      <th className="px-4 py-4">Campaign</th>
                      <th className="px-4 py-4">Package</th>
                      <th className="px-4 py-4">Charged</th>
                      <th className="px-4 py-4">Purchased</th>
                      <th className="px-4 py-4 text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sales.items.map((item) => (
                      <tr key={item.campaignId} className="border-t border-line">
                        <td className="px-4 py-4">
                          <p className="font-semibold text-ink">{item.clientName}</p>
                          <p className="text-xs text-ink-soft">{item.clientEmail}</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          <p>{item.campaignName}</p>
                          <p className="text-xs">{item.convertedFromProspect ? 'Converted from prospect' : 'Direct sale'}</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          <p>{item.packageBandName}</p>
                          <p className="text-xs">Budget {fmtCurrency(item.selectedBudget)}</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">
                          <p>{fmtCurrency(item.chargedAmount)}</p>
                          <p className="text-xs">{item.paymentProvider.toUpperCase()}</p>
                        </td>
                        <td className="px-4 py-4 text-ink-soft">{fmtDate(item.purchasedAt)}</td>
                        <td className="px-4 py-4 text-right">
                          <Link to={`/agent/campaigns/${item.campaignId}`} className="button-secondary px-3 py-2">
                            Open
                          </Link>
                        </td>
                      </tr>
                    ))}
                    {sales.items.length === 0 ? (
                      <tr>
                        <td colSpan={6} className="px-4 py-8 text-center text-sm text-ink-soft">
                          No sales recorded yet.
                        </td>
                      </tr>
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

import { Eye, Pencil } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  AgentPageShell,
  AgentQueryBoundary,
  fmtCurrency,
  titleize,
  useAgentInboxQuery,
  usePackagesQuery,
} from './agentWorkspace';

export function AgentPackageSelectionPage() {
  const packagesQuery = usePackagesQuery();
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={packagesQuery} loadingLabel="Loading package selection...">
      <AgentPageShell title="Package selection" description="Guide clients through the live package bands, the budgets they support, and the planning signals each band unlocks.">
        {(() => {
          const packageBands = packagesQuery.data ?? [];
          const inbox = inboxQuery.data;
          return (
            <section className="space-y-6">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="panel p-6">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Active package bands</p>
                  <p className="mt-4 text-3xl font-semibold text-ink">{packageBands.length}</p>
                </div>
                <div className="panel p-6">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Newly paid today</p>
                  <p className="mt-4 text-3xl font-semibold text-ink">{inbox?.newlyPaidCount ?? 0}</p>
                </div>
                <div className="panel p-6">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Planning ready</p>
                  <p className="mt-4 text-3xl font-semibold text-ink">{inbox?.planningReadyCount ?? 0}</p>
                </div>
              </div>

              <div className="grid gap-4 xl:grid-cols-[1.25fr_0.85fr]">
                <div className="space-y-4">
                  {packageBands.map((pkg) => (
                    <div key={pkg.id} className="panel p-6">
                      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">{pkg.code}</p>
                          <h3 className="mt-2 text-xl font-semibold text-ink">{pkg.name}</h3>
                          <p className="mt-2 text-sm text-ink-soft">{pkg.description}</p>
                        </div>
                        <span className="pill border-brand/15 bg-brand-soft text-brand">{fmtCurrency(pkg.minBudget)} - {fmtCurrency(pkg.maxBudget)}</span>
                      </div>
                      <div className="mt-4 grid gap-3 md:grid-cols-2">
                        <div className="rounded-2xl border border-line bg-slate-50 p-4 text-sm text-ink-soft">
                          <p><span className="font-semibold text-ink">Audience fit:</span> {pkg.audienceFit}</p>
                          <p className="mt-2"><span className="font-semibold text-ink">Lead time:</span> {pkg.leadTime}</p>
                          <p className="mt-2"><span className="font-semibold text-ink">Radio:</span> {titleize(pkg.includeRadio)}</p>
                          <p className="mt-2"><span className="font-semibold text-ink">TV:</span> {titleize(pkg.includeTv)}</p>
                        </div>
                        <div className="rounded-2xl border border-line bg-white p-4 text-sm text-ink-soft">
                          <p className="font-semibold text-ink">Benefits</p>
                          <ul className="mt-2 space-y-1">
                            {pkg.benefits.map((benefit) => <li key={benefit}>- {benefit}</li>)}
                          </ul>
                        </div>
                      </div>
                      <div className="mt-4 flex justify-end gap-2">
                        <Link to="/packages" className="button-secondary p-2" title={`View ${pkg.name} on public packages`}>
                          <Eye className="size-4" />
                        </Link>
                        <Link to="/agent/recommendation-builder" className="button-secondary p-2" title={`Use ${pkg.name} in recommendation planning`}>
                          <Pencil className="size-4" />
                        </Link>
                      </div>
                    </div>
                  ))}
                </div>

                <div className="space-y-4">
                  <div className="panel p-6">
                    <h3 className="text-lg font-semibold text-ink">Package explorer</h3>
                    <p className="mt-2 text-sm text-ink-soft">Use the public package experience when you want to walk a client through indicative examples before a purchase.</p>
                    <Link to="/packages" className="button-primary mt-4 inline-flex px-4 py-2">Open package page</Link>
                  </div>
                  <div className="panel p-6">
                    <h3 className="text-lg font-semibold text-ink">Sales guidance</h3>
                    <p className="mt-2 text-sm text-ink-soft">The package layer is live and rule-backed. Geography and channel mix still become campaign-specific inside the recommendation workflow.</p>
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

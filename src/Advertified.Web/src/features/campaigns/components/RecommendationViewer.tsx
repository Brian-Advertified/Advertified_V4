import { formatCurrency } from '../../../lib/utils';
import type { CampaignRecommendation } from '../../../types/domain';
import { StatusBadge } from '../../../components/ui/StatusBadge';

export function RecommendationViewer({ recommendation }: { recommendation: CampaignRecommendation }) {
  return (
    <div className="panel space-y-6 px-6 py-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Recommendation</p>
          {recommendation.buildSourceLabel ? (
            <div className="mt-3 inline-flex rounded-full border border-line bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">
              {recommendation.buildSourceLabel}
            </div>
          ) : null}
          <h3 className="mt-2 text-2xl font-semibold tracking-tight text-ink">{recommendation.summary}</h3>
          <p className="mt-3 max-w-3xl text-sm leading-7 text-ink-soft">{recommendation.rationale}</p>
        </div>
        <div className="space-y-3">
          <StatusBadge status={recommendation.status} />
          <div className="rounded-2xl bg-brand-soft px-4 py-3 text-right">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Projected total</p>
            <p className="mt-1 text-2xl font-semibold text-ink">{formatCurrency(recommendation.totalCost)}</p>
          </div>
        </div>
      </div>
      <div className="grid gap-4">
        {recommendation.items.map((item) => (
          <div key={item.id} className="rounded-[24px] border border-line bg-slate-50 px-5 py-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <div className="pill bg-white text-ink-soft">{item.channel}</div>
                <p className="mt-3 text-lg font-semibold text-ink">{item.title}</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">{item.rationale}</p>
              </div>
              <div className="text-right">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">{item.type}</p>
                <p className="mt-2 text-xl font-semibold text-ink">{formatCurrency(item.cost)}</p>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

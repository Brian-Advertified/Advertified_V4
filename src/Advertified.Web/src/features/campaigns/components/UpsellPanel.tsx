import { ArrowUpRight, Sparkles } from 'lucide-react';
import { formatCurrency } from '../../../lib/utils';
import type { CampaignRecommendation } from '../../../types/domain';

export function UpsellPanel({ recommendation }: { recommendation?: CampaignRecommendation }) {
  const upsells = recommendation?.items.filter((item) => item.type === 'upsell') ?? [];

  return (
    <div className="panel px-6 py-6">
      <div className="flex items-center gap-3">
        <div className="rounded-2xl bg-highlight-soft p-3 text-highlight"><Sparkles className="size-5" /></div>
        <div>
          <p className="text-sm font-semibold text-ink">Optional upsells</p>
          <p className="text-sm text-ink-soft">Keep these visible but secondary to the base recommendation.</p>
        </div>
      </div>
      <div className="mt-5 space-y-4">
        {upsells.length ? upsells.map((item) => (
          <div key={item.id} className="rounded-[24px] bg-slate-50 px-5 py-5">
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-base font-semibold text-ink">{item.title}</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">{item.rationale}</p>
              </div>
              <p className="text-sm font-semibold text-ink">{formatCurrency(item.cost)}</p>
            </div>
          </div>
        )) : (
          <div className="rounded-[24px] bg-slate-50 px-5 py-5 text-sm leading-7 text-ink-soft">
            No upsells suggested yet. Submit the brief and choose a planning mode to unlock them.
          </div>
        )}
      </div>
      <div className="mt-5 inline-flex items-center gap-2 text-sm font-semibold text-brand">
        Upsell suggestions stay optional
        <ArrowUpRight className="size-4" />
      </div>
    </div>
  );
}

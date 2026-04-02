import { Download } from 'lucide-react';
import { titleCase, formatCurrency } from '../../../lib/utils';
import { advertifiedApi } from '../../../services/advertifiedApi';
import type { CampaignRecommendation } from '../../../types/domain';
import { StatusBadge } from '../../../components/ui/StatusBadge';

export function RecommendationViewer({ recommendation, recommendationPdfUrl }: { recommendation: CampaignRecommendation; recommendationPdfUrl?: string }) {
  const baseItems = recommendation.items.filter((item) => item.type === 'base');
  const groupedChannels = Array.from(new Set(baseItems.map((item) => formatClientChannelLabel(item.channel))));
  const topReasons = Array.from(new Set(baseItems.flatMap((item) => item.selectionReasons))).slice(0, 4);

  return (
    <div className="panel space-y-6 px-6 py-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Recommendation</p>
          <div className="mt-3 flex flex-wrap gap-2">
            {recommendation.proposalLabel ? (
              <div className="inline-flex rounded-full border border-brand/15 bg-brand-soft px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-brand">
                {recommendation.proposalLabel}
              </div>
            ) : null}
            {recommendation.proposalStrategy ? (
              <div className="inline-flex rounded-full border border-line bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">
                {recommendation.proposalStrategy}
              </div>
            ) : null}
            {recommendation.buildSourceLabel ? (
              <div className="inline-flex rounded-full border border-line bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">
                {recommendation.buildSourceLabel}
              </div>
            ) : null}
          </div>
          <h3 className="mt-2 text-2xl font-semibold tracking-tight text-ink">{toClientFriendlyCopy(recommendation.summary)}</h3>
          <p className="mt-3 max-w-3xl text-sm leading-7 text-ink-soft">{toClientFriendlyCopy(recommendation.rationale)}</p>
        </div>
        <div className="space-y-3">
          <StatusBadge status={recommendation.status} />
          {recommendationPdfUrl ? (
            <button
              type="button"
              onClick={() => {
                void advertifiedApi.downloadProtectedFile(
                  recommendationPdfUrl,
                  `recommendation-${recommendation.campaignId}.pdf`,
                );
              }}
              className="inline-flex items-center gap-2 rounded-full border border-line bg-white px-4 py-2 text-sm font-semibold text-ink-soft transition hover:border-brand/30 hover:text-ink"
            >
              <Download className="size-4" />
              Download detailed PDF
            </button>
          ) : null}
          <div className="rounded-2xl bg-brand-soft px-4 py-3 text-right">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Campaign total</p>
            <p className="mt-1 text-2xl font-semibold text-ink">{formatCurrency(recommendation.totalCost)}</p>
          </div>
        </div>
      </div>
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="rounded-[24px] border border-line bg-slate-50 px-5 py-5">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Recommended mix</p>
          <p className="mt-3 text-lg font-semibold text-ink">{groupedChannels.join(' + ') || 'Not set'}</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">
            This mix reflects the channels selected for the main campaign recommendation.
          </p>
        </div>
        <div className="rounded-[24px] border border-line bg-slate-50 px-5 py-5">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Planned placements</p>
          <p className="mt-3 text-lg font-semibold text-ink">{baseItems.length} placement{baseItems.length === 1 ? '' : 's'}</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">
            Each placement has been checked against campaign fit and available inventory.
          </p>
        </div>
        <div className="rounded-[24px] border border-line bg-slate-50 px-5 py-5">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Why this was chosen</p>
          <div className="mt-3 flex flex-wrap gap-2">
            {topReasons.length > 0 ? topReasons.map((reason) => (
              <span key={reason} className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-ink-soft ring-1 ring-line">
                {reason}
              </span>
            )) : (
              <span className="text-sm text-ink-soft">Reason codes are still being prepared for this recommendation.</span>
            )}
          </div>
        </div>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        {recommendation.items.map((item) => (
          <div key={item.id} className="rounded-[24px] border border-line bg-slate-50 px-5 py-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <div className="pill bg-white text-ink-soft">{formatClientChannelLabel(item.channel)}</div>
                <p className="mt-3 text-lg font-semibold text-ink">{item.title}</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">{item.rationale}</p>
                <div className="mt-3 flex flex-wrap gap-2">
                  {item.region ? (
                    <span className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-ink-soft ring-1 ring-line">
                      {item.region}
                    </span>
                  ) : null}
                  {item.timeBand ? (
                    <span className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-ink-soft ring-1 ring-line">
                      {item.timeBand}
                    </span>
                  ) : null}
                  {item.duration ? (
                    <span className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-ink-soft ring-1 ring-line">
                      {item.duration}
                    </span>
                  ) : null}
                </div>
                {item.selectionReasons.length > 0 ? (
                  <div className="mt-3 flex flex-wrap gap-2">
                    {item.selectionReasons.slice(0, 3).map((reason) => (
                      <span key={reason} className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-ink-soft ring-1 ring-line">
                        {reason}
                      </span>
                    ))}
                  </div>
                ) : null}
                {item.restrictions ? (
                  <p className="mt-3 text-sm leading-7 text-ink-soft">
                    <span className="font-semibold text-ink">Booking note:</span> {toClientFriendlyCopy(item.restrictions)}
                  </p>
                ) : null}
              </div>
              <div className="text-right">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">{titleCase(item.type)}</p>
                <p className="mt-2 text-sm font-semibold text-ink">{item.quantity} placement{item.quantity === 1 ? '' : 's'}</p>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function formatClientChannelLabel(value: string) {
  if (!value) {
    return value;
  }

  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}

function toClientFriendlyCopy(value: string) {
  if (!value) {
    return value;
  }

  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}


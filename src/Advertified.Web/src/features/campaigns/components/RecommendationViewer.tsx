import { Download } from 'lucide-react';
import { buildRecommendationTimingLabel, titleCase, formatCurrency } from '../../../lib/utils';
import type { CampaignRecommendation } from '../../../types/domain';
import { StatusBadge } from '../../../components/ui/StatusBadge';
import { formatChannelLabel } from '../../channels/channelUtils';
import type { CampaignOpportunityContext } from '../briefModel';

export function RecommendationViewer({
  recommendation,
  recommendationPdfUrl,
  onDownloadPdf,
  opportunityContext,
}: {
  recommendation: CampaignRecommendation;
  recommendationPdfUrl?: string;
  onDownloadPdf?: () => void | Promise<void>;
  opportunityContext?: CampaignOpportunityContext;
}) {
  const baseItems = recommendation.items.filter((item) => item.type === 'base');
  const groupedChannels = Array.from(new Set(baseItems.map((item) => formatChannelLabel(item.channel))));
  const topReasons = Array.from(new Set(baseItems.flatMap((item) => item.selectionReasons))).slice(0, 4);
  const narrative = recommendation.narrative;

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
                void onDownloadPdf?.();
              }}
              className="inline-flex items-center gap-2 rounded-full border border-line bg-white px-4 py-2 text-sm font-semibold text-ink-soft transition hover:border-brand/30 hover:text-ink"
            >
              <Download className="size-4" />
              Download detailed PDF
            </button>
          ) : null}
          <div className="rounded-2xl bg-brand-soft px-4 py-3 text-right">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Recommended investment</p>
            <p className="mt-1 text-2xl font-semibold text-ink">{formatCurrency(recommendation.totalCost)}</p>
            <p className="mt-1 text-xs text-ink-soft">Need a lighter option? Ask for a lean start or phased rollout.</p>
          </div>
        </div>
      </div>
      {narrative ? (
        <div className="rounded-[24px] border border-brand/15 bg-brand-soft/25 px-5 py-5">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Strategic plan</p>
          <div className="mt-4 grid gap-4 lg:grid-cols-3">
            {narrative.clientChallenge ? (
              <div className="rounded-[18px] border border-brand/15 bg-white px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Business reality</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">{toClientFriendlyCopy(narrative.clientChallenge)}</p>
              </div>
            ) : null}
            {narrative.strategicApproach ? (
              <div className="rounded-[18px] border border-brand/15 bg-white px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Strategy</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">{toClientFriendlyCopy(narrative.strategicApproach)}</p>
              </div>
            ) : null}
            {narrative.expectedOutcome ? (
              <div className="rounded-[18px] border border-brand/15 bg-white px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Expected effect</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">{toClientFriendlyCopy(narrative.expectedOutcome)}</p>
              </div>
            ) : null}
          </div>
          {narrative.channelRoles.length > 0 ? (
            <div className="mt-4 grid gap-3 md:grid-cols-2">
              {narrative.channelRoles.map((role) => (
                <p key={role} className="rounded-[16px] border border-line bg-white px-4 py-3 text-sm leading-7 text-ink-soft">
                  {toClientFriendlyCopy(role)}
                </p>
              ))}
            </div>
          ) : null}
        </div>
      ) : null}
      <div className="grid gap-4 lg:grid-cols-3">
        {opportunityContext ? (
          <div className="rounded-[24px] border border-brand/15 bg-brand-soft/30 px-5 py-5 lg:col-span-3">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Why you are receiving this</p>
            {opportunityContext.detectedGaps.length > 0 ? (
              <div className="mt-3 space-y-2 text-sm leading-7 text-ink-soft">
                {opportunityContext.detectedGaps.map((gap) => (
                  <p key={gap}>- {gap}</p>
                ))}
              </div>
            ) : null}
            {opportunityContext.insightSummary ? (
              <p className="mt-3 text-sm leading-7 text-ink-soft">{opportunityContext.insightSummary}</p>
            ) : null}
            {opportunityContext.expectedOutcome ? (
              <p className="mt-3 text-sm font-semibold text-ink">{opportunityContext.expectedOutcome}</p>
            ) : null}
          </div>
        ) : null}
        <div className="rounded-[24px] border border-line bg-slate-50 px-5 py-5">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Included channels</p>
          <p className="mt-3 text-lg font-semibold text-ink">{groupedChannels.join(' + ') || 'Not set'}</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">
            These are the channels included in the main option we would like you to consider first.
          </p>
        </div>
        <div className="rounded-[24px] border border-line bg-slate-50 px-5 py-5">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">What is included</p>
          <p className="mt-3 text-lg font-semibold text-ink">{baseItems.length} item{baseItems.length === 1 ? '' : 's'}</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">
            Each item has been chosen to match your campaign, timing, and location needs.
          </p>
        </div>
        <div className="rounded-[24px] border border-line bg-slate-50 px-5 py-5">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Why this option fits</p>
          <div className="mt-3 flex flex-wrap gap-2">
            {topReasons.length > 0 ? topReasons.map((reason) => (
              <span key={reason} className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-ink-soft ring-1 ring-line">
                {reason}
              </span>
            )) : (
              <span className="text-sm text-ink-soft">More detail will appear here as this recommendation is finalized.</span>
            )}
          </div>
        </div>
      </div>
      <div className="rounded-[24px] border border-amber-200 bg-amber-50/80 px-5 py-5">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-amber-900">Need a different starting point?</p>
        <p className="mt-3 text-sm leading-7 text-amber-950">
          You do not need to reject the whole offer if the current investment feels heavy. Ask us for a leaner start, fewer items, or a phased rollout and we can reshape the option around that.
        </p>
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        {recommendation.items.map((item) => {
          const timingLabel = buildRecommendationTimingLabel(item);
          return (
            <div key={item.id} className="rounded-[24px] border border-line bg-slate-50 px-5 py-5">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <div className="pill bg-white text-ink-soft">{formatChannelLabel(item.channel)}</div>
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
                    {timingLabel ? (
                      <span className="rounded-full bg-white px-3 py-1 text-xs font-semibold text-ink-soft ring-1 ring-line">
                        {timingLabel}
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
          );
        })}
      </div>
    </div>
  );
}

function toClientFriendlyCopy(value: string) {
  if (!value) {
    return value;
  }

  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}


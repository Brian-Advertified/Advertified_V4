import type { Campaign } from '../../../types/domain';
import { formatCompactBudget, formatCurrency, formatDate } from '../../../lib/utils';
import { buildCampaignPerformanceSnapshot, type CampaignPerformanceMetricKey, type CampaignPerformanceSnapshot } from './campaignPerformance';

function formatMetricLabel(metric: CampaignPerformanceMetricKey) {
  if (metric === 'impressions') {
    return 'Impressions';
  }

  if (metric === 'playsOrSpots') {
    return 'Plays / spots';
  }

  return 'Spend delivered';
}

function formatMetricValue(metric: CampaignPerformanceMetricKey, value: number) {
  if (metric === 'spendDelivered') {
    return formatCurrency(value);
  }

  return value.toLocaleString('en-ZA');
}

function getChannelStatusTone(status: string) {
  if (status === 'on_track') {
    return 'border-emerald-200 bg-emerald-50 text-emerald-700';
  }

  if (status === 'live') {
    return 'border-sky-200 bg-sky-50 text-sky-700';
  }

  if (status === 'booked') {
    return 'border-amber-200 bg-amber-50 text-amber-700';
  }

  return 'border-slate-200 bg-slate-100 text-slate-600';
}

function getBarWidth(value: number, maxValue: number) {
  if (maxValue <= 0 || value <= 0) {
    return 0;
  }

  return Math.max(8, Math.round((value / maxValue) * 100));
}

export function CampaignPerformancePanel({
  campaign,
  snapshot,
  title = 'Performance',
  subtitle = 'Live campaign delivery across channels.',
}: {
  campaign: Campaign;
  snapshot?: CampaignPerformanceSnapshot;
  title?: string;
  subtitle?: string;
}) {
  const view = snapshot ?? buildCampaignPerformanceSnapshot(campaign);

  if (view.bookingCount === 0 && view.reportCount === 0) {
    return null;
  }

  const timelinePeak = Math.max(...view.timeline.map((item) => item.value), 0);
  const channelPeak = Math.max(...view.channels.map((item) => Math.max(item.deliveredSpend, item.bookedSpend, item.impressions, item.playsOrSpots)), 0);
  const primaryMetricLabel = formatMetricLabel(view.primaryMetric);

  return (
    <section className="rounded-[30px] border border-line bg-white p-7 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
      <div className="mb-6 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h3 className="text-xl font-semibold text-ink">{title}</h3>
          <p className="mt-2 text-sm text-ink-soft">{subtitle}</p>
          <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Booked</p>
              <p className="mt-2 text-2xl font-semibold text-ink">{formatCurrency(view.totalBookedSpend)}</p>
            </div>
            <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Delivered</p>
              <p className="mt-2 text-2xl font-semibold text-ink">{formatCurrency(view.totalDeliveredSpend)}</p>
            </div>
            <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Impressions</p>
              <p className="mt-2 text-2xl font-semibold text-ink">{formatCompactBudget(view.totalImpressions)}</p>
            </div>
            {view.totalSyncedClicks > 0 ? (
              <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Clicks</p>
                <p className="mt-2 text-2xl font-semibold text-ink">{formatCompactBudget(view.totalSyncedClicks)}</p>
              </div>
            ) : null}
            <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Delivery</p>
              <p className="mt-2 text-2xl font-semibold text-ink">{view.spendDeliveryPercent}%</p>
            </div>
          </div>
        </div>

        <div className="rounded-[20px] border border-brand/15 bg-brand-soft/30 px-5 py-4 text-sm text-ink">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-brand">Live reporting</p>
          <div className="mt-2 flex flex-wrap gap-2">
            <span className="user-pill">{view.reportCount} updates</span>
            <span className="user-pill">{view.bookingCount} bookings</span>
            <span className="user-pill">{primaryMetricLabel}</span>
          </div>
        </div>
      </div>

      <div className="grid gap-5 lg:grid-cols-[1.1fr_0.9fr]">
        <div className="rounded-[22px] border border-line bg-slate-50/70 p-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <p className="text-sm font-semibold text-ink">Trend</p>
            <span className="text-xs font-semibold uppercase tracking-[0.14em] text-ink-soft">{primaryMetricLabel}</span>
          </div>
          {view.timeline.length > 0 ? (
            <div className="grid grid-cols-6 items-end gap-3">
              {view.timeline.map((point, index) => (
                <div key={`${point.date}-${index}`} className="flex flex-col items-center gap-2">
                  <div className="flex h-40 w-full items-end rounded-[16px] bg-white px-2 py-2">
                    <div
                      className="w-full rounded-[12px] bg-[linear-gradient(180deg,#4eb193_0%,#0d5c4f_100%)]"
                      style={{ height: `${getBarWidth(point.value, timelinePeak)}%` }}
                    />
                  </div>
                  <div className="text-center">
                    <div className="text-xs font-semibold text-ink">{formatMetricValue(view.primaryMetric, point.value)}</div>
                    <div className="text-[11px] text-ink-soft">
                      {point.date ? formatDate(point.date) : `Update ${index + 1}`}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="rounded-[16px] border border-dashed border-line bg-white px-4 py-8 text-center text-sm text-ink-soft">
              No report points yet.
            </div>
          )}
        </div>

        <div className="rounded-[22px] border border-line bg-slate-50/70 p-5">
          <div className="mb-4 flex items-center justify-between gap-3">
            <p className="text-sm font-semibold text-ink">Channel split</p>
            <span className="text-xs font-semibold uppercase tracking-[0.14em] text-ink-soft">Live vs booked</span>
          </div>
          {view.channels.length > 0 ? (
            <div className="space-y-4">
              {view.channels.map((channel) => (
                <div key={channel.channel} className="rounded-[16px] border border-line bg-white px-4 py-4">
                  <div className="mb-3 flex items-start justify-between gap-3">
                    <div>
                      <div className="text-sm font-semibold text-ink">{channel.label}</div>
                      <div className="mt-1 text-xs text-ink-soft">
                        {channel.impressions > 0
                          ? `${channel.impressions.toLocaleString('en-ZA')} impressions`
                          : `${channel.playsOrSpots.toLocaleString('en-ZA')} ${channel.activityLabel.toLowerCase()}`}
                      </div>
                    </div>
                    <span className={`rounded-full border px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.12em] ${getChannelStatusTone(channel.status)}`}>
                      {channel.status.replace(/_/g, ' ')}
                    </span>
                  </div>

                  <div className="space-y-2">
                    <div>
                      <div className="mb-1 flex items-center justify-between text-xs text-ink-soft">
                        <span>Delivered</span>
                        <span>{formatCurrency(channel.deliveredSpend)}</span>
                      </div>
                      <div className="h-2 rounded-full bg-slate-100">
                        <div
                          className="h-2 rounded-full bg-brand"
                          style={{ width: `${getBarWidth(channel.deliveredSpend, channelPeak)}%` }}
                        />
                      </div>
                    </div>
                    <div>
                      <div className="mb-1 flex items-center justify-between text-xs text-ink-soft">
                        <span>Booked</span>
                        <span>{formatCurrency(channel.bookedSpend)}</span>
                      </div>
                      <div className="h-2 rounded-full bg-slate-100">
                        <div
                          className="h-2 rounded-full bg-slate-400"
                          style={{ width: `${getBarWidth(channel.bookedSpend, channelPeak)}%` }}
                        />
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="rounded-[16px] border border-dashed border-line bg-white px-4 py-8 text-center text-sm text-ink-soft">
              No channel performance yet.
            </div>
          )}
        </div>
      </div>
    </section>
  );
}

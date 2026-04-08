import { formatCurrency } from '../../../lib/utils';

type AdMetricsSummary = {
  variantCount: number;
  publishedVariantCount: number;
  impressions: number;
  clicks: number;
  conversions: number;
  costZar: number;
  ctr: number;
  conversionRate: number;
  cplZar?: number | null;
  roas?: number | null;
  topVariantId?: string | null;
  topVariantConversionRate?: number | null;
  lastRecordedAt?: string | null;
};

type AdVariantPerformanceItem = {
  id: string;
  platform: string;
  channel: string;
  language: string;
  status: string;
  impressions?: number;
  clicks?: number;
  conversions?: number;
  costZar?: number;
  ctr?: number;
  conversionRate?: number;
  cplZar?: number | null;
  roas?: number | null;
};

interface CreativeStudioPerformancePanelProps {
  title: string;
  subtitle: string;
  summary?: AdMetricsSummary;
  variants: AdVariantPerformanceItem[];
  isLoading?: boolean;
  isSyncing?: boolean;
  onSyncMetrics?: () => void;
}

function formatPercent(value: number | undefined): string {
  if (!value || value <= 0) {
    return "0.0%";
  }

  return `${(value * 100).toFixed(1)}%`;
}

function formatDecimal(value: number | undefined | null, suffix = ""): string {
  if (typeof value !== "number" || Number.isNaN(value)) {
    return "-";
  }

  return `${value.toFixed(2)}${suffix}`;
}

function formatNumber(value: number | undefined): string {
  return (value ?? 0).toLocaleString("en-ZA");
}

function formatUpdatedAt(value?: string | null): string {
  if (!value) {
    return "No synced metrics yet";
  }

  return `Updated ${new Date(value).toLocaleString("en-ZA")}`;
}

export function CreativeStudioPerformancePanel({
  title,
  subtitle,
  summary,
  variants,
  isLoading = false,
  isSyncing = false,
  onSyncMetrics,
}: CreativeStudioPerformancePanelProps) {
  const cards = [
    { label: "Spend", value: formatCurrency(summary?.costZar ?? 0) },
    { label: "Reach", value: formatNumber(summary?.impressions) },
    { label: "Leads", value: formatNumber(summary?.conversions) },
    {
      label: "CPL",
      value: summary?.cplZar != null ? formatCurrency(summary.cplZar) : "-",
    },
    { label: "CTR", value: formatPercent(summary?.ctr) },
    { label: "ROAS", value: formatDecimal(summary?.roas, "x") },
  ];

  return (
    <section className="rounded-[30px] border border-line bg-white p-7 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h3 className="text-xl font-semibold text-ink">{title}</h3>
          <p className="mt-2 text-sm leading-7 text-ink-soft">{subtitle}</p>
          <p className="mt-2 text-xs text-ink-soft">{formatUpdatedAt(summary?.lastRecordedAt)}</p>
        </div>
        {onSyncMetrics ? (
          <button
            type="button"
            onClick={onSyncMetrics}
            disabled={isSyncing}
            className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSyncing ? "Syncing..." : "Sync latest metrics"}
          </button>
        ) : null}
      </div>

      <div className="mt-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        {cards.map((card) => (
          <div key={card.label} className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">{card.label}</p>
            <p className="mt-2 text-xl font-semibold text-ink">{card.value}</p>
          </div>
        ))}
      </div>

      <div className="mt-6 overflow-x-auto">
        <table className="min-w-full divide-y divide-line">
          <thead>
            <tr className="text-left text-xs font-semibold uppercase tracking-[0.14em] text-ink-soft">
              <th className="px-3 py-2">Variant</th>
              <th className="px-3 py-2">Spend</th>
              <th className="px-3 py-2">Reach</th>
              <th className="px-3 py-2">Leads</th>
              <th className="px-3 py-2">CPL</th>
              <th className="px-3 py-2">CTR</th>
              <th className="px-3 py-2">ROAS</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line text-sm text-ink">
            {isLoading ? (
              <tr>
                <td colSpan={7} className="px-3 py-4 text-ink-soft">Loading metrics...</td>
              </tr>
            ) : variants.length === 0 ? (
              <tr>
                <td colSpan={7} className="px-3 py-4 text-ink-soft">No ad variants yet.</td>
              </tr>
            ) : (
              variants.map((variant) => (
                <tr key={variant.id}>
                  <td className="px-3 py-3">
                    <div className="font-semibold text-ink">{variant.platform} | {variant.channel}</div>
                    <div className="text-xs text-ink-soft">{variant.language} | {variant.status}</div>
                  </td>
                  <td className="px-3 py-3">{formatCurrency(variant.costZar ?? 0)}</td>
                  <td className="px-3 py-3">{formatNumber(variant.impressions)}</td>
                  <td className="px-3 py-3">{formatNumber(variant.conversions)}</td>
                  <td className="px-3 py-3">{variant.cplZar != null ? formatCurrency(variant.cplZar) : "-"}</td>
                  <td className="px-3 py-3">{formatPercent(variant.ctr)}</td>
                  <td className="px-3 py-3">{formatDecimal(variant.roas, "x")}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}

import { CheckCircle2, TrendingUp } from 'lucide-react';
import type { ReactNode } from 'react';
import { formatCurrency } from '../../../lib/utils';
import type { PackageBand, PackagePreview } from '../../../types/domain';

export function SpendPreviewPanel({
  band,
  selectedSpend,
  livePreview,
}: {
  band: PackageBand;
  selectedSpend: number;
  livePreview?: PackagePreview;
}) {
  const tierToneClass = getTierToneClass(getTierForSpend(band, selectedSpend));

  return (
    <div className="panel flex flex-col gap-5 px-6 py-6">
      <div className={`spend-preview-hero ${tierToneClass} rounded-[24px] px-5 py-5`}>
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Your package</p>
            <p className="mt-3 text-2xl font-semibold tracking-tight text-ink">{band.name}</p>
            <div className="mt-3">
              <span className={`spend-tier-badge ${tierToneClass}`}>{livePreview?.tierLabel ?? `${band.name} package`}</span>
            </div>
          </div>
          <div className="text-right">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Selected spend</p>
            <p className="mt-2 text-3xl font-semibold tracking-tight text-ink">{formatCurrency(selectedSpend)}</p>
          </div>
        </div>
      </div>

      {livePreview ? (
        <>
          <Section title="Channel access" icon={<CheckCircle2 className="size-4 text-brand" />}>
            <div className="space-y-3">
              <ChannelRule label="Include radio" value={band.includeRadio} />
              <ChannelRule label="Include TV" value={band.includeTv} />
            </div>
          </Section>

          <Section title="What you get" icon={<CheckCircle2 className="size-4 text-brand" />} defaultOpen>
            <List items={livePreview.mediaMix.length > 0 ? livePreview.mediaMix : livePreview.typicalInclusions} />
          </Section>

          <Section title={`Example media in ${livePreview.selectedArea}`} icon={<CheckCircle2 className="size-4 text-brand" />}>
            <div className="space-y-4">
              <MediaGroup title="Outdoor" items={livePreview.exampleLocations} />
              {livePreview.radioSupportExamples.length > 0 ? <MediaGroup title="Radio" items={livePreview.radioSupportExamples} /> : null}
              {livePreview.tvSupportExamples.length > 0 ? <MediaGroup title="TV" items={livePreview.tvSupportExamples} /> : null}
            </div>
          </Section>

          <Section title="Estimated reach" icon={<TrendingUp className="size-4 text-brand" />}>
            <p className="text-sm font-semibold text-ink">{livePreview.reachEstimate}</p>
          </Section>
        </>
      ) : (
        <Section title="What you get" icon={<CheckCircle2 className="size-4 text-brand" />} defaultOpen>
          <p className="text-sm leading-7 text-ink-soft">Loading package preview...</p>
        </Section>
      )}

      <div className="rounded-[22px] border border-dashed border-brand/20 bg-white px-4 py-4 text-sm leading-7 text-ink-soft">
        <span className="font-semibold text-ink">Note</span>
        <span className="ml-2">{livePreview?.note ?? 'Examples shown are indicative. Final media selection depends on your campaign brief, timing, and availability.'}</span>
      </div>
    </div>
  );
}

function getTierToneClass(tier: 'entry' | 'mid' | 'premium') {
  if (tier === 'premium') {
    return 'spend-tier-premium';
  }

  if (tier === 'mid') {
    return 'spend-tier-mid';
  }

  return 'spend-tier-entry';
}

function getTierForSpend(band: PackageBand, selectedSpend: number): 'entry' | 'mid' | 'premium' {
  const span = band.maxBudget - band.minBudget;
  if (span <= 0) {
    return 'entry';
  }

  const ratio = (selectedSpend - band.minBudget) / span;
  if (ratio < 0.34) {
    return 'entry';
  }

  if (ratio < 0.72) {
    return 'mid';
  }

  return 'premium';
}

function Section({
  title,
  icon,
  children,
  defaultOpen = false,
}: {
  title: string;
  icon: ReactNode;
  children: ReactNode;
  defaultOpen?: boolean;
}) {
  return (
    <>
      <details className="preview-accordion md:hidden" open={defaultOpen}>
        <summary className="preview-accordion-summary">
          <span className="flex items-center gap-2">
            {icon}
            <span className="text-sm font-semibold text-ink">{title}</span>
          </span>
        </summary>
        <div className="mt-3">{children}</div>
      </details>

      <section className="hidden rounded-[22px] border border-line bg-white px-4 py-4 md:block">
        <div className="flex items-center gap-2">
          {icon}
          <p className="text-sm font-semibold text-ink">{title}</p>
        </div>
        <div className="mt-3">{children}</div>
      </section>
    </>
  );
}

function List({ items }: { items: string[] }) {
  return (
    <ul className="space-y-2">
      {items.map((item) => (
        <li key={item} className="flex items-start gap-3 text-sm leading-7 text-ink-soft">
          <span className="mt-2 size-1.5 shrink-0 rounded-full bg-brand" />
          <span>{item}</span>
        </li>
      ))}
    </ul>
  );
}

function MediaGroup({ title, items }: { title: string; items: string[] }) {
  return (
    <div>
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-brand">{title}</p>
      <div className="mt-2">
        <List items={items} />
      </div>
    </div>
  );
}

function ChannelRule({
  label,
  value,
}: {
  label: string;
  value: 'yes' | 'optional' | 'no';
}) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-2xl bg-slate-50 px-3 py-3">
      <span className="text-sm text-ink-soft">{label}</span>
      <span className={`rounded-full px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.14em] ${getRuleTone(value)}`}>
        {value}
      </span>
    </div>
  );
}

function getRuleTone(value: 'yes' | 'optional' | 'no') {
  if (value === 'yes') {
    return 'bg-emerald-100 text-emerald-700';
  }

  if (value === 'optional') {
    return 'bg-amber-100 text-amber-700';
  }

  return 'bg-rose-100 text-rose-700';
}

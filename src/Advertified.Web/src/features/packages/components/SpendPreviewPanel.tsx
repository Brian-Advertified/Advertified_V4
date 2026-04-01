import { CheckCircle2, TrendingUp } from 'lucide-react';
import { lazy, Suspense, type ReactNode } from 'react';
import { formatCurrency } from '../../../lib/utils';
import type { PackageBand, PackagePreview } from '../../../types/domain';

const OutdoorPreviewMap = lazy(async () => import('./OutdoorPreviewMap').then((module) => ({ default: module.OutdoorPreviewMap })));

export function SpendPreviewPanel({
  band,
  selectedSpend,
  livePreview,
  mapAnchorRef,
}: {
  band: PackageBand;
  selectedSpend: number;
  livePreview?: PackagePreview;
  mapAnchorRef?: React.RefObject<HTMLDivElement | null>;
}) {
  const tierToneClass = getTierToneClass(getTierForSpend(band, selectedSpend));

  return (
    <div className="panel flex flex-col gap-5 px-6 py-6">
      <div className={`spend-preview-hero ${tierToneClass} rounded-[24px] px-5 py-5`}>
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Selected spend</p>
        <p className="mt-2 text-3xl font-semibold tracking-tight text-ink">{formatCurrency(selectedSpend)}</p>
      </div>

      {livePreview ? (
        <>
          <Section title="Includes" icon={<CheckCircle2 className="size-4 text-brand" />} defaultOpen>
            <div className="flex flex-wrap gap-2">
              {getPackageInclusions(band).map((item) => (
                <span
                  key={item}
                  className="rounded-full bg-slate-100 px-3 py-2 text-xs font-semibold text-ink-soft"
                >
                  {item}
                </span>
              ))}
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

          <div ref={mapAnchorRef} />
          <Suspense
            fallback={(
              <section className="rounded-[22px] border border-line bg-white px-4 py-4">
                <p className="text-sm font-semibold text-ink">Loading interactive map...</p>
                <p className="mt-2 text-sm text-ink-soft">Preparing the outdoor inventory view for this package area.</p>
              </section>
            )}
          >
            <OutdoorPreviewMap points={livePreview.outdoorMapPoints} />
          </Suspense>

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

function getPackageInclusions(band: PackageBand) {
  const inclusions = ['Billboards and digital screens'];

  if (band.includeRadio !== 'no') {
    inclusions.push(band.includeRadio === 'optional' ? 'Radio support' : 'Radio');
  }

  if (band.includeTv !== 'no') {
    inclusions.push('TV');
  }

  if (!isLaunchBand(band)) {
    inclusions.push('Advertified Studio');
  }

  return inclusions;
}

function isLaunchBand(band: PackageBand) {
  return band.code.toLowerCase() === 'launch' || band.name.toLowerCase().includes('launch');
}

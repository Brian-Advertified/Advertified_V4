import { ArrowRight } from 'lucide-react';
import { formatCompactBudget } from '../../../lib/utils';
import type { PackageBand } from '../../../types/domain';
import { cn } from '../../../lib/utils';

export function PackageCard({
  band,
  selected,
  onSelect,
}: {
  band: PackageBand;
  selected?: boolean;
  onSelect?: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={cn('package-card panel flex h-full flex-col gap-5 px-6 py-6 text-left transition duration-200 hover:-translate-y-1', selected && 'package-card-selected')}
    >
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.22em] text-brand">{band.name}</p>
          <p className="mt-3 whitespace-nowrap text-3xl font-semibold tracking-tight text-ink">{formatCompactBudget(band.minBudget)} - {formatCompactBudget(band.maxBudget)}</p>
        </div>
        {band.isRecommended ? <div className="package-card-flag">Most popular</div> : null}
      </div>

      <p className="text-sm leading-7 text-ink-soft">{band.quickBenefit}</p>

      <div className="rounded-[20px] border border-line bg-white/80 px-4 py-4">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Includes</p>
        <div className="mt-3 flex flex-wrap gap-2">
          {getPackageInclusions(band).map((item) => (
            <span
              key={item}
              className="rounded-full bg-slate-100 px-3 py-1.5 text-xs font-semibold text-ink-soft"
            >
              {item}
            </span>
          ))}
        </div>
      </div>

      <div className="rounded-[20px] border border-brand/20 bg-brand/[0.06] px-4 py-4">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">AI Studio included</p>
        <div className="mt-3 flex flex-wrap gap-2">
          {getAiStudioCapabilities(band).map((item) => (
            <span
              key={item}
              className="rounded-full border border-brand/25 bg-white px-3 py-1.5 text-xs font-semibold text-brand"
            >
              {item}
            </span>
          ))}
        </div>
      </div>

      <div className="mt-auto flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-brand">
        <ArrowRight className="size-4 text-highlight" />
        Choose package
      </div>
    </button>
  );
}

function getPackageInclusions(band: PackageBand) {
  const inclusions = ['Billboards and Digital Screens'];

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

function getAiStudioCapabilities(band: PackageBand) {
  const capabilities: string[] = [];
  const variantsLabel = band.maxAdVariants === 1 ? '1 ad variant' : `${band.maxAdVariants} ad variants`;
  capabilities.push(variantsLabel);

  if (band.allowedAdPlatforms.length > 0) {
    capabilities.push(`Platforms: ${band.allowedAdPlatforms.join(', ')}`);
  }

  if (band.allowedVoicePackTiers.length > 0) {
    capabilities.push(`Voice tiers: ${band.allowedVoicePackTiers.join(', ')}`);
  }

  capabilities.push(band.allowAdMetricsSync ? 'Metrics sync' : 'No metrics sync');
  capabilities.push(band.allowAdAutoOptimize ? 'Auto optimize' : 'Manual optimize');

  const regenerationsLabel = band.maxAdRegenerations === 1
    ? '1 regeneration'
    : `${band.maxAdRegenerations} regenerations`;
  capabilities.push(regenerationsLabel);

  return capabilities;
}


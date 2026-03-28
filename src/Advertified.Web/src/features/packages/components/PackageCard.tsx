import { ArrowRight } from 'lucide-react';
import { formatCurrency } from '../../../lib/utils';
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
          <p className="mt-3 text-3xl font-semibold tracking-tight text-ink">{formatCurrency(band.minBudget)} - {formatCurrency(band.maxBudget)}</p>
        </div>
        <div className="flex flex-col items-end gap-2">
          {band.isRecommended ? <div className="package-card-flag">Most popular</div> : null}
          <div className="package-chip rounded-2xl px-3 py-2 text-xs font-semibold">{band.leadTime}</div>
        </div>
      </div>

      <p className="text-sm leading-7 text-ink-soft">{band.quickBenefit}</p>

      <div className="rounded-[20px] border border-line bg-white/80 px-4 py-4">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Channel access</p>
        <div className="mt-3 grid gap-2 text-sm text-ink-soft">
          <ChannelRule label="Include radio" value={band.includeRadio} />
          <ChannelRule label="Include TV" value={band.includeTv} />
        </div>
      </div>

      <div className="mt-auto flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-brand">
        <ArrowRight className="size-4 text-highlight" />
        Choose package
      </div>
    </button>
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
    <div className="flex items-center justify-between gap-3">
      <span>{label}</span>
      <span className={cn('rounded-full px-2.5 py-1 text-xs font-semibold uppercase tracking-[0.12em]', getRuleTone(value))}>
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

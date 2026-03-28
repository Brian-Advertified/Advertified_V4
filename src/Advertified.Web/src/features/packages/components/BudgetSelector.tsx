import { formatCurrency } from '../../../lib/utils';
import type { PackageBand, PackagePreview } from '../../../types/domain';

export function BudgetSelector({
  band,
  value,
  preview,
  selectedArea,
  onAreaChange,
  onChange,
}: {
  band: PackageBand;
  value: number;
  preview?: PackagePreview;
  selectedArea: string;
  onAreaChange: (value: string) => void;
  onChange: (value: number) => void;
}) {
  return (
    <div className="panel flex flex-col gap-5 px-6 py-6">
      <div>
        <p className="text-sm font-semibold text-ink">Choose your spend and preview your package</p>
        <p className="mt-2 text-sm leading-7 text-ink-soft">
          Select the budget you want to invest in this package band. We&apos;ll use it after purchase to build your campaign recommendation, media mix, and coverage plan.
        </p>
        {band.recommendedSpend ? <p className="mt-3 text-sm font-semibold text-brand">Most clients in this package spend around {formatCurrency(band.recommendedSpend)}.</p> : null}
      </div>

      <input
        type="range"
        min={band.minBudget}
        max={band.maxBudget}
        step={1_000}
        value={value}
        onChange={(event) => onChange(Number(event.target.value))}
        className="h-2 w-full cursor-pointer appearance-none rounded-full bg-brand-soft accent-brand"
      />

      <div className="grid gap-4 md:grid-cols-[1fr_auto_auto] md:items-end">
        <label>
          <span className="label-base">Campaign area</span>
          <select className="input-base" value={selectedArea} onChange={(event) => onAreaChange(event.target.value)}>
            <option value="gauteng">Gauteng</option>
            <option value="western-cape">Western Cape</option>
            <option value="eastern-cape">Eastern Cape</option>
            <option value="national">National</option>
          </select>
        </label>
      </div>

      <div className="grid gap-4 md:grid-cols-[1fr_auto_auto] md:items-end">
        <label>
          <span className="label-base">Spend amount</span>
          <input
            type="number"
            className="input-base"
            min={band.minBudget}
            max={band.maxBudget}
            step={1_000}
            value={value}
            onChange={(event) => onChange(Number(event.target.value))}
          />
        </label>
        <div className="budget-stat rounded-2xl px-4 py-3 text-sm text-ink-soft">
          Min {formatCurrency(band.minBudget)}
        </div>
        <div className="budget-stat rounded-2xl px-4 py-3 text-sm text-ink-soft">
          Max {formatCurrency(band.maxBudget)}
        </div>
      </div>

      <div className="package-preview-tab">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="package-preview-tab-label">Selected package</p>
            <p className="mt-2 text-lg font-semibold tracking-tight text-ink">{preview?.tierLabel ?? `${band.name} package`}</p>
            <p className="mt-2 text-sm text-ink-soft">Example media in {preview?.selectedArea ?? 'Gauteng'}</p>
          </div>
          <div className="package-chip rounded-full px-3 py-2 text-[11px] font-semibold uppercase tracking-[0.18em]">
            {band.name}
          </div>
        </div>

        <div className="mt-5 space-y-4">
          <div>
            <p className="text-sm font-semibold text-ink">What you typically get</p>
            <p className="mt-2 text-sm leading-7 text-ink-soft">{preview?.packagePurpose ?? band.packagePurpose}</p>
          </div>

          <div>
            <p className="text-sm font-semibold text-ink">Indicative mix</p>
            <ul className="mt-2 space-y-2">
              {(preview?.typicalInclusions ?? []).map((item) => (
                <li key={item} className="package-preview-list-item">
                  <span className="package-preview-bullet" />
                  <span>{item}</span>
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-[18px] border border-brand/10 bg-white/80 px-4 py-3 text-sm leading-7 text-ink-soft">
            {(preview?.indicativeMix ?? []).join(' • ')}
          </div>
        </div>
      </div>

      <p className="helper-text">
        Inventory shown here is indicative. Final media selection depends on your brief, market, timing, and availability.
      </p>
    </div>
  );
}

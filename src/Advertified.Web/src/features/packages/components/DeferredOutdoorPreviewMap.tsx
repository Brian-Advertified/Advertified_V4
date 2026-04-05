import { MapPinned } from 'lucide-react';
import { useMemo } from 'react';
import type { PackagePreviewMapPoint } from '../../../types/domain';

export function DeferredOutdoorPreviewMap({ points }: { points: PackagePreviewMapPoint[] }) {
  const selectedAreaCount = useMemo(
    () => points.filter((point) => point.isInSelectedArea).length,
    [points],
  );
  const sampleLabels = useMemo(
    () => points.slice(0, 3).map((point) => point.siteName),
    [points],
  );
  const previewPins = useMemo(() => buildPreviewPins(points), [points]);

  return (
    <section className="overflow-hidden rounded-[22px] border border-line bg-white">
      <div className="border-b border-line px-4 py-3">
        <p className="text-sm font-semibold text-ink">Billboards and Digital Screens near this campaign area</p>
        <p className="mt-1 text-xs leading-6 text-ink-soft">
          This lightweight preview shows the inventory spread without loading a heavy interactive map on first view.
        </p>
      </div>
      <div className="grid gap-5 bg-[radial-gradient(circle_at_top,#f8fafc_0%,#eef2f7_60%,#e2e8f0_100%)] px-6 py-6 lg:grid-cols-[minmax(0,1.4fr)_minmax(280px,0.9fr)]">
        <div className="rounded-[24px] border border-white/80 bg-white/65 p-4 shadow-[0_18px_40px_rgba(15,23,42,0.06)] backdrop-blur">
          <div className="flex items-start justify-between gap-3">
            <div>
              <p className="text-sm font-semibold text-ink">Static inventory footprint</p>
              <p className="mt-1 text-xs leading-6 text-ink-soft">
                Pins are positioned relative to supplier coordinates to show the approximate spread for this package.
              </p>
            </div>
            <span className="flex size-10 items-center justify-center rounded-full bg-brand-soft text-brand">
              <MapPinned className="size-5" />
            </span>
          </div>
          <div className="relative mt-4 h-[260px] overflow-hidden rounded-[22px] border border-line bg-[linear-gradient(180deg,rgba(255,255,255,0.92)_0%,rgba(241,245,249,0.96)_100%)]">
            <div className="pointer-events-none absolute inset-[14px] rounded-[18px] border border-dashed border-brand/20" />
            <div className="pointer-events-none absolute left-[12%] top-[18%] h-[90px] w-[140px] rounded-full bg-brand/6 blur-2xl" />
            <div className="pointer-events-none absolute bottom-[18%] right-[10%] h-[100px] w-[160px] rounded-full bg-sky-200/35 blur-2xl" />
            <div className="pointer-events-none absolute inset-x-0 top-1/2 h-px -translate-y-1/2 bg-line/60" />
            <div className="pointer-events-none absolute inset-y-0 left-1/2 w-px -translate-x-1/2 bg-line/60" />
            {previewPins.length > 0 ? (
              previewPins.map((point) => (
                <div
                  key={point.key}
                  className="group absolute -translate-x-1/2 -translate-y-1/2"
                  style={{ left: `${point.x}%`, top: `${point.y}%` }}
                >
                  <span
                    className={[
                      'flex size-4 items-center justify-center rounded-full border-2 shadow-[0_0_0_5px_rgba(20,184,166,0.12)] transition-transform duration-200 group-hover:scale-110',
                      point.isInSelectedArea
                        ? 'border-[#0f766e] bg-[#14b8a6]'
                        : 'border-slate-400 bg-white',
                    ].join(' ')}
                    title={`${point.siteName} · ${point.label}`}
                  />
                </div>
              ))
            ) : (
              <div className="flex h-full flex-col items-center justify-center px-6 text-center">
                <p className="text-sm font-semibold text-ink">No mapped inventory points yet</p>
                <p className="mt-2 max-w-sm text-sm leading-6 text-ink-soft">
                  Supplier coordinates will appear here once the preview has outdoor inventory for the selected package area.
                </p>
              </div>
            )}
          </div>
          <div className="mt-4 flex flex-wrap gap-2">
            <span className="rounded-full border border-line bg-white px-3 py-2 text-xs font-semibold text-ink-soft">
              {points.length} mapped sites
            </span>
            <span className="rounded-full border border-line bg-white px-3 py-2 text-xs font-semibold text-ink-soft">
              {selectedAreaCount} in selected area
            </span>
            <span className="rounded-full border border-line bg-white px-3 py-2 text-xs font-semibold text-ink-soft">
              Static preview only
            </span>
          </div>
        </div>

        <div className="space-y-4">
          <div className="rounded-[22px] border border-line bg-white/80 px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">How to read it</p>
            <div className="mt-3 space-y-3 text-sm leading-6 text-ink-soft">
              <div className="flex items-center gap-3">
                <span className="flex size-4 shrink-0 rounded-full border-2 border-[#0f766e] bg-[#14b8a6]" />
                <span>Recommended for the selected campaign area</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="flex size-4 shrink-0 rounded-full border-2 border-slate-400 bg-white" />
                <span>Additional mapped inventory nearby</span>
              </div>
            </div>
          </div>
          {sampleLabels.length > 0 ? (
            <div className="rounded-[22px] border border-line bg-white/80 px-4 py-4">
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Sample locations</p>
              <div className="mt-3 space-y-2">
                {sampleLabels.map((label) => (
                  <p key={label} className="text-sm text-ink-soft">{label}</p>
                ))}
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </section>
  );
}

function buildPreviewPins(points: PackagePreviewMapPoint[]) {
  if (points.length === 0) {
    return [];
  }

  const latitudes = points.map((point) => point.latitude);
  const longitudes = points.map((point) => point.longitude);
  const minLatitude = Math.min(...latitudes);
  const maxLatitude = Math.max(...latitudes);
  const minLongitude = Math.min(...longitudes);
  const maxLongitude = Math.max(...longitudes);
  const latitudeSpan = Math.max(maxLatitude - minLatitude, 0.01);
  const longitudeSpan = Math.max(maxLongitude - minLongitude, 0.01);

  return points.map((point) => ({
    key: `${point.siteName}-${point.latitude}-${point.longitude}`,
    siteName: point.siteName,
    label: point.label,
    isInSelectedArea: point.isInSelectedArea,
    x: 12 + (((point.longitude - minLongitude) / longitudeSpan) * 76),
    y: 14 + ((1 - ((point.latitude - minLatitude) / latitudeSpan)) * 72),
  }));
}

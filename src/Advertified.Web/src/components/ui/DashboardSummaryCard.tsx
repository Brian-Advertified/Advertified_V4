import type { ReactNode } from 'react';

export function DashboardSummaryCard({
  label,
  value,
  helper,
  icon,
}: {
  label: string;
  value: string;
  helper: string;
  icon: ReactNode;
}) {
  return (
    <div className="panel flex flex-col gap-4 px-6 py-6">
      <div className="flex items-center justify-between">
        <span className="text-sm font-semibold text-ink-soft">{label}</span>
        <div className="rounded-2xl bg-brand-soft p-3 text-brand">{icon}</div>
      </div>
      <div>
        <p className="text-3xl font-semibold tracking-tight text-ink">{value}</p>
        <p className="mt-2 text-sm leading-6 text-ink-soft">{helper}</p>
      </div>
    </div>
  );
}

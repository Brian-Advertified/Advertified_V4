import { CheckCircle2, Circle, Clock3 } from 'lucide-react';
import type { CampaignTimelineStep } from '../../../types/domain';

type CampaignTimelineProps = {
  steps: CampaignTimelineStep[];
};

function TimelineIcon({ state }: { state: CampaignTimelineStep['state'] }) {
  if (state === 'complete') {
    return <CheckCircle2 className="size-5 text-emerald-600" />;
  }

  if (state === 'current') {
    return <Clock3 className="size-5 text-brand" />;
  }

  return <Circle className="size-5 text-slate-300" />;
}

export function CampaignTimeline({ steps }: CampaignTimelineProps) {
  if (steps.length === 0) {
    return null;
  }

  return (
    <div className="panel px-6 py-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Campaign progress</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">
            Follow each milestone from payment through review and approval.
          </p>
        </div>
      </div>
      <div className="mt-6 grid gap-4 md:grid-cols-5">
        {steps.map((step, index) => (
          <div
            key={step.key}
            className={`rounded-[18px] border px-4 py-4 ${
              step.state === 'complete'
                ? 'border-emerald-200 bg-emerald-50/80'
                : step.state === 'current'
                  ? 'border-brand/20 bg-brand-soft/40'
                  : 'border-line bg-slate-50'
            }`}
          >
            <div className="flex items-center gap-3">
              <TimelineIcon state={step.state} />
              <span className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Step {index + 1}
              </span>
            </div>
            <p className="mt-4 text-sm font-semibold text-ink">{step.label}</p>
            <p className="mt-2 text-sm leading-6 text-ink-soft">{step.description}</p>
          </div>
        ))}
      </div>
    </div>
  );
}

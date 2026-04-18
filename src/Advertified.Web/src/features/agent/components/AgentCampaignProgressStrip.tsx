import type { CampaignTimelineStep } from '../../../types/domain';

type AgentCampaignProgressStripProps = {
  timeline?: CampaignTimelineStep[];
};

export function AgentCampaignProgressStrip({ timeline }: AgentCampaignProgressStripProps) {
  const steps = (timeline ?? []).filter((step) => step.label.trim().length > 0);
  if (steps.length === 0) {
    return null;
  }

  return (
    <div className="rounded-[24px] border border-line bg-slate-50/90 px-4 py-4">
      <div className="flex flex-wrap items-center gap-3">
        {steps.map((step, index) => {
          const isComplete = step.state === 'complete';
          const isCurrent = step.state === 'current';

          return (
            <div key={step.key} className="flex items-center gap-3">
              <div className="flex items-center gap-2 text-xs">
                <span
                  className={`size-2.5 rounded-full ${
                    isComplete
                      ? 'bg-emerald-600'
                      : isCurrent
                        ? 'bg-emerald-600 shadow-[0_0_0_4px_rgba(68,184,143,0.22)]'
                        : 'bg-slate-300'
                  }`}
                />
                <span className={isCurrent ? 'font-semibold text-emerald-700' : isComplete ? 'text-ink-soft' : 'text-slate-400'}>
                  {step.label}
                </span>
              </div>
              {index < steps.length - 1 ? <div className="h-px w-4 bg-line" aria-hidden="true" /> : null}
            </div>
          );
        })}
      </div>
    </div>
  );
}

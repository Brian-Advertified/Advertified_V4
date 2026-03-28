import { BrainCircuit, ConciergeBell, Layers3 } from 'lucide-react';
import { cn } from '../../../lib/utils';
import type { PlanningMode } from '../../../types/domain';

const options = [
  { value: 'ai_assisted', title: 'AI assisted', copy: 'Fast first-pass planning with clear rationale.', icon: BrainCircuit },
  { value: 'agent_assisted', title: 'Agent assisted', copy: 'Human-led recommendation refinement and packaging.', icon: ConciergeBell },
  { value: 'hybrid', title: 'Hybrid', copy: 'AI acceleration with agent oversight where it matters.', icon: Layers3 },
] as const;

export function PlanningModeSelector({
  value,
  onChange,
}: {
  value?: PlanningMode;
  onChange: (value: PlanningMode) => void;
}) {
  return (
    <div className="grid gap-4 md:grid-cols-3">
      {options.map((option) => {
        const Icon = option.icon;
        return (
          <button
            key={option.value}
            type="button"
            onClick={() => onChange(option.value)}
            className={cn(
              'panel flex flex-col items-start gap-4 px-5 py-5 text-left transition hover:-translate-y-1 hover:border-brand/30',
              value === option.value && 'border-brand bg-brand-soft/40 ring-4 ring-brand/10',
            )}
          >
            <div className="rounded-2xl bg-brand-soft p-3 text-brand"><Icon className="size-5" /></div>
            <div>
              <p className="text-lg font-semibold text-ink">{option.title}</p>
              <p className="mt-2 text-sm leading-7 text-ink-soft">{option.copy}</p>
            </div>
          </button>
        );
      })}
    </div>
  );
}

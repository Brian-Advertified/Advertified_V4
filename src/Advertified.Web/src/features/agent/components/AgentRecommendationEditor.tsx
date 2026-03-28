import { useState } from 'react';

export function AgentRecommendationEditor({
  initialValue,
  onSave,
  loading,
  selectedCount,
}: {
  initialValue?: string;
  onSave: (notes: string) => Promise<void>;
  loading: boolean;
  selectedCount: number;
}) {
  const [value, setValue] = useState(initialValue ?? '');

  return (
    <div className="panel px-6 py-6">
      <h3 className="text-xl font-semibold text-ink">Recommendation editor</h3>
      <p className="mt-2 text-sm leading-7 text-ink-soft">Draft the summary and rationale before sending the recommendation back to the client.</p>
      <p className="mt-2 text-sm leading-7 text-ink-soft">
        {selectedCount > 0 ? `${selectedCount} inventory item${selectedCount === 1 ? '' : 's'} selected for this plan.` : 'Select inventory rows below to build the plan mix.'}
      </p>
      <textarea
        value={value}
        onChange={(event) => setValue(event.target.value)}
        className="input-base mt-5 min-h-[180px]"
        placeholder="Summarise the recommended media approach, weighting, and why it fits the purchased package."
      />
      <div className="mt-5 flex justify-end">
        <button type="button" disabled={loading} onClick={() => onSave(value)} className="rounded-full bg-ink px-5 py-3 text-sm font-semibold text-white">
          {loading ? 'Saving...' : 'Save recommendation'}
        </button>
      </div>
    </div>
  );
}

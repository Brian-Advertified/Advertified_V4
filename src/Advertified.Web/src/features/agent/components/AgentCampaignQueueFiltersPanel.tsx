import { Filter, Layers3 } from 'lucide-react';
import type { CampaignOwnershipFilter, CampaignQueueFocusFilter, CampaignQueueStageFilter } from '../../../pages/agent/agentCampaignQueueFilters';
import { AgentSectionIntro } from '../../../pages/agent/agentSectionShared';

type QueueFilterChip<T extends string> = {
  id: T;
  label: string;
  count: number;
};

function FilterPillGroup<T extends string>({
  label,
  icon,
  value,
  items,
  onChange,
}: {
  label: string;
  icon: 'filter' | 'layers';
  value: T;
  items: QueueFilterChip<T>[];
  onChange: (value: T) => void;
}) {
  const Icon = icon === 'layers' ? Layers3 : Filter;

  return (
    <div className="rounded-[22px] border border-line bg-slate-50/70 p-4">
      <div className="flex items-center gap-2">
        <Icon className="size-4 text-brand" />
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">{label}</p>
      </div>
      <div className="mt-3 flex flex-wrap gap-2">
        {items.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => onChange(item.id)}
            className={`rounded-full border px-4 py-2 text-sm font-semibold transition ${
              value === item.id
                ? 'border-brand bg-brand text-white'
                : 'border-line bg-white text-ink-soft hover:border-brand/20 hover:text-ink'
            }`}
          >
            {item.label} ({item.count})
          </button>
        ))}
      </div>
    </div>
  );
}

export function AgentCampaignQueueFiltersPanel({
  stageFilter,
  focusFilter,
  ownershipFilter,
  queueTabs,
  focusTabs,
  ownershipTabs,
  visibleCampaignCount,
  onStageChange,
  onFocusChange,
  onOwnershipChange,
}: {
  stageFilter: CampaignQueueStageFilter;
  focusFilter: CampaignQueueFocusFilter;
  ownershipFilter: CampaignOwnershipFilter;
  queueTabs: QueueFilterChip<CampaignQueueStageFilter>[];
  focusTabs: QueueFilterChip<CampaignQueueFocusFilter>[];
  ownershipTabs: QueueFilterChip<CampaignOwnershipFilter>[];
  visibleCampaignCount: number;
  onStageChange: (value: CampaignQueueStageFilter) => void;
  onFocusChange: (value: CampaignQueueFocusFilter) => void;
  onOwnershipChange: (value: CampaignOwnershipFilter) => void;
}) {
  return (
    <div className="panel px-6 py-6">
      <AgentSectionIntro
        title="Campaign queue"
        description="Filter the live queue to the exact work slice you need, then move straight into the next action."
        action={(
          <div className="rounded-full border border-brand/20 bg-brand-soft px-4 py-2 text-sm font-semibold text-brand">
            Showing {visibleCampaignCount} campaign{visibleCampaignCount === 1 ? '' : 's'}
          </div>
        )}
      />

      <div className="mt-5 space-y-4">
        <FilterPillGroup label="Queue view" icon="layers" value={stageFilter} items={queueTabs} onChange={onStageChange} />
        <FilterPillGroup label="Action focus" icon="filter" value={focusFilter} items={focusTabs} onChange={onFocusChange} />
        <FilterPillGroup label="Ownership" icon="filter" value={ownershipFilter} items={ownershipTabs} onChange={onOwnershipChange} />
      </div>
    </div>
  );
}

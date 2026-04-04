import { Search, X } from 'lucide-react';
import { useDeferredValue, useMemo, useState } from 'react';
import type { InventoryRow } from '../../../types/domain';
import { InventoryTable } from './InventoryTable';

function matchesSearch(item: InventoryRow, query: string) {
  if (query.length < 3) {
    return true;
  }

  const haystack = [
    item.station,
    item.region,
    item.language,
    item.showDaypart,
    item.timeBand,
    item.slotType,
    item.duration,
    item.restrictions,
  ].join(' ').toLowerCase();

  return haystack.includes(query.toLowerCase());
}

export function AgentInventorySelectionModal({
  isOpen,
  items,
  selectedItemIds,
  canModifyPlan,
  onClose,
  onToggleItem,
  formatChannelLabel,
}: {
  isOpen: boolean;
  items: InventoryRow[];
  selectedItemIds: string[];
  canModifyPlan: boolean;
  onClose: () => void;
  onToggleItem: (item: InventoryRow) => void;
  formatChannelLabel: (value: string) => string;
}) {
  const [inventoryTypeFilter, setInventoryTypeFilter] = useState('all');
  const [inventoryRegionFilter, setInventoryRegionFilter] = useState('all');
  const [inventoryLanguageFilter, setInventoryLanguageFilter] = useState('all');
  const [inventorySearchInput, setInventorySearchInput] = useState('');
  const deferredInventorySearchInput = useDeferredValue(inventorySearchInput);

  const inventoryTypeOptions = useMemo(
    () => Array.from(new Set(items.map((item) => item.type))).sort(),
    [items],
  );
  const inventoryRegionOptions = useMemo(
    () => Array.from(new Set(items.map((item) => item.region).filter((value) => value?.trim()))).sort(),
    [items],
  );
  const inventoryLanguageOptions = useMemo(
    () => Array.from(new Set(items.map((item) => item.language).filter((value) => value?.trim()))).sort(),
    [items],
  );

  const inventorySearchQuery = deferredInventorySearchInput.trim().toLowerCase();
  const filteredInventoryItems = useMemo(() => items.filter((item) => {
    if (inventoryTypeFilter !== 'all' && item.type !== inventoryTypeFilter) {
      return false;
    }

    if (inventoryRegionFilter !== 'all' && item.region !== inventoryRegionFilter) {
      return false;
    }

    if (inventoryLanguageFilter !== 'all' && item.language !== inventoryLanguageFilter) {
      return false;
    }

    return matchesSearch(item, inventorySearchQuery);
  }), [inventoryLanguageFilter, inventoryRegionFilter, inventorySearchQuery, inventoryTypeFilter, items]);

  if (!isOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-[90] bg-slate-950/45 backdrop-blur-[1px]">
      <div className="mx-auto mt-8 flex h-[calc(100vh-4rem)] w-[min(1300px,95vw)] flex-col rounded-[24px] border border-line bg-white shadow-[0_25px_60px_rgba(15,23,42,0.22)]">
        <div className="flex items-center justify-between gap-3 border-b border-line px-6 py-4">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Matching inventory</p>
            <p className="mt-1 text-sm text-ink-soft">Search starts filtering from the 3rd keystroke.</p>
          </div>
          <button type="button" onClick={onClose} className="button-secondary inline-flex items-center gap-2 px-3 py-2">
            <X className="size-4" />
            Close
          </button>
        </div>

        <div className="grid gap-3 border-b border-line px-6 py-4 md:grid-cols-[1.3fr_0.8fr_0.8fr_0.8fr]">
          <label className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
            <input
              type="search"
              value={inventorySearchInput}
              onChange={(event) => setInventorySearchInput(event.target.value)}
              placeholder="Search station, region, language, daypart..."
              className="input-base pl-9"
            />
          </label>
          <select className="input-base" value={inventoryTypeFilter} onChange={(event) => setInventoryTypeFilter(event.target.value)}>
            <option value="all">All types</option>
            {inventoryTypeOptions.map((value) => <option key={value} value={value}>{formatChannelLabel(value)}</option>)}
          </select>
          <select className="input-base" value={inventoryRegionFilter} onChange={(event) => setInventoryRegionFilter(event.target.value)}>
            <option value="all">All regions</option>
            {inventoryRegionOptions.map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
          <select className="input-base" value={inventoryLanguageFilter} onChange={(event) => setInventoryLanguageFilter(event.target.value)}>
            <option value="all">All languages</option>
            {inventoryLanguageOptions.map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
        </div>

        <div className="flex items-center justify-between gap-3 px-6 py-3 text-xs text-ink-soft">
          <span>Showing {filteredInventoryItems.length} of {items.length} inventory row(s)</span>
          <span>{inventorySearchInput.trim().length > 0 && inventorySearchInput.trim().length < 3 ? 'Type at least 3 characters to start search filtering.' : ''}</span>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-6 pb-6 pr-3">
          <InventoryTable
            items={filteredInventoryItems}
            selectedItemIds={selectedItemIds}
            onToggleItem={canModifyPlan ? onToggleItem : undefined}
          />
        </div>
      </div>
    </div>
  );
}

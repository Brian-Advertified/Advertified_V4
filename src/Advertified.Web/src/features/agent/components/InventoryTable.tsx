import { formatCurrency } from '../../../lib/utils';
import type { InventoryRow } from '../../../types/domain';

export function InventoryTable({
  items,
  selectedItemIds,
  onToggleItem,
}: {
  items: InventoryRow[];
  selectedItemIds?: string[];
  onToggleItem?: (item: InventoryRow) => void;
}) {
  const selectedSet = new Set(selectedItemIds ?? []);
  const selectable = typeof onToggleItem === 'function';
  const displayValue = (value?: string) => value && value.trim().length > 0 ? value : 'Not specified';

  return (
    <div className="panel overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-[1200px] text-left text-sm">
          <thead className="bg-slate-50 text-ink-soft">
            <tr>
              {['Type', 'Station', 'Region', 'Language', 'Show / Daypart', 'Time band', 'Slot type', 'Duration', 'Rate', 'Restrictions', ...(selectable ? ['Action'] : [])].map((header) => (
                <th
                  key={header}
                  className={`px-4 py-4 font-semibold ${header === 'Action' ? 'pr-8 whitespace-nowrap' : ''}`}
                >
                  {header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} className={`border-t border-line align-top ${selectedSet.has(item.id) ? 'bg-brand-soft/40' : ''}`}>
                <td className="px-4 py-4 font-semibold capitalize text-ink">{item.type}</td>
                <td className="px-4 py-4 text-ink">{item.station}</td>
                <td className="px-4 py-4 text-ink-soft">{displayValue(item.region)}</td>
                <td className="px-4 py-4 text-ink-soft">{displayValue(item.language)}</td>
                <td className="px-4 py-4 text-ink-soft">{displayValue(item.showDaypart)}</td>
                <td className="px-4 py-4 text-ink-soft">{displayValue(item.timeBand)}</td>
                <td className="px-4 py-4 text-ink-soft">{displayValue(item.slotType)}</td>
                <td className="px-4 py-4 text-ink-soft">{displayValue(item.duration)}</td>
                <td className="px-4 py-4 font-semibold text-ink">{formatCurrency(item.rate)}</td>
                <td className="px-4 py-4 text-ink-soft">{displayValue(item.restrictions)}</td>
                {selectable ? (
                  <td className="px-4 py-4 pr-8 whitespace-nowrap">
                    <button
                      type="button"
                      onClick={() => onToggleItem(item)}
                      className={`whitespace-nowrap rounded-full px-4 py-2 text-xs font-semibold ${selectedSet.has(item.id) ? 'bg-ink text-white' : 'bg-brand text-white'}`}
                    >
                      {selectedSet.has(item.id) ? 'Remove from plan' : 'Add to plan'}
                    </button>
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

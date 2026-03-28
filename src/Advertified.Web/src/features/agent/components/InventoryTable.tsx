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

  return (
    <div className="panel overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full text-left text-sm">
          <thead className="bg-slate-50 text-ink-soft">
            <tr>
              {['Type', 'Station', 'Region', 'Language', 'Show / Daypart', 'Time band', 'Slot type', 'Duration', 'Rate', 'Restrictions', ...(selectable ? ['Action'] : [])].map((header) => (
                <th key={header} className="px-4 py-4 font-semibold">{header}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} className={`border-t border-line align-top ${selectedSet.has(item.id) ? 'bg-brand-soft/40' : ''}`}>
                <td className="px-4 py-4 font-semibold capitalize text-ink">{item.type}</td>
                <td className="px-4 py-4 text-ink">{item.station}</td>
                <td className="px-4 py-4 text-ink-soft">{item.region}</td>
                <td className="px-4 py-4 text-ink-soft">{item.language}</td>
                <td className="px-4 py-4 text-ink-soft">{item.showDaypart}</td>
                <td className="px-4 py-4 text-ink-soft">{item.timeBand}</td>
                <td className="px-4 py-4 text-ink-soft">{item.slotType}</td>
                <td className="px-4 py-4 text-ink-soft">{item.duration}</td>
                <td className="px-4 py-4 font-semibold text-ink">{formatCurrency(item.rate)}</td>
                <td className="px-4 py-4 text-ink-soft">{item.restrictions}</td>
                {selectable ? (
                  <td className="px-4 py-4">
                    <button
                      type="button"
                      onClick={() => onToggleItem(item)}
                      className={`rounded-full px-4 py-2 text-xs font-semibold ${selectedSet.has(item.id) ? 'bg-ink text-white' : 'bg-brand text-white'}`}
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

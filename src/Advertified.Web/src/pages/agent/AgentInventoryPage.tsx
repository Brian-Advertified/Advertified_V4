import { useQuery } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import { useMemo, useState } from 'react';
import { InventoryTable } from '../../features/agent/components/InventoryTable';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { agentInventoryFallbackEnabled } from '../../lib/featureFlags';
import { advertifiedApi } from '../../services/advertifiedApi';

export function AgentInventoryPage() {
  const [search, setSearch] = useState('');
  const [type, setType] = useState('all');
  const inventoryQuery = useQuery({
    queryKey: ['inventory'],
    queryFn: advertifiedApi.getInventory,
    enabled: agentInventoryFallbackEnabled,
  });

  if (!agentInventoryFallbackEnabled) {
    return (
      <section className="page-shell space-y-6">
        <div>
          <div className="pill bg-brand-soft text-brand">Agent inventory</div>
          <h1 className="mt-4 text-4xl font-semibold tracking-tight text-ink">Raw inventory access is temporarily hidden.</h1>
        </div>
        <EmptyState
          title="Inventory will return once the backend endpoint is ready"
          description="The temporary fallback dataset is now gated behind a dev-only flag, so production-style agent sessions no longer see placeholder supply rows."
        />
      </section>
    );
  }

  const items = useMemo(() => {
    return (inventoryQuery.data ?? []).filter((item) => {
      const matchesType = type === 'all' || item.type === type;
      const haystack = `${item.station} ${item.region} ${item.showDaypart} ${item.language}`.toLowerCase();
      const matchesSearch = haystack.includes(search.toLowerCase());
      return matchesType && matchesSearch;
    });
  }, [inventoryQuery.data, search, type]);

  if (inventoryQuery.isLoading) {
    return <LoadingState label="Loading inventory..." />;
  }

  return (
    <section className="page-shell space-y-6">
      <div>
        <div className="pill bg-brand-soft text-brand">Agent inventory</div>
        <h1 className="mt-4 text-4xl font-semibold tracking-tight text-ink">Raw inventory access for the operations team only.</h1>
      </div>
      <div className="panel flex flex-col gap-4 px-6 py-6 lg:flex-row lg:items-center">
        <label className="relative flex-1">
          <Search className="absolute left-4 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} className="input-base pl-11" placeholder="Search station, region, language..." />
        </label>
        <select value={type} onChange={(event) => setType(event.target.value)} className="input-base max-w-[220px]">
          <option value="all">All media types</option>
          <option value="radio">Radio</option>
          <option value="ooh">OOH</option>
          <option value="digital">Digital</option>
        </select>
      </div>
      <InventoryTable items={items} />
    </section>
  );
}

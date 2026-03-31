import { useQuery } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import { useMemo, useState } from 'react';
import { InventoryTable } from '../../features/agent/components/InventoryTable';
import { LoadingState } from '../../components/ui/LoadingState';
import { advertifiedApi } from '../../services/advertifiedApi';
import { PageHero } from '../../components/marketing/PageHero';

export function AgentInventoryPage() {
  const [search, setSearch] = useState('');
  const [type, setType] = useState('all');
  const inventoryQuery = useQuery({
    queryKey: ['inventory'],
    queryFn: () => advertifiedApi.getInventory(),
  });

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
      <PageHero
        kicker="Agent inventory"
        title="Raw inventory access for the operations team only."
        description="Search live supplier-backed inventory across channels so agents can inspect availability and build stronger recommendation mixes."
      />
      <div className="panel flex flex-col gap-4 px-6 py-6 lg:flex-row lg:items-center">
        <label className="relative flex-1">
          <Search className="absolute left-4 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} className="input-base pl-11" placeholder="Search station, region, language..." />
        </label>
        <select value={type} onChange={(event) => setType(event.target.value)} className="input-base max-w-[220px]">
          <option value="all">All media types</option>
          <option value="radio">Radio</option>
          <option value="ooh">Billboards and digital screens</option>
          <option value="tv">TV</option>
          <option value="digital">Digital</option>
        </select>
      </div>
      <InventoryTable items={items} />
    </section>
  );
}

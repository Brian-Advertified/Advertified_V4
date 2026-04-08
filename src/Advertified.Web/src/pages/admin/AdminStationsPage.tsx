import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, PlusCircle, Save, Trash2, X } from 'lucide-react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { QueryStateBoundary } from '../../components/ui/QueryStateBoundary';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AdminCreateOutletInput, AdminUpdateOutletInput } from '../../types/domain';
import { AdminPageShell, splitList, titleize, tone } from './adminWorkspace';
import { ActionButton, hasText } from './adminSectionShared';

type SelectOption = {
  value: string;
  label: string;
};

const LANGUAGE_OPTIONS: SelectOption[] = [
  { value: 'english', label: 'English' },
  { value: 'xitsonga', label: 'Xitsonga' },
  { value: 'isizulu', label: 'isiZulu' },
  { value: 'isixhosa', label: 'isiXhosa' },
  { value: 'sesotho', label: 'Sesotho' },
  { value: 'setswana', label: 'Setswana' },
  { value: 'afrikaans', label: 'Afrikaans' },
  { value: 'sepedi', label: 'Sepedi' },
  { value: 'tshivenda', label: 'Tshivenda' },
  { value: 'siswati', label: 'Siswati' },
  { value: 'isindebele', label: 'isiNdebele' },
  { value: 'multilingual', label: 'Multilingual' },
  { value: 'unknown', label: 'Unknown' },
];
const PROVINCE_OPTIONS: SelectOption[] = [
  { value: 'eastern_cape', label: 'Eastern Cape' },
  { value: 'free_state', label: 'Free State' },
  { value: 'gauteng', label: 'Gauteng' },
  { value: 'kwazulu_natal', label: 'KwaZulu-Natal' },
  { value: 'limpopo', label: 'Limpopo' },
  { value: 'mpumalanga', label: 'Mpumalanga' },
  { value: 'north_west', label: 'North West' },
  { value: 'northern_cape', label: 'Northern Cape' },
  { value: 'western_cape', label: 'Western Cape' },
  { value: 'national', label: 'National' },
];
const CITY_OPTIONS: SelectOption[] = [
  { value: 'Johannesburg', label: 'Johannesburg' },
  { value: 'Pretoria', label: 'Pretoria' },
  { value: 'Sandton', label: 'Sandton' },
  { value: 'Randburg', label: 'Randburg' },
  { value: 'Soweto', label: 'Soweto' },
  { value: 'Cape Town', label: 'Cape Town' },
  { value: 'Bellville', label: 'Bellville' },
  { value: 'Durban', label: 'Durban' },
  { value: 'Umhlanga', label: 'Umhlanga' },
  { value: 'Pietermaritzburg', label: 'Pietermaritzburg' },
  { value: 'Gqeberha', label: 'Gqeberha' },
  { value: 'East London', label: 'East London' },
  { value: 'Bloemfontein', label: 'Bloemfontein' },
  { value: 'Polokwane', label: 'Polokwane' },
  { value: 'Mbombela', label: 'Mbombela' },
  { value: 'Rustenburg', label: 'Rustenburg' },
];
const AUDIENCE_KEYWORD_OPTIONS: SelectOption[] = [
  { value: 'commuters', label: 'Commuters' },
  { value: 'professionals', label: 'Professionals' },
  { value: 'youth', label: 'Youth' },
  { value: 'families', label: 'Families' },
  { value: 'shoppers', label: 'Shoppers' },
  { value: 'smes', label: 'SMEs' },
  { value: 'mass market', label: 'Mass market' },
  { value: 'premium audience', label: 'Premium audience' },
  { value: 'pan-african', label: 'Pan-African' },
];
const BROADCAST_FREQUENCY_OPTIONS = ['Hourly', 'Daily', 'Weekdays only', 'Weekends only', 'Drive time', 'All day rotation'];
const TARGET_AUDIENCE_OPTIONS = ['General audience', 'Youth audience', 'Working professionals', 'Households and families', 'Business decision-makers', 'Pan-African / beyond-SA audience'];
const COVERAGE_TYPE_OPTIONS: SelectOption[] = [
  { value: 'local', label: 'Local' },
  { value: 'regional', label: 'Regional' },
  { value: 'national', label: 'National' },
  { value: 'digital', label: 'Digital' },
  { value: 'mixed', label: 'Mixed' },
  { value: 'unknown', label: 'Unknown' },
];
const CATALOG_HEALTH_OPTIONS: SelectOption[] = [
  { value: 'strong', label: 'Strong' },
  { value: 'mixed', label: 'Mixed' },
  { value: 'mixed_not_fully_healthy', label: 'Mixed not fully healthy' },
  { value: 'unknown', label: 'Unknown' },
  { value: 'weak_partial_pricing', label: 'Weak partial pricing' },
  { value: 'weak_unpriced', label: 'Weak unpriced' },
  { value: 'weak_no_inventory', label: 'Weak no inventory' },
];

function humanizeToken(value: string) {
  return value
    .replace(/_/g, ' ')
    .replace(/\b\w/g, (char) => char.toUpperCase());
}

function mergeOptionValues(
  baseOptions: SelectOption[],
  ...valueSets: Array<string | undefined>
) {
  const merged = new Map<string, SelectOption>();

  for (const option of baseOptions) {
    if (hasText(option.value)) {
      merged.set(option.value.trim().toLowerCase(), option);
    }
  }

  for (const valueSet of valueSets) {
    for (const value of splitList(valueSet ?? '')) {
      if (!hasText(value)) {
        continue;
      }

      const trimmed = value.trim();
      const key = trimmed.toLowerCase();
      if (!merged.has(key)) {
        merged.set(key, { value: trimmed, label: humanizeToken(trimmed) });
      }
    }
  }

  return Array.from(merged.values());
}

function mergeTextOptions(baseOptions: string[], ...valueSets: Array<string | undefined>) {
  const merged = new Map<string, string>();

  for (const option of baseOptions) {
    if (!hasText(option)) {
      continue;
    }

    merged.set(option.trim().toLowerCase(), option.trim());
  }

  for (const valueSet of valueSets) {
    for (const value of splitList(valueSet ?? '')) {
      if (!hasText(value)) {
        continue;
      }

      const trimmed = value.trim();
      const key = trimmed.toLowerCase();
      if (!merged.has(key)) {
        merged.set(key, trimmed);
      }
    }
  }

  return Array.from(merged.values());
}

export function AdminStationsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const handledSearchRef = useRef<string | null>(null);
  const [sortBy, setSortBy] = useState<'priority' | 'name' | 'coverage'>('priority');
  const [page, setPage] = useState(1);
  const [showAllOutlets, setShowAllOutlets] = useState(false);
  const pageSize = 25;
  const [dialogMode, setDialogMode] = useState<'create' | 'view' | 'edit' | null>(null);
  const [selectedOutletCode, setSelectedOutletCode] = useState<string | null>(null);
  const [outletForm, setOutletForm] = useState({
    code: '',
    name: '',
    mediaType: 'radio',
    coverageType: 'regional',
    catalogHealth: 'mixed_not_fully_healthy',
    operatorName: '',
    isNational: false,
    hasPricing: false,
    languageNotes: '',
    targetAudience: '',
    broadcastFrequency: '',
    primaryLanguages: '',
    provinceCodes: '',
    cityLabels: '',
    audienceKeywords: '',
  });

  const outletsPageQuery = useQuery({
    queryKey: ['admin-outlets-page', page, pageSize, showAllOutlets, sortBy],
    queryFn: () => advertifiedApi.getAdminOutletsPage(page, pageSize, !showAllOutlets, sortBy),
  });

  const selectedOutletQuery = useQuery({
    queryKey: ['admin-outlet', selectedOutletCode],
    queryFn: () => advertifiedApi.getAdminOutlet(selectedOutletCode!),
    enabled: dialogMode !== null && dialogMode !== 'create' && !!selectedOutletCode,
  });
  const outletMasterDataQuery = useQuery({
    queryKey: ['admin-outlet-master-data'],
    queryFn: () => advertifiedApi.getAdminOutletMasterData(),
  });

  const openCreateDialog = () => {
    setSelectedOutletCode(null);
    setOutletForm({
      code: '',
      name: '',
      mediaType: 'radio',
      coverageType: 'regional',
      catalogHealth: 'mixed_not_fully_healthy',
      operatorName: '',
      isNational: false,
      hasPricing: false,
      languageNotes: '',
      targetAudience: '',
      broadcastFrequency: '',
      primaryLanguages: '',
      provinceCodes: '',
      cityLabels: '',
      audienceKeywords: '',
    });
    setDialogMode('create');
  };

  const hydrateFormFromDetail = (detail: Awaited<ReturnType<typeof advertifiedApi.getAdminOutlet>>) => {
    setOutletForm({
      code: detail.code,
      name: detail.name,
      mediaType: detail.mediaType,
      coverageType: detail.coverageType,
      catalogHealth: detail.catalogHealth,
      operatorName: detail.operatorName ?? '',
      isNational: detail.isNational,
      hasPricing: detail.hasPricing,
      languageNotes: detail.languageNotes ?? '',
      targetAudience: detail.targetAudience ?? '',
      broadcastFrequency: detail.broadcastFrequency ?? '',
      primaryLanguages: detail.primaryLanguages.join(', '),
      provinceCodes: detail.provinceCodes.join(', '),
      cityLabels: detail.cityLabels.join(', '),
      audienceKeywords: detail.audienceKeywords.join(', '),
    });
  };

  const openExistingDialog = useCallback(async (code: string, mode: 'view' | 'edit') => {
    try {
      const detail = await queryClient.fetchQuery({
        queryKey: ['admin-outlet', code],
        queryFn: () => advertifiedApi.getAdminOutlet(code),
      });
      setSelectedOutletCode(code);
      hydrateFormFromDetail(detail);
      setDialogMode(mode);
    } catch (error) {
      pushToast({ title: 'Could not load outlet.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error');
    }
  }, [pushToast, queryClient]);

  const closeDialog = () => {
    setDialogMode(null);
    setSelectedOutletCode(null);
    if (searchParams.get('outlet') || searchParams.get('mode')) {
      setSearchParams({});
    }
  };

  const openPricingForOutlet = (code?: string | null) => {
    const outletCode = code?.trim();
    if (!outletCode) {
      return;
    }

    closeDialog();
    navigate(`/admin/pricing?outlet=${encodeURIComponent(outletCode)}`);
  };

  const buildOutletPayload = (): AdminCreateOutletInput & AdminUpdateOutletInput => ({
    code: outletForm.code,
    name: outletForm.name,
    mediaType: outletForm.mediaType,
    coverageType: outletForm.coverageType,
    catalogHealth: deriveCatalogHealthForSave(
      outletForm.catalogHealth,
      outletForm.hasPricing,
      selectedOutletQuery.data?.packageCount ?? 0,
      selectedOutletQuery.data?.slotRateCount ?? 0,
    ),
    operatorName: outletForm.operatorName || undefined,
    isNational: outletForm.isNational,
    hasPricing: outletForm.hasPricing,
    languageNotes: outletForm.languageNotes || undefined,
    targetAudience: outletForm.targetAudience || undefined,
    broadcastFrequency: outletForm.broadcastFrequency || undefined,
    primaryLanguages: splitList(outletForm.primaryLanguages),
    provinceCodes: splitList(outletForm.provinceCodes),
    cityLabels: splitList(outletForm.cityLabels),
    audienceKeywords: splitList(outletForm.audienceKeywords),
  });
  const outletFormIsValid = hasText(outletForm.code) && hasText(outletForm.name);

  const createOutletMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminOutlet(buildOutletPayload()),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] }),
        queryClient.invalidateQueries({ queryKey: ['admin-outlets-page'] }),
      ]);
      closeDialog();
      pushToast({ title: 'Outlet added.', description: 'The new outlet is now part of the live broadcast catalog.' });
    },
    onError: (error) => pushToast({ title: 'Could not add outlet.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updateOutletMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminOutlet(selectedOutletCode ?? outletForm.code, buildOutletPayload()),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] }),
        queryClient.invalidateQueries({ queryKey: ['admin-outlets-page'] }),
      ]);
      if (selectedOutletCode) {
        await queryClient.invalidateQueries({ queryKey: ['admin-outlet', selectedOutletCode] });
      }
      closeDialog();
      pushToast({ title: 'Outlet updated.', description: 'The outlet record was updated successfully.' });
    },
    onError: (error) => pushToast({ title: 'Could not update outlet.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteOutletMutation = useMutation({
    mutationFn: (code: string) => advertifiedApi.deleteAdminOutlet(code),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] }),
        queryClient.invalidateQueries({ queryKey: ['admin-outlets-page'] }),
      ]);
      closeDialog();
      pushToast({ title: 'Outlet deleted.', description: 'The outlet and its linked broadcast records were removed.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete outlet.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const outletFromSearch = searchParams.get('outlet');
  const modeFromSearch = searchParams.get('mode');
  const searchDialogKey = `${outletFromSearch ?? ''}|${modeFromSearch ?? ''}`;
  useEffect(() => {
    if (!outletFromSearch || (modeFromSearch !== 'edit' && modeFromSearch !== 'view') || handledSearchRef.current === searchDialogKey) {
      return;
    }

    handledSearchRef.current = searchDialogKey;
    const timeoutId = window.setTimeout(() => {
      void openExistingDialog(outletFromSearch, modeFromSearch);
    }, 0);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [modeFromSearch, openExistingDialog, outletFromSearch, searchDialogKey]);

  useEffect(() => {
    setPage(1);
  }, [showAllOutlets, sortBy]);

  return (
    <QueryStateBoundary
      query={outletsPageQuery}
      loadingLabel="Loading outlet page..."
      errorTitle="Outlet list unavailable"
      errorDescription="The outlet management page could not be loaded."
      emptyTitle="No outlets found."
      emptyDescription="There are no outlets matching the current view."
    >
      {(outletPage) => {
        const sortedOutlets = outletPage.items;
        const issueCount = outletPage.issueCount;
        const strongCount = outletPage.strongCount;
        const isReadOnly = dialogMode === 'view';
        const activeDetail = dialogMode === 'create' ? null : selectedOutletQuery.data;
        const effectiveCatalogHealth = deriveCatalogHealthForSave(
          outletForm.catalogHealth,
          outletForm.hasPricing,
          activeDetail?.packageCount ?? 0,
          activeDetail?.slotRateCount ?? 0,
        );
        const masterData = outletMasterDataQuery.data;
        const languageOptions = mergeOptionValues(masterData?.languages ?? LANGUAGE_OPTIONS, outletForm.primaryLanguages);
        const provinceOptions = mergeOptionValues(masterData?.provinces ?? PROVINCE_OPTIONS, outletForm.provinceCodes);
        const cityOptions = mergeOptionValues(masterData?.cities ?? CITY_OPTIONS, outletForm.cityLabels);
        const audienceKeywordOptions = mergeOptionValues(masterData?.audienceKeywords ?? AUDIENCE_KEYWORD_OPTIONS, outletForm.audienceKeywords);
        const broadcastFrequencyOptions = mergeTextOptions(masterData?.broadcastFrequencySuggestions ?? BROADCAST_FREQUENCY_OPTIONS, outletForm.broadcastFrequency);
        const targetAudienceOptions = mergeTextOptions(masterData?.targetAudienceSuggestions ?? TARGET_AUDIENCE_OPTIONS, outletForm.targetAudience);
        const coverageTypeOptions = masterData?.coverageTypes ?? COVERAGE_TYPE_OPTIONS;
        const catalogHealthOptions = masterData?.catalogHealthStates ?? CATALOG_HEALTH_OPTIONS;
        const canMarkStrong = outletForm.hasPricing && ((activeDetail?.packageCount ?? 0) > 0 || (activeDetail?.slotRateCount ?? 0) > 0);
        const displayCatalogHealth = canMarkStrong || outletForm.catalogHealth !== 'strong'
          ? outletForm.catalogHealth
          : 'mixed_not_fully_healthy';

        return (
        <AdminPageShell title="Stations and channels" description="Manage live broadcast outlets, add new stations, and review health, coverage, geography, and pricing signals.">
          <section className="space-y-6">
            <div className="panel p-6">
              <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div>
                  <h3 className="text-lg font-semibold text-ink">Outlet management</h3>
                  <p className="mt-2 text-sm text-ink-soft">
                    {showAllOutlets
                      ? 'Showing every live broadcast outlet. Switch back to focus only on records that still need attention.'
                      : `Showing only outlets with issues so you can work the fix queue faster. ${issueCount} outlet${issueCount === 1 ? '' : 's'} currently need attention.`}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-3">
                  <button
                    type="button"
                    className="button-secondary px-4 py-3"
                    onClick={() => setShowAllOutlets((current) => !current)}
                  >
                    {showAllOutlets ? 'Show issue queue only' : 'Show all outlets'}
                  </button>
                  <select className="input-base min-w-[210px]" value={sortBy} onChange={(event) => setSortBy(event.target.value as 'priority' | 'name' | 'coverage')}>
                    <option value="priority">Sort by priority</option>
                    <option value="name">Sort by name</option>
                    <option value="coverage">Sort by coverage</option>
                  </select>
                  <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3" onClick={openCreateDialog}>
                    <PlusCircle className="size-4" />
                    Add outlet
                  </button>
                </div>
              </div>
              <div className="mt-4 flex flex-wrap items-center gap-3 text-sm">
                <span className={`inline-flex rounded-full border px-3 py-1 font-semibold ${showAllOutlets ? 'border-brand/20 bg-brand-soft text-brand' : 'border-amber-200 bg-amber-50 text-amber-700'}`}>
                  {showAllOutlets ? 'Viewing all outlets' : 'Viewing issue queue only'}
                </span>
                <span className="text-ink-soft">
                  {showAllOutlets
                    ? `${outletPage.totalCount} outlets in this result set, with ${strongCount} strong and ${issueCount} needing attention across the full catalog.`
                    : `${issueCount} outlets need attention. Strong outlets are hidden until you switch to Show all outlets.`}
                </span>
              </div>
            </div>
            <div className="overflow-hidden rounded-[28px] border border-line">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Outlet</th><th className="px-4 py-4">Type</th><th className="px-4 py-4">Coverage</th><th className="px-4 py-4">Geography</th><th className="px-4 py-4">Pricing</th><th className="px-4 py-4">Health</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                <tbody>
                  {sortedOutlets.map((item, index) => <tr key={item.code} className="border-t border-line"><td className="px-4 py-4"><div className="flex items-center gap-3"><div><p className="font-semibold text-ink">{item.name}</p><p className="text-xs text-ink-soft">{item.languageDisplay ?? 'Language not specified'}</p></div>{sortBy === 'priority' && index < 3 ? <span className="inline-flex rounded-full border border-amber-200 bg-amber-50 px-2 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-700">Needs attention</span> : null}</div></td><td className="px-4 py-4 text-ink-soft">{titleize(item.mediaType)}</td><td className="px-4 py-4 text-ink-soft">{titleize(item.coverageType)}</td><td className="px-4 py-4 text-ink-soft">{item.geographyLabel}</td><td className="px-4 py-4 text-ink-soft"><div>{item.hasPricing ? 'Available' : 'Missing'}</div><div className="text-xs">{item.packageCount} packages | {item.slotRateCount} slot rows</div></td><td className="px-4 py-4"><span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${tone(item.catalogHealth)}`}>{titleize(item.catalogHealth)}</span></td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View ${item.name}`} icon={Eye} onClick={() => openExistingDialog(item.code, 'view')} /><ActionButton label={`Edit ${item.name}`} icon={Pencil} onClick={() => openExistingDialog(item.code, 'edit')} /><ActionButton label={`Delete ${item.name}`} icon={Trash2} variant="danger" disabled={deleteOutletMutation.isPending} onClick={() => { if (window.confirm(`Delete ${item.name}? This will remove the outlet and linked broadcast pricing records.`)) { deleteOutletMutation.mutate(item.code); } }} /></div></td></tr>)}
                </tbody>
              </table>
            </div>
            <div className="panel flex flex-col gap-4 p-4 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-sm text-ink-soft">
                Page {outletPage.page} of {outletPage.totalPages} · {outletPage.totalCount} outlet{outletPage.totalCount === 1 ? '' : 's'} in this result set
              </p>
              <div className="flex items-center gap-3">
                <button
                  type="button"
                  className="button-secondary px-4 py-2"
                  disabled={outletPage.page <= 1}
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                >
                  Previous
                </button>
                <button
                  type="button"
                  className="button-secondary px-4 py-2"
                  disabled={outletPage.page >= outletPage.totalPages}
                  onClick={() => setPage((current) => Math.min(outletPage.totalPages, current + 1))}
                >
                  Next
                </button>
              </div>
            </div>

            {dialogMode ? (
              <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                <div className="max-h-[92vh] w-full max-w-5xl overflow-y-auto rounded-[32px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)] sm:p-8">
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <h3 className="text-2xl font-semibold text-ink">{dialogMode === 'create' ? 'Add new outlet' : dialogMode === 'edit' ? 'Edit outlet' : 'View outlet'}</h3>
                      <p className="mt-2 text-sm text-ink-soft">{dialogMode === 'create' ? 'Create a real outlet record in `media_outlet` with language, geography, and keyword mappings.' : 'Review or update the live outlet record and its planning metadata.'}</p>
                    </div>
                    <div className="flex items-center gap-3">
                      {activeDetail && dialogMode !== 'create' ? <span className="pill">{activeDetail.packageCount} packages | {activeDetail.slotRateCount} slot rows</span> : null}
                      <button type="button" className="button-secondary p-3" onClick={closeDialog}><X className="size-4" /></button>
                    </div>
                  </div>

                  {selectedOutletQuery.isLoading && dialogMode !== 'create' ? <p className="mt-6 text-sm text-ink-soft">Loading outlet details...</p> : null}

                  <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                    <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Code" value={outletForm.code} onChange={(event) => setOutletForm((current) => ({ ...current, code: event.target.value }))} />
                    <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Name" value={outletForm.name} onChange={(event) => setOutletForm((current) => ({ ...current, name: event.target.value }))} />
                    <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Operator name" value={outletForm.operatorName} onChange={(event) => setOutletForm((current) => ({ ...current, operatorName: event.target.value }))} />
                    <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={outletForm.mediaType} onChange={(event) => setOutletForm((current) => ({ ...current, mediaType: event.target.value }))}>
                      <option value="radio">Radio</option>
                      <option value="tv">TV</option>
                    </select>
                    <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={outletForm.coverageType} onChange={(event) => setOutletForm((current) => ({ ...current, coverageType: event.target.value }))}>
                      {coverageTypeOptions.map((option) => (
                        <option key={option.value} value={option.value}>{option.label}</option>
                      ))}
                    </select>
                    <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={displayCatalogHealth} onChange={(event) => setOutletForm((current) => ({ ...current, catalogHealth: event.target.value }))}>
                      {catalogHealthOptions.map((option) => (
                        <option key={option.value} value={option.value} disabled={option.value === 'strong' && !canMarkStrong}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                    <LabeledMultiSelect
                      disabled={isReadOnly}
                      label="Primary languages"
                      help="Choose the languages this outlet mainly serves."
                      options={languageOptions}
                      value={outletForm.primaryLanguages}
                      onChange={(value) => setOutletForm((current) => ({ ...current, primaryLanguages: value }))}
                    />
                    <LabeledMultiSelect
                      disabled={isReadOnly}
                      label="Province coverage"
                      help="Choose the provinces this outlet covers."
                      options={provinceOptions}
                      value={outletForm.provinceCodes}
                      onChange={(value) => setOutletForm((current) => ({ ...current, provinceCodes: value }))}
                    />
                    <LabeledMultiSelect
                      disabled={isReadOnly}
                      label="City coverage"
                      help="Choose the main cities or metros this outlet reaches."
                      options={cityOptions}
                      value={outletForm.cityLabels}
                      onChange={(value) => setOutletForm((current) => ({ ...current, cityLabels: value }))}
                    />
                    <LabeledMultiSelect
                      disabled={isReadOnly}
                      label="Audience keywords"
                      help="Choose the closest audience descriptors."
                      options={audienceKeywordOptions}
                      value={outletForm.audienceKeywords}
                      onChange={(value) => setOutletForm((current) => ({ ...current, audienceKeywords: value }))}
                    />
                    <label className="block text-sm font-semibold text-ink">
                      Broadcast frequency
                      <input
                        list="broadcast-frequency-options"
                        disabled={isReadOnly}
                        className="input-base mt-2 disabled:bg-slate-50"
                        value={outletForm.broadcastFrequency}
                        onChange={(event) => setOutletForm((current) => ({ ...current, broadcastFrequency: event.target.value }))}
                        placeholder="Choose frequency"
                      />
                      <datalist id="broadcast-frequency-options">
                        {broadcastFrequencyOptions.map((option) => <option key={option} value={option} />)}
                      </datalist>
                      <span className="mt-2 block text-xs font-normal text-ink-soft">Use this when the station follows a known broadcast rhythm.</span>
                    </label>
                    <label className="block text-sm font-semibold text-ink">
                      Target audience
                      <input
                        list="target-audience-options"
                        disabled={isReadOnly}
                        className="input-base mt-2 disabled:bg-slate-50"
                        value={outletForm.targetAudience}
                        onChange={(event) => setOutletForm((current) => ({ ...current, targetAudience: event.target.value }))}
                        placeholder="Choose target audience"
                      />
                      <datalist id="target-audience-options">
                        {targetAudienceOptions.map((option) => <option key={option} value={option} />)}
                      </datalist>
                      <span className="mt-2 block text-xs font-normal text-ink-soft">Pick the closest audience profile instead of writing a custom phrase.</span>
                    </label>
                  </div>
                  <textarea disabled={isReadOnly} className="input-base mt-4 min-h-[120px] disabled:bg-slate-50" placeholder="Language or coverage notes. Example: Strong commuter listenership in Gauteng mornings." value={outletForm.languageNotes} onChange={(event) => setOutletForm((current) => ({ ...current, languageNotes: event.target.value }))} />
                  <div className="mt-4 flex flex-wrap items-center gap-5 text-sm text-ink-soft">
                    <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={outletForm.isNational} onChange={(event) => setOutletForm((current) => ({ ...current, isNational: event.target.checked }))} /> National capable</label>
                    <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={outletForm.hasPricing} onChange={(event) => setOutletForm((current) => ({ ...current, hasPricing: event.target.checked }))} /> Pricing already available</label>
                    {activeDetail ? <span>Min package {activeDetail.minPackagePrice ? new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', maximumFractionDigits: 0 }).format(activeDetail.minPackagePrice) : 'N/A'}</span> : null}
                    {activeDetail ? <span>Min slot {activeDetail.minSlotRate ? new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', maximumFractionDigits: 0 }).format(activeDetail.minSlotRate) : 'N/A'}</span> : null}
                    <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${tone(effectiveCatalogHealth)}`}>
                      Effective health: {titleize(effectiveCatalogHealth)}
                    </span>
                    {!canMarkStrong ? (
                      <span className="text-xs">
                        Strong requires pricing enabled and at least one package or slot-rate row.
                      </span>
                    ) : null}
                    {!isReadOnly && hasText(outletForm.code) ? (
                      <button
                        type="button"
                        className="rounded-full border border-brand/20 bg-brand-soft px-3 py-1 text-xs font-semibold text-brand transition hover:border-brand/40"
                        onClick={() => openPricingForOutlet(outletForm.code)}
                      >
                        Open pricing setup
                      </button>
                    ) : null}
                  </div>
                  <div className="mt-6 flex flex-wrap items-center justify-end gap-3">
                    <button type="button" className="button-secondary px-5 py-3" onClick={closeDialog}>Close</button>
                    {dialogMode === 'edit' ? <button type="button" className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50" onClick={() => { if (selectedOutletCode && window.confirm(`Delete ${outletForm.name || selectedOutletCode}? This will remove the outlet and linked broadcast pricing records.`)) { deleteOutletMutation.mutate(selectedOutletCode); } }} disabled={deleteOutletMutation.isPending}>Delete outlet</button> : null}
                    {!outletFormIsValid && dialogMode !== 'view' ? <p className="text-sm text-rose-600">Outlet code and name are required before saving.</p> : null}
                    {dialogMode !== 'view' ? <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60" onClick={() => dialogMode === 'create' ? createOutletMutation.mutate() : updateOutletMutation.mutate()} disabled={createOutletMutation.isPending || updateOutletMutation.isPending || !outletFormIsValid}>
                      <Save className="size-4" />
                      {dialogMode === 'create' ? 'Save outlet' : 'Update outlet'}
                    </button> : null}
                  </div>
                </div>
              </div>
            ) : null}
          </section>
        </AdminPageShell>
      )}}
    </QueryStateBoundary>
  );
}

function LabeledMultiSelect({
  disabled,
  label,
  help,
  options,
  value,
  onChange,
}: {
  disabled: boolean;
  label: string;
  help: string;
  options: SelectOption[];
  value: string;
  onChange: (value: string) => void;
}) {
  const selectedValues = splitList(value);

  return (
    <label className="block text-sm font-semibold text-ink">
      {label}
      <select
        multiple
        disabled={disabled}
        className="input-base mt-2 min-h-[120px] disabled:bg-slate-50"
        value={selectedValues}
        onChange={(event) => {
          const next = Array.from(event.target.selectedOptions).map((option) => option.value).join(', ');
          onChange(next);
        }}
      >
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
      <span className="mt-2 block text-xs font-normal text-ink-soft">{help}</span>
    </label>
  );
}

function deriveCatalogHealthForSave(
  requestedHealth: string,
  hasPricing: boolean,
  packageCount: number,
  slotRateCount: number,
) {
  if (!hasPricing) {
    return 'weak_unpriced';
  }

  if (packageCount === 0 && slotRateCount === 0) {
    return 'weak_no_inventory';
  }

  return requestedHealth;
}


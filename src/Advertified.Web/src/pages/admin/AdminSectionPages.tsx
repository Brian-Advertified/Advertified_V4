import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Globe2, MapPin, Pencil, PlusCircle, Save, ShieldCheck, Trash2, Upload, X, Zap } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type {
  AdminCreateGeographyInput,
  AdminCreateOutletInput,
  AdminGeographyDetail,
  AdminUpdateEnginePolicyInput,
  AdminUpdateGeographyInput,
  AdminOutletPricingPackage,
  AdminOutletSlotRate,
  AdminUpsertPackageSettingInput,
  AdminUpsertGeographyMappingInput,
  AdminUpdateOutletInput,
  AdminUpsertOutletPricingPackageInput,
  AdminUpsertOutletSlotRateInput,
} from '../../types/domain';
import { Link, useSearchParams } from 'react-router-dom';
import {
  AdminPageShell,
  AdminQueryBoundary,
  fmtCurrency,
  fmtDate,
  splitList,
  titleize,
  tone,
  useAdminDashboardQuery,
} from './adminWorkspace';

type ActionButtonProps = {
  label: string;
  onClick?: () => void;
  icon: typeof Eye;
  variant?: 'default' | 'danger';
  disabled?: boolean;
};

function ActionButton({ label, onClick, icon: Icon, variant = 'default', disabled = false }: ActionButtonProps) {
  const baseClassName = variant === 'danger'
    ? 'rounded-full border border-rose-200 bg-white p-2 text-rose-600 transition hover:bg-rose-50'
    : 'button-secondary p-2';

  return (
    <button type="button" className={baseClassName} onClick={onClick} title={label} aria-label={label} disabled={disabled}>
      <Icon className="size-4" />
    </button>
  );
}

function ReadOnlyNotice({ label }: { label: string }) {
  return (
    <div className="rounded-[24px] border border-dashed border-line bg-white/75 px-4 py-3 text-sm text-ink-soft">
      {label}
    </div>
  );
}

function EmptyTableState({ message, action }: { message: string; action?: React.ReactNode }) {
  return (
    <div className="rounded-[24px] border border-dashed border-line bg-white px-6 py-10 text-center">
      <p className="text-sm text-ink-soft">{message}</p>
      {action ? <div className="mt-4 flex justify-center">{action}</div> : null}
    </div>
  );
}

type AdminUserFormState = {
  fullName: string;
  email: string;
  phone: string;
  password: string;
  role: 'client' | 'agent' | 'creative_director' | 'admin';
  accountStatus: string;
  isSaCitizen: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  assignedAreaCodes: string[];
};

const hasText = (value: string) => value.trim().length > 0;

export function AdminStationsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const handledSearchRef = useRef<string | null>(null);
  const [sortBy, setSortBy] = useState<'priority' | 'name' | 'coverage'>('priority');
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

  const selectedOutletQuery = useQuery({
    queryKey: ['admin-outlet', selectedOutletCode],
    queryFn: () => advertifiedApi.getAdminOutlet(selectedOutletCode!),
    enabled: dialogMode !== null && dialogMode !== 'create' && !!selectedOutletCode,
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

  const openExistingDialog = async (code: string, mode: 'view' | 'edit') => {
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
  };

  const closeDialog = () => {
    setDialogMode(null);
    setSelectedOutletCode(null);
    if (searchParams.get('outlet') || searchParams.get('mode')) {
      setSearchParams({});
    }
  };

  const buildOutletPayload = (): AdminCreateOutletInput & AdminUpdateOutletInput => ({
    code: outletForm.code,
    name: outletForm.name,
    mediaType: outletForm.mediaType,
    coverageType: outletForm.coverageType,
    catalogHealth: outletForm.catalogHealth,
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
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      closeDialog();
      pushToast({ title: 'Outlet added.', description: 'The new outlet is now part of the live broadcast catalog.' });
    },
    onError: (error) => pushToast({ title: 'Could not add outlet.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updateOutletMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminOutlet(selectedOutletCode ?? outletForm.code, buildOutletPayload()),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
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
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      closeDialog();
      pushToast({ title: 'Outlet deleted.', description: 'The outlet and its linked broadcast records were removed.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete outlet.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const outletFromSearch = searchParams.get('outlet');
  const modeFromSearch = searchParams.get('mode');
  const searchDialogKey = `${outletFromSearch ?? ''}|${modeFromSearch ?? ''}`;
  if (outletFromSearch && (modeFromSearch === 'edit' || modeFromSearch === 'view') && handledSearchRef.current !== searchDialogKey) {
    handledSearchRef.current = searchDialogKey;
    void openExistingDialog(outletFromSearch, modeFromSearch);
  }

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const priorityScore = (item: (typeof dashboard.outlets)[number]) => {
          let score = 0;
          if (item.catalogHealth === 'weak_no_inventory') score += 100;
          else if (item.catalogHealth === 'weak_unpriced') score += 85;
          else if (item.catalogHealth === 'mixed_not_fully_healthy') score += 60;
          else score += 20;
          if (!item.hasPricing) score += 25;
          if (item.packageCount === 0 && item.slotRateCount === 0) score += 20;
          if (!item.languageDisplay) score += 8;
          return score;
        };

        const sortedOutlets = [...dashboard.outlets].sort((left, right) => {
          if (sortBy === 'name') return left.name.localeCompare(right.name);
          if (sortBy === 'coverage') return left.coverageType.localeCompare(right.coverageType) || left.name.localeCompare(right.name);
          return priorityScore(right) - priorityScore(left) || left.name.localeCompare(right.name);
        });
        const isReadOnly = dialogMode === 'view';
        const activeDetail = dialogMode === 'create' ? null : selectedOutletQuery.data;

        return (
        <AdminPageShell title="Stations and channels" description="Manage live broadcast outlets, add new stations, and review health, coverage, geography, and pricing signals.">
          <section className="space-y-6">
            <div className="panel p-6">
              <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                <div>
                  <h3 className="text-lg font-semibold text-ink">Outlet management</h3>
                  <p className="mt-2 text-sm text-ink-soft">Use live actions to review, update, or remove broadcast outlets. The highest-priority records are surfaced first.</p>
                </div>
                <div className="flex flex-wrap items-center gap-3">
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
            </div>
            <div className="overflow-hidden rounded-[28px] border border-line">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Outlet</th><th className="px-4 py-4">Type</th><th className="px-4 py-4">Coverage</th><th className="px-4 py-4">Geography</th><th className="px-4 py-4">Pricing</th><th className="px-4 py-4">Health</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                <tbody>
                  {sortedOutlets.map((item, index) => <tr key={item.code} className="border-t border-line"><td className="px-4 py-4"><div className="flex items-center gap-3"><div><p className="font-semibold text-ink">{item.name}</p><p className="text-xs text-ink-soft">{item.languageDisplay ?? 'Language not specified'}</p></div>{sortBy === 'priority' && index < 3 ? <span className="inline-flex rounded-full border border-amber-200 bg-amber-50 px-2 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-700">Needs attention</span> : null}</div></td><td className="px-4 py-4 text-ink-soft">{titleize(item.mediaType)}</td><td className="px-4 py-4 text-ink-soft">{titleize(item.coverageType)}</td><td className="px-4 py-4 text-ink-soft">{item.geographyLabel}</td><td className="px-4 py-4 text-ink-soft"><div>{item.hasPricing ? 'Available' : 'Missing'}</div><div className="text-xs">{item.packageCount} packages | {item.slotRateCount} slot rows</div></td><td className="px-4 py-4"><span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${tone(item.catalogHealth)}`}>{titleize(item.catalogHealth)}</span></td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View ${item.name}`} icon={Eye} onClick={() => openExistingDialog(item.code, 'view')} /><ActionButton label={`Edit ${item.name}`} icon={Pencil} onClick={() => openExistingDialog(item.code, 'edit')} /><ActionButton label={`Delete ${item.name}`} icon={Trash2} variant="danger" disabled={deleteOutletMutation.isPending} onClick={() => { if (window.confirm(`Delete ${item.name}? This will remove the outlet and linked broadcast pricing records.`)) { deleteOutletMutation.mutate(item.code); } }} /></div></td></tr>)}
                </tbody>
              </table>
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
                      <option value="local">Local</option>
                      <option value="regional">Regional</option>
                      <option value="national">National</option>
                    </select>
                    <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={outletForm.catalogHealth} onChange={(event) => setOutletForm((current) => ({ ...current, catalogHealth: event.target.value }))}>
                      <option value="strong">Strong</option>
                      <option value="mixed_not_fully_healthy">Mixed not fully healthy</option>
                      <option value="weak_unpriced">Weak unpriced</option>
                      <option value="weak_no_inventory">Weak no inventory</option>
                    </select>
                    <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Primary languages, comma separated" value={outletForm.primaryLanguages} onChange={(event) => setOutletForm((current) => ({ ...current, primaryLanguages: event.target.value }))} />
                    <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Province codes, comma separated" value={outletForm.provinceCodes} onChange={(event) => setOutletForm((current) => ({ ...current, provinceCodes: event.target.value }))} />
                    <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="City labels, comma separated" value={outletForm.cityLabels} onChange={(event) => setOutletForm((current) => ({ ...current, cityLabels: event.target.value }))} />
                    <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Audience keywords, comma separated" value={outletForm.audienceKeywords} onChange={(event) => setOutletForm((current) => ({ ...current, audienceKeywords: event.target.value }))} />
                    <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Broadcast frequency" value={outletForm.broadcastFrequency} onChange={(event) => setOutletForm((current) => ({ ...current, broadcastFrequency: event.target.value }))} />
                    <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Target audience" value={outletForm.targetAudience} onChange={(event) => setOutletForm((current) => ({ ...current, targetAudience: event.target.value }))} />
                  </div>
                  <textarea disabled={isReadOnly} className="input-base mt-4 min-h-[120px] disabled:bg-slate-50" placeholder="Language notes" value={outletForm.languageNotes} onChange={(event) => setOutletForm((current) => ({ ...current, languageNotes: event.target.value }))} />
                  <div className="mt-4 flex flex-wrap items-center gap-5 text-sm text-ink-soft">
                    <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={outletForm.isNational} onChange={(event) => setOutletForm((current) => ({ ...current, isNational: event.target.checked }))} /> National capable</label>
                    <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={outletForm.hasPricing} onChange={(event) => setOutletForm((current) => ({ ...current, hasPricing: event.target.checked }))} /> Pricing already available</label>
                    {activeDetail ? <span>Min package {activeDetail.minPackagePrice ? new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', maximumFractionDigits: 0 }).format(activeDetail.minPackagePrice) : 'N/A'}</span> : null}
                    {activeDetail ? <span>Min slot {activeDetail.minSlotRate ? new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', maximumFractionDigits: 0 }).format(activeDetail.minSlotRate) : 'N/A'}</span> : null}
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
    </AdminQueryBoundary>
  );
}

export function AdminPricingPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [selectedOutletCodeState, setSelectedOutletCodeState] = useState('');
  const [packageSettingDialog, setPackageSettingDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [packageDialog, setPackageDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [slotDialog, setSlotDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [packageSettingForm, setPackageSettingForm] = useState<AdminUpsertPackageSettingInput>({
    code: '',
    name: '',
    minBudget: 0,
    maxBudget: 0,
    sortOrder: 0,
    isActive: true,
    description: '',
    audienceFit: '',
    quickBenefit: '',
    packagePurpose: '',
    includeRadio: 'optional',
    includeTv: 'no',
    leadTime: '',
    recommendedSpend: undefined,
    isRecommended: false,
    benefits: [],
  });
  const [packageForm, setPackageForm] = useState<AdminUpsertOutletPricingPackageInput>({
    packageName: '',
    packageType: '',
    exposureCount: undefined,
    monthlyExposureCount: undefined,
    valueZar: undefined,
    discountZar: undefined,
    savingZar: undefined,
    investmentZar: undefined,
    costPerMonthZar: undefined,
    durationMonths: undefined,
    durationWeeks: undefined,
    notes: '',
    sourceName: '',
    sourceDate: '',
    isActive: true,
  });
  const [slotForm, setSlotForm] = useState<AdminUpsertOutletSlotRateInput>({
    dayGroup: 'weekday',
    startTime: '06:00',
    endTime: '09:00',
    adDurationSeconds: 30,
    rateZar: 0,
    rateType: 'spot',
    sourceName: '',
    sourceDate: '',
    isActive: true,
  });
  const selectedOutletCode = searchParams.get('outlet')
    ?? selectedOutletCodeState
    ?? query.data?.outlets?.[0]?.code
    ?? '';

  const outletPricingQuery = useQuery({
    queryKey: ['admin-outlet-pricing', selectedOutletCode],
    queryFn: () => advertifiedApi.getAdminOutletPricing(selectedOutletCode),
    enabled: !!selectedOutletCode,
  });

  const refreshPricing = async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
    if (selectedOutletCode) {
      await queryClient.invalidateQueries({ queryKey: ['admin-outlet-pricing', selectedOutletCode] });
    }
  };

  const refreshPackageSettings = async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
  };

  const createPackageSettingMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminPackageSetting(packageSettingForm),
    onSuccess: async () => {
      await refreshPackageSettings();
      setPackageSettingDialog(null);
      pushToast({ title: 'Package band saved.', description: 'The package settings are now available across the platform.' });
    },
    onError: (error) => pushToast({ title: 'Could not save package band.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updatePackageSettingMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminPackageSetting(packageSettingDialog?.id ?? '', packageSettingForm),
    onSuccess: async () => {
      await refreshPackageSettings();
      setPackageSettingDialog(null);
      pushToast({ title: 'Package band updated.', description: 'The package settings were updated successfully.' });
    },
    onError: (error) => pushToast({ title: 'Could not update package band.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deletePackageSettingMutation = useMutation({
    mutationFn: (id: string) => advertifiedApi.deleteAdminPackageSetting(id),
    onSuccess: async () => {
      await refreshPackageSettings();
      setPackageSettingDialog(null);
      pushToast({ title: 'Package band deleted.', description: 'The package band was removed from the catalog.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete package band.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const createPackageMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminOutletPricingPackage(selectedOutletCode, packageForm),
    onSuccess: async () => {
      await refreshPricing();
      setPackageDialog(null);
      pushToast({ title: 'Pricing package saved.', description: 'The outlet package row is now live.' });
    },
    onError: (error) => pushToast({ title: 'Could not save pricing package.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updatePackageMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminOutletPricingPackage(selectedOutletCode, packageDialog?.id ?? '', packageForm),
    onSuccess: async () => {
      await refreshPricing();
      setPackageDialog(null);
      pushToast({ title: 'Pricing package updated.', description: 'The outlet package row was updated.' });
    },
    onError: (error) => pushToast({ title: 'Could not update pricing package.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deletePackageMutation = useMutation({
    mutationFn: (id: string) => advertifiedApi.deleteAdminOutletPricingPackage(selectedOutletCode, id),
    onSuccess: async () => {
      await refreshPricing();
      setPackageDialog(null);
      pushToast({ title: 'Pricing package deleted.', description: 'The outlet package row was removed.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete pricing package.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const createSlotMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminOutletSlotRate(selectedOutletCode, slotForm),
    onSuccess: async () => {
      await refreshPricing();
      setSlotDialog(null);
      pushToast({ title: 'Slot rate saved.', description: 'The outlet slot rate is now live.' });
    },
    onError: (error) => pushToast({ title: 'Could not save slot rate.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updateSlotMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminOutletSlotRate(selectedOutletCode, slotDialog?.id ?? '', slotForm),
    onSuccess: async () => {
      await refreshPricing();
      setSlotDialog(null);
      pushToast({ title: 'Slot rate updated.', description: 'The outlet slot rate was updated.' });
    },
    onError: (error) => pushToast({ title: 'Could not update slot rate.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteSlotMutation = useMutation({
    mutationFn: (id: string) => advertifiedApi.deleteAdminOutletSlotRate(selectedOutletCode, id),
    onSuccess: async () => {
      await refreshPricing();
      setSlotDialog(null);
      pushToast({ title: 'Slot rate deleted.', description: 'The outlet slot rate was removed.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete slot rate.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const parseNumber = (value: string) => value.trim() === '' ? undefined : Number(value);
  const packageSettingFormIsValid = hasText(packageSettingForm.code)
    && hasText(packageSettingForm.name)
    && hasText(packageSettingForm.leadTime)
    && packageSettingForm.maxBudget >= packageSettingForm.minBudget
    && packageSettingForm.maxBudget > 0;
  const packageFormIsValid = hasText(packageForm.packageName);
  const slotFormIsValid = hasText(slotForm.dayGroup) && hasText(slotForm.startTime) && hasText(slotForm.endTime) && Number.isFinite(slotForm.rateZar);
  const selectedPackage = packageDialog?.id && outletPricingQuery.data
    ? outletPricingQuery.data.packages.find((entry) => entry.id === packageDialog.id)
    : null;
  const selectedSlotRate = slotDialog?.id && outletPricingQuery.data
    ? outletPricingQuery.data.slotRates.find((entry) => entry.id === slotDialog.id)
    : null;

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const outletOptions = [...dashboard.outlets].sort((left, right) => Number(left.hasPricing) - Number(right.hasPricing) || left.name.localeCompare(right.name));
        const selectedPricing = outletPricingQuery.data;
        const selectedPackageSetting = packageSettingDialog?.id
          ? dashboard.packageSettings.find((entry) => entry.id === packageSettingDialog.id) ?? null
          : null;

        const hydratePackageSettingForm = (item: (typeof dashboard.packageSettings)[number]) => {
          setPackageSettingForm({
            code: item.code,
            name: item.name,
            minBudget: item.minBudget,
            maxBudget: item.maxBudget,
            sortOrder: item.sortOrder,
            isActive: item.isActive,
            description: item.description,
            audienceFit: item.audienceFit,
            quickBenefit: item.quickBenefit,
            packagePurpose: item.packagePurpose,
            includeRadio: item.includeRadio,
            includeTv: item.includeTv,
            leadTime: item.leadTime,
            recommendedSpend: item.recommendedSpend,
            isRecommended: item.isRecommended,
            benefits: item.benefits,
          });
        };

        const openPackageSettingDialog = (mode: 'create' | 'view' | 'edit', id?: string) => {
          if ((mode === 'edit' || mode === 'view') && id) {
            const item = dashboard.packageSettings.find((entry) => entry.id === id);
            if (item) {
              hydratePackageSettingForm(item);
            }
          } else {
            const nextSortOrder = dashboard.packageSettings.length > 0
              ? Math.max(...dashboard.packageSettings.map((item) => item.sortOrder)) + 10
              : 10;
            setPackageSettingForm({
              code: '',
              name: '',
              minBudget: 0,
              maxBudget: 0,
              sortOrder: nextSortOrder,
              isActive: true,
              description: '',
              audienceFit: '',
              quickBenefit: '',
              packagePurpose: '',
              includeRadio: 'optional',
              includeTv: 'no',
              leadTime: '',
              recommendedSpend: undefined,
              isRecommended: false,
              benefits: [],
            });
          }
          setPackageSettingDialog({ mode, id });
        };

        const hydratePackageForm = (item: AdminOutletPricingPackage) => {
          setPackageForm({
            packageName: item.packageName,
            packageType: item.packageType ?? '',
            exposureCount: item.exposureCount,
            monthlyExposureCount: item.monthlyExposureCount,
            valueZar: item.valueZar,
            discountZar: item.discountZar,
            savingZar: item.savingZar,
            investmentZar: item.investmentZar,
            costPerMonthZar: item.costPerMonthZar,
            durationMonths: item.durationMonths,
            durationWeeks: item.durationWeeks,
            notes: item.notes ?? '',
            sourceName: item.sourceName ?? '',
            sourceDate: item.sourceDate ?? '',
            isActive: item.isActive,
          });
        };

        const hydrateSlotForm = (item: AdminOutletSlotRate) => {
          setSlotForm({
            dayGroup: item.dayGroup,
            startTime: item.startTime,
            endTime: item.endTime,
            adDurationSeconds: item.adDurationSeconds,
            rateZar: item.rateZar,
            rateType: item.rateType,
            sourceName: item.sourceName ?? '',
            sourceDate: item.sourceDate ?? '',
            isActive: item.isActive,
          });
        };

        const openPackageDialog = (mode: 'create' | 'view' | 'edit', id?: string) => {
          if ((mode === 'edit' || mode === 'view') && id && selectedPricing) {
            const item = selectedPricing.packages.find((entry) => entry.id === id);
            if (item) {
              hydratePackageForm(item);
            }
          } else {
            setPackageForm({
              packageName: '',
              packageType: '',
              exposureCount: undefined,
              monthlyExposureCount: undefined,
              valueZar: undefined,
              discountZar: undefined,
              savingZar: undefined,
              investmentZar: undefined,
              costPerMonthZar: undefined,
              durationMonths: undefined,
              durationWeeks: undefined,
              notes: '',
              sourceName: '',
              sourceDate: '',
              isActive: true,
            });
          }
          setPackageDialog({ mode, id });
        };

        const openSlotDialog = (mode: 'create' | 'view' | 'edit', id?: string) => {
          if ((mode === 'edit' || mode === 'view') && id && selectedPricing) {
            const item = selectedPricing.slotRates.find((entry) => entry.id === id);
            if (item) {
              hydrateSlotForm(item);
            }
          } else {
            setSlotForm({
              dayGroup: 'weekday',
              startTime: '06:00',
              endTime: '09:00',
              adDurationSeconds: 30,
              rateZar: 0,
              rateType: 'spot',
              sourceName: '',
              sourceDate: '',
              isActive: true,
            });
          }
          setSlotDialog({ mode, id });
        };

        return (
          <AdminPageShell title="Pricing and packages" description="Repair live outlet pricing directly, then review package band settings used across the platform.">
            <section className="space-y-6">
              <div className="panel p-6">
                <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                  <div>
                    <h3 className="text-lg font-semibold text-ink">Outlet pricing repair</h3>
                    <p className="mt-2 text-sm text-ink-soft">Select an outlet and manage its live package and slot-rate rows directly.</p>
                  </div>
                  <select
                    className="input-base min-w-[280px]"
                    value={selectedOutletCode}
                    onChange={(event) => {
                      const next = event.target.value;
                      setSelectedOutletCodeState(next);
                      setSearchParams(next ? { outlet: next } : {});
                    }}
                  >
                    {outletOptions.map((outlet) => (
                      <option key={outlet.code} value={outlet.code}>
                        {outlet.name} {outlet.hasPricing ? '' : '- missing pricing'}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {selectedPricing ? (
                <>
                  <div className="grid gap-4 md:grid-cols-3">
                    <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Outlet</p><p className="mt-3 text-2xl font-semibold text-ink">{selectedPricing.outletName}</p><p className="mt-2 text-sm text-ink-soft">{titleize(selectedPricing.mediaType)} / {titleize(selectedPricing.coverageType)}</p></div>
                    <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Packages</p><p className="mt-3 text-2xl font-semibold text-ink">{selectedPricing.packages.length}</p></div>
                    <div className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Slot rates</p><p className="mt-3 text-2xl font-semibold text-ink">{selectedPricing.slotRates.length}</p></div>
                  </div>

                  <div className="panel p-6">
                    <div className="flex items-center justify-between gap-4">
                      <div><h3 className="text-lg font-semibold text-ink">Pricing packages</h3><p className="mt-2 text-sm text-ink-soft">Package-level pricing rows for the selected outlet.</p></div>
                      <button type="button" className="button-primary inline-flex items-center gap-2 px-4 py-3" onClick={() => openPackageDialog('create')}>
                        <PlusCircle className="size-4" />
                        Add package
                      </button>
                    </div>
                    {selectedPricing.packages.length > 0 ? (
                      <div className="mt-4 overflow-hidden rounded-[24px] border border-line">
                        <table className="w-full border-collapse text-sm">
                          <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Package</th><th className="px-4 py-4">Investment</th><th className="px-4 py-4">Monthly</th><th className="px-4 py-4">Duration</th><th className="px-4 py-4">Status</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                          <tbody>
                            {selectedPricing.packages.map((item) => <tr key={item.id} className="border-t border-line"><td className="px-4 py-4"><p className="font-semibold text-ink">{item.packageName}</p><p className="text-xs text-ink-soft">{item.packageType ?? 'General package'}</p></td><td className="px-4 py-4 text-ink-soft">{fmtCurrency(item.investmentZar ?? item.valueZar)}</td><td className="px-4 py-4 text-ink-soft">{fmtCurrency(item.costPerMonthZar)}</td><td className="px-4 py-4 text-ink-soft">{item.durationMonths ? `${item.durationMonths} month(s)` : item.durationWeeks ? `${item.durationWeeks} week(s)` : 'Not set'}</td><td className="px-4 py-4 text-ink-soft">{item.isActive ? 'Active' : 'Inactive'}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View package ${item.packageName}`} icon={Eye} onClick={() => openPackageDialog('view', item.id)} /><ActionButton label={`Edit package ${item.packageName}`} icon={Pencil} onClick={() => openPackageDialog('edit', item.id)} /><ActionButton label={`Delete package ${item.packageName}`} icon={Trash2} variant="danger" disabled={deletePackageMutation.isPending} onClick={() => { if (window.confirm(`Delete package ${item.packageName}?`)) { deletePackageMutation.mutate(item.id); } }} /></div></td></tr>)}
                          </tbody>
                        </table>
                      </div>
                    ) : (
                      <div className="mt-4">
                        <EmptyTableState message="No package rows exist for this outlet yet." action={<button type="button" className="button-primary inline-flex items-center gap-2 px-4 py-3" onClick={() => openPackageDialog('create')}><PlusCircle className="size-4" />Add package</button>} />
                      </div>
                    )}
                  </div>

                  <div className="panel p-6">
                    <div className="flex items-center justify-between gap-4">
                      <div><h3 className="text-lg font-semibold text-ink">Slot rates</h3><p className="mt-2 text-sm text-ink-soft">Spot-level or slot-based pricing rows for the selected outlet.</p></div>
                      <button type="button" className="button-primary inline-flex items-center gap-2 px-4 py-3" onClick={() => openSlotDialog('create')}>
                        <PlusCircle className="size-4" />
                        Add slot rate
                      </button>
                    </div>
                    <div className="mt-4 overflow-hidden rounded-[24px] border border-line">
                      <table className="w-full border-collapse text-sm">
                        <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Day group</th><th className="px-4 py-4">Time band</th><th className="px-4 py-4">Duration</th><th className="px-4 py-4">Rate</th><th className="px-4 py-4">Type</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                        <tbody>
                          {selectedPricing.slotRates.length > 0 ? selectedPricing.slotRates.map((item) => <tr key={item.id} className="border-t border-line"><td className="px-4 py-4 font-semibold text-ink">{titleize(item.dayGroup)}</td><td className="px-4 py-4 text-ink-soft">{item.startTime} - {item.endTime}</td><td className="px-4 py-4 text-ink-soft">{item.adDurationSeconds}s</td><td className="px-4 py-4 text-ink-soft">{fmtCurrency(item.rateZar)}</td><td className="px-4 py-4 text-ink-soft">{titleize(item.rateType)}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View slot rate ${item.dayGroup} ${item.startTime}-${item.endTime}`} icon={Eye} onClick={() => openSlotDialog('view', item.id)} /><ActionButton label={`Edit slot rate ${item.dayGroup} ${item.startTime}-${item.endTime}`} icon={Pencil} onClick={() => openSlotDialog('edit', item.id)} /><ActionButton label={`Delete slot rate ${item.dayGroup} ${item.startTime}-${item.endTime}`} icon={Trash2} variant="danger" disabled={deleteSlotMutation.isPending} onClick={() => { if (window.confirm(`Delete the ${item.dayGroup} ${item.startTime}-${item.endTime} slot rate?`)) { deleteSlotMutation.mutate(item.id); } }} /></div></td></tr>) : <tr><td colSpan={6} className="px-4 py-8"><EmptyTableState message="No slot-rate rows exist for this outlet yet." action={<button type="button" className="button-primary inline-flex items-center gap-2 px-4 py-3" onClick={() => openSlotDialog('create')}><PlusCircle className="size-4" />Add slot rate</button>} /></td></tr>}
                        </tbody>
                      </table>
                    </div>
                  </div>
                </>
              ) : outletPricingQuery.isLoading ? <div className="panel p-6 text-sm text-ink-soft">Loading outlet pricing...</div> : <EmptyTableState message="Select an outlet to load package and slot-rate rows." />}

              <div className="panel p-6">
                <div className="flex items-center justify-between gap-4">
                  <div>
                    <h3 className="text-lg font-semibold text-ink">Package band settings</h3>
                    <p className="mt-2 text-sm text-ink-soft">Manage the package catalog that drives public package selection, agent guidance, and planning rules.</p>
                  </div>
                  <button type="button" className="button-primary inline-flex items-center gap-2 px-4 py-3" onClick={() => openPackageSettingDialog('create')}>
                    <PlusCircle className="size-4" />
                    Add package band
                  </button>
                </div>
                <div className="mt-4 overflow-hidden rounded-[24px] border border-line">
                  <table className="w-full border-collapse text-sm">
                    <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Package band</th><th className="px-4 py-4">Budget range</th><th className="px-4 py-4">Planning signals</th><th className="px-4 py-4">Status</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                    <tbody>
                      {dashboard.packageSettings.length > 0 ? dashboard.packageSettings.map((band) => (
                        <tr key={band.id} className="border-t border-line">
                          <td className="px-4 py-4">
                            <p className="font-semibold text-ink">{band.name}</p>
                            <p className="text-xs text-ink-soft">{band.code} | sort {band.sortOrder}</p>
                            <p className="mt-1 text-xs text-ink-soft">{band.quickBenefit}</p>
                          </td>
                          <td className="px-4 py-4 text-ink-soft">
                            <p>{fmtCurrency(band.minBudget)} - {fmtCurrency(band.maxBudget)}</p>
                            <p className="text-xs">Recommended {fmtCurrency(band.recommendedSpend)}</p>
                          </td>
                          <td className="px-4 py-4 text-ink-soft">
                            <p>{band.packagePurpose}</p>
                            <p className="text-xs">Radio {titleize(band.includeRadio)} | TV {titleize(band.includeTv)} | {band.leadTime}</p>
                          </td>
                          <td className="px-4 py-4 text-ink-soft">
                            <div>{band.isActive ? 'Active' : 'Inactive'}</div>
                            <div className="text-xs">{band.isRecommended ? 'Recommended package' : 'Standard package'}</div>
                          </td>
                          <td className="px-4 py-4">
                            <div className="flex justify-end gap-2">
                              <ActionButton label={`View package band ${band.name}`} icon={Eye} onClick={() => openPackageSettingDialog('view', band.id)} />
                              <ActionButton label={`Edit package band ${band.name}`} icon={Pencil} onClick={() => openPackageSettingDialog('edit', band.id)} />
                              <ActionButton label={`Delete package band ${band.name}`} icon={Trash2} variant="danger" disabled={deletePackageSettingMutation.isPending} onClick={() => { if (window.confirm(`Delete package band ${band.name}? Package bands linked to campaigns or orders cannot be removed.`)) { deletePackageSettingMutation.mutate(band.id); } }} />
                            </div>
                          </td>
                        </tr>
                      )) : (
                        <tr><td colSpan={5} className="px-4 py-8"><EmptyTableState message="No package bands exist yet." action={<button type="button" className="button-primary inline-flex items-center gap-2 px-4 py-3" onClick={() => openPackageSettingDialog('create')}><PlusCircle className="size-4" />Add package band</button>} /></td></tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

              {packageSettingDialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="max-h-[92vh] w-full max-w-5xl overflow-y-auto rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{packageSettingDialog.mode === 'create' ? 'Add package band' : packageSettingDialog.mode === 'view' ? 'View package band' : 'Edit package band'}</h3><button type="button" className="button-secondary p-3" onClick={() => setPackageSettingDialog(null)}><X className="size-4" /></button></div>
                    {packageSettingDialog.mode === 'view' && selectedPackageSetting ? <ReadOnlyNotice label={`Viewing ${selectedPackageSetting.name}. Switch to edit mode when you want to change catalog budgets, messaging, or package guidance.`} /> : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                      <input disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Code" value={packageSettingForm.code} onChange={(event) => setPackageSettingForm((current) => ({ ...current, code: event.target.value }))} />
                      <input disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Name" value={packageSettingForm.name} onChange={(event) => setPackageSettingForm((current) => ({ ...current, name: event.target.value }))} />
                      <input disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Sort order" value={packageSettingForm.sortOrder} onChange={(event) => setPackageSettingForm((current) => ({ ...current, sortOrder: Number(event.target.value) }))} />
                      <input disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Min budget ZAR" value={packageSettingForm.minBudget} onChange={(event) => setPackageSettingForm((current) => ({ ...current, minBudget: Number(event.target.value) }))} />
                      <input disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Max budget ZAR" value={packageSettingForm.maxBudget} onChange={(event) => setPackageSettingForm((current) => ({ ...current, maxBudget: Number(event.target.value) }))} />
                      <input disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Recommended spend ZAR" value={packageSettingForm.recommendedSpend ?? ''} onChange={(event) => setPackageSettingForm((current) => ({ ...current, recommendedSpend: parseNumber(event.target.value) }))} />
                      <select disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" value={packageSettingForm.includeRadio} onChange={(event) => setPackageSettingForm((current) => ({ ...current, includeRadio: event.target.value }))}>
                        <option value="yes">Radio required</option>
                        <option value="optional">Radio optional</option>
                        <option value="no">Radio excluded</option>
                      </select>
                      <select disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" value={packageSettingForm.includeTv} onChange={(event) => setPackageSettingForm((current) => ({ ...current, includeTv: event.target.value }))}>
                        <option value="yes">TV required</option>
                        <option value="optional">TV optional</option>
                        <option value="no">TV excluded</option>
                      </select>
                      <input disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Lead time" value={packageSettingForm.leadTime} onChange={(event) => setPackageSettingForm((current) => ({ ...current, leadTime: event.target.value }))} />
                    </div>
                    <div className="mt-4 grid gap-4 md:grid-cols-2">
                      <input disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Audience fit" value={packageSettingForm.audienceFit} onChange={(event) => setPackageSettingForm((current) => ({ ...current, audienceFit: event.target.value }))} />
                      <input disabled={packageSettingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Quick benefit" value={packageSettingForm.quickBenefit} onChange={(event) => setPackageSettingForm((current) => ({ ...current, quickBenefit: event.target.value }))} />
                    </div>
                    <textarea disabled={packageSettingDialog.mode === 'view'} className="input-base mt-4 min-h-[100px] disabled:bg-slate-50" placeholder="Description" value={packageSettingForm.description} onChange={(event) => setPackageSettingForm((current) => ({ ...current, description: event.target.value }))} />
                    <textarea disabled={packageSettingDialog.mode === 'view'} className="input-base mt-4 min-h-[100px] disabled:bg-slate-50" placeholder="Package purpose" value={packageSettingForm.packagePurpose} onChange={(event) => setPackageSettingForm((current) => ({ ...current, packagePurpose: event.target.value }))} />
                    <textarea disabled={packageSettingDialog.mode === 'view'} className="input-base mt-4 min-h-[100px] disabled:bg-slate-50" placeholder="Benefits, one per line" value={packageSettingForm.benefits.join('\n')} onChange={(event) => setPackageSettingForm((current) => ({ ...current, benefits: event.target.value.split('\n').map((item) => item.trim()).filter(Boolean) }))} />
                    <div className="mt-4 flex flex-wrap items-center gap-5 text-sm text-ink-soft">
                      <label className="inline-flex items-center gap-2"><input disabled={packageSettingDialog.mode === 'view'} type="checkbox" checked={packageSettingForm.isActive} onChange={(event) => setPackageSettingForm((current) => ({ ...current, isActive: event.target.checked }))} /> Active</label>
                      <label className="inline-flex items-center gap-2"><input disabled={packageSettingDialog.mode === 'view'} type="checkbox" checked={packageSettingForm.isRecommended} onChange={(event) => setPackageSettingForm((current) => ({ ...current, isRecommended: event.target.checked }))} /> Recommended package</label>
                    </div>
                    {!packageSettingFormIsValid ? <p className="mt-3 text-sm text-rose-600">Code, name, lead time, and a valid budget range are required before saving.</p> : null}
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => setPackageSettingDialog(null)}>Close</button>
                      {packageSettingDialog.mode === 'view' && selectedPackageSetting ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openPackageSettingDialog('edit', selectedPackageSetting.id)}>Edit package band</button> : null}
                      {packageSettingDialog.mode === 'edit' && selectedPackageSetting ? <button type="button" className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50" disabled={deletePackageSettingMutation.isPending} onClick={() => { if (window.confirm(`Delete package band ${selectedPackageSetting.name}? Package bands linked to campaigns or orders cannot be removed.`)) { deletePackageSettingMutation.mutate(selectedPackageSetting.id); } }}>Delete package band</button> : null}
                      {packageSettingDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" disabled={!packageSettingFormIsValid || createPackageSettingMutation.isPending} onClick={() => createPackageSettingMutation.mutate()}>Save package band</button> : null}
                      {packageSettingDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" disabled={!packageSettingFormIsValid || updatePackageSettingMutation.isPending} onClick={() => updatePackageSettingMutation.mutate()}>Update package band</button> : null}
                    </div>
                  </div>
                </div>
              ) : null}

              {packageDialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-4xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{packageDialog.mode === 'create' ? 'Add pricing package' : packageDialog.mode === 'view' ? 'View pricing package' : 'Edit pricing package'}</h3><button type="button" className="button-secondary p-3" onClick={() => setPackageDialog(null)}><X className="size-4" /></button></div>
                    {packageDialog.mode === 'view' && selectedPackage ? <ReadOnlyNotice label={`Viewing ${selectedPackage.packageName}. Use the edit icon or the button below to switch this record into edit mode.`} /> : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Package name" value={packageForm.packageName} onChange={(event) => setPackageForm((current) => ({ ...current, packageName: event.target.value }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Package type" value={packageForm.packageType ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, packageType: event.target.value }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Source name" value={packageForm.sourceName ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, sourceName: event.target.value }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Investment ZAR" value={packageForm.investmentZar ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, investmentZar: parseNumber(event.target.value) }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Cost per month ZAR" value={packageForm.costPerMonthZar ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, costPerMonthZar: parseNumber(event.target.value) }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Value ZAR" value={packageForm.valueZar ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, valueZar: parseNumber(event.target.value) }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Exposure count" value={packageForm.exposureCount ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, exposureCount: parseNumber(event.target.value) }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Monthly exposure count" value={packageForm.monthlyExposureCount ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, monthlyExposureCount: parseNumber(event.target.value) }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="date" value={packageForm.sourceDate ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, sourceDate: event.target.value }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Duration months" value={packageForm.durationMonths ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, durationMonths: parseNumber(event.target.value) }))} />
                      <input disabled={packageDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Duration weeks" value={packageForm.durationWeeks ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, durationWeeks: parseNumber(event.target.value) }))} />
                      <label className="inline-flex items-center gap-2 rounded-full border border-line px-4 py-3 text-sm text-ink-soft"><input disabled={packageDialog.mode === 'view'} type="checkbox" checked={packageForm.isActive} onChange={(event) => setPackageForm((current) => ({ ...current, isActive: event.target.checked }))} /> Active</label>
                    </div>
                    <textarea disabled={packageDialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Notes" value={packageForm.notes ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, notes: event.target.value }))} />
                    {!packageFormIsValid ? <p className="mt-3 text-sm text-rose-600">Package name is required before saving.</p> : null}
                    <div className="mt-6 flex justify-end gap-3"><button type="button" className="button-secondary px-5 py-3" onClick={() => setPackageDialog(null)}>Close</button>{packageDialog.mode === 'view' && selectedPackage ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openPackageDialog('edit', selectedPackage.id)}>Edit package</button> : null}{packageDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" disabled={!packageFormIsValid || createPackageMutation.isPending} onClick={() => createPackageMutation.mutate()}>Save package</button> : packageDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" disabled={!packageFormIsValid || updatePackageMutation.isPending} onClick={() => updatePackageMutation.mutate()}>Update package</button> : null}</div>
                  </div>
                </div>
              ) : null}

              {slotDialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-3xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{slotDialog.mode === 'create' ? 'Add slot rate' : slotDialog.mode === 'view' ? 'View slot rate' : 'Edit slot rate'}</h3><button type="button" className="button-secondary p-3" onClick={() => setSlotDialog(null)}><X className="size-4" /></button></div>
                    {slotDialog.mode === 'view' && selectedSlotRate ? <ReadOnlyNotice label={`Viewing ${titleize(selectedSlotRate.dayGroup)} ${selectedSlotRate.startTime}-${selectedSlotRate.endTime}. Use edit when you want to change this live row.`} /> : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2">
                      <input disabled={slotDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Day group" value={slotForm.dayGroup} onChange={(event) => setSlotForm((current) => ({ ...current, dayGroup: event.target.value }))} />
                      <input disabled={slotDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Rate type" value={slotForm.rateType} onChange={(event) => setSlotForm((current) => ({ ...current, rateType: event.target.value }))} />
                      <input disabled={slotDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="time" value={slotForm.startTime} onChange={(event) => setSlotForm((current) => ({ ...current, startTime: event.target.value }))} />
                      <input disabled={slotDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="time" value={slotForm.endTime} onChange={(event) => setSlotForm((current) => ({ ...current, endTime: event.target.value }))} />
                      <input disabled={slotDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Ad duration seconds" value={slotForm.adDurationSeconds} onChange={(event) => setSlotForm((current) => ({ ...current, adDurationSeconds: Number(event.target.value) }))} />
                      <input disabled={slotDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Rate ZAR" value={slotForm.rateZar} onChange={(event) => setSlotForm((current) => ({ ...current, rateZar: Number(event.target.value) }))} />
                      <input disabled={slotDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Source name" value={slotForm.sourceName ?? ''} onChange={(event) => setSlotForm((current) => ({ ...current, sourceName: event.target.value }))} />
                      <input disabled={slotDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="date" value={slotForm.sourceDate ?? ''} onChange={(event) => setSlotForm((current) => ({ ...current, sourceDate: event.target.value }))} />
                      <label className="inline-flex items-center gap-2 rounded-full border border-line px-4 py-3 text-sm text-ink-soft"><input disabled={slotDialog.mode === 'view'} type="checkbox" checked={slotForm.isActive} onChange={(event) => setSlotForm((current) => ({ ...current, isActive: event.target.checked }))} /> Active</label>
                    </div>
                    {!slotFormIsValid ? <p className="mt-3 text-sm text-rose-600">Day group, start time, end time, and rate are required before saving.</p> : null}
                    <div className="mt-6 flex justify-end gap-3"><button type="button" className="button-secondary px-5 py-3" onClick={() => setSlotDialog(null)}>Close</button>{slotDialog.mode === 'view' && selectedSlotRate ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openSlotDialog('edit', selectedSlotRate.id)}>Edit slot rate</button> : null}{slotDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" disabled={!slotFormIsValid || createSlotMutation.isPending} onClick={() => createSlotMutation.mutate()}>Save slot rate</button> : slotDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" disabled={!slotFormIsValid || updateSlotMutation.isPending} onClick={() => updateSlotMutation.mutate()}>Update slot rate</button> : null}</div>
                  </div>
                </div>
              ) : null}
            </section>
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

export function AdminImportsPage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const rateCardFileInputRef = useRef<HTMLInputElement | null>(null);
  const [importDialog, setImportDialog] = useState<{ mode: 'view' | 'edit'; sourceFile: string } | null>(null);
  const [rateCardForm, setRateCardForm] = useState({
    channel: 'radio',
    supplierOrStation: '',
    documentTitle: '',
    notes: '',
    file: null as File | null,
  });
  const [uploadAttempted, setUploadAttempted] = useState(false);
  const [importForm, setImportForm] = useState({
    channel: 'radio',
    supplierOrStation: '',
    documentTitle: '',
    notes: '',
  });

  const uploadRateCardMutation = useMutation({
    mutationFn: () => {
      setUploadAttempted(true);
      if (!rateCardForm.file) {
        throw new Error('Choose a file to upload first.');
      }
      return advertifiedApi.uploadAdminRateCard({
        channel: rateCardForm.channel,
        supplierOrStation: rateCardForm.supplierOrStation || undefined,
        documentTitle: rateCardForm.documentTitle || undefined,
        notes: rateCardForm.notes || undefined,
        file: rateCardForm.file,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setUploadAttempted(false);
      setRateCardForm({
        channel: 'radio',
        supplierOrStation: '',
        documentTitle: '',
        notes: '',
        file: null,
      });
      pushToast({ title: 'Rate card uploaded.', description: 'The file was stored and added to the live import manifest.' });
    },
    onError: (error) => pushToast({ title: 'Could not upload rate card.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });
  const uploadFormIsValid = hasText(rateCardForm.channel) && !!rateCardForm.file;
  const importFormIsValid = hasText(importForm.channel);

  const updateRateCardMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminRateCard(importDialog?.sourceFile ?? '', {
      channel: importForm.channel,
      supplierOrStation: importForm.supplierOrStation || undefined,
      documentTitle: importForm.documentTitle || undefined,
      notes: importForm.notes || undefined,
    }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setImportDialog(null);
      pushToast({ title: 'Rate card updated.', description: 'The import metadata was updated successfully.' });
    },
    onError: (error) => pushToast({ title: 'Could not update rate card.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteRateCardMutation = useMutation({
    mutationFn: (sourceFile: string) => advertifiedApi.deleteAdminRateCard(sourceFile),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setImportDialog(null);
      pushToast({ title: 'Rate card deleted.', description: 'The import record and stored file were removed.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete rate card.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const selectedImport = importDialog ? dashboard.recentImports.find((item) => item.sourceFile === importDialog.sourceFile) ?? null : null;
        const openImportDialog = (mode: 'view' | 'edit', item: (typeof dashboard.recentImports)[number]) => {
          setImportForm({
            channel: item.channel,
            supplierOrStation: item.supplierOrStation ?? '',
            documentTitle: item.documentTitle ?? '',
            notes: item.notes ?? '',
          });
          setImportDialog({ mode, sourceFile: item.sourceFile });
        };

        return (
        <AdminPageShell title="Imports and rate cards" description="Upload new source files and review the live import manifest and document metadata already in the system.">
          <section className="space-y-6">
            <div className="panel p-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h3 className="text-lg font-semibold text-ink">Upload rate card</h3>
                  <p className="mt-2 text-sm text-ink-soft">Store a real rate-card file, create the import manifest record, and attach its metadata in the live import tables.</p>
                </div>
                <span className="pill"><Upload className="mr-2 inline size-4" />Live upload</span>
              </div>
              <div className="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <select className="input-base" value={rateCardForm.channel} onChange={(event) => setRateCardForm((current) => ({ ...current, channel: event.target.value }))}>
                  <option value="radio">Radio</option>
                  <option value="tv">TV</option>
                  <option value="ooh">OOH</option>
                  <option value="digital">Digital</option>
                </select>
                <input className="input-base" placeholder="Supplier or station" value={rateCardForm.supplierOrStation} onChange={(event) => setRateCardForm((current) => ({ ...current, supplierOrStation: event.target.value }))} />
                <input className="input-base" placeholder="Document title" value={rateCardForm.documentTitle} onChange={(event) => setRateCardForm((current) => ({ ...current, documentTitle: event.target.value }))} />
                <div className="flex min-h-[60px] items-center justify-between gap-3 rounded-[20px] border border-line bg-white px-4 py-3">
                  <div className="min-w-0">
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Rate-card file</p>
                    <p className="mt-1 truncate text-sm text-ink">{rateCardForm.file?.name ?? 'No file selected yet'}</p>
                  </div>
                  <input
                    ref={rateCardFileInputRef}
                    type="file"
                    className="hidden"
                    onChange={(event) => setRateCardForm((current) => ({ ...current, file: event.target.files?.[0] ?? null }))}
                  />
                  <button type="button" className="button-secondary shrink-0 px-4 py-2" onClick={() => rateCardFileInputRef.current?.click()}>
                    {rateCardForm.file ? 'Change file' : 'Choose file'}
                  </button>
                </div>
              </div>
              <textarea className="input-base mt-4 min-h-[100px]" placeholder="Notes" value={rateCardForm.notes} onChange={(event) => setRateCardForm((current) => ({ ...current, notes: event.target.value }))} />
              {uploadAttempted && !uploadFormIsValid ? <p className="mt-3 text-sm text-rose-600">Choose a channel and file before uploading a rate card.</p> : <p className="mt-3 text-sm text-ink-soft">Upload a source file once the channel and file are ready.</p>}
              <div className="mt-5 flex justify-end">
                <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60" onClick={() => uploadRateCardMutation.mutate()} disabled={uploadRateCardMutation.isPending || !uploadFormIsValid}>
                  <Upload className="size-4" />
                  Upload rate card
                </button>
              </div>
            </div>
            <div className="overflow-hidden rounded-[28px] border border-line">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Document</th><th className="px-4 py-4">Channel</th><th className="px-4 py-4">Supplier / Station</th><th className="px-4 py-4">Pages</th><th className="px-4 py-4">Imported</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                <tbody>
                  {dashboard.recentImports.map((item) => <tr key={`${item.sourceFile}-${item.importedAt}`} className="border-t border-line"><td className="px-4 py-4"><p className="font-semibold text-ink">{item.documentTitle ?? item.sourceFile}</p><p className="text-xs text-ink-soft">{item.sourceFile}</p></td><td className="px-4 py-4 text-ink-soft">{titleize(item.channel)}</td><td className="px-4 py-4 text-ink-soft">{item.supplierOrStation ?? 'Not classified yet'}</td><td className="px-4 py-4 text-ink-soft">{item.pageCount ?? 0}</td><td className="px-4 py-4 text-ink-soft">{fmtDate(item.importedAt)}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View ${item.documentTitle ?? item.sourceFile}`} icon={Eye} onClick={() => openImportDialog('view', item)} /><ActionButton label={`Edit ${item.documentTitle ?? item.sourceFile}`} icon={Pencil} onClick={() => openImportDialog('edit', item)} /><ActionButton label={`Delete ${item.documentTitle ?? item.sourceFile}`} icon={Trash2} variant="danger" disabled={deleteRateCardMutation.isPending} onClick={() => { if (window.confirm(`Delete ${item.documentTitle ?? item.sourceFile}? This removes the stored file and import metadata.`)) { deleteRateCardMutation.mutate(item.sourceFile); } }} /></div></td></tr>)}
                </tbody>
              </table>
            </div>

            {importDialog && selectedImport ? (
              <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                <div className="w-full max-w-4xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                  <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{importDialog.mode === 'view' ? 'View import' : 'Edit import'}</h3><button type="button" className="button-secondary p-3" onClick={() => setImportDialog(null)}><X className="size-4" /></button></div>
                  {importDialog.mode === 'view' ? <ReadOnlyNotice label="This import is open in view mode. Switch to edit mode to change channel or document metadata." /> : null}
                  <div className="mt-4 flex flex-wrap items-center gap-3 text-sm text-ink-soft"><span className="pill">{selectedImport.sourceFile}</span><span>Imported {fmtDate(selectedImport.importedAt)}</span></div>
                  <div className="mt-6 grid gap-4 md:grid-cols-2">
                    <select disabled={importDialog.mode === 'view'} className="input-base disabled:bg-slate-50" value={importForm.channel} onChange={(event) => setImportForm((current) => ({ ...current, channel: event.target.value }))}>
                      <option value="radio">Radio</option>
                      <option value="tv">TV</option>
                      <option value="ooh">OOH</option>
                      <option value="digital">Digital</option>
                    </select>
                    <input disabled={importDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Supplier or station" value={importForm.supplierOrStation} onChange={(event) => setImportForm((current) => ({ ...current, supplierOrStation: event.target.value }))} />
                    <input disabled={importDialog.mode === 'view'} className="input-base md:col-span-2 disabled:bg-slate-50" placeholder="Document title" value={importForm.documentTitle} onChange={(event) => setImportForm((current) => ({ ...current, documentTitle: event.target.value }))} />
                  </div>
                  <textarea disabled={importDialog.mode === 'view'} className="input-base mt-4 min-h-[120px] disabled:bg-slate-50" placeholder="Notes" value={importForm.notes} onChange={(event) => setImportForm((current) => ({ ...current, notes: event.target.value }))} />
                    {!importFormIsValid ? <p className="mt-3 text-sm text-rose-600">Channel is required before you can update this import.</p> : null}
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => setImportDialog(null)}>Close</button>
                      {importDialog.mode === 'view' ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openImportDialog('edit', selectedImport)}>Edit import</button> : null}
                      {importDialog.mode === 'edit' ? <button type="button" className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50" disabled={deleteRateCardMutation.isPending} onClick={() => { if (window.confirm(`Delete ${selectedImport.documentTitle ?? selectedImport.sourceFile}? This removes the stored file and import metadata.`)) { deleteRateCardMutation.mutate(selectedImport.sourceFile); } }}>Delete import</button> : null}
                    {importDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" onClick={() => updateRateCardMutation.mutate()} disabled={updateRateCardMutation.isPending || !importFormIsValid}>Update import</button> : null}
                    </div>
                </div>
              </div>
            ) : null}
          </section>
        </AdminPageShell>
      )}}
    </AdminQueryBoundary>
  );
}

export function AdminHealthPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => (
        <AdminPageShell title="Data quality and health" description="Track weak outlets, missing pricing, and live catalog issues so the planning engine stays reliable.">
          <section className="space-y-6">
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {[['Strong', dashboard.health.strongCount], ['Mixed', dashboard.health.mixedCount], ['Weak unpriced', dashboard.health.weakUnpricedCount], ['Weak no inventory', dashboard.health.weakNoInventoryCount]].map(([label, value]) => <div key={String(label)} className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{label}</p><p className="mt-4 text-4xl font-semibold text-ink">{value}</p></div>)}
            </div>
            <div className="rounded-[28px] border border-line bg-white p-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h3 className="text-lg font-semibold text-ink">Fix queue</h3>
                  <p className="mt-2 text-sm text-ink-soft">Live issues surfaced from outlet health signals and import quality checks.</p>
                </div>
                <div className="rounded-2xl bg-brand-soft p-3 text-brand"><ShieldCheck className="size-5" /></div>
              </div>
              <div className="mt-4 overflow-hidden rounded-[24px] border border-line">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Outlet</th><th className="px-4 py-4">Issue</th><th className="px-4 py-4">Impact</th><th className="px-4 py-4">Suggested fix</th><th className="px-4 py-4 text-right">Action</th></tr></thead>
                  <tbody>
                    {dashboard.healthIssues.map((item) => {
                      const goesToPricing = item.issue.toLowerCase().includes('pricing') || item.issue.toLowerCase().includes('inventory');
                      const href = goesToPricing ? `/admin/pricing?outlet=${encodeURIComponent(item.outletCode)}` : `/admin/stations?outlet=${encodeURIComponent(item.outletCode)}&mode=edit`;

                      return (
                        <tr key={`${item.outletCode}-${item.issue}`} className="border-t border-line">
                          <td className="px-4 py-4 font-semibold text-ink">{item.outletName}</td>
                          <td className="px-4 py-4 text-ink-soft">{item.issue}</td>
                          <td className="px-4 py-4 text-ink-soft">{item.impact}</td>
                          <td className="px-4 py-4 text-ink-soft">{item.suggestedFix}</td>
                          <td className="px-4 py-4">
                            <div className="flex justify-end">
                              <Link to={href} className="button-secondary inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold">
                                <Pencil className="size-4" />
                                Apply fix
                              </Link>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </section>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}

export function AdminGeographyPage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [selectedAreaCodeState, setSelectedAreaCodeState] = useState('');
  const [areaDialog, setAreaDialog] = useState<{ mode: 'create' | 'view' | 'edit' } | null>(null);
  const [mappingDialog, setMappingDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [areaForm, setAreaForm] = useState<AdminCreateGeographyInput & AdminUpdateGeographyInput>({
    code: '',
    label: '',
    description: '',
    fallbackLocations: [],
    sortOrder: 100,
    isActive: true,
  });
  const [fallbackLocationsInput, setFallbackLocationsInput] = useState('');
  const [mappingForm, setMappingForm] = useState<AdminUpsertGeographyMappingInput>({
    province: '',
    city: '',
    stationOrChannelName: '',
  });
  const selectedAreaCode = selectedAreaCodeState || query.data?.areas?.[0]?.code || '';

  const geographyDetailQuery = useQuery({
    queryKey: ['admin-geography', selectedAreaCode],
    queryFn: () => advertifiedApi.getAdminGeography(selectedAreaCode),
    enabled: !!selectedAreaCode,
  });

  const areaPayloadIsValid = hasText(areaForm.code) && hasText(areaForm.label);
  const mappingPayloadIsValid = hasText(mappingForm.province ?? '') || hasText(mappingForm.city ?? '') || hasText(mappingForm.stationOrChannelName ?? '');

  const createAreaMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminGeography(areaForm),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setSelectedAreaCodeState(result.code);
      setAreaDialog(null);
      pushToast({ title: 'Area created.', description: 'The geography area is now available to package previews and planning flows.' });
    },
    onError: (error) => pushToast({ title: 'Could not create area.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updateAreaMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminGeography(selectedAreaCode, areaForm),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      await queryClient.invalidateQueries({ queryKey: ['admin-geography', selectedAreaCode] });
      setSelectedAreaCodeState(result.code);
      setAreaDialog(null);
      pushToast({ title: 'Area updated.', description: 'The geography area details were saved successfully.' });
    },
    onError: (error) => pushToast({ title: 'Could not update area.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteAreaMutation = useMutation({
    mutationFn: (code: string) => advertifiedApi.deleteAdminGeography(code),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setSelectedAreaCodeState('');
      setAreaDialog(null);
      pushToast({ title: 'Area deleted.', description: 'The geography area and its mapping rows were removed.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete area.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const createMappingMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminGeographyMapping(selectedAreaCode, mappingForm),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      await queryClient.invalidateQueries({ queryKey: ['admin-geography', selectedAreaCode] });
      setMappingDialog(null);
      pushToast({ title: 'Mapping created.', description: 'The region-cluster mapping was added successfully.' });
    },
    onError: (error) => pushToast({ title: 'Could not create mapping.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updateMappingMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminGeographyMapping(selectedAreaCode, mappingDialog?.id ?? '', mappingForm),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      await queryClient.invalidateQueries({ queryKey: ['admin-geography', selectedAreaCode] });
      setMappingDialog(null);
      pushToast({ title: 'Mapping updated.', description: 'The region-cluster mapping was saved successfully.' });
    },
    onError: (error) => pushToast({ title: 'Could not update mapping.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteMappingMutation = useMutation({
    mutationFn: (id: string) => advertifiedApi.deleteAdminGeographyMapping(selectedAreaCode, id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      await queryClient.invalidateQueries({ queryKey: ['admin-geography', selectedAreaCode] });
      setMappingDialog(null);
      pushToast({ title: 'Mapping deleted.', description: 'The region-cluster mapping row was removed.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete mapping.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const selectedDetail = geographyDetailQuery.data;
        const selectedMapping = mappingDialog?.id && selectedDetail
          ? selectedDetail.mappings.find((item) => item.id === mappingDialog.id) ?? null
          : null;
        const openAreaDialog = (mode: 'create' | 'view' | 'edit', detail?: AdminGeographyDetail | null) => {
          if (mode === 'create' || !detail) {
            setAreaForm({ code: '', label: '', description: '', fallbackLocations: [], sortOrder: 100, isActive: true });
            setFallbackLocationsInput('');
            setAreaDialog({ mode });
            return;
          }

          setAreaForm({
            code: detail.code,
            label: detail.label,
            description: detail.description,
            fallbackLocations: detail.fallbackLocations,
            sortOrder: detail.sortOrder,
            isActive: detail.isActive,
          });
          setFallbackLocationsInput(detail.fallbackLocations.join(', '));
          setAreaDialog({ mode });
        };
        const openMappingDialog = (mode: 'create' | 'view' | 'edit', mapping?: AdminGeographyDetail['mappings'][number] | null) => {
          if (mode === 'create' || !mapping) {
            setMappingForm({ province: '', city: '', stationOrChannelName: '' });
            setMappingDialog({ mode });
            return;
          }

          setMappingForm({
            province: mapping.province ?? '',
            city: mapping.city ?? '',
            stationOrChannelName: mapping.stationOrChannelName ?? '',
          });
          setMappingDialog({ mode, id: mapping.id });
        };

        return (
          <AdminPageShell title="Geography mapping" description="Manage package area profiles and the live region-cluster mappings that support planning coverage.">
            <section className="space-y-6">
              <div className="panel p-6">
                <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                  <div>
                    <h3 className="text-lg font-semibold text-ink">Area profiles</h3>
                    <p className="mt-2 text-sm text-ink-soft">Create and maintain package-area profiles, fallback locations, and the cluster mappings used in preview and planning logic.</p>
                  </div>
                  <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3" onClick={() => openAreaDialog('create')}>
                    <PlusCircle className="size-4" />
                    Add area
                  </button>
                </div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Area</th><th className="px-4 py-4">Code</th><th className="px-4 py-4">Description</th><th className="px-4 py-4">Mapped records</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                  <tbody>
                    {dashboard.areas.map((area) => <tr key={area.code} className="border-t border-line"><td className="px-4 py-4 font-semibold text-ink">{area.label}</td><td className="px-4 py-4 text-ink-soft">{area.code}</td><td className="px-4 py-4 text-ink-soft">{area.description}</td><td className="px-4 py-4 text-ink-soft">{area.mappingCount}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View ${area.label}`} icon={Eye} onClick={async () => { setSelectedAreaCodeState(area.code); const detail = await queryClient.fetchQuery({ queryKey: ['admin-geography', area.code], queryFn: () => advertifiedApi.getAdminGeography(area.code) }); openAreaDialog('view', detail); }} /><ActionButton label={`Edit ${area.label}`} icon={Pencil} onClick={async () => { setSelectedAreaCodeState(area.code); const detail = await queryClient.fetchQuery({ queryKey: ['admin-geography', area.code], queryFn: () => advertifiedApi.getAdminGeography(area.code) }); openAreaDialog('edit', detail); }} /><ActionButton label={`Delete ${area.label}`} icon={Trash2} variant="danger" disabled={deleteAreaMutation.isPending} onClick={() => { if (window.confirm(`Delete ${area.label}? This also removes its cluster mappings.`)) { deleteAreaMutation.mutate(area.code); } }} /></div></td></tr>)}
                  </tbody>
                </table>
              </div>

              {selectedDetail ? (
                <div className="panel p-6">
                  <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                    <div>
                      <h3 className="text-lg font-semibold text-ink">{selectedDetail.label}</h3>
                      <p className="mt-2 text-sm text-ink-soft">{selectedDetail.description || 'No description configured.'}</p>
                    </div>
                    <div className="flex flex-wrap gap-3">
                      <span className="pill">Code: {selectedDetail.code}</span>
                      <span className="pill">{selectedDetail.isActive ? 'Active' : 'Inactive'}</span>
                      <button type="button" className="button-primary inline-flex items-center gap-2 px-4 py-3" onClick={() => openMappingDialog('create')}>
                        <PlusCircle className="size-4" />
                        Add mapping
                      </button>
                    </div>
                  </div>
                  <div className="mt-4 grid gap-4 md:grid-cols-2">
                    <div className="rounded-[24px] border border-line bg-slate-50 p-4">
                      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Fallback locations</p>
                      <p className="mt-3 text-sm text-ink-soft">{selectedDetail.fallbackLocations.join(', ') || 'No fallback locations configured.'}</p>
                    </div>
                    <div className="rounded-[24px] border border-line bg-slate-50 p-4">
                      <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Sort order</p>
                      <p className="mt-3 text-sm text-ink-soft">{selectedDetail.sortOrder}</p>
                    </div>
                  </div>
                  <div className="mt-6 overflow-hidden rounded-[24px] border border-line">
                    <table className="w-full border-collapse text-sm">
                      <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Province</th><th className="px-4 py-4">City</th><th className="px-4 py-4">Station / channel</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                      <tbody>
                        {selectedDetail.mappings.length > 0 ? selectedDetail.mappings.map((mapping) => <tr key={mapping.id} className="border-t border-line"><td className="px-4 py-4 text-ink-soft">{mapping.province ?? 'Not set'}</td><td className="px-4 py-4 text-ink-soft">{mapping.city ?? 'Not set'}</td><td className="px-4 py-4 text-ink-soft">{mapping.stationOrChannelName ?? 'Not set'}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label="View mapping" icon={Eye} onClick={() => openMappingDialog('view', mapping)} /><ActionButton label="Edit mapping" icon={Pencil} onClick={() => openMappingDialog('edit', mapping)} /><ActionButton label="Delete mapping" icon={Trash2} variant="danger" disabled={deleteMappingMutation.isPending} onClick={() => { if (window.confirm('Delete this mapping row?')) { deleteMappingMutation.mutate(mapping.id); } }} /></div></td></tr>) : <tr><td colSpan={4} className="px-4 py-8"><EmptyTableState message="No region-cluster mappings have been configured for this area yet." action={<button type="button" className="button-primary inline-flex items-center gap-2 px-4 py-3" onClick={() => openMappingDialog('create')}><PlusCircle className="size-4" />Add mapping</button>} /></td></tr>}
                      </tbody>
                    </table>
                  </div>
                </div>
              ) : geographyDetailQuery.isLoading ? <div className="panel p-6 text-sm text-ink-soft">Loading area details...</div> : null}

              {areaDialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-4xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{areaDialog.mode === 'create' ? 'Add area' : areaDialog.mode === 'view' ? 'View area' : 'Edit area'}</h3><button type="button" className="button-secondary p-3" onClick={() => setAreaDialog(null)}><X className="size-4" /></button></div>
                    {areaDialog.mode === 'view' ? <ReadOnlyNotice label="This area is open in view mode. Switch to edit mode to change profile details or fallback locations." /> : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2">
                      <input disabled={areaDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Area code" value={areaForm.code} onChange={(event) => setAreaForm((current) => ({ ...current, code: event.target.value }))} />
                      <input disabled={areaDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Area label" value={areaForm.label} onChange={(event) => setAreaForm((current) => ({ ...current, label: event.target.value }))} />
                      <input disabled={areaDialog.mode === 'view'} className="input-base md:col-span-2 disabled:bg-slate-50" placeholder="Fallback locations, comma separated" value={fallbackLocationsInput} onChange={(event) => { setFallbackLocationsInput(event.target.value); setAreaForm((current) => ({ ...current, fallbackLocations: splitList(event.target.value) })); }} />
                      <input disabled={areaDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Sort order" value={areaForm.sortOrder} onChange={(event) => setAreaForm((current) => ({ ...current, sortOrder: Number(event.target.value) }))} />
                      <label className="inline-flex items-center gap-2 rounded-full border border-line px-4 py-3 text-sm text-ink-soft"><input disabled={areaDialog.mode === 'view'} type="checkbox" checked={areaForm.isActive} onChange={(event) => setAreaForm((current) => ({ ...current, isActive: event.target.checked }))} /> Active</label>
                    </div>
                    <textarea disabled={areaDialog.mode === 'view'} className="input-base mt-4 min-h-[120px] disabled:bg-slate-50" placeholder="Description" value={areaForm.description} onChange={(event) => setAreaForm((current) => ({ ...current, description: event.target.value }))} />
                    {!areaPayloadIsValid ? <p className="mt-3 text-sm text-rose-600">Area code and label are required before you can save.</p> : null}
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => setAreaDialog(null)}>Close</button>
                      {areaDialog.mode === 'view' && selectedDetail ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openAreaDialog('edit', selectedDetail)}>Edit area</button> : null}
                      {areaDialog.mode === 'edit' ? <button type="button" className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50" disabled={deleteAreaMutation.isPending} onClick={() => { if (window.confirm(`Delete ${areaForm.label || areaForm.code}? This also removes its cluster mappings.`)) { deleteAreaMutation.mutate(selectedAreaCode); } }}>Delete area</button> : null}
                      {areaDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" disabled={createAreaMutation.isPending || !areaPayloadIsValid} onClick={() => createAreaMutation.mutate()}>Save area</button> : null}
                      {areaDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" disabled={updateAreaMutation.isPending || !areaPayloadIsValid} onClick={() => updateAreaMutation.mutate()}>Update area</button> : null}
                    </div>
                  </div>
                </div>
              ) : null}

              {mappingDialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-3xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{mappingDialog.mode === 'create' ? 'Add mapping' : mappingDialog.mode === 'view' ? 'View mapping' : 'Edit mapping'}</h3><button type="button" className="button-secondary p-3" onClick={() => setMappingDialog(null)}><X className="size-4" /></button></div>
                    {mappingDialog.mode === 'view' ? <ReadOnlyNotice label="This mapping row is open in view mode. Switch to edit mode to change province, city, or station targeting." /> : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2">
                      <input disabled={mappingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Province" value={mappingForm.province ?? ''} onChange={(event) => setMappingForm((current) => ({ ...current, province: event.target.value }))} />
                      <input disabled={mappingDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="City" value={mappingForm.city ?? ''} onChange={(event) => setMappingForm((current) => ({ ...current, city: event.target.value }))} />
                      <input disabled={mappingDialog.mode === 'view'} className="input-base md:col-span-2 disabled:bg-slate-50" placeholder="Station or channel name" value={mappingForm.stationOrChannelName ?? ''} onChange={(event) => setMappingForm((current) => ({ ...current, stationOrChannelName: event.target.value }))} />
                    </div>
                    {!mappingPayloadIsValid ? <p className="mt-3 text-sm text-rose-600">Provide at least one of province, city, or station/channel name before saving.</p> : null}
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => setMappingDialog(null)}>Close</button>
                      {mappingDialog.mode === 'view' && selectedMapping ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openMappingDialog('edit', selectedMapping)}>Edit mapping</button> : null}
                      {mappingDialog.mode === 'edit' && selectedMapping ? <button type="button" className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50" disabled={deleteMappingMutation.isPending} onClick={() => { if (window.confirm('Delete this mapping row?')) { deleteMappingMutation.mutate(selectedMapping.id); } }}>Delete mapping</button> : null}
                      {mappingDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" disabled={createMappingMutation.isPending || !mappingPayloadIsValid} onClick={() => createMappingMutation.mutate()}>Save mapping</button> : null}
                      {mappingDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" disabled={updateMappingMutation.isPending || !mappingPayloadIsValid} onClick={() => updateMappingMutation.mutate()}>Update mapping</button> : null}
                    </div>
                  </div>
                </div>
              ) : null}
            </section>
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

export function AdminEnginePage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [engineDialog, setEngineDialog] = useState<{ mode: 'view' | 'edit'; packageCode: string } | null>(null);
  const [engineForm, setEngineForm] = useState<AdminUpdateEnginePolicyInput>({
    budgetFloor: 0,
    minimumNationalRadioCandidates: 0,
    requireNationalCapableRadio: false,
    requirePremiumNationalRadio: false,
    nationalRadioBonus: 0,
    nonNationalRadioPenalty: 0,
    regionalRadioPenalty: 0,
  });

  const updateEnginePolicyMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminEnginePolicy(engineDialog?.packageCode ?? '', engineForm),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setEngineDialog(null);
      pushToast({ title: 'Engine policy updated.', description: 'The planning policy override is now persisted for the live engine.' });
    },
    onError: (error) => pushToast({ title: 'Could not update engine policy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const selectedPolicy = engineDialog ? dashboard.enginePolicies.find((policy) => policy.packageCode === engineDialog.packageCode) ?? null : null;
        const openEngineDialog = (mode: 'view' | 'edit', policy: (typeof dashboard.enginePolicies)[number]) => {
          setEngineForm({
            budgetFloor: policy.budgetFloor,
            minimumNationalRadioCandidates: policy.minimumNationalRadioCandidates,
            requireNationalCapableRadio: policy.requireNationalCapableRadio,
            requirePremiumNationalRadio: policy.requirePremiumNationalRadio,
            nationalRadioBonus: policy.nationalRadioBonus,
            nonNationalRadioPenalty: policy.nonNationalRadioPenalty,
            regionalRadioPenalty: policy.regionalRadioPenalty,
          });
          setEngineDialog({ mode, packageCode: policy.packageCode });
        };

        return (
          <AdminPageShell title="Engine settings" description="Manage the persisted planning policy thresholds that govern candidate qualification, national requirements, and scoring pressure.">
            <div className="grid gap-4 xl:grid-cols-2">
              {dashboard.enginePolicies.map((policy) => <div key={policy.packageCode} className="panel p-6"><div className="flex items-start justify-between gap-4"><div><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{policy.packageCode}</p><p className="mt-3 text-2xl font-semibold text-ink">{fmtCurrency(policy.budgetFloor)}</p></div><div className="flex items-center gap-2"><span className="pill">{policy.requirePremiumNationalRadio ? 'Premium national radio required' : 'Flexible radio qualification'}</span><ActionButton label={`View ${policy.packageCode} policy`} icon={Eye} onClick={() => openEngineDialog('view', policy)} /><ActionButton label={`Edit ${policy.packageCode} policy`} icon={Pencil} onClick={() => openEngineDialog('edit', policy)} /></div></div><div className="mt-5 grid gap-2 text-sm text-ink-soft sm:grid-cols-2"><div><span className="font-semibold text-ink">Min national radio:</span> {policy.minimumNationalRadioCandidates}</div><div><span className="font-semibold text-ink">National capable:</span> {policy.requireNationalCapableRadio ? 'Yes' : 'No'}</div><div><span className="font-semibold text-ink">National bonus:</span> {policy.nationalRadioBonus}</div><div><span className="font-semibold text-ink">Non-national penalty:</span> {policy.nonNationalRadioPenalty}</div><div><span className="font-semibold text-ink">Regional penalty:</span> {policy.regionalRadioPenalty}</div></div></div>)}
            </div>

            {engineDialog && selectedPolicy ? (
              <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                <div className="w-full max-w-4xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                  <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{engineDialog.mode === 'view' ? 'View engine policy' : 'Edit engine policy'}</h3><button type="button" className="button-secondary p-3" onClick={() => setEngineDialog(null)}><X className="size-4" /></button></div>
                  {engineDialog.mode === 'view' ? <ReadOnlyNotice label="This policy is open in view mode. Switch to edit mode to persist a new engine-policy override." /> : null}
                  <div className="mt-4 flex flex-wrap items-center gap-3 text-sm text-ink-soft"><span className="pill">{selectedPolicy.packageCode}</span></div>
                  <div className="mt-6 grid gap-4 md:grid-cols-2">
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Budget floor" value={engineForm.budgetFloor} onChange={(event) => setEngineForm((current) => ({ ...current, budgetFloor: Number(event.target.value) }))} />
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Minimum national radio candidates" value={engineForm.minimumNationalRadioCandidates} onChange={(event) => setEngineForm((current) => ({ ...current, minimumNationalRadioCandidates: Number(event.target.value) }))} />
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="National radio bonus" value={engineForm.nationalRadioBonus} onChange={(event) => setEngineForm((current) => ({ ...current, nationalRadioBonus: Number(event.target.value) }))} />
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Non-national radio penalty" value={engineForm.nonNationalRadioPenalty} onChange={(event) => setEngineForm((current) => ({ ...current, nonNationalRadioPenalty: Number(event.target.value) }))} />
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Regional radio penalty" value={engineForm.regionalRadioPenalty} onChange={(event) => setEngineForm((current) => ({ ...current, regionalRadioPenalty: Number(event.target.value) }))} />
                    <div className="flex flex-wrap items-center gap-5 rounded-[24px] border border-line px-4 py-3 text-sm text-ink-soft md:col-span-2"><label className="inline-flex items-center gap-2"><input disabled={engineDialog.mode === 'view'} type="checkbox" checked={engineForm.requireNationalCapableRadio} onChange={(event) => setEngineForm((current) => ({ ...current, requireNationalCapableRadio: event.target.checked }))} /> Require national-capable radio</label><label className="inline-flex items-center gap-2"><input disabled={engineDialog.mode === 'view'} type="checkbox" checked={engineForm.requirePremiumNationalRadio} onChange={(event) => setEngineForm((current) => ({ ...current, requirePremiumNationalRadio: event.target.checked }))} /> Require premium national radio</label></div>
                  </div>
                  <div className="mt-6 flex justify-end gap-3"><button type="button" className="button-secondary px-5 py-3" onClick={() => setEngineDialog(null)}>Close</button>{engineDialog.mode === 'view' ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openEngineDialog('edit', selectedPolicy)}>Edit policy</button> : <button type="button" className="button-primary px-5 py-3" disabled={updateEnginePolicyMutation.isPending} onClick={() => updateEnginePolicyMutation.mutate()}>Save policy</button>}</div>
                </div>
              </div>
            ) : null}
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

export function AdminPreviewRulesPage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [previewRuleDialog, setPreviewRuleDialog] = useState<{ mode: 'view' | 'edit'; key: string } | null>(null);
  const [previewRuleForm, setPreviewRuleForm] = useState({
    tierLabel: '',
    typicalInclusions: '',
    indicativeMix: '',
  });

  const updatePreviewRuleMutation = useMutation({
    mutationFn: () => {
      if (!previewRuleDialog?.key) {
        throw new Error('Choose a preview rule to edit first.');
      }

      const [packageCode, tierCode] = previewRuleDialog.key.split('|');
      return advertifiedApi.updateAdminPreviewRule(packageCode, tierCode, {
        tierLabel: previewRuleForm.tierLabel,
        typicalInclusions: splitList(previewRuleForm.typicalInclusions),
        indicativeMix: splitList(previewRuleForm.indicativeMix),
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setPreviewRuleDialog(null);
      pushToast({ title: 'Preview rule updated.', description: 'The live preview tier rule has been saved.' });
    },
    onError: (error) => pushToast({ title: 'Could not update preview rule.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });
  const previewRuleFormIsValid = hasText(previewRuleForm.tierLabel) && !!previewRuleDialog?.key;

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const selectedPreviewRule = previewRuleDialog
          ? dashboard.previewRules.find((rule) => `${rule.packageCode}|${rule.tierCode}` === previewRuleDialog.key) ?? null
          : null;
        const openPreviewRuleDialog = (mode: 'view' | 'edit', rule: (typeof dashboard.previewRules)[number]) => {
          setPreviewRuleForm({
            tierLabel: rule.tierLabel,
            typicalInclusions: rule.typicalInclusions.join(', '),
            indicativeMix: rule.indicativeMix.join(', '),
          });
          setPreviewRuleDialog({ mode, key: `${rule.packageCode}|${rule.tierCode}` });
        };

        return (
          <AdminPageShell title="Preview rules" description="Edit the real preview tier rules used by the package preview flow and inspect the configured inclusions by tier.">
            <section className="space-y-6">
              <div className="panel p-6">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <h3 className="text-lg font-semibold text-ink">Rule catalog</h3>
                    <p className="mt-2 text-sm text-ink-soft">Review each preview tier rule, open it in view mode, and switch into edit mode when a live update is needed.</p>
                  </div>
                  <span className="pill"><Save className="mr-2 inline size-4" />Live rule update</span>
                </div>
              </div>
              <div className="overflow-hidden rounded-[28px] border border-line">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Package</th><th className="px-4 py-4">Tier</th><th className="px-4 py-4">Typical inclusions</th><th className="px-4 py-4">Indicative mix</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                  <tbody>
                    {dashboard.previewRules.map((rule) => <tr key={`${rule.packageCode}-${rule.tierCode}`} className="border-t border-line"><td className="px-4 py-4"><p className="font-semibold text-ink">{rule.packageName}</p><p className="text-xs text-ink-soft">{rule.packageCode}</p></td><td className="px-4 py-4"><p className="font-semibold text-ink">{rule.tierLabel}</p><p className="text-xs text-ink-soft">{rule.tierCode}</p></td><td className="px-4 py-4 text-ink-soft">{rule.typicalInclusions.join(', ') || 'None configured'}</td><td className="px-4 py-4 text-ink-soft">{rule.indicativeMix.join(', ') || 'None configured'}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View preview rule ${rule.packageName} ${rule.tierCode}`} icon={Eye} onClick={() => openPreviewRuleDialog('view', rule)} /><ActionButton label={`Edit preview rule ${rule.packageName} ${rule.tierCode}`} icon={Pencil} onClick={() => openPreviewRuleDialog('edit', rule)} /></div></td></tr>)}
                  </tbody>
                </table>
              </div>

              {previewRuleDialog && selectedPreviewRule ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-4xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{previewRuleDialog.mode === 'view' ? 'View preview rule' : 'Edit preview rule'}</h3><button type="button" className="button-secondary p-3" onClick={() => setPreviewRuleDialog(null)}><X className="size-4" /></button></div>
                    <div className="mt-4 flex flex-wrap items-center gap-3 text-sm text-ink-soft"><span className="pill">{selectedPreviewRule.packageName}</span><span className="pill">{selectedPreviewRule.tierCode}</span></div>
                    {previewRuleDialog.mode === 'view' ? <ReadOnlyNotice label="This rule is currently open in view mode. Switch to edit mode to save a live rule change." /> : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2">
                      <input disabled={previewRuleDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Tier label" value={previewRuleForm.tierLabel} onChange={(event) => setPreviewRuleForm((current) => ({ ...current, tierLabel: event.target.value }))} />
                      <div className="rounded-[24px] border border-line bg-slate-50 px-4 py-3 text-sm text-ink-soft">Package code: <span className="font-semibold text-ink">{selectedPreviewRule.packageCode}</span></div>
                      <input disabled={previewRuleDialog.mode === 'view'} className="input-base md:col-span-2 disabled:bg-slate-50" placeholder="Typical inclusions, comma separated" value={previewRuleForm.typicalInclusions} onChange={(event) => setPreviewRuleForm((current) => ({ ...current, typicalInclusions: event.target.value }))} />
                      <input disabled={previewRuleDialog.mode === 'view'} className="input-base md:col-span-2 disabled:bg-slate-50" placeholder="Indicative mix, comma separated" value={previewRuleForm.indicativeMix} onChange={(event) => setPreviewRuleForm((current) => ({ ...current, indicativeMix: event.target.value }))} />
                    </div>
                    {!previewRuleFormIsValid && previewRuleDialog.mode === 'edit' ? <p className="mt-3 text-sm text-rose-600">Tier label is required before you can save this preview rule.</p> : null}
                    <div className="mt-6 flex justify-end gap-3"><button type="button" className="button-secondary px-5 py-3" onClick={() => setPreviewRuleDialog(null)}>Close</button>{previewRuleDialog.mode === 'view' ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openPreviewRuleDialog('edit', selectedPreviewRule)}>Edit rule</button> : <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60" onClick={() => updatePreviewRuleMutation.mutate()} disabled={updatePreviewRuleMutation.isPending || !previewRuleFormIsValid}><Save className="size-4" />Save rule</button>}</div>
                  </div>
                </div>
              ) : null}
            </section>
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

export function AdminMonitoringPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => (
        <AdminPageShell title="Monitoring" description="Track live operational metrics for campaigns, recommendations, areas, and inventory coverage in one place.">
          <ReadOnlyNotice label="Monitoring is a live operational snapshot. These metrics are observational and are not edited from the admin UI." />
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {[['Total campaigns', dashboard.monitoring.totalCampaigns], ['Planning ready', dashboard.monitoring.planningReadyCount], ['Waiting on client', dashboard.monitoring.waitingOnClientCount], ['Inventory rows', dashboard.monitoring.inventoryRows], ['Active areas', dashboard.monitoring.activeAreaCount], ['Recommendation sets', dashboard.monitoring.recommendationCount]].map(([label, value]) => <div key={String(label)} className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{label}</p><p className="mt-4 text-4xl font-semibold text-ink">{value}</p></div>)}
          </div>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}

export function AdminUsersPage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [userDialog, setUserDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [userForm, setUserForm] = useState<AdminUserFormState>({
    fullName: '',
    email: '',
    phone: '',
    password: '',
    role: 'client',
    accountStatus: 'PendingVerification',
    isSaCitizen: true,
    emailVerified: false,
    phoneVerified: false,
    assignedAreaCodes: [],
  });
  const userCreateIsValid = hasText(userForm.fullName) && hasText(userForm.email) && hasText(userForm.phone) && hasText(userForm.password);
  const userUpdateIsValid = hasText(userForm.fullName) && hasText(userForm.email) && hasText(userForm.phone);

  const resetUserForm = () => {
    setUserForm({
      fullName: '',
      email: '',
      phone: '',
      password: '',
      role: 'client',
      accountStatus: 'PendingVerification',
      isSaCitizen: true,
      emailVerified: false,
      phoneVerified: false,
      assignedAreaCodes: [],
    });
  };

  const createUserMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminUser(userForm),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setUserDialog(null);
      resetUserForm();
      pushToast({ title: 'User created.', description: 'The user account is now available in the live admin workspace.' });
    },
    onError: (error) => pushToast({ title: 'Could not create user.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updateUserMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminUser(userDialog?.id ?? '', { ...userForm, password: userForm.password || undefined }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setUserDialog(null);
      resetUserForm();
      pushToast({ title: 'User updated.', description: 'The account details were saved successfully.' });
    },
    onError: (error) => pushToast({ title: 'Could not update user.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteUserMutation = useMutation({
    mutationFn: (id: string) => advertifiedApi.deleteAdminUser(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setUserDialog(null);
      resetUserForm();
      pushToast({ title: 'User deleted.', description: 'The account was removed from the system.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete user.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const selectedUser = userDialog?.id ? dashboard.users.find((item) => item.id === userDialog.id) ?? null : null;
        const isReadOnly = userDialog?.mode === 'view';
        const openUserDialog = (mode: 'create' | 'view' | 'edit', user?: (typeof dashboard.users)[number]) => {
          if (mode === 'create' || !user) {
            resetUserForm();
            setUserDialog({ mode });
            return;
          }

          setUserForm({
            fullName: user.fullName,
            email: user.email,
            phone: user.phone,
            password: '',
            role: user.role,
            accountStatus: user.accountStatus,
            isSaCitizen: user.isSaCitizen,
            emailVerified: user.emailVerified,
            phoneVerified: user.phoneVerified,
            assignedAreaCodes: user.assignedAreaCodes,
          });
          setUserDialog({ mode, id: user.id });
        };

        return (
          <AdminPageShell title="Users and roles" description="Manage the live user base across admin, agent, and client roles without leaving the admin workspace.">
            <section className="space-y-6">
              <div className="panel p-6">
                <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                  <div>
                    <h3 className="text-lg font-semibold text-ink">Account management</h3>
                    <p className="mt-2 text-sm text-ink-soft">Create accounts, adjust role and verification state, and safely remove accounts that do not own live work yet.</p>
                  </div>
                  <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3" onClick={() => openUserDialog('create')}>
                    <PlusCircle className="size-4" />
                    Add user
                  </button>
                </div>
              </div>

              <div className="overflow-hidden rounded-[28px] border border-line">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Name</th><th className="px-4 py-4">Contact</th><th className="px-4 py-4">Role</th><th className="px-4 py-4">Coverage</th><th className="px-4 py-4">Status</th><th className="px-4 py-4">Verification</th><th className="px-4 py-4">Joined</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                  <tbody>
                    {dashboard.users.map((item) => <tr key={item.id} className="border-t border-line"><td className="px-4 py-4"><p className="font-semibold text-ink">{item.fullName}</p><p className="text-xs text-ink-soft">{item.isSaCitizen ? 'SA citizen' : 'International user'}</p></td><td className="px-4 py-4 text-ink-soft"><div>{item.email}</div><div className="text-xs">{item.phone}</div></td><td className="px-4 py-4 text-ink-soft">{titleize(item.role)}</td><td className="px-4 py-4 text-ink-soft">{item.role === 'agent' ? item.assignedAreaLabels.length > 0 ? item.assignedAreaLabels.join(', ') : 'No areas assigned' : item.role === 'creative_director' ? 'Creative studio access' : 'Not applicable'}</td><td className="px-4 py-4 text-ink-soft">{titleize(item.accountStatus)}</td><td className="px-4 py-4 text-ink-soft"><div>Email: {item.emailVerified ? 'Verified' : 'Pending'}</div><div className="text-xs">Phone: {item.phoneVerified ? 'Verified' : 'Pending'}</div></td><td className="px-4 py-4 text-ink-soft">{fmtDate(item.createdAt)}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View ${item.fullName}`} icon={Eye} onClick={() => openUserDialog('view', item)} /><ActionButton label={`Edit ${item.fullName}`} icon={Pencil} onClick={() => openUserDialog('edit', item)} /><ActionButton label={`Delete ${item.fullName}`} icon={Trash2} variant="danger" disabled={deleteUserMutation.isPending} onClick={() => { if (window.confirm(`Delete ${item.fullName}? Accounts with linked campaigns, recommendations, or orders cannot be deleted.`)) { deleteUserMutation.mutate(item.id); } }} /></div></td></tr>)}
                  </tbody>
                </table>
              </div>

              {userDialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-4xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{userDialog.mode === 'create' ? 'Add user' : userDialog.mode === 'view' ? 'View user' : 'Edit user'}</h3><button type="button" className="button-secondary p-3" onClick={() => { setUserDialog(null); resetUserForm(); }}><X className="size-4" /></button></div>
                    {isReadOnly ? <ReadOnlyNotice label="This account is open in view mode. Switch to edit mode to change role, status, verification state, or credentials." /> : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2">
                      <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Full name" value={userForm.fullName} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, fullName: event.target.value }))} />
                      <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Email" value={userForm.email} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, email: event.target.value }))} />
                      <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder="Phone" value={userForm.phone} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, phone: event.target.value }))} />
                      <input disabled={isReadOnly} className="input-base disabled:bg-slate-50" placeholder={userDialog.mode === 'edit' ? 'New password (optional)' : 'Password'} type="password" value={userForm.password ?? ''} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, password: event.target.value }))} />
                      <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={userForm.role} onChange={(event) => setUserForm((current: AdminUserFormState) => {
                        const nextRole = event.target.value as AdminUserFormState['role'];
                        return { ...current, role: nextRole, assignedAreaCodes: nextRole === 'agent' ? current.assignedAreaCodes : [] };
                      })}>
                        <option value="client">Client</option>
                        <option value="agent">Agent</option>
                        <option value="creative_director">Creative director</option>
                        <option value="admin">Admin</option>
                      </select>
                      <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={userForm.accountStatus} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, accountStatus: event.target.value }))}>
                        <option value="PendingVerification">Pending verification</option>
                        <option value="Active">Active</option>
                        <option value="Suspended">Suspended</option>
                      </select>
                    </div>
                    <div className="mt-4 space-y-3">
                      <div>
                        <p className="text-sm font-semibold text-ink">Assigned areas</p>
                        <p className="mt-1 text-sm text-ink-soft">Area routing is reserved for agent accounts. Creative directors work in the production studio after recommendation approval.</p>
                      </div>
                      {userForm.role === 'agent' ? (
                        <div className="grid gap-3 md:grid-cols-2">
                          {dashboard.areas.map((area) => (
                            <label key={area.code} className="inline-flex items-center gap-3 rounded-[18px] border border-line px-4 py-3 text-sm text-ink-soft">
                              <input
                                disabled={isReadOnly}
                                type="checkbox"
                                checked={userForm.assignedAreaCodes.includes(area.code)}
                                onChange={(event) => setUserForm((current: AdminUserFormState) => ({
                                  ...current,
                                  assignedAreaCodes: event.target.checked
                                    ? [...current.assignedAreaCodes, area.code]
                                    : current.assignedAreaCodes.filter((code) => code !== area.code),
                                }))}
                              />
                              <span>
                                <span className="font-semibold text-ink">{area.label}</span>
                                <span className="block text-xs">{area.code}</span>
                              </span>
                            </label>
                          ))}
                        </div>
                      ) : (
                        <ReadOnlyNotice label="Area routing is only used for agent accounts. Switching this user away from the agent role clears any existing area ownership." />
                      )}
                    </div>
                    <div className="mt-4 flex flex-wrap items-center gap-5 text-sm text-ink-soft">
                      <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={userForm.isSaCitizen} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, isSaCitizen: event.target.checked }))} /> South African citizen</label>
                      <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={userForm.emailVerified} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, emailVerified: event.target.checked }))} /> Email verified</label>
                      <label className="inline-flex items-center gap-2"><input disabled={isReadOnly} type="checkbox" checked={userForm.phoneVerified} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, phoneVerified: event.target.checked }))} /> Phone verified</label>
                      {selectedUser ? <span>Updated {fmtDate(selectedUser.updatedAt)}</span> : null}
                    </div>
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => { setUserDialog(null); resetUserForm(); }}>Close</button>
                      {userDialog.mode === 'view' && selectedUser ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openUserDialog('edit', selectedUser)}>Edit user</button> : null}
                      {userDialog.mode === 'edit' && selectedUser ? <button type="button" className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50" disabled={deleteUserMutation.isPending} onClick={() => { if (window.confirm(`Delete ${selectedUser.fullName}? Accounts with linked campaigns, recommendations, or orders cannot be deleted.`)) { deleteUserMutation.mutate(selectedUser.id); } }}>Delete user</button> : null}
                      {userDialog.mode === 'create' && !userCreateIsValid ? <p className="text-sm text-rose-600">Full name, email, phone, and password are required to create a user.</p> : null}
                      {userDialog.mode === 'edit' && !userUpdateIsValid ? <p className="text-sm text-rose-600">Full name, email, and phone are required to update a user.</p> : null}
                      {userDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" onClick={() => createUserMutation.mutate()} disabled={createUserMutation.isPending || !userCreateIsValid}>Save user</button> : null}
                      {userDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" onClick={() => updateUserMutation.mutate()} disabled={updateUserMutation.isPending || !userUpdateIsValid}>Update user</button> : null}
                    </div>
                  </div>
                </div>
              ) : null}
            </section>
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

export function AdminAuditPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => (
        <AdminPageShell title="Audit log" description="Review recent admin and agent changes alongside key payment integration events in one immutable timeline.">
          <ReadOnlyNotice label="Audit entries are immutable records. This page is intentionally read-only so operational history stays trustworthy across admin, agent, and system activity." />
          <div className="rounded-[28px] border border-line bg-white p-6">
            <div className="overflow-hidden rounded-[24px] border border-line">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Source</th><th className="px-4 py-4">Actor</th><th className="px-4 py-4">Action</th><th className="px-4 py-4">Entity</th><th className="px-4 py-4">Context</th><th className="px-4 py-4">When</th></tr></thead>
                <tbody>
                  {dashboard.auditEntries.map((entry) => <tr key={entry.id} className="border-t border-line"><td className="px-4 py-4 text-ink">{entry.source}</td><td className="px-4 py-4 text-ink-soft"><div>{entry.actorName}</div><div className="text-xs">{titleize(entry.actorRole || 'system')}</div></td><td className="px-4 py-4 text-ink-soft"><div>{titleize(entry.eventType)}</div>{entry.statusLabel ? <div className="text-xs">{entry.statusLabel}</div> : null}</td><td className="px-4 py-4 text-ink-soft"><div>{entry.entityLabel ?? 'Platform event'}</div><div className="text-xs">{entry.entityType ? titleize(entry.entityType) : 'System'}</div></td><td className="px-4 py-4 text-ink-soft">{entry.context}</td><td className="px-4 py-4 text-ink-soft">{fmtDate(entry.createdAt)}</td></tr>)}
                </tbody>
              </table>
            </div>
          </div>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}

export function AdminIntegrationsPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const platformStatus = dashboard.integrations.lastPaymentWebhookAt || dashboard.integrations.lastPaymentRequestAt ? 'Connected' : 'Waiting for first activity';

        return (
          <AdminPageShell title="Integrations" description="Track the current health of live integration activity, payment requests, and webhook streams.">
            <ReadOnlyNotice label="Integration health is read-only here and is derived from live request and webhook activity rather than admin-managed settings." />
            <div className="grid gap-4 xl:grid-cols-3">
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><Zap className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Payment requests</p></div><p className="mt-4 text-3xl font-semibold text-ink">{dashboard.integrations.paymentRequestAuditCount}</p><p className="mt-3 text-sm text-ink-soft">Requests recorded against the payment provider integration.</p><p className="mt-3 text-xs text-ink-soft">Last request {fmtDate(dashboard.integrations.lastPaymentRequestAt)}</p></div>
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><MapPin className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Payment webhooks</p></div><p className="mt-4 text-3xl font-semibold text-ink">{dashboard.integrations.paymentWebhookAuditCount}</p><p className="mt-3 text-sm text-ink-soft">Webhook callbacks received from the payment provider.</p><p className="mt-3 text-xs text-ink-soft">Last webhook {fmtDate(dashboard.integrations.lastPaymentWebhookAt)}</p></div>
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><Globe2 className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Platform status</p></div><p className="mt-4 text-2xl font-semibold text-ink">{platformStatus}</p><p className="mt-3 text-sm text-ink-soft">Status is derived from live integration activity rather than a hardcoded admin label.</p></div>
            </div>
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

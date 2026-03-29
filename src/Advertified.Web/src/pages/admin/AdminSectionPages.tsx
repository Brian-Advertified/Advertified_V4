import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Globe2, MapPin, Pencil, PlusCircle, Save, ShieldCheck, Trash2, Upload, X, Zap } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type {
  AdminCreateOutletInput,
  AdminOutletPricingPackage,
  AdminOutletSlotRate,
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
  role: 'client' | 'agent' | 'admin';
  accountStatus: string;
  isSaCitizen: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
};

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

  useEffect(() => {
    const outlet = searchParams.get('outlet');
    const mode = searchParams.get('mode');
    const key = `${outlet ?? ''}|${mode ?? ''}`;
    if (!outlet || !mode || handledSearchRef.current === key) {
      return;
    }

    if (mode === 'edit' || mode === 'view') {
      handledSearchRef.current = key;
      void openExistingDialog(outlet, mode);
    }
  }, [searchParams]);

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
                    {dialogMode !== 'view' ? <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60" onClick={() => dialogMode === 'create' ? createOutletMutation.mutate() : updateOutletMutation.mutate()} disabled={createOutletMutation.isPending || updateOutletMutation.isPending}>
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
  const [selectedOutletCode, setSelectedOutletCode] = useState('');
  const [packageDialog, setPackageDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [slotDialog, setSlotDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
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
  const dashboard = query.data;
  const selectedPackage = packageDialog?.id && outletPricingQuery.data
    ? outletPricingQuery.data.packages.find((entry) => entry.id === packageDialog.id)
    : null;
  const selectedSlotRate = slotDialog?.id && outletPricingQuery.data
    ? outletPricingQuery.data.slotRates.find((entry) => entry.id === slotDialog.id)
    : null;

  useEffect(() => {
    const pricingTarget = searchParams.get('outlet');
    const outletOptions = dashboard?.outlets ?? [];
    if (pricingTarget && pricingTarget !== selectedOutletCode) {
      setSelectedOutletCode(pricingTarget);
      return;
    }

    if (!pricingTarget && !selectedOutletCode && outletOptions.length > 0) {
      setSelectedOutletCode(outletOptions[0].code);
    }
  }, [dashboard?.outlets, searchParams, selectedOutletCode]);

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const outletOptions = [...dashboard.outlets].sort((left, right) => Number(left.hasPricing) - Number(right.hasPricing) || left.name.localeCompare(right.name));
        const selectedPricing = outletPricingQuery.data;

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
                      setSelectedOutletCode(next);
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
                    <div className="mt-4 overflow-hidden rounded-[24px] border border-line">
                      <table className="w-full border-collapse text-sm">
                        <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Package</th><th className="px-4 py-4">Investment</th><th className="px-4 py-4">Monthly</th><th className="px-4 py-4">Duration</th><th className="px-4 py-4">Status</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                        <tbody>
                          {selectedPricing.packages.length > 0 ? selectedPricing.packages.map((item) => <tr key={item.id} className="border-t border-line"><td className="px-4 py-4"><p className="font-semibold text-ink">{item.packageName}</p><p className="text-xs text-ink-soft">{item.packageType ?? 'General package'}</p></td><td className="px-4 py-4 text-ink-soft">{fmtCurrency(item.investmentZar ?? item.valueZar)}</td><td className="px-4 py-4 text-ink-soft">{fmtCurrency(item.costPerMonthZar)}</td><td className="px-4 py-4 text-ink-soft">{item.durationMonths ? `${item.durationMonths} month(s)` : item.durationWeeks ? `${item.durationWeeks} week(s)` : 'Not set'}</td><td className="px-4 py-4 text-ink-soft">{item.isActive ? 'Active' : 'Inactive'}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View package ${item.packageName}`} icon={Eye} onClick={() => openPackageDialog('view', item.id)} /><ActionButton label={`Edit package ${item.packageName}`} icon={Pencil} onClick={() => openPackageDialog('edit', item.id)} /><ActionButton label={`Delete package ${item.packageName}`} icon={Trash2} variant="danger" disabled={deletePackageMutation.isPending} onClick={() => { if (window.confirm(`Delete package ${item.packageName}?`)) { deletePackageMutation.mutate(item.id); } }} /></div></td></tr>) : <tr><td colSpan={6} className="px-4 py-8"><EmptyTableState message="No package rows exist for this outlet yet." action={<button type="button" className="button-primary inline-flex items-center gap-2 px-4 py-3" onClick={() => openPackageDialog('create')}><PlusCircle className="size-4" />Add package</button>} /></td></tr>}
                        </tbody>
                      </table>
                    </div>
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

              <ReadOnlyNotice label="Package band settings below are read-only on this page. They come from the live package catalog and planning rules rather than the outlet-pricing CRUD endpoints." />
              <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                {dashboard.packageSettings.map((band) => <div key={band.id} className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{band.name}</p><p className="mt-3 text-2xl font-semibold text-ink">{fmtCurrency(band.minBudget)} - {fmtCurrency(band.maxBudget)}</p><p className="mt-3 text-sm text-ink-soft">{band.quickBenefit}</p><div className="mt-4 space-y-2 text-sm text-ink-soft"><div><span className="font-semibold text-ink">Purpose:</span> {band.packagePurpose}</div><div><span className="font-semibold text-ink">Recommended spend:</span> {fmtCurrency(band.recommendedSpend)}</div><div><span className="font-semibold text-ink">Radio:</span> {titleize(band.includeRadio)}</div><div><span className="font-semibold text-ink">TV:</span> {titleize(band.includeTv)}</div><div><span className="font-semibold text-ink">Lead time:</span> {band.leadTime}</div></div></div>)}
              </section>

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
                    <div className="mt-6 flex justify-end gap-3"><button type="button" className="button-secondary px-5 py-3" onClick={() => setPackageDialog(null)}>Close</button>{packageDialog.mode === 'view' && selectedPackage ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openPackageDialog('edit', selectedPackage.id)}>Edit package</button> : null}{packageDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" onClick={() => createPackageMutation.mutate()}>Save package</button> : packageDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" onClick={() => updatePackageMutation.mutate()}>Update package</button> : null}</div>
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
                    <div className="mt-6 flex justify-end gap-3"><button type="button" className="button-secondary px-5 py-3" onClick={() => setSlotDialog(null)}>Close</button>{slotDialog.mode === 'view' && selectedSlotRate ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openSlotDialog('edit', selectedSlotRate.id)}>Edit slot rate</button> : null}{slotDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" onClick={() => createSlotMutation.mutate()}>Save slot rate</button> : slotDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" onClick={() => updateSlotMutation.mutate()}>Update slot rate</button> : null}</div>
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
  const [rateCardForm, setRateCardForm] = useState({
    channel: 'radio',
    supplierOrStation: '',
    documentTitle: '',
    notes: '',
    file: null as File | null,
  });

  const uploadRateCardMutation = useMutation({
    mutationFn: () => {
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

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => (
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
                <input type="file" className="input-base file:mr-3 file:rounded-full file:border-0 file:bg-brand-soft file:px-3 file:py-2 file:text-sm file:font-semibold file:text-brand" onChange={(event) => setRateCardForm((current) => ({ ...current, file: event.target.files?.[0] ?? null }))} />
              </div>
              <textarea className="input-base mt-4 min-h-[100px]" placeholder="Notes" value={rateCardForm.notes} onChange={(event) => setRateCardForm((current) => ({ ...current, notes: event.target.value }))} />
              <div className="mt-5 flex justify-end">
                <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60" onClick={() => uploadRateCardMutation.mutate()} disabled={uploadRateCardMutation.isPending}>
                  <Upload className="size-4" />
                  Upload rate card
                </button>
              </div>
            </div>
            <div className="overflow-hidden rounded-[28px] border border-line">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Document</th><th className="px-4 py-4">Channel</th><th className="px-4 py-4">Supplier / Station</th><th className="px-4 py-4">Pages</th><th className="px-4 py-4">Imported</th></tr></thead>
                <tbody>
                  {dashboard.recentImports.map((item) => <tr key={`${item.sourceFile}-${item.importedAt}`} className="border-t border-line"><td className="px-4 py-4"><p className="font-semibold text-ink">{item.documentTitle ?? item.sourceFile}</p><p className="text-xs text-ink-soft">{item.sourceFile}</p></td><td className="px-4 py-4 text-ink-soft">{titleize(item.channel)}</td><td className="px-4 py-4 text-ink-soft">{item.supplierOrStation ?? 'Not classified yet'}</td><td className="px-4 py-4 text-ink-soft">{item.pageCount ?? 0}</td><td className="px-4 py-4 text-ink-soft">{fmtDate(item.importedAt)}</td></tr>)}
                </tbody>
              </table>
            </div>
          </section>
        </AdminPageShell>
      )}
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

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => (
        <AdminPageShell title="Geography mapping" description="Review the live package area profiles and how many cluster mappings are backing each area.">
          <ReadOnlyNotice label="Geography mapping is read-only here. This page reflects the live package-area profile data currently backing planning and package coverage." />
          <div className="overflow-hidden rounded-[28px] border border-line">
            <table className="w-full border-collapse text-sm">
              <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Area</th><th className="px-4 py-4">Code</th><th className="px-4 py-4">Description</th><th className="px-4 py-4">Mapped records</th></tr></thead>
              <tbody>
                {dashboard.areas.map((area) => <tr key={area.code} className="border-t border-line"><td className="px-4 py-4 font-semibold text-ink">{area.label}</td><td className="px-4 py-4 text-ink-soft">{area.code}</td><td className="px-4 py-4 text-ink-soft">{area.description}</td><td className="px-4 py-4 text-ink-soft">{area.mappingCount}</td></tr>)}
              </tbody>
            </table>
          </div>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}

export function AdminEnginePage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => (
        <AdminPageShell title="Engine settings" description="Inspect the live planning policy configuration that governs candidate thresholds, national requirements, and scoring pressure.">
          <ReadOnlyNotice label="Engine policy values are read-only on this screen. They are derived from the live planning configuration and are shown here for operational review." />
          <div className="grid gap-4 xl:grid-cols-2">
            {dashboard.enginePolicies.map((policy) => <div key={policy.packageCode} className="panel p-6"><div className="flex items-start justify-between gap-4"><div><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{policy.packageCode}</p><p className="mt-3 text-2xl font-semibold text-ink">{fmtCurrency(policy.budgetFloor)}</p></div><span className="pill">{policy.requirePremiumNationalRadio ? 'Premium national radio required' : 'Flexible radio qualification'}</span></div><div className="mt-5 grid gap-2 text-sm text-ink-soft sm:grid-cols-2"><div><span className="font-semibold text-ink">Min national radio:</span> {policy.minimumNationalRadioCandidates}</div><div><span className="font-semibold text-ink">National capable:</span> {policy.requireNationalCapableRadio ? 'Yes' : 'No'}</div><div><span className="font-semibold text-ink">National bonus:</span> {policy.nationalRadioBonus}</div><div><span className="font-semibold text-ink">Non-national penalty:</span> {policy.nonNationalRadioPenalty}</div><div><span className="font-semibold text-ink">Regional penalty:</span> {policy.regionalRadioPenalty}</div></div></div>)}
          </div>
        </AdminPageShell>
      )}
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
                    <div className="mt-6 flex justify-end gap-3"><button type="button" className="button-secondary px-5 py-3" onClick={() => setPreviewRuleDialog(null)}>Close</button>{previewRuleDialog.mode === 'view' ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openPreviewRuleDialog('edit', selectedPreviewRule)}>Edit rule</button> : <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60" onClick={() => updatePreviewRuleMutation.mutate()} disabled={updatePreviewRuleMutation.isPending}><Save className="size-4" />Save rule</button>}</div>
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
  });

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
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Name</th><th className="px-4 py-4">Contact</th><th className="px-4 py-4">Role</th><th className="px-4 py-4">Status</th><th className="px-4 py-4">Verification</th><th className="px-4 py-4">Joined</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                  <tbody>
                    {dashboard.users.map((item) => <tr key={item.id} className="border-t border-line"><td className="px-4 py-4"><p className="font-semibold text-ink">{item.fullName}</p><p className="text-xs text-ink-soft">{item.isSaCitizen ? 'SA citizen' : 'International user'}</p></td><td className="px-4 py-4 text-ink-soft"><div>{item.email}</div><div className="text-xs">{item.phone}</div></td><td className="px-4 py-4 text-ink-soft">{titleize(item.role)}</td><td className="px-4 py-4 text-ink-soft">{titleize(item.accountStatus)}</td><td className="px-4 py-4 text-ink-soft"><div>Email: {item.emailVerified ? 'Verified' : 'Pending'}</div><div className="text-xs">Phone: {item.phoneVerified ? 'Verified' : 'Pending'}</div></td><td className="px-4 py-4 text-ink-soft">{fmtDate(item.createdAt)}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View ${item.fullName}`} icon={Eye} onClick={() => openUserDialog('view', item)} /><ActionButton label={`Edit ${item.fullName}`} icon={Pencil} onClick={() => openUserDialog('edit', item)} /><ActionButton label={`Delete ${item.fullName}`} icon={Trash2} variant="danger" disabled={deleteUserMutation.isPending} onClick={() => { if (window.confirm(`Delete ${item.fullName}? Accounts with linked campaigns, recommendations, or orders cannot be deleted.`)) { deleteUserMutation.mutate(item.id); } }} /></div></td></tr>)}
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
                      <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={userForm.role} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, role: event.target.value as AdminUserFormState['role'] }))}>
                        <option value="client">Client</option>
                        <option value="agent">Agent</option>
                        <option value="admin">Admin</option>
                      </select>
                      <select disabled={isReadOnly} className="input-base disabled:bg-slate-50" value={userForm.accountStatus} onChange={(event) => setUserForm((current: AdminUserFormState) => ({ ...current, accountStatus: event.target.value }))}>
                        <option value="PendingVerification">Pending verification</option>
                        <option value="Active">Active</option>
                        <option value="Suspended">Suspended</option>
                      </select>
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
                      {userDialog.mode === 'create' ? <button type="button" className="button-primary px-5 py-3" onClick={() => createUserMutation.mutate()} disabled={createUserMutation.isPending}>Save user</button> : null}
                      {userDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" onClick={() => updateUserMutation.mutate()} disabled={updateUserMutation.isPending}>Update user</button> : null}
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
        <AdminPageShell title="Audit log" description="Review recent live payment request and webhook events captured in the audit tables.">
          <ReadOnlyNotice label="Audit entries are immutable records. This page is intentionally read-only so operational history stays trustworthy." />
          <div className="rounded-[28px] border border-line bg-white p-6">
            <div className="overflow-hidden rounded-[24px] border border-line">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Source</th><th className="px-4 py-4">Provider</th><th className="px-4 py-4">Event</th><th className="px-4 py-4">Status</th><th className="px-4 py-4">When</th></tr></thead>
                <tbody>
                  {dashboard.auditEntries.map((entry) => <tr key={entry.id} className="border-t border-line"><td className="px-4 py-4 text-ink">{entry.source}</td><td className="px-4 py-4 text-ink-soft">{entry.provider}</td><td className="px-4 py-4 text-ink-soft">{entry.eventType}</td><td className="px-4 py-4 text-ink-soft">{entry.responseStatusCode ?? 'Pending'}</td><td className="px-4 py-4 text-ink-soft">{fmtDate(entry.createdAt)}</td></tr>)}
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

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, PlusCircle, Save, Trash2, X } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type {
  AdminUpdatePricingSettingsInput,
  AdminOutletPricingPackage,
  AdminOutletSlotRate,
  AdminUpsertPackageSettingInput,
  AdminUpsertOutletPricingPackageInput,
  AdminUpsertOutletSlotRateInput,
} from '../../types/domain';
import { AdminPageShell, AdminQueryBoundary, fmtCurrency, titleize, useAdminDashboardQuery } from './adminWorkspace';
import { ActionButton, EmptyTableState, ReadOnlyNotice, hasText } from './adminSectionShared';

const PRICING_PACKAGE_TYPE_OPTIONS = ['Station package', 'Sponsorship package', 'Promo package', 'Bundle package', 'Seasonal package'];
const PRICING_SOURCE_OPTIONS = ['Rate card', 'Supplier quote', 'Manual admin entry', 'Media owner pricing sheet'];

export function AdminPricingPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [selectedOutletCodeState, setSelectedOutletCodeState] = useState('');
  const [packageSettingDialog, setPackageSettingDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [packageDialog, setPackageDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [slotDialog, setSlotDialog] = useState<{ mode: 'create' | 'view' | 'edit'; id?: string } | null>(null);
  const [pricingSettingsForm, setPricingSettingsForm] = useState<AdminUpdatePricingSettingsInput>({
    aiStudioReservePercent: 0.1,
    oohMarkupPercent: 0.05,
    radioMarkupPercent: 0.1,
    tvMarkupPercent: 0.1,
  });
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

  const updatePricingSettingsMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminPricingSettings(pricingSettingsForm),
    onSuccess: async () => {
      await refreshPackageSettings();
      pushToast({ title: 'Pricing settings updated.', description: 'Checkout reserve and channel markups are now live.' });
    },
    onError: (error) => pushToast({ title: 'Could not update pricing settings.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

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
        if (
          pricingSettingsForm.aiStudioReservePercent !== dashboard.pricingSettings.aiStudioReservePercent
          || pricingSettingsForm.oohMarkupPercent !== dashboard.pricingSettings.oohMarkupPercent
          || pricingSettingsForm.radioMarkupPercent !== dashboard.pricingSettings.radioMarkupPercent
          || pricingSettingsForm.tvMarkupPercent !== dashboard.pricingSettings.tvMarkupPercent
        ) {
          setPricingSettingsForm(dashboard.pricingSettings);
        }

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
                    <h3 className="text-lg font-semibold text-ink">Platform markups and reserve</h3>
                    <p className="mt-2 text-sm text-ink-soft">Control the hidden AI Studio reserve collected at checkout and the markup percentages applied to OOH, radio, and TV planning costs.</p>
                  </div>
                  <button
                    type="button"
                    className="button-primary inline-flex items-center gap-2 px-4 py-3"
                    disabled={updatePricingSettingsMutation.isPending}
                    onClick={() => updatePricingSettingsMutation.mutate()}
                  >
                    <Save className="size-4" />
                    Save pricing settings
                  </button>
                </div>
                <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                  <label className="space-y-2">
                    <span className="text-sm font-medium text-ink">AI Studio reserve %</span>
                    <input className="input-base" type="number" min="0" max="100" step="0.1" value={pricingSettingsForm.aiStudioReservePercent * 100} onChange={(event) => setPricingSettingsForm((current) => ({ ...current, aiStudioReservePercent: Number(event.target.value) / 100 }))} />
                  </label>
                  <label className="space-y-2">
                    <span className="text-sm font-medium text-ink">OOH and billboard markup %</span>
                    <input className="input-base" type="number" min="0" max="100" step="0.1" value={pricingSettingsForm.oohMarkupPercent * 100} onChange={(event) => setPricingSettingsForm((current) => ({ ...current, oohMarkupPercent: Number(event.target.value) / 100 }))} />
                  </label>
                  <label className="space-y-2">
                    <span className="text-sm font-medium text-ink">Radio markup %</span>
                    <input className="input-base" type="number" min="0" max="100" step="0.1" value={pricingSettingsForm.radioMarkupPercent * 100} onChange={(event) => setPricingSettingsForm((current) => ({ ...current, radioMarkupPercent: Number(event.target.value) / 100 }))} />
                  </label>
                  <label className="space-y-2">
                    <span className="text-sm font-medium text-ink">TV markup %</span>
                    <input className="input-base" type="number" min="0" max="100" step="0.1" value={pricingSettingsForm.tvMarkupPercent * 100} onChange={(event) => setPricingSettingsForm((current) => ({ ...current, tvMarkupPercent: Number(event.target.value) / 100 }))} />
                  </label>
                </div>
                <div className="mt-4 grid gap-4 md:grid-cols-4">
                  <div className="rounded-[20px] border border-line bg-brand-soft px-4 py-4">
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">AI Studio</p>
                    <p className="mt-2 text-2xl font-semibold text-ink">{(pricingSettingsForm.aiStudioReservePercent * 100).toFixed(1)}%</p>
                  </div>
                  <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">OOH / Billboard</p>
                    <p className="mt-2 text-2xl font-semibold text-ink">{(pricingSettingsForm.oohMarkupPercent * 100).toFixed(1)}%</p>
                  </div>
                  <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Radio</p>
                    <p className="mt-2 text-2xl font-semibold text-ink">{(pricingSettingsForm.radioMarkupPercent * 100).toFixed(1)}%</p>
                  </div>
                  <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">TV</p>
                    <p className="mt-2 text-2xl font-semibold text-ink">{(pricingSettingsForm.tvMarkupPercent * 100).toFixed(1)}%</p>
                  </div>
                </div>
              </div>

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
                      <label className="block text-sm font-semibold text-ink">
                        Package type
                        <select disabled={packageDialog.mode === 'view'} className="input-base mt-2 disabled:bg-slate-50" value={packageForm.packageType ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, packageType: event.target.value }))}>
                          <option value="">Choose package type</option>
                          {PRICING_PACKAGE_TYPE_OPTIONS.map((option) => <option key={option} value={option}>{option}</option>)}
                        </select>
                        <span className="mt-2 block text-xs font-normal text-ink-soft">What kind of commercial package is this?</span>
                      </label>
                      <label className="block text-sm font-semibold text-ink">
                        Source name
                        <select disabled={packageDialog.mode === 'view'} className="input-base mt-2 disabled:bg-slate-50" value={packageForm.sourceName ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, sourceName: event.target.value }))}>
                          <option value="">Choose pricing source</option>
                          {PRICING_SOURCE_OPTIONS.map((option) => <option key={option} value={option}>{option}</option>)}
                        </select>
                        <span className="mt-2 block text-xs font-normal text-ink-soft">Where did this pricing record come from?</span>
                      </label>
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
                    <textarea disabled={packageDialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Notes. Example: Includes drive-time mentions and social support." value={packageForm.notes ?? ''} onChange={(event) => setPackageForm((current) => ({ ...current, notes: event.target.value }))} />
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



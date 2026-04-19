import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, Plus, Trash2, X } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type {
  AdminPlanningBudgetBand,
  AdminUpdateEnginePolicyInput,
  AdminUpdatePlanningAllocationSettingsInput,
} from '../../types/domain';
import { ActionButton, ReadOnlyNotice } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, fmtCurrency, useAdminDashboardQuery } from './adminWorkspace';

const defaultBudgetBand = (): AdminPlanningBudgetBand => ({
  name: '',
  min: 0,
  max: 0,
  oohTarget: 0.4,
  tvMin: 0,
  tvEligible: false,
  radioRange: [0.25, 0.3],
  digitalRange: [0.2, 0.25],
});

function formatRatio(value: number) {
  return `${Math.round(value * 100)}%`;
}

export function AdminEnginePage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [engineDialog, setEngineDialog] = useState<{ mode: 'view' | 'edit'; packageCode: string } | null>(null);
  const [allocationDialogMode, setAllocationDialogMode] = useState<'view' | 'edit' | null>(null);
  const [engineForm, setEngineForm] = useState<AdminUpdateEnginePolicyInput>({
    budgetFloor: 0,
    minimumNationalRadioCandidates: 0,
    requireNationalCapableRadio: false,
    requirePremiumNationalRadio: false,
    nationalRadioBonus: 0,
    nonNationalRadioPenalty: 0,
    regionalRadioPenalty: 0,
  });
  const [allocationForm, setAllocationForm] = useState<AdminUpdatePlanningAllocationSettingsInput>({
    budgetBands: [],
    globalRules: {
      maxOoh: 0.5,
      minDigital: 0.15,
      enforceTvFloorIfPreferred: true,
    },
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

  const updatePlanningAllocationMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminPlanningAllocationSettings(allocationForm),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setAllocationDialogMode(null);
      pushToast({ title: 'Planning allocation settings updated.', description: 'Budget bands and global allocation rules are now persisted for the live engine.' });
    },
    onError: (error) => pushToast({ title: 'Could not update planning allocation settings.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
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

        const openAllocationDialog = (mode: 'view' | 'edit') => {
          setAllocationForm({
            budgetBands: dashboard.planningAllocationSettings.budgetBands.map((band) => ({
              ...band,
              radioRange: [...band.radioRange] as [number, number],
              digitalRange: [...band.digitalRange] as [number, number],
            })),
            globalRules: { ...dashboard.planningAllocationSettings.globalRules },
          });
          setAllocationDialogMode(mode);
        };

        const updateBudgetBand = (index: number, update: (band: AdminPlanningBudgetBand) => AdminPlanningBudgetBand) => {
          setAllocationForm((current) => ({
            ...current,
            budgetBands: current.budgetBands.map((band, bandIndex) => (bandIndex === index ? update(band) : band)),
          }));
        };

        return (
          <AdminPageShell title="Engine settings" description="Manage persisted planning policy thresholds and the live budget-band allocation rules that drive channel mix.">
            <div className="space-y-8">
              <section className="space-y-4">
                <div className="flex items-center justify-between gap-4">
                  <div>
                    <h2 className="text-lg font-semibold text-ink">Radio qualification policy</h2>
                    <p className="mt-1 text-sm text-ink-soft">These overrides govern national-capable radio qualification and scoring pressure by package tier.</p>
                  </div>
                </div>
                <div className="grid gap-4 xl:grid-cols-2">
                  {dashboard.enginePolicies.map((policy) => (
                    <div key={policy.packageCode} className="panel p-6">
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{policy.packageCode}</p>
                          <p className="mt-3 text-2xl font-semibold text-ink">{fmtCurrency(policy.budgetFloor)}</p>
                        </div>
                        <div className="flex items-center gap-2">
                          <span className="pill">{policy.requirePremiumNationalRadio ? 'Premium national radio required' : 'Flexible radio qualification'}</span>
                          <ActionButton label={`View ${policy.packageCode} policy`} icon={Eye} onClick={() => openEngineDialog('view', policy)} />
                          <ActionButton label={`Edit ${policy.packageCode} policy`} icon={Pencil} onClick={() => openEngineDialog('edit', policy)} />
                        </div>
                      </div>
                      <div className="mt-5 grid gap-2 text-sm text-ink-soft sm:grid-cols-2">
                        <div><span className="font-semibold text-ink">Min national radio:</span> {policy.minimumNationalRadioCandidates}</div>
                        <div><span className="font-semibold text-ink">National capable:</span> {policy.requireNationalCapableRadio ? 'Yes' : 'No'}</div>
                        <div><span className="font-semibold text-ink">National bonus:</span> {policy.nationalRadioBonus}</div>
                        <div><span className="font-semibold text-ink">Non-national penalty:</span> {policy.nonNationalRadioPenalty}</div>
                        <div><span className="font-semibold text-ink">Regional penalty:</span> {policy.regionalRadioPenalty}</div>
                      </div>
                    </div>
                  ))}
                </div>
              </section>

              <section className="space-y-4">
                <div className="flex items-center justify-between gap-4">
                  <div>
                    <h2 className="text-lg font-semibold text-ink">Planning allocation bands</h2>
                    <p className="mt-1 text-sm text-ink-soft">These bands are the single source of truth for OOH priority, TV floor eligibility, and radio/digital distribution by budget.</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <ActionButton label="View planning allocation settings" icon={Eye} onClick={() => openAllocationDialog('view')} />
                    <ActionButton label="Edit planning allocation settings" icon={Pencil} onClick={() => openAllocationDialog('edit')} />
                  </div>
                </div>

                <div className="grid gap-4 xl:grid-cols-2">
                  {dashboard.planningAllocationSettings.budgetBands.map((band) => (
                    <div key={band.name} className="panel p-6">
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{band.name}</p>
                          <p className="mt-3 text-base font-semibold text-ink">{fmtCurrency(band.min)} to {fmtCurrency(band.max)}</p>
                        </div>
                        <span className="pill">{band.tvEligible ? 'TV eligible' : 'TV not eligible'}</span>
                      </div>
                      <div className="mt-5 grid gap-2 text-sm text-ink-soft sm:grid-cols-2">
                        <div><span className="font-semibold text-ink">OOH target:</span> {formatRatio(band.oohTarget)}</div>
                        <div><span className="font-semibold text-ink">TV minimum:</span> {formatRatio(band.tvMin)}</div>
                        <div><span className="font-semibold text-ink">Radio range:</span> {formatRatio(band.radioRange[0])} to {formatRatio(band.radioRange[1])}</div>
                        <div><span className="font-semibold text-ink">Digital range:</span> {formatRatio(band.digitalRange[0])} to {formatRatio(band.digitalRange[1])}</div>
                      </div>
                    </div>
                  ))}
                </div>

                <div className="panel p-6">
                  <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Global rules</p>
                  <div className="mt-4 grid gap-2 text-sm text-ink-soft sm:grid-cols-3">
                    <div><span className="font-semibold text-ink">Max OOH:</span> {formatRatio(dashboard.planningAllocationSettings.globalRules.maxOoh)}</div>
                    <div><span className="font-semibold text-ink">Min digital:</span> {formatRatio(dashboard.planningAllocationSettings.globalRules.minDigital)}</div>
                    <div><span className="font-semibold text-ink">TV floor if preferred:</span> {dashboard.planningAllocationSettings.globalRules.enforceTvFloorIfPreferred ? 'Yes' : 'No'}</div>
                  </div>
                </div>
              </section>
            </div>

            {engineDialog && selectedPolicy ? (
              <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                <div className="w-full max-w-4xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                  <div className="flex items-center justify-between gap-4">
                    <h3 className="text-xl font-semibold text-ink">{engineDialog.mode === 'view' ? 'View engine policy' : 'Edit engine policy'}</h3>
                    <button type="button" className="button-secondary p-3" onClick={() => setEngineDialog(null)}>
                      <X className="size-4" />
                    </button>
                  </div>
                  {engineDialog.mode === 'view' ? <ReadOnlyNotice label="This policy is open in view mode. Switch to edit mode to persist a new engine-policy override." /> : null}
                  <div className="mt-4 flex flex-wrap items-center gap-3 text-sm text-ink-soft"><span className="pill">{selectedPolicy.packageCode}</span></div>
                  <div className="mt-6 grid gap-4 md:grid-cols-2">
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Budget floor" value={engineForm.budgetFloor} onChange={(event) => setEngineForm((current) => ({ ...current, budgetFloor: Number(event.target.value) }))} />
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Minimum national radio candidates" value={engineForm.minimumNationalRadioCandidates} onChange={(event) => setEngineForm((current) => ({ ...current, minimumNationalRadioCandidates: Number(event.target.value) }))} />
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="National radio bonus" value={engineForm.nationalRadioBonus} onChange={(event) => setEngineForm((current) => ({ ...current, nationalRadioBonus: Number(event.target.value) }))} />
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Non-national radio penalty" value={engineForm.nonNationalRadioPenalty} onChange={(event) => setEngineForm((current) => ({ ...current, nonNationalRadioPenalty: Number(event.target.value) }))} />
                    <input disabled={engineDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Regional radio penalty" value={engineForm.regionalRadioPenalty} onChange={(event) => setEngineForm((current) => ({ ...current, regionalRadioPenalty: Number(event.target.value) }))} />
                    <div className="flex flex-wrap items-center gap-5 rounded-[24px] border border-line px-4 py-3 text-sm text-ink-soft md:col-span-2">
                      <label className="inline-flex items-center gap-2">
                        <input disabled={engineDialog.mode === 'view'} type="checkbox" checked={engineForm.requireNationalCapableRadio} onChange={(event) => setEngineForm((current) => ({ ...current, requireNationalCapableRadio: event.target.checked }))} />
                        Require national-capable radio
                      </label>
                      <label className="inline-flex items-center gap-2">
                        <input disabled={engineDialog.mode === 'view'} type="checkbox" checked={engineForm.requirePremiumNationalRadio} onChange={(event) => setEngineForm((current) => ({ ...current, requirePremiumNationalRadio: event.target.checked }))} />
                        Require premium national radio
                      </label>
                    </div>
                  </div>
                  <div className="mt-6 flex justify-end gap-3">
                    <button type="button" className="button-secondary px-5 py-3" onClick={() => setEngineDialog(null)}>Close</button>
                    {engineDialog.mode === 'view' ? (
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => openEngineDialog('edit', selectedPolicy)}>Edit policy</button>
                    ) : (
                      <button type="button" className="button-primary px-5 py-3" disabled={updateEnginePolicyMutation.isPending} onClick={() => updateEnginePolicyMutation.mutate()}>Save policy</button>
                    )}
                  </div>
                </div>
              </div>
            ) : null}

            {allocationDialogMode ? (
              <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                <div className="w-full max-w-6xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                  <div className="flex items-center justify-between gap-4">
                    <h3 className="text-xl font-semibold text-ink">{allocationDialogMode === 'view' ? 'View planning allocation settings' : 'Edit planning allocation settings'}</h3>
                    <button type="button" className="button-secondary p-3" onClick={() => setAllocationDialogMode(null)}>
                      <X className="size-4" />
                    </button>
                  </div>
                  {allocationDialogMode === 'view' ? <ReadOnlyNotice label="These live budget bands are open in view mode. Switch to edit mode to persist changes." /> : null}

                  <div className="mt-6 space-y-6">
                    <div className="grid gap-4 md:grid-cols-3">
                      <label className="text-sm text-ink-soft">
                        <span className="font-semibold text-ink">Max OOH</span>
                        <input
                          disabled={allocationDialogMode === 'view'}
                          className="input-base mt-2 disabled:bg-slate-50"
                          type="number"
                          step="0.01"
                          value={allocationForm.globalRules.maxOoh}
                          onChange={(event) => setAllocationForm((current) => ({
                            ...current,
                            globalRules: { ...current.globalRules, maxOoh: Number(event.target.value) },
                          }))}
                        />
                      </label>
                      <label className="text-sm text-ink-soft">
                        <span className="font-semibold text-ink">Min digital</span>
                        <input
                          disabled={allocationDialogMode === 'view'}
                          className="input-base mt-2 disabled:bg-slate-50"
                          type="number"
                          step="0.01"
                          value={allocationForm.globalRules.minDigital}
                          onChange={(event) => setAllocationForm((current) => ({
                            ...current,
                            globalRules: { ...current.globalRules, minDigital: Number(event.target.value) },
                          }))}
                        />
                      </label>
                      <label className="flex items-center gap-2 rounded-[24px] border border-line px-4 py-3 text-sm text-ink-soft">
                        <input
                          disabled={allocationDialogMode === 'view'}
                          type="checkbox"
                          checked={allocationForm.globalRules.enforceTvFloorIfPreferred}
                          onChange={(event) => setAllocationForm((current) => ({
                            ...current,
                            globalRules: { ...current.globalRules, enforceTvFloorIfPreferred: event.target.checked },
                          }))}
                        />
                        Enforce TV floor if preferred
                      </label>
                    </div>

                    <div className="space-y-4">
                      <div className="flex items-center justify-between gap-4">
                        <p className="text-sm font-semibold text-ink">Budget bands</p>
                        {allocationDialogMode === 'edit' ? (
                          <button
                            type="button"
                            className="button-secondary inline-flex items-center gap-2 px-4 py-2"
                            onClick={() => setAllocationForm((current) => ({
                              ...current,
                              budgetBands: [...current.budgetBands, defaultBudgetBand()],
                            }))}
                          >
                            <Plus className="size-4" />
                            Add band
                          </button>
                        ) : null}
                      </div>

                      {allocationForm.budgetBands.map((band, index) => (
                        <div key={`${band.name || 'band'}-${index}`} className="rounded-[24px] border border-line p-5">
                          <div className="flex items-center justify-between gap-4">
                            <p className="text-sm font-semibold text-ink">Band {index + 1}</p>
                            {allocationDialogMode === 'edit' && allocationForm.budgetBands.length > 1 ? (
                              <button
                                type="button"
                                className="button-secondary inline-flex items-center gap-2 px-4 py-2 text-rose-600"
                                onClick={() => setAllocationForm((current) => ({
                                  ...current,
                                  budgetBands: current.budgetBands.filter((_, bandIndex) => bandIndex !== index),
                                }))}
                              >
                                <Trash2 className="size-4" />
                                Remove
                              </button>
                            ) : null}
                          </div>

                          <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-5">
                            <label className="text-sm text-ink-soft">
                              <span className="font-semibold text-ink">Name</span>
                              <input disabled={allocationDialogMode === 'view'} className="input-base mt-2 disabled:bg-slate-50" value={band.name} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, name: event.target.value }))} />
                            </label>
                            <label className="text-sm text-ink-soft">
                              <span className="font-semibold text-ink">Min</span>
                              <input disabled={allocationDialogMode === 'view'} className="input-base mt-2 disabled:bg-slate-50" type="number" value={band.min} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, min: Number(event.target.value) }))} />
                            </label>
                            <label className="text-sm text-ink-soft">
                              <span className="font-semibold text-ink">Max</span>
                              <input disabled={allocationDialogMode === 'view'} className="input-base mt-2 disabled:bg-slate-50" type="number" value={band.max} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, max: Number(event.target.value) }))} />
                            </label>
                            <label className="text-sm text-ink-soft">
                              <span className="font-semibold text-ink">OOH target</span>
                              <input disabled={allocationDialogMode === 'view'} className="input-base mt-2 disabled:bg-slate-50" type="number" step="0.01" value={band.oohTarget} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, oohTarget: Number(event.target.value) }))} />
                            </label>
                            <label className="text-sm text-ink-soft">
                              <span className="font-semibold text-ink">TV minimum</span>
                              <input disabled={allocationDialogMode === 'view'} className="input-base mt-2 disabled:bg-slate-50" type="number" step="0.01" value={band.tvMin} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, tvMin: Number(event.target.value) }))} />
                            </label>
                            <label className="text-sm text-ink-soft">
                              <span className="font-semibold text-ink">Radio min</span>
                              <input disabled={allocationDialogMode === 'view'} className="input-base mt-2 disabled:bg-slate-50" type="number" step="0.01" value={band.radioRange[0]} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, radioRange: [Number(event.target.value), current.radioRange[1]] }))} />
                            </label>
                            <label className="text-sm text-ink-soft">
                              <span className="font-semibold text-ink">Radio max</span>
                              <input disabled={allocationDialogMode === 'view'} className="input-base mt-2 disabled:bg-slate-50" type="number" step="0.01" value={band.radioRange[1]} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, radioRange: [current.radioRange[0], Number(event.target.value)] }))} />
                            </label>
                            <label className="text-sm text-ink-soft">
                              <span className="font-semibold text-ink">Digital min</span>
                              <input disabled={allocationDialogMode === 'view'} className="input-base mt-2 disabled:bg-slate-50" type="number" step="0.01" value={band.digitalRange[0]} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, digitalRange: [Number(event.target.value), current.digitalRange[1]] }))} />
                            </label>
                            <label className="text-sm text-ink-soft">
                              <span className="font-semibold text-ink">Digital max</span>
                              <input disabled={allocationDialogMode === 'view'} className="input-base mt-2 disabled:bg-slate-50" type="number" step="0.01" value={band.digitalRange[1]} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, digitalRange: [current.digitalRange[0], Number(event.target.value)] }))} />
                            </label>
                            <label className="flex items-center gap-2 rounded-[24px] border border-line px-4 py-3 text-sm text-ink-soft">
                              <input disabled={allocationDialogMode === 'view'} type="checkbox" checked={band.tvEligible} onChange={(event) => updateBudgetBand(index, (current) => ({ ...current, tvEligible: event.target.checked }))} />
                              TV eligible
                            </label>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>

                  <div className="mt-6 flex justify-end gap-3">
                    <button type="button" className="button-secondary px-5 py-3" onClick={() => setAllocationDialogMode(null)}>Close</button>
                    {allocationDialogMode === 'view' ? (
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => openAllocationDialog('edit')}>Edit settings</button>
                    ) : (
                      <button type="button" className="button-primary px-5 py-3" disabled={updatePlanningAllocationMutation.isPending} onClick={() => updatePlanningAllocationMutation.mutate()}>Save settings</button>
                    )}
                  </div>
                </div>
              </div>
            ) : null}
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

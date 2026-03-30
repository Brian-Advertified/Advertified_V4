import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, X } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AdminUpdateEnginePolicyInput } from '../../types/domain';
import { ActionButton, ReadOnlyNotice } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, fmtCurrency, useAdminDashboardQuery } from './adminWorkspace';

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

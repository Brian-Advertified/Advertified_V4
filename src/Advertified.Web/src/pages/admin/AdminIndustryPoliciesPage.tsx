import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, PlusCircle, Trash2, X } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AdminLeadIndustryPolicy, AdminUpsertLeadIndustryPolicyInput } from '../../types/domain';
import { ActionButton, ReadOnlyNotice, hasText } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, useAdminDashboardQuery } from './adminWorkspace';

const splitLines = (value: string) => value.split('\n').map((item) => item.trim()).filter(Boolean);
const joinLines = (items: string[]) => items.join('\n');

export function AdminIndustryPoliciesPage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [dialog, setDialog] = useState<{ mode: 'create' | 'view' | 'edit'; key?: string } | null>(null);
  const [form, setForm] = useState<AdminUpsertLeadIndustryPolicyInput>({
    key: '',
    name: '',
    objectiveOverride: '',
    preferredTone: '',
    preferredChannels: [],
    cta: '',
    messagingAngle: '',
    guardrails: [],
    additionalGap: '',
    additionalOutcome: '',
    sortOrder: 0,
    isActive: true,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
  };

  const createMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminLeadIndustryPolicy(form),
    onSuccess: async () => {
      await refresh();
      setDialog(null);
      pushToast({ title: 'Industry policy created.', description: 'The lead strategy policy is now stored as live operator-managed data.' });
    },
    onError: (error) => pushToast({ title: 'Could not create industry policy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updateMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminLeadIndustryPolicy(dialog?.key ?? '', form),
    onSuccess: async () => {
      await refresh();
      setDialog(null);
      pushToast({ title: 'Industry policy updated.', description: 'The lead strategy policy changes are now live.' });
    },
    onError: (error) => pushToast({ title: 'Could not update industry policy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteMutation = useMutation({
    mutationFn: (key: string) => advertifiedApi.deleteAdminLeadIndustryPolicy(key),
    onSuccess: async () => {
      await refresh();
      setDialog(null);
      pushToast({ title: 'Industry policy deleted.', description: 'The policy was removed from the live strategy catalog.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete industry policy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const selectedPolicy = dialog?.key
          ? dashboard.leadIndustryPolicies.find((item) => item.key === dialog.key) ?? null
          : null;

        const hydrateForm = (item: AdminLeadIndustryPolicy) => {
          setForm({
            key: item.key,
            name: item.name,
            objectiveOverride: item.objectiveOverride ?? '',
            preferredTone: item.preferredTone ?? '',
            preferredChannels: item.preferredChannels,
            cta: item.cta,
            messagingAngle: item.messagingAngle,
            guardrails: item.guardrails,
            additionalGap: item.additionalGap,
            additionalOutcome: item.additionalOutcome,
            sortOrder: item.sortOrder,
            isActive: item.isActive,
          });
        };

        const openDialog = (mode: 'create' | 'view' | 'edit', item?: AdminLeadIndustryPolicy) => {
          if (item) {
            hydrateForm(item);
            setDialog({ mode, key: item.key });
            return;
          }

          const nextSortOrder = dashboard.leadIndustryPolicies.length > 0
            ? Math.max(...dashboard.leadIndustryPolicies.map((entry) => entry.sortOrder)) + 10
            : 10;

          setForm({
            key: '',
            name: '',
            objectiveOverride: '',
            preferredTone: '',
            preferredChannels: [],
            cta: '',
            messagingAngle: '',
            guardrails: [],
            additionalGap: '',
            additionalOutcome: '',
            sortOrder: nextSortOrder,
            isActive: true,
          });
          setDialog({ mode });
        };

        const formIsValid = hasText(form.key) && hasText(form.name) && hasText(form.cta) && hasText(form.messagingAngle);

        return (
          <AdminPageShell title="Lead industry policies" description="Manage the live business-strategy rules used to steer lead intelligence, campaign guidance, and messaging defaults without deploying code.">
            <section className="space-y-6">
              <div className="panel p-6">
                <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                  <div>
                    <h3 className="text-lg font-semibold text-ink">Policy catalog</h3>
                    <p className="mt-2 text-sm text-ink-soft">These policies drive lead strategy guidance at runtime. Keep them operationally owned and review them like any other live business rule.</p>
                  </div>
                  <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3" onClick={() => openDialog('create')}>
                    <PlusCircle className="size-4" />
                    Add industry policy
                  </button>
                </div>
              </div>

              <div className="grid gap-4 xl:grid-cols-2">
                {dashboard.leadIndustryPolicies.map((policy) => (
                  <div key={policy.key} className="panel p-6">
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="pill">{policy.key}</span>
                          <span className={`pill ${policy.isActive ? '' : 'border-rose-200 bg-rose-50 text-rose-700'}`}>
                            {policy.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </div>
                        <h3 className="mt-4 text-xl font-semibold text-ink">{policy.name}</h3>
                        <p className="mt-2 text-sm text-ink-soft">{policy.messagingAngle}</p>
                      </div>
                      <div className="flex items-center gap-2">
                        <ActionButton label={`View ${policy.name}`} icon={Eye} onClick={() => openDialog('view', policy)} />
                        <ActionButton label={`Edit ${policy.name}`} icon={Pencil} onClick={() => openDialog('edit', policy)} />
                      </div>
                    </div>
                    <div className="mt-5 grid gap-2 text-sm text-ink-soft">
                      <div><span className="font-semibold text-ink">Objective:</span> {policy.objectiveOverride || 'Not forced'}</div>
                      <div><span className="font-semibold text-ink">Tone:</span> {policy.preferredTone || 'Not forced'}</div>
                      <div><span className="font-semibold text-ink">Channels:</span> {policy.preferredChannels.length > 0 ? policy.preferredChannels.join(', ') : 'No preferred channels'}</div>
                      <div><span className="font-semibold text-ink">Sort order:</span> {policy.sortOrder}</div>
                    </div>
                  </div>
                ))}
              </div>

              {dialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-5xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4">
                      <h3 className="text-xl font-semibold text-ink">
                        {dialog.mode === 'create' ? 'Add industry policy' : dialog.mode === 'view' ? 'View industry policy' : 'Edit industry policy'}
                      </h3>
                      <button type="button" className="button-secondary p-3" onClick={() => setDialog(null)}>
                        <X className="size-4" />
                      </button>
                    </div>
                    {dialog.mode === 'view' && selectedPolicy ? (
                      <ReadOnlyNotice label={`Viewing ${selectedPolicy.name}. Switch to edit mode when you need to change runtime lead strategy behaviour.`} />
                    ) : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                      <input disabled={dialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Policy key" value={form.key} onChange={(event) => setForm((current) => ({ ...current, key: event.target.value }))} />
                      <input disabled={dialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Display name" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} />
                      <input disabled={dialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Sort order" value={form.sortOrder} onChange={(event) => setForm((current) => ({ ...current, sortOrder: Number(event.target.value) }))} />
                      <input disabled={dialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Objective override" value={form.objectiveOverride ?? ''} onChange={(event) => setForm((current) => ({ ...current, objectiveOverride: event.target.value }))} />
                      <input disabled={dialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Preferred tone" value={form.preferredTone ?? ''} onChange={(event) => setForm((current) => ({ ...current, preferredTone: event.target.value }))} />
                      <label className="inline-flex items-center gap-2 rounded-full border border-line px-4 py-3 text-sm text-ink-soft">
                        <input disabled={dialog.mode === 'view'} type="checkbox" checked={form.isActive} onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))} />
                        Active
                      </label>
                    </div>
                    <div className="mt-4 grid gap-4 md:grid-cols-2">
                      <textarea disabled={dialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Preferred channels, one per line" value={joinLines(form.preferredChannels)} onChange={(event) => setForm((current) => ({ ...current, preferredChannels: splitLines(event.target.value) }))} />
                      <textarea disabled={dialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Guardrails, one per line" value={joinLines(form.guardrails)} onChange={(event) => setForm((current) => ({ ...current, guardrails: splitLines(event.target.value) }))} />
                    </div>
                    <input disabled={dialog.mode === 'view'} className="input-base mt-4 disabled:bg-slate-50" placeholder="CTA" value={form.cta} onChange={(event) => setForm((current) => ({ ...current, cta: event.target.value }))} />
                    <textarea disabled={dialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Messaging angle" value={form.messagingAngle} onChange={(event) => setForm((current) => ({ ...current, messagingAngle: event.target.value }))} />
                    <textarea disabled={dialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Additional gap" value={form.additionalGap} onChange={(event) => setForm((current) => ({ ...current, additionalGap: event.target.value }))} />
                    <textarea disabled={dialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Additional outcome" value={form.additionalOutcome} onChange={(event) => setForm((current) => ({ ...current, additionalOutcome: event.target.value }))} />
                    {!formIsValid ? <p className="mt-3 text-sm text-rose-600">Key, name, CTA, and messaging angle are required before saving.</p> : null}
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => setDialog(null)}>Close</button>
                      {dialog.mode === 'view' && selectedPolicy ? (
                        <button type="button" className="button-secondary px-5 py-3" onClick={() => openDialog('edit', selectedPolicy)}>
                          Edit policy
                        </button>
                      ) : null}
                      {dialog.mode === 'edit' && selectedPolicy ? (
                        <button
                          type="button"
                          className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50"
                          disabled={deleteMutation.isPending}
                          onClick={() => {
                            if (window.confirm(`Delete industry policy ${selectedPolicy.name}? This changes live lead strategy behaviour.`)) {
                              deleteMutation.mutate(selectedPolicy.key);
                            }
                          }}
                        >
                          <Trash2 className="mr-2 inline size-4" />
                          Delete policy
                        </button>
                      ) : null}
                      {dialog.mode === 'create' ? (
                        <button type="button" className="button-primary px-5 py-3" disabled={!formIsValid || createMutation.isPending} onClick={() => createMutation.mutate()}>
                          Save policy
                        </button>
                      ) : null}
                      {dialog.mode === 'edit' ? (
                        <button type="button" className="button-primary px-5 py-3" disabled={!formIsValid || updateMutation.isPending} onClick={() => updateMutation.mutate()}>
                          Update policy
                        </button>
                      ) : null}
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

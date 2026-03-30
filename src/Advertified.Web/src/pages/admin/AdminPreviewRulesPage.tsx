import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, Save, X } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import { ActionButton, ReadOnlyNotice, hasText } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, splitList, useAdminDashboardQuery } from './adminWorkspace';

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

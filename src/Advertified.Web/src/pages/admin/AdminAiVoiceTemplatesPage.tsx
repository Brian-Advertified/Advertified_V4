import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileText, Plus, Trash2 } from 'lucide-react';
import { AdminPageShell, AdminQueryBoundary, fmtDate, useAdminDashboardQuery } from './adminWorkspace';
import { advertifiedApi } from '../../services/advertifiedApi';

type VoiceTemplate = {
  id: string;
  templateNumber: number;
  category: string;
  name: string;
  promptTemplate: string;
  primaryVoicePackName: string;
  fallbackVoicePackNames: string[];
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
};

type VoiceTemplateForm = {
  templateNumber: number;
  category: string;
  name: string;
  promptTemplate: string;
  primaryVoicePackName: string;
  fallbackVoicePackNames: string;
  isActive: boolean;
  sortOrder: number;
};

const emptyForm: VoiceTemplateForm = {
  templateNumber: 1,
  category: '',
  name: '',
  promptTemplate: '',
  primaryVoicePackName: '',
  fallbackVoicePackNames: '',
  isActive: true,
  sortOrder: 0,
};

function splitList(value: string) {
  return value.split(',').map((item) => item.trim()).filter(Boolean);
}

export function AdminAiVoiceTemplatesPage() {
  const dashboardQuery = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const [form, setForm] = useState<VoiceTemplateForm>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const templatesQuery = useQuery({
    queryKey: ['admin-ai-voice-templates'],
    queryFn: () => advertifiedApi.getAdminAiVoiceTemplates(),
  });

  const saveMutation = useMutation({
    mutationFn: async () => {
      const payload = {
        templateNumber: form.templateNumber,
        category: form.category,
        name: form.name,
        promptTemplate: form.promptTemplate,
        primaryVoicePackName: form.primaryVoicePackName,
        fallbackVoicePackNames: splitList(form.fallbackVoicePackNames),
        isActive: form.isActive,
        sortOrder: form.sortOrder,
      };

      if (editingId) {
        return advertifiedApi.updateAdminAiVoiceTemplate(editingId, payload);
      }

      return advertifiedApi.createAdminAiVoiceTemplate(payload);
    },
    onSuccess: () => {
      setMessage(editingId ? 'Voice template updated.' : 'Voice template created.');
      setEditingId(null);
      setForm(emptyForm);
      queryClient.invalidateQueries({ queryKey: ['admin-ai-voice-templates'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => advertifiedApi.deleteAdminAiVoiceTemplate(id),
    onSuccess: () => {
      setMessage('Voice template removed.');
      setEditingId(null);
      setForm(emptyForm);
      queryClient.invalidateQueries({ queryKey: ['admin-ai-voice-templates'] });
    },
  });

  const canSave = useMemo(
    () => form.templateNumber > 0
      && form.category.trim().length > 0
      && form.name.trim().length > 0
      && form.promptTemplate.trim().length > 0
      && form.primaryVoicePackName.trim().length > 0
      && !saveMutation.isPending,
    [form, saveMutation.isPending],
  );

  const startEdit = (item: VoiceTemplate) => {
    setEditingId(item.id);
    setForm({
      templateNumber: item.templateNumber,
      category: item.category,
      name: item.name,
      promptTemplate: item.promptTemplate,
      primaryVoicePackName: item.primaryVoicePackName,
      fallbackVoicePackNames: item.fallbackVoicePackNames.join(', '),
      isActive: item.isActive,
      sortOrder: item.sortOrder,
    });
    setMessage(null);
  };

  return (
    <AdminQueryBoundary query={dashboardQuery}>
      {() => (
        <AdminPageShell title="AI Voice Templates" description="Manage the 55+ South African voice ad prompt templates and mapping metadata.">
          <div className="panel p-6">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Template Library</p>
                <h2 className="mt-2 text-2xl font-semibold text-ink">Voice script templates</h2>
                <p className="mt-2 text-sm text-ink-soft">Each template drives auto-selection and voice-pack mapping.</p>
              </div>
              <div className="rounded-2xl border border-brand/30 bg-brand-soft px-4 py-3 text-xs text-brand">
                <div className="flex items-center gap-2 font-semibold">
                  <FileText className="size-4" />
                  55+ SA Prompt Templates
                </div>
              </div>
            </div>

            <div className="mt-6 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Template number
                <input className="input-base" type="number" value={form.templateNumber} onChange={(event) => setForm((current) => ({ ...current, templateNumber: Number(event.target.value) || 0 }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Category
                <input className="input-base" value={form.category} onChange={(event) => setForm((current) => ({ ...current, category: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Name
                <input className="input-base" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Primary voice pack
                <input className="input-base" value={form.primaryVoicePackName} onChange={(event) => setForm((current) => ({ ...current, primaryVoicePackName: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft md:col-span-2">
                Fallback voice packs (comma separated)
                <input className="input-base" value={form.fallbackVoicePackNames} onChange={(event) => setForm((current) => ({ ...current, fallbackVoicePackNames: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft xl:col-span-3">
                Prompt template
                <textarea className="input-base min-h-[140px]" value={form.promptTemplate} onChange={(event) => setForm((current) => ({ ...current, promptTemplate: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Sort order
                <input className="input-base" type="number" value={form.sortOrder} onChange={(event) => setForm((current) => ({ ...current, sortOrder: Number(event.target.value) || 0 }))} />
              </label>
              <label className="flex items-center gap-2 text-sm text-ink">
                <input type="checkbox" checked={form.isActive} onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))} />
                Active
              </label>
            </div>

            <div className="mt-4 flex flex-wrap gap-2">
              <button type="button" onClick={() => saveMutation.mutate()} disabled={!canSave} className="button-primary inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50">
                <Plus className="size-4" />
                {editingId ? 'Update template' : 'Add template'}
              </button>
              <button type="button" onClick={() => { setEditingId(null); setForm(emptyForm); setMessage(null); }} className="button-secondary px-4 py-2 text-sm font-semibold">Clear</button>
            </div>

            {message ? <p className="mt-3 text-sm text-brand">{message}</p> : null}
            {saveMutation.error instanceof Error ? <p className="mt-3 text-sm text-rose-600">{saveMutation.error.message}</p> : null}
          </div>

          <div className="panel overflow-hidden p-0">
            <div className="border-b border-line px-6 py-4">
              <h3 className="text-lg font-semibold text-ink">Configured templates</h3>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-slate-50 text-xs uppercase tracking-[0.14em] text-ink-soft">
                  <tr>
                    <th className="px-4 py-3">#</th>
                    <th className="px-4 py-3">Name</th>
                    <th className="px-4 py-3">Category</th>
                    <th className="px-4 py-3">Primary Voice</th>
                    <th className="px-4 py-3">Updated</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {(templatesQuery.data ?? []).map((item) => (
                    <tr key={item.id} className="border-t border-line">
                      <td className="px-4 py-3 font-semibold text-ink">{item.templateNumber}</td>
                      <td className="px-4 py-3 text-ink">{item.name}</td>
                      <td className="px-4 py-3 text-ink-soft">{item.category}</td>
                      <td className="px-4 py-3 text-ink-soft">{item.primaryVoicePackName}</td>
                      <td className="px-4 py-3 text-ink-soft">{fmtDate(item.updatedAt)}</td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-2">
                          <button type="button" onClick={() => startEdit(item)} className="button-secondary px-3 py-1.5 text-xs font-semibold">Edit</button>
                          <button type="button" onClick={() => deleteMutation.mutate(item.id)} disabled={deleteMutation.isPending} className="inline-flex items-center gap-1 rounded-lg border border-rose-200 bg-rose-50 px-3 py-1.5 text-xs font-semibold text-rose-700 disabled:opacity-50">
                            <Trash2 className="size-3.5" />
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {!templatesQuery.isLoading && (templatesQuery.data ?? []).length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-6 text-center text-sm text-ink-soft">No templates yet.</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          </div>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Mic2, Plus, Trash2 } from 'lucide-react';
import { AdminPageShell, AdminQueryBoundary, fmtDate, useAdminDashboardQuery } from './adminWorkspace';
import { advertifiedApi } from '../../services/advertifiedApi';

type VoiceProfile = {
  id: string;
  provider: string;
  label: string;
  voiceId: string;
  language?: string | null;
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
};

type VoiceProfileForm = {
  provider: string;
  label: string;
  voiceId: string;
  language: string;
  isActive: boolean;
  sortOrder: number;
};

const emptyForm: VoiceProfileForm = {
  provider: 'ElevenLabs',
  label: '',
  voiceId: '',
  language: '',
  isActive: true,
  sortOrder: 0,
};

export function AdminAiVoicesPage() {
  const dashboardQuery = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const [form, setForm] = useState<VoiceProfileForm>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const voicesQuery = useQuery({
    queryKey: ['admin-ai-voices'],
    queryFn: () => advertifiedApi.getAdminAiVoiceProfiles('ElevenLabs'),
  });

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (editingId) {
        return advertifiedApi.updateAdminAiVoiceProfile(editingId, form);
      }

      return advertifiedApi.createAdminAiVoiceProfile(form);
    },
    onSuccess: () => {
      setMessage(editingId ? 'Voice updated.' : 'Voice created.');
      setForm(emptyForm);
      setEditingId(null);
      queryClient.invalidateQueries({ queryKey: ['admin-ai-voices'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => advertifiedApi.deleteAdminAiVoiceProfile(id),
    onSuccess: () => {
      setMessage('Voice removed.');
      queryClient.invalidateQueries({ queryKey: ['admin-ai-voices'] });
      if (editingId) {
        setEditingId(null);
        setForm(emptyForm);
      }
    },
  });

  const canSave = useMemo(
    () => form.provider.trim().length > 0 && form.label.trim().length > 0 && form.voiceId.trim().length > 0 && !saveMutation.isPending,
    [form.provider, form.label, form.voiceId, saveMutation.isPending],
  );

  const startEdit = (item: VoiceProfile) => {
    setEditingId(item.id);
    setForm({
      provider: item.provider,
      label: item.label,
      voiceId: item.voiceId,
      language: item.language ?? '',
      isActive: item.isActive,
      sortOrder: item.sortOrder,
    });
    setMessage(null);
  };

  const resetForm = () => {
    setEditingId(null);
    setForm(emptyForm);
    setMessage(null);
  };

  return (
    <AdminQueryBoundary query={dashboardQuery}>
      {() => (
        <AdminPageShell title="AI Voice Library" description="Manage ElevenLabs voice labels and IDs from admin without server env edits.">
          <div className="panel p-6">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Voice Profiles</p>
                <h2 className="mt-2 text-2xl font-semibold text-ink">ElevenLabs mappings</h2>
                <p className="mt-2 text-sm text-ink-soft">Use stable labels so agents select from dropdowns instead of typing IDs manually.</p>
              </div>
              <div className="rounded-2xl border border-brand/30 bg-brand-soft px-4 py-3 text-xs text-brand">
                <div className="flex items-center gap-2 font-semibold">
                  <Mic2 className="size-4" />
                  Provider: ElevenLabs
                </div>
              </div>
            </div>

            <div className="mt-6 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Label
                <input className="input-base" value={form.label} onChange={(event) => setForm((current) => ({ ...current, label: event.target.value }))} placeholder="Male_NewsReader" />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Voice ID
                <input className="input-base" value={form.voiceId} onChange={(event) => setForm((current) => ({ ...current, voiceId: event.target.value }))} placeholder="5MzHthZLVdOq5l6Zr0Zo" />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Language (Optional)
                <input className="input-base" value={form.language} onChange={(event) => setForm((current) => ({ ...current, language: event.target.value }))} placeholder="Afrikaans" />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Sort Order
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
                {editingId ? 'Update voice' : 'Add voice'}
              </button>
              <button type="button" onClick={resetForm} className="button-secondary px-4 py-2 text-sm font-semibold">Clear</button>
            </div>

            {message ? <p className="mt-3 text-sm text-brand">{message}</p> : null}
            {saveMutation.error instanceof Error ? <p className="mt-3 text-sm text-rose-600">{saveMutation.error.message}</p> : null}
          </div>

          <div className="panel overflow-hidden p-0">
            <div className="border-b border-line px-6 py-4">
              <h3 className="text-lg font-semibold text-ink">Configured voices</h3>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-slate-50 text-xs uppercase tracking-[0.14em] text-ink-soft">
                  <tr>
                    <th className="px-4 py-3">Label</th>
                    <th className="px-4 py-3">Voice ID</th>
                    <th className="px-4 py-3">Language</th>
                    <th className="px-4 py-3">Active</th>
                    <th className="px-4 py-3">Updated</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {(voicesQuery.data ?? []).map((item) => (
                    <tr key={item.id} className="border-t border-line">
                      <td className="px-4 py-3 font-semibold text-ink">{item.label}</td>
                      <td className="px-4 py-3 font-mono text-xs text-ink-soft">{item.voiceId}</td>
                      <td className="px-4 py-3 text-ink-soft">{item.language ?? '-'}</td>
                      <td className="px-4 py-3 text-ink-soft">{item.isActive ? 'Yes' : 'No'}</td>
                      <td className="px-4 py-3 text-ink-soft">{fmtDate(item.updatedAt)}</td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-2">
                          <button type="button" onClick={() => startEdit(item)} className="button-secondary px-3 py-1.5 text-xs font-semibold">Edit</button>
                          <button
                            type="button"
                            onClick={() => deleteMutation.mutate(item.id)}
                            disabled={deleteMutation.isPending}
                            className="inline-flex items-center gap-1 rounded-lg border border-rose-200 bg-rose-50 px-3 py-1.5 text-xs font-semibold text-rose-700 disabled:opacity-50"
                          >
                            <Trash2 className="size-3.5" />
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {!voicesQuery.isLoading && (voicesQuery.data ?? []).length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-6 text-center text-sm text-ink-soft">No voice profiles yet.</td>
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

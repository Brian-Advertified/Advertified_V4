import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Layers3, Plus, Trash2 } from 'lucide-react';
import { AdminPageShell, AdminQueryBoundary, fmtDate, useAdminDashboardQuery } from './adminWorkspace';
import { advertifiedApi } from '../../services/advertifiedApi';

type VoicePack = {
  id: string;
  provider: string;
  name: string;
  accent?: string | null;
  language?: string | null;
  tone?: string | null;
  persona?: string | null;
  useCases: string[];
  voiceId: string;
  sampleAudioUrl?: string | null;
  promptTemplate: string;
  pricingTier: 'standard' | 'premium' | 'exclusive';
  isClientSpecific: boolean;
  clientUserId?: string | null;
  isClonedVoice: boolean;
  audienceTags: string[];
  objectiveTags: string[];
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
};

type VoicePackForm = {
  provider: string;
  name: string;
  accent: string;
  language: string;
  tone: string;
  persona: string;
  useCases: string;
  voiceId: string;
  sampleAudioUrl: string;
  promptTemplate: string;
  pricingTier: 'standard' | 'premium' | 'exclusive';
  isClientSpecific: boolean;
  clientUserId: string;
  isClonedVoice: boolean;
  audienceTags: string;
  objectiveTags: string;
  isActive: boolean;
  sortOrder: number;
};

const emptyForm: VoicePackForm = {
  provider: 'ElevenLabs',
  name: '',
  accent: '',
  language: '',
  tone: '',
  persona: '',
  useCases: '',
  voiceId: '',
  sampleAudioUrl: '',
  promptTemplate: '',
  pricingTier: 'standard',
  isClientSpecific: false,
  clientUserId: '',
  isClonedVoice: false,
  audienceTags: '',
  objectiveTags: '',
  isActive: true,
  sortOrder: 0,
};

function splitUseCases(value: string) {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
}

export function AdminAiVoicePacksPage() {
  const dashboardQuery = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const [form, setForm] = useState<VoicePackForm>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const voicePacksQuery = useQuery({
    queryKey: ['admin-ai-voice-packs'],
    queryFn: () => advertifiedApi.getAdminAiVoicePacks('ElevenLabs'),
  });

  const saveMutation = useMutation({
    mutationFn: async () => {
      const payload = {
        provider: form.provider,
        name: form.name,
        accent: form.accent || undefined,
        language: form.language || undefined,
        tone: form.tone || undefined,
        persona: form.persona || undefined,
        useCases: splitUseCases(form.useCases),
        voiceId: form.voiceId,
        sampleAudioUrl: form.sampleAudioUrl || undefined,
        promptTemplate: form.promptTemplate,
        pricingTier: form.pricingTier,
        isClientSpecific: form.isClientSpecific,
        clientUserId: form.clientUserId || undefined,
        isClonedVoice: form.isClonedVoice,
        audienceTags: splitUseCases(form.audienceTags),
        objectiveTags: splitUseCases(form.objectiveTags),
        isActive: form.isActive,
        sortOrder: form.sortOrder,
      };

      if (editingId) {
        return advertifiedApi.updateAdminAiVoicePack(editingId, payload);
      }

      return advertifiedApi.createAdminAiVoicePack(payload);
    },
    onSuccess: () => {
      setMessage(editingId ? 'Voice pack updated.' : 'Voice pack created.');
      setForm(emptyForm);
      setEditingId(null);
      queryClient.invalidateQueries({ queryKey: ['admin-ai-voice-packs'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => advertifiedApi.deleteAdminAiVoicePack(id),
    onSuccess: () => {
      setMessage('Voice pack removed.');
      setEditingId(null);
      setForm(emptyForm);
      queryClient.invalidateQueries({ queryKey: ['admin-ai-voice-packs'] });
    },
  });

  const canSave = useMemo(
    () => form.name.trim().length > 0
      && form.voiceId.trim().length > 0
      && form.promptTemplate.trim().length > 0
      && (!form.isClientSpecific || form.clientUserId.trim().length > 0)
      && !saveMutation.isPending,
    [form.name, form.voiceId, form.promptTemplate, form.isClientSpecific, form.clientUserId, saveMutation.isPending],
  );

  const startEdit = (item: VoicePack) => {
    setEditingId(item.id);
    setForm({
      provider: item.provider,
      name: item.name,
      accent: item.accent ?? '',
      language: item.language ?? '',
      tone: item.tone ?? '',
      persona: item.persona ?? '',
      useCases: item.useCases.join(', '),
      voiceId: item.voiceId,
      sampleAudioUrl: item.sampleAudioUrl ?? '',
      promptTemplate: item.promptTemplate,
      pricingTier: item.pricingTier,
      isClientSpecific: item.isClientSpecific,
      clientUserId: item.clientUserId ?? '',
      isClonedVoice: item.isClonedVoice,
      audienceTags: item.audienceTags.join(', '),
      objectiveTags: item.objectiveTags.join(', '),
      isActive: item.isActive,
      sortOrder: item.sortOrder,
    });
    setMessage(null);
  };

  return (
    <AdminQueryBoundary query={dashboardQuery}>
      {() => (
        <AdminPageShell title="AI Voice Packs" description="Create South African-ready voice identities with prompt templates and pricing tiers.">
          <div className="panel p-6">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Voice Marketplace</p>
                <h2 className="mt-2 text-2xl font-semibold text-ink">South African voice packs</h2>
                <p className="mt-2 text-sm text-ink-soft">Each pack combines voice, tone, language style, and reusable prompt template.</p>
              </div>
              <div className="rounded-2xl border border-brand/30 bg-brand-soft px-4 py-3 text-xs text-brand">
                <div className="flex items-center gap-2 font-semibold">
                  <Layers3 className="size-4" />
                  Provider: ElevenLabs
                </div>
              </div>
            </div>

            <div className="mt-6 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Pack name
                <input className="input-base" value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Voice ID
                <input className="input-base" value={form.voiceId} onChange={(event) => setForm((current) => ({ ...current, voiceId: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Pricing tier
                <select className="input-base" value={form.pricingTier} onChange={(event) => setForm((current) => ({ ...current, pricingTier: event.target.value as VoicePackForm['pricingTier'] }))}>
                  <option value="standard">Standard</option>
                  <option value="premium">Premium</option>
                  <option value="exclusive">Exclusive</option>
                </select>
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Client user id (optional)
                <input className="input-base" value={form.clientUserId} onChange={(event) => setForm((current) => ({ ...current, clientUserId: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Accent
                <input className="input-base" value={form.accent} onChange={(event) => setForm((current) => ({ ...current, accent: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Language
                <input className="input-base" value={form.language} onChange={(event) => setForm((current) => ({ ...current, language: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Tone
                <input className="input-base" value={form.tone} onChange={(event) => setForm((current) => ({ ...current, tone: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Persona
                <input className="input-base" value={form.persona} onChange={(event) => setForm((current) => ({ ...current, persona: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Use cases (comma separated)
                <input className="input-base" value={form.useCases} onChange={(event) => setForm((current) => ({ ...current, useCases: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Audience tags (comma separated)
                <input className="input-base" value={form.audienceTags} onChange={(event) => setForm((current) => ({ ...current, audienceTags: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Objective tags (comma separated)
                <input className="input-base" value={form.objectiveTags} onChange={(event) => setForm((current) => ({ ...current, objectiveTags: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Sample audio URL
                <input className="input-base" value={form.sampleAudioUrl} onChange={(event) => setForm((current) => ({ ...current, sampleAudioUrl: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft md:col-span-2 xl:col-span-3">
                Prompt template
                <textarea className="input-base min-h-[120px]" value={form.promptTemplate} onChange={(event) => setForm((current) => ({ ...current, promptTemplate: event.target.value }))} />
              </label>
              <label className="space-y-1 text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">
                Sort order
                <input className="input-base" type="number" value={form.sortOrder} onChange={(event) => setForm((current) => ({ ...current, sortOrder: Number(event.target.value) || 0 }))} />
              </label>
              <label className="flex items-center gap-2 text-sm text-ink">
                <input type="checkbox" checked={form.isActive} onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))} />
                Active
              </label>
              <label className="flex items-center gap-2 text-sm text-ink">
                <input type="checkbox" checked={form.isClientSpecific} onChange={(event) => setForm((current) => ({ ...current, isClientSpecific: event.target.checked }))} />
                Client specific pack
              </label>
              <label className="flex items-center gap-2 text-sm text-ink">
                <input type="checkbox" checked={form.isClonedVoice} onChange={(event) => setForm((current) => ({ ...current, isClonedVoice: event.target.checked }))} />
                Cloned/client voice
              </label>
            </div>

            <div className="mt-4 flex flex-wrap gap-2">
              <button type="button" onClick={() => saveMutation.mutate()} disabled={!canSave} className="button-primary inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50">
                <Plus className="size-4" />
                {editingId ? 'Update voice pack' : 'Add voice pack'}
              </button>
              <button type="button" onClick={() => { setEditingId(null); setForm(emptyForm); setMessage(null); }} className="button-secondary px-4 py-2 text-sm font-semibold">Clear</button>
            </div>

            {message ? <p className="mt-3 text-sm text-brand">{message}</p> : null}
            {saveMutation.error instanceof Error ? <p className="mt-3 text-sm text-rose-600">{saveMutation.error.message}</p> : null}
          </div>

          <div className="panel overflow-hidden p-0">
            <div className="border-b border-line px-6 py-4">
              <h3 className="text-lg font-semibold text-ink">Configured voice packs</h3>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-slate-50 text-xs uppercase tracking-[0.14em] text-ink-soft">
                  <tr>
                    <th className="px-4 py-3">Name</th>
                    <th className="px-4 py-3">Language</th>
                    <th className="px-4 py-3">Tone</th>
                    <th className="px-4 py-3">Tier</th>
                    <th className="px-4 py-3">Updated</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {(voicePacksQuery.data ?? []).map((item) => (
                    <tr key={item.id} className="border-t border-line">
                      <td className="px-4 py-3 font-semibold text-ink">{item.name}</td>
                      <td className="px-4 py-3 text-ink-soft">{item.language ?? '-'}</td>
                      <td className="px-4 py-3 text-ink-soft">{item.tone ?? '-'}</td>
                      <td className="px-4 py-3 text-ink-soft">{item.pricingTier}</td>
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
                  {!voicePacksQuery.isLoading && (voicePacksQuery.data ?? []).length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-6 text-center text-sm text-ink-soft">No voice packs yet.</td>
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

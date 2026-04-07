import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BrainCircuit, DatabaseZap, Inbox, LoaderCircle, Plus, Radar } from 'lucide-react';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { LeadIntelligence } from '../../types/domain';
import { AgentPageShell, AgentQueryBoundary, fmtDate } from './agentWorkspace';

type CreateLeadFormState = {
  name: string;
  website: string;
  location: string;
  category: string;
};

const emptyForm: CreateLeadFormState = {
  name: '',
  website: '',
  location: '',
  category: '',
};

const sampleImport = `name,website,location,category,source,source_reference
Fit Lab,fitlab.co.za,Johannesburg,Fitness,google_maps,gmaps-001
Urban Dental,,Cape Town,Healthcare,business_directory,dir-204`;

function intentTone(intentLevel: string) {
  switch (intentLevel) {
    case 'High':
      return 'border-emerald-200 bg-emerald-50 text-emerald-700';
    case 'Medium':
      return 'border-amber-200 bg-amber-50 text-amber-700';
    default:
      return 'border-slate-200 bg-slate-50 text-slate-600';
  }
}

export function AgentLeadIntelligencePage() {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<CreateLeadFormState>(emptyForm);
  const [selectedLeadId, setSelectedLeadId] = useState<number | null>(null);
  const [importCsv, setImportCsv] = useState(sampleImport);
  const [importSource, setImportSource] = useState('csv_import');
  const [interactionType, setInteractionType] = useState('note');
  const [interactionNotes, setInteractionNotes] = useState('');
  const [interactionActionId, setInteractionActionId] = useState<number | ''>('');

  const intelligenceQuery = useQuery({
    queryKey: ['lead-intelligence'],
    queryFn: advertifiedApi.getLeadIntelligenceList,
  });

  const actionInboxQuery = useQuery({
    queryKey: ['lead-action-inbox'],
    queryFn: advertifiedApi.getLeadActionInbox,
  });

  const selectedLeadSummary = useMemo<LeadIntelligence | undefined>(
    () => intelligenceQuery.data?.find((item) => item.lead.id === selectedLeadId) ?? intelligenceQuery.data?.[0],
    [intelligenceQuery.data, selectedLeadId],
  );

  const selectedLead = useQuery({
    queryKey: ['lead-intelligence', selectedLeadSummary?.lead.id],
    queryFn: () => advertifiedApi.getLeadIntelligence(selectedLeadSummary!.lead.id),
    enabled: !!selectedLeadSummary?.lead.id,
    initialData: selectedLeadSummary,
  });

  const createLeadMutation = useMutation({
    mutationFn: () => advertifiedApi.createLead({
      name: form.name.trim(),
      website: form.website.trim() || undefined,
      location: form.location.trim(),
      category: form.category.trim(),
    }),
    onSuccess: async (lead) => {
      setForm(emptyForm);
      setSelectedLeadId(lead.id);
      await queryClient.invalidateQueries({ queryKey: ['lead-intelligence'] });
      await queryClient.invalidateQueries({ queryKey: ['lead-action-inbox'] });
    },
  });

  const analyzeLeadMutation = useMutation({
    mutationFn: (leadId: number) => advertifiedApi.analyzeLead(leadId),
    onSuccess: async (result) => {
      setSelectedLeadId(result.lead.id);
      await queryClient.invalidateQueries({ queryKey: ['lead-intelligence'] });
      await queryClient.invalidateQueries({ queryKey: ['lead-action-inbox'] });
    },
  });

  const importLeadMutation = useMutation({
    mutationFn: () => advertifiedApi.importLeadCsv({
      csvText: importCsv,
      defaultSource: importSource.trim() || 'csv_import',
    }),
    onSuccess: async (result) => {
      const latestLead = result.leads[0];
      if (latestLead) {
        setSelectedLeadId(latestLead.id);
      }

      await queryClient.invalidateQueries({ queryKey: ['lead-intelligence'] });
      await queryClient.invalidateQueries({ queryKey: ['lead-action-inbox'] });
    },
  });

  const updateActionMutation = useMutation({
    mutationFn: ({ leadId, actionId, status }: { leadId: number; actionId: number; status: 'completed' | 'dismissed' }) =>
      advertifiedApi.updateLeadActionStatus(leadId, actionId, status),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['lead-intelligence'] });
      await queryClient.invalidateQueries({ queryKey: ['lead-action-inbox'] });
    },
  });

  const assignActionMutation = useMutation({
    mutationFn: ({ leadId, actionId, mode }: { leadId: number; actionId: number; mode: 'assign' | 'unassign' }) =>
      mode === 'assign'
        ? advertifiedApi.assignLeadActionToMe(leadId, actionId)
        : advertifiedApi.unassignLeadAction(leadId, actionId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['lead-intelligence'] });
      await queryClient.invalidateQueries({ queryKey: ['lead-action-inbox'] });
    },
  });

  const createInteractionMutation = useMutation({
    mutationFn: (leadId: number) => advertifiedApi.createLeadInteraction({
      leadId,
      leadActionId: typeof interactionActionId === 'number' ? interactionActionId : undefined,
      interactionType: interactionType.trim() || 'note',
      notes: interactionNotes.trim(),
    }),
    onSuccess: async () => {
      setInteractionNotes('');
      setInteractionActionId('');
      await queryClient.invalidateQueries({ queryKey: ['lead-intelligence'] });
    },
  });

  const canCreateLead = form.name.trim() && form.location.trim() && form.category.trim();

  return (
    <AgentQueryBoundary query={intelligenceQuery} loadingLabel="Loading lead intelligence...">
      <AgentPageShell
        title="Lead Intelligence"
        description="Create a lead, import discovered businesses, and see scores, trends, and AI insight in one place."
      >
        <div className="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
          <div className="space-y-6">
            <section className="panel border-line/90 px-6 py-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-lg font-semibold text-ink">Action inbox</h2>
                  <p className="mt-1 text-sm text-ink-soft">Work recommended actions across all leads, then jump into the lead detail when you need context.</p>
                </div>
                <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                  <Inbox className="size-5" />
                </div>
              </div>

              <div className="mt-5 grid gap-3 md:grid-cols-4">
                <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Open</p>
                  <p className="mt-2 text-2xl font-semibold text-ink">{actionInboxQuery.data?.totalOpenActions ?? 0}</p>
                </div>
                <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Assigned to me</p>
                  <p className="mt-2 text-2xl font-semibold text-ink">{actionInboxQuery.data?.assignedToMeCount ?? 0}</p>
                </div>
                <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Unassigned</p>
                  <p className="mt-2 text-2xl font-semibold text-ink">{actionInboxQuery.data?.unassignedCount ?? 0}</p>
                </div>
                <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">High priority</p>
                  <p className="mt-2 text-2xl font-semibold text-ink">{actionInboxQuery.data?.highPriorityCount ?? 0}</p>
                </div>
              </div>

              <div className="mt-5 space-y-3">
                {(actionInboxQuery.data?.items ?? []).length > 0 ? (
                  actionInboxQuery.data!.items.map((item) => (
                    <div key={item.actionId} className="rounded-[20px] border border-line bg-white px-4 py-4">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <button type="button" onClick={() => setSelectedLeadId(item.leadId)} className="text-left">
                            <p className="text-sm font-semibold text-ink">{item.action.title}</p>
                            <p className="mt-1 text-sm text-ink-soft">{item.leadName} | {item.leadLocation} | {item.leadCategory}</p>
                          </button>
                        </div>
                        <div className="text-right">
                          <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">{item.action.priority} priority</p>
                          <p className="mt-1 text-xs text-ink-soft">
                            {item.action.assignedAgentName
                              ? `Assigned to ${item.action.assignedAgentName}`
                              : 'Unassigned'}
                          </p>
                        </div>
                      </div>
                      <p className="mt-3 text-sm text-ink">{item.action.description}</p>
                      <div className="mt-3 flex flex-wrap items-center gap-2">
                        <button
                          type="button"
                          onClick={() => setSelectedLeadId(item.leadId)}
                          className="button-secondary px-3 py-2 text-xs"
                        >
                          Open lead
                        </button>
                        {!item.action.isAssignedToCurrentUser ? (
                          <button
                            type="button"
                            onClick={() => assignActionMutation.mutate({ leadId: item.leadId, actionId: item.actionId, mode: 'assign' })}
                            disabled={assignActionMutation.isPending}
                            className="button-secondary px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-60"
                          >
                            Assign to me
                          </button>
                        ) : (
                          <button
                            type="button"
                            onClick={() => assignActionMutation.mutate({ leadId: item.leadId, actionId: item.actionId, mode: 'unassign' })}
                            disabled={assignActionMutation.isPending}
                            className="button-secondary px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-60"
                          >
                            Unassign
                          </button>
                        )}
                      </div>
                    </div>
                  ))
                ) : (
                  <div className="rounded-[20px] border border-dashed border-line px-4 py-6 text-sm text-ink-soft">
                    No open actions yet. Run lead analysis to generate the queue.
                  </div>
                )}
              </div>
            </section>

            <section className="panel border-line/90 px-6 py-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-lg font-semibold text-ink">Create a lead</h2>
                  <p className="mt-1 text-sm text-ink-soft">Add a business manually, then run the signal engine when you are ready.</p>
                </div>
                <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                  <Plus className="size-5" />
                </div>
              </div>

              <div className="mt-5 grid gap-4 md:grid-cols-2">
                <label className="block">
                  <span className="label-base">Business name</span>
                  <input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} className="input-base" placeholder="Gym XYZ" />
                </label>
                <label className="block">
                  <span className="label-base">Website</span>
                  <input value={form.website} onChange={(event) => setForm((current) => ({ ...current, website: event.target.value }))} className="input-base" placeholder="example.co.za" />
                </label>
                <label className="block">
                  <span className="label-base">Location</span>
                  <input value={form.location} onChange={(event) => setForm((current) => ({ ...current, location: event.target.value }))} className="input-base" placeholder="Johannesburg" />
                </label>
                <label className="block">
                  <span className="label-base">Category</span>
                  <input value={form.category} onChange={(event) => setForm((current) => ({ ...current, category: event.target.value }))} className="input-base" placeholder="Fitness" />
                </label>
              </div>

              <div className="mt-5">
                <button
                  type="button"
                  onClick={() => createLeadMutation.mutate()}
                  disabled={!canCreateLead || createLeadMutation.isPending}
                  className="button-primary inline-flex items-center gap-2 px-4 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {createLeadMutation.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Plus className="size-4" />}
                  {createLeadMutation.isPending ? 'Creating lead...' : 'Create lead'}
                </button>
              </div>
            </section>

            <section className="panel border-line/90 px-6 py-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-lg font-semibold text-ink">Import discovered leads</h2>
                  <p className="mt-1 text-sm text-ink-soft">Paste CSV exports from Maps, directories, or social research to ingest them in bulk.</p>
                </div>
                <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                  <DatabaseZap className="size-5" />
                </div>
              </div>

              <div className="mt-5 grid gap-4 md:grid-cols-[0.35fr_0.65fr]">
                <label className="block">
                  <span className="label-base">Default source</span>
                  <input
                    value={importSource}
                    onChange={(event) => setImportSource(event.target.value)}
                    className="input-base"
                    placeholder="google_maps"
                  />
                </label>
                <label className="block">
                  <span className="label-base">CSV rows</span>
                  <textarea
                    value={importCsv}
                    onChange={(event) => setImportCsv(event.target.value)}
                    className="input-base min-h-[170px] resize-y"
                    placeholder="name,website,location,category,source,source_reference"
                  />
                </label>
              </div>

              <div className="mt-5 flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  onClick={() => importLeadMutation.mutate()}
                  disabled={!importCsv.trim() || importLeadMutation.isPending}
                  className="button-primary inline-flex items-center gap-2 px-4 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {importLeadMutation.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <DatabaseZap className="size-4" />}
                  {importLeadMutation.isPending ? 'Importing leads...' : 'Import CSV'}
                </button>
                <p className="text-xs text-ink-soft">Expected headers: `name, website, location, category, source, source_reference`.</p>
              </div>
            </section>

            <section className="panel border-line/90 px-6 py-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-lg font-semibold text-ink">Lead queue</h2>
                  <p className="mt-1 text-sm text-ink-soft">Run analysis per lead and inspect the latest score.</p>
                </div>
                <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                  <Radar className="size-5" />
                </div>
              </div>

              <div className="mt-5 overflow-hidden rounded-[24px] border border-line">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.16em] text-ink-soft">
                    <tr>
                      <th className="px-4 py-4">Business</th>
                      <th className="px-4 py-4">Intent</th>
                      <th className="px-4 py-4">Latest signal</th>
                      <th className="px-4 py-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(intelligenceQuery.data ?? []).map((item) => {
                      const isSelected = selectedLead.data?.lead.id === item.lead.id;
                      const isAnalyzing = analyzeLeadMutation.isPending && analyzeLeadMutation.variables === item.lead.id;

                      return (
                        <tr
                          key={item.lead.id}
                          className={`border-t border-line transition ${isSelected ? 'bg-brand-soft/20' : 'bg-white'}`}
                        >
                          <td className="px-4 py-4">
                            <button type="button" onClick={() => setSelectedLeadId(item.lead.id)} className="text-left">
                              <p className="font-semibold text-ink">{item.lead.name}</p>
                              <p className="text-xs text-ink-soft">{item.lead.location} | {item.lead.category}</p>
                              <p className="text-[11px] uppercase tracking-[0.16em] text-ink-soft">{item.lead.source}</p>
                            </button>
                          </td>
                          <td className="px-4 py-4">
                            <div className="flex items-center gap-3">
                              <span className={`rounded-full border px-3 py-1 text-xs font-semibold ${intentTone(item.score.intentLevel)}`}>{item.score.intentLevel}</span>
                              <span className="text-sm font-semibold text-ink">{item.score.score}</span>
                            </div>
                          </td>
                          <td className="px-4 py-4 text-xs text-ink-soft">
                            {item.latestSignal ? fmtDate(item.latestSignal.createdAt) : 'Not analyzed'}
                          </td>
                          <td className="px-4 py-4 text-right">
                            <button
                              type="button"
                              onClick={() => analyzeLeadMutation.mutate(item.lead.id)}
                              disabled={analyzeLeadMutation.isPending}
                              className="button-secondary inline-flex items-center gap-2 px-3 py-2 disabled:cursor-not-allowed disabled:opacity-60"
                            >
                              {isAnalyzing ? <LoaderCircle className="size-4 animate-spin" /> : <BrainCircuit className="size-4" />}
                              Analyze
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                    {(intelligenceQuery.data?.length ?? 0) === 0 ? (
                      <tr>
                        <td colSpan={4} className="px-4 py-8 text-center text-sm text-ink-soft">No leads yet. Create the first one above.</td>
                      </tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
            </section>
          </div>

          <section className="panel border-line/90 px-6 py-6">
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 className="text-lg font-semibold text-ink">Latest intelligence</h2>
                <p className="mt-1 text-sm text-ink-soft">Signals, score, AI summary, and source context for the selected lead.</p>
              </div>
              <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                <BrainCircuit className="size-5" />
              </div>
            </div>

            {selectedLead.data ? (
              <div className="mt-5 space-y-4">
                <div className="rounded-[24px] border border-line bg-slate-50 px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Business</p>
                  <h3 className="mt-2 text-xl font-semibold text-ink">{selectedLead.data.lead.name}</h3>
                  <p className="mt-1 text-sm text-ink-soft">{selectedLead.data.lead.location} | {selectedLead.data.lead.category}</p>
                  <p className="mt-1 text-sm text-ink-soft">{selectedLead.data.lead.website || 'No website provided'}</p>
                  <p className="mt-1 text-xs uppercase tracking-[0.16em] text-ink-soft">
                    Source: {selectedLead.data.lead.source}
                    {selectedLead.data.lead.lastDiscoveredAt ? ` | Last discovered ${fmtDate(selectedLead.data.lead.lastDiscoveredAt)}` : ''}
                  </p>
                </div>

                <div className="grid gap-4 md:grid-cols-3">
                  <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                    <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Score</p>
                    <p className="mt-2 text-3xl font-semibold text-ink">{selectedLead.data.score.score}</p>
                    <p className={`mt-2 inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${intentTone(selectedLead.data.score.intentLevel)}`}>{selectedLead.data.score.intentLevel}</p>
                  </div>
                  <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                    <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Promo</p>
                    <p className="mt-2 text-lg font-semibold text-ink">{selectedLead.data.latestSignal?.hasPromo ? 'Detected' : 'Not detected'}</p>
                  </div>
                  <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                    <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Meta ads</p>
                    <p className="mt-2 text-lg font-semibold text-ink">{selectedLead.data.latestSignal?.hasMetaAds ? 'Detected' : 'Not detected'}</p>
                  </div>
                </div>

                <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Insight</p>
                  <p className="mt-3 text-sm leading-7 text-ink">{selectedLead.data.insight || 'No insight yet. Run analysis first.'}</p>
                  <p className="mt-3 text-xs text-ink-soft">{selectedLead.data.trendSummary || 'No trend summary yet.'}</p>
                </div>

                <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Recommended actions</p>
                  <div className="mt-4 space-y-3">
                    {(selectedLead.data.recommendedActions ?? []).length > 0 ? (
                      selectedLead.data.recommendedActions.map((action) => (
                        <div key={action.id} className="rounded-[18px] border border-line bg-slate-50 px-4 py-3">
                          <div className="flex items-center justify-between gap-3">
                            <p className="text-sm font-semibold text-ink">{action.title}</p>
                            <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">{action.priority} priority</p>
                          </div>
                          <p className="mt-2 text-sm text-ink">{action.description}</p>
                          <p className="mt-2 text-xs text-ink-soft">
                            {action.actionType} | {action.status} | {fmtDate(action.createdAt)}
                            {action.assignedAgentName ? ` | Assigned to ${action.assignedAgentName}` : ' | Unassigned'}
                          </p>
                          {action.status === 'open' ? (
                            <div className="mt-3 flex flex-wrap gap-2">
                              {!action.isAssignedToCurrentUser ? (
                                <button
                                  type="button"
                                  onClick={() => assignActionMutation.mutate({ leadId: selectedLead.data!.lead.id, actionId: action.id, mode: 'assign' })}
                                  disabled={assignActionMutation.isPending}
                                  className="button-secondary px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-60"
                                >
                                  Assign to me
                                </button>
                              ) : (
                                <button
                                  type="button"
                                  onClick={() => assignActionMutation.mutate({ leadId: selectedLead.data!.lead.id, actionId: action.id, mode: 'unassign' })}
                                  disabled={assignActionMutation.isPending}
                                  className="button-secondary px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-60"
                                >
                                  Unassign
                                </button>
                              )}
                              <button
                                type="button"
                                onClick={() => updateActionMutation.mutate({ leadId: selectedLead.data!.lead.id, actionId: action.id, status: 'completed' })}
                                disabled={updateActionMutation.isPending}
                                className="button-secondary px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-60"
                              >
                                Complete
                              </button>
                              <button
                                type="button"
                                onClick={() => updateActionMutation.mutate({ leadId: selectedLead.data!.lead.id, actionId: action.id, status: 'dismissed' })}
                                disabled={updateActionMutation.isPending}
                                className="button-secondary px-3 py-2 text-xs disabled:cursor-not-allowed disabled:opacity-60"
                              >
                                Dismiss
                              </button>
                            </div>
                          ) : null}
                        </div>
                      ))
                    ) : (
                      <p className="text-sm text-ink-soft">No actions recommended yet. Run another analysis after new signals arrive.</p>
                    )}
                  </div>
                </div>

                <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Log interaction</p>
                  <div className="mt-4 grid gap-4 md:grid-cols-[0.3fr_0.7fr]">
                    <label className="block">
                      <span className="label-base">Type</span>
                      <input
                        value={interactionType}
                        onChange={(event) => setInteractionType(event.target.value)}
                        className="input-base"
                        placeholder="call"
                      />
                    </label>
                    <label className="block">
                      <span className="label-base">Related action</span>
                      <select
                        value={interactionActionId}
                        onChange={(event) => setInteractionActionId(event.target.value ? Number(event.target.value) : '')}
                        className="input-base"
                      >
                        <option value="">None</option>
                        {(selectedLead.data.recommendedActions ?? []).map((action) => (
                          <option key={action.id} value={action.id}>{action.title}</option>
                        ))}
                      </select>
                    </label>
                  </div>
                  <label className="mt-4 block">
                    <span className="label-base">Notes</span>
                    <textarea
                      value={interactionNotes}
                      onChange={(event) => setInteractionNotes(event.target.value)}
                      className="input-base min-h-[120px] resize-y"
                      placeholder="Called lead, no answer. Retry tomorrow morning."
                    />
                  </label>
                  <div className="mt-4">
                    <button
                      type="button"
                      onClick={() => createInteractionMutation.mutate(selectedLead.data!.lead.id)}
                      disabled={!interactionNotes.trim() || createInteractionMutation.isPending}
                      className="button-primary px-4 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {createInteractionMutation.isPending ? 'Saving interaction...' : 'Save interaction'}
                    </button>
                  </div>
                </div>

                <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Last analysis</p>
                  <p className="mt-3 text-sm text-ink-soft">{selectedLead.data.latestSignal ? fmtDate(selectedLead.data.latestSignal.createdAt) : 'Not analyzed yet'}</p>
                  <p className="mt-2 text-sm text-ink-soft">Website updated recently: {selectedLead.data.latestSignal?.websiteUpdatedRecently ? 'Yes' : 'No'}</p>
                </div>

                <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Interaction history</p>
                  <div className="mt-4 space-y-3">
                    {(selectedLead.data.interactionHistory ?? []).length > 0 ? (
                      selectedLead.data.interactionHistory.map((interaction) => (
                        <div key={interaction.id} className="rounded-[18px] border border-line bg-slate-50 px-4 py-3">
                          <div className="flex items-center justify-between gap-3">
                            <p className="text-sm font-semibold text-ink">{interaction.interactionType}</p>
                            <p className="text-xs text-ink-soft">{fmtDate(interaction.createdAt)}</p>
                          </div>
                          <p className="mt-2 text-sm text-ink">{interaction.notes}</p>
                        </div>
                      ))
                    ) : (
                      <p className="text-sm text-ink-soft">No interactions logged yet.</p>
                    )}
                  </div>
                </div>

                <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Insight timeline</p>
                  <div className="mt-4 space-y-3">
                    {(selectedLead.data.insightHistory ?? []).length > 0 ? (
                      selectedLead.data.insightHistory.map((item) => (
                        <div key={item.id} className="rounded-[18px] border border-line bg-slate-50 px-4 py-3">
                          <div className="flex items-center justify-between gap-3">
                            <p className="text-sm font-semibold text-ink">{item.intentLevelSnapshot} intent | {item.scoreSnapshot}</p>
                            <p className="text-xs text-ink-soft">{fmtDate(item.createdAt)}</p>
                          </div>
                          <p className="mt-2 text-sm text-ink">{item.text}</p>
                          <p className="mt-2 text-xs text-ink-soft">{item.trendSummary}</p>
                        </div>
                      ))
                    ) : (
                      <p className="text-sm text-ink-soft">No timeline yet. Run analysis to create the first snapshot.</p>
                    )}
                  </div>
                </div>
              </div>
            ) : (
              <div className="mt-5 rounded-[24px] border border-dashed border-line px-4 py-8 text-sm text-ink-soft">
                Create, import, or select a lead to inspect its intelligence.
              </div>
            )}
          </section>
        </div>
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

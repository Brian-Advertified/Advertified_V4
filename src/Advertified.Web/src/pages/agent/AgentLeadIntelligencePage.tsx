import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BrainCircuit, DatabaseZap, Inbox, LoaderCircle, Plus, Radar } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { LeadIntelligence, PackageBand } from '../../types/domain';
import {
  buildAutoBriefFromLead,
  buildEstimatedImpactRange,
  buildResearchEvidenceLines,
  buildSocialQualityNote,
  buildWorkingSignals,
  formatCoverageStatus,
  getChannelScore,
} from '../../features/leads/leadIntelligenceViewModel';
import { AgentPageShell, AgentQueryBoundary, fmtDate } from './agentWorkspace';

type CreateLeadFormState = {
  name: string;
  website: string;
  location: string;
  category: string;
};

type ProposalFormState = {
  fullName: string;
  email: string;
  phone: string;
  packageBandId: string;
  campaignName: string;
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

const googleMapsSampleImport = `business_name,website,city,main_category,place_id
Fit Lab,fitlab.co.za,Johannesburg,Fitness,ChIJ123
Urban Dental,,Cape Town,Dentist,ChIJ456`;

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

function channelConfidenceTone(confidence: string) {
  switch (confidence) {
    case 'detected':
      return 'border-emerald-200 bg-emerald-50 text-emerald-700';
    case 'strongly_inferred':
      return 'border-sky-200 bg-sky-50 text-sky-700';
    case 'weakly_inferred':
      return 'border-amber-200 bg-amber-50 text-amber-700';
    case 'weak_signal':
      return 'border-orange-200 bg-orange-50 text-orange-700';
    default:
      return 'border-slate-200 bg-slate-50 text-slate-600';
  }
}

function formatChannelLabel(channel: string) {
  switch (channel) {
    case 'billboards_ooh':
      return 'Billboards and Digital Screens';
    default:
      return channel
        .split('_')
        .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
        .join(' ');
  }
}

function inferDefaultPackageBandId(packageBands: PackageBand[] | undefined, lead: LeadIntelligence | undefined): string {
  if (!packageBands || packageBands.length === 0) {
    return '';
  }

  if (lead?.score.intentLevel === 'High') {
    return packageBands.find((item) => item.code.toLowerCase() === 'scale')?.id
      ?? packageBands.find((item) => item.isRecommended)?.id
      ?? packageBands[0].id;
  }

  if (lead?.score.intentLevel === 'Medium') {
    return packageBands.find((item) => item.isRecommended)?.id
      ?? packageBands[0].id;
  }

  return packageBands[0].id;
}

export function AgentLeadIntelligencePage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [form, setForm] = useState<CreateLeadFormState>(emptyForm);
  const [selectedLeadId, setSelectedLeadId] = useState<number | null>(null);
  const [importCsv, setImportCsv] = useState(sampleImport);
  const [importSource, setImportSource] = useState('csv_import');
  const [importProfile, setImportProfile] = useState<'standard' | 'google_maps'>('standard');
  const [interactionType, setInteractionType] = useState('note');
  const [interactionNotes, setInteractionNotes] = useState('');
  const [interactionActionId, setInteractionActionId] = useState<number | ''>('');
  const [showProposalForm, setShowProposalForm] = useState(false);
  const [proposalForm, setProposalForm] = useState<ProposalFormState>({
    fullName: '',
    email: '',
    phone: '',
    packageBandId: '',
    campaignName: '',
  });

  const intelligenceQuery = useQuery({
    queryKey: ['lead-intelligence'],
    queryFn: advertifiedApi.getLeadIntelligenceList,
  });

  const packagesQuery = useQuery({
    queryKey: ['packages'],
    queryFn: advertifiedApi.getPackages,
  });

  const actionInboxQuery = useQuery({
    queryKey: ['lead-action-inbox'],
    queryFn: advertifiedApi.getLeadActionInbox,
  });

  const sourceAutomationStatusQuery = useQuery({
    queryKey: ['lead-source-automation-status'],
    queryFn: advertifiedApi.getLeadSourceAutomationStatus,
  });
  const paidMediaSyncStatusQuery = useQuery({
    queryKey: ['lead-paid-media-sync-status'],
    queryFn: advertifiedApi.getLeadPaidMediaSyncStatus,
  });

  const processSourceAutomationMutation = useMutation({
    mutationFn: advertifiedApi.processLeadSourceAutomationNow,
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['lead-source-automation-status'] });
      await queryClient.invalidateQueries({ queryKey: ['lead-intelligence'] });
      await queryClient.invalidateQueries({ queryKey: ['lead-action-inbox'] });

      if (result.importedLeadCount > 0) {
        await selectedLead.refetch();
      }
    },
  });
  const runPaidMediaSyncMutation = useMutation({
    mutationFn: advertifiedApi.runLeadPaidMediaSyncNow,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['lead-paid-media-sync-status'] });
      await queryClient.invalidateQueries({ queryKey: ['lead-intelligence'] });
      await queryClient.invalidateQueries({ queryKey: ['lead-action-inbox'] });
      await selectedLead.refetch();
    },
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

  useEffect(() => {
    if (!selectedLead.data) {
      return;
    }

    const defaultPackageBandId = inferDefaultPackageBandId(packagesQuery.data, selectedLead.data);
    setProposalForm({
      fullName: selectedLead.data.lead.name,
      email: '',
      phone: '',
      packageBandId: defaultPackageBandId,
      campaignName: `${selectedLead.data.lead.name} Growth Opportunity Campaign`,
    });
    setShowProposalForm(false);
  }, [packagesQuery.data, selectedLead.data?.lead.id]);

  const createLeadMutation = useMutation({
    mutationFn: () => advertifiedApi.createLead({
      name: form.name.trim(),
      website: form.website.trim() || undefined,
      location: form.location.trim() || undefined,
      category: form.category.trim() || undefined,
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
      importProfile,
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

  const generateProposalMutation = useMutation({
    mutationFn: async () => {
      const lead = selectedLead.data;
      if (!lead) {
        throw new Error('Select a lead before generating a proposal.');
      }
      if (lead.enrichment.confidenceGate.isBlocked) {
        throw new Error(lead.enrichment.confidenceGate.message);
      }

      const opportunityProfile = lead.opportunityProfile;
      const industryPolicy = lead.industryPolicy;
      const autoBrief = buildAutoBriefFromLead(lead, opportunityProfile, industryPolicy);
      const evidenceLines = buildResearchEvidenceLines(lead);
      const socialQualityNote = buildSocialQualityNote(lead);

      const campaign = await advertifiedApi.createAgentProspectCampaign({
        fullName: proposalForm.fullName.trim(),
        email: proposalForm.email.trim(),
        phone: proposalForm.phone.trim(),
        packageBandId: proposalForm.packageBandId,
        campaignName: proposalForm.campaignName.trim() || `${lead.lead.name} Growth Opportunity Campaign`,
      });

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
      ]);

      navigate(`/agent/recommendations/new?campaignId=${campaign.id}`, {
        state: {
          convertToCampaign: {
            leadId: lead.lead.id,
            businessName: lead.lead.name,
            location: lead.lead.location,
            archetypeName: opportunityProfile.name,
            suggestedCampaignType: opportunityProfile.suggestedCampaignType,
            detectedGaps: opportunityProfile.detectedGaps,
            insightSummary: lead.insight,
            expectedOutcome: opportunityProfile.expectedOutcome,
            recommendedChannels: opportunityProfile.recommendedChannels,
            strategyChannels: (lead.strategy?.channels ?? []).map((channel) => ({
              channel: channel.channel,
              budgetSharePercent: channel.budgetSharePercent,
              reason: channel.reason,
            })),
            industryProfileName: industryPolicy.name,
            industryMessagingAngle: industryPolicy.messagingAngle,
            industryCta: industryPolicy.cta,
            industryGuardrails: industryPolicy.guardrails,
            whoWeAre:
              `Advertified is a South African advertising company focused on helping businesses capture more demand using real market signals and practical campaign execution. We identify missed opportunities, build clear campaign plans, and help launch quickly without unnecessary complexity. For ${industryPolicy.name}, we prioritize ${industryPolicy.messagingAngle}.`,
            researchBasis: evidenceLines,
            lastResearchedAtUtc: lead.latestSignal?.createdAt ?? new Date().toISOString(),
            socialQualityNote,
            flexibleRollout:
              'Each option can be launched using our buy now, pay later model.',
            whyActNow: opportunityProfile.whyActNow,
            nextStep:
              `Reply with your preferred option and we will handle setup, creative coordination, and launch planning. Recommended CTA for this industry: ${industryPolicy.cta}.`,
            autoGenerateDraft: true,
            autoBrief,
          },
        },
      });
    },
  });

  const canCreateLead = Boolean(
    form.name.trim()
    && ((form.location.trim() && form.category.trim()) || form.website.trim()),
  );
  const canGenerateProposal = Boolean(
    selectedLead.data
    && proposalForm.fullName.trim()
    && proposalForm.email.trim()
    && proposalForm.phone.trim()
    && proposalForm.packageBandId
    && !selectedLead.data.enrichment.confidenceGate.isBlocked,
  );
  const selectedLeadData = selectedLead.data;
  const selectedLeadArchetype = selectedLeadData?.opportunityProfile ?? null;
  const priorityActions = (selectedLeadData?.recommendedActions ?? []).filter((action) => action.status === 'open').slice(0, 3);
  const visibleChannelCoverage = selectedLeadData
    ? [
        { key: 'social', label: 'Social', score: getChannelScore(selectedLeadData, 'social') },
        { key: 'search', label: 'Search', score: getChannelScore(selectedLeadData, 'search') },
        { key: 'radio', label: 'Radio', score: getChannelScore(selectedLeadData, 'radio') },
        { key: 'billboards_ooh', label: 'Billboards and Digital Screens', score: getChannelScore(selectedLeadData, 'billboards_ooh') },
      ]
    : [];

  return (
    <AgentQueryBoundary query={intelligenceQuery} loadingLabel="Loading lead intelligence...">
      <AgentPageShell
        title="Lead Intelligence"
        description="See the growth opportunity, the next best action, and the fastest path to a proposal draft."
      >
        <div className="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
          <div className="space-y-6">
            <section className="panel border-brand/20 bg-[linear-gradient(180deg,#f7fcfa_0%,#ffffff_100%)] px-6 py-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="text-xs uppercase tracking-[0.16em] text-brand">Primary action</p>
                  <h2 className="mt-1 text-2xl font-semibold text-ink">Create lead</h2>
                  <p className="mt-1 text-sm text-ink-soft">Start here. Add a business, create the lead, then run analysis and generate a proposal.</p>
                  <div className="mt-4 flex flex-wrap items-center gap-2">
                    <span className="rounded-full border border-brand/20 bg-white px-3 py-1 text-xs font-semibold text-ink">1. Create lead</span>
                    <span className="rounded-full border border-line bg-white px-3 py-1 text-xs text-ink-soft">2. Analyze</span>
                    <span className="rounded-full border border-line bg-white px-3 py-1 text-xs text-ink-soft">3. Generate proposal</span>
                  </div>
                </div>
                <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                  <Plus className="size-5" />
                </div>
              </div>

              <div className="mt-5 grid gap-4 md:grid-cols-2">
                <label className="block">
                  <span className="label-base">Business name</span>
                  <input
                    value={form.name}
                    onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
                    className="input-base"
                    placeholder="Gym XYZ"
                  />
                </label>
                <label className="block">
                  <span className="label-base">Website</span>
                  <input
                    value={form.website}
                    onChange={(event) => setForm((current) => ({ ...current, website: event.target.value }))}
                    className="input-base"
                    placeholder="example.co.za"
                  />
                </label>
                <label className="block">
                  <span className="label-base">Location</span>
                  <input
                    value={form.location}
                    onChange={(event) => setForm((current) => ({ ...current, location: event.target.value }))}
                    className="input-base"
                    placeholder="Johannesburg"
                  />
                </label>
                <label className="block">
                  <span className="label-base">Category</span>
                  <input
                    value={form.category}
                    onChange={(event) => setForm((current) => ({ ...current, category: event.target.value }))}
                    className="input-base"
                    placeholder="Fitness"
                  />
                </label>
              </div>

              <div className="mt-5 flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  onClick={() => createLeadMutation.mutate()}
                  disabled={!canCreateLead || createLeadMutation.isPending}
                  className="button-primary inline-flex items-center gap-2 px-4 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {createLeadMutation.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Plus className="size-4" />}
                  {createLeadMutation.isPending ? 'Creating lead...' : 'Create lead'}
                </button>
                <p className="text-xs text-ink-soft">Required: business name, plus either (location and category) or a website URL for auto-inference.</p>
              </div>
            </section>

            <section className="panel border-line/90 px-6 py-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h2 className="text-lg font-semibold text-ink">Action inbox</h2>
                  <p className="mt-1 text-sm text-ink-soft">Operational follow-ups. Expand only when you need assignment work.</p>
                </div>
                <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                  <Inbox className="size-5" />
                </div>
              </div>

              <details className="mt-5">
                <summary className="cursor-pointer list-none text-sm font-semibold text-ink">
                  View action queue ({actionInboxQuery.data?.totalOpenActions ?? 0} open)
                </summary>
                <div className="mt-4 grid gap-3 md:grid-cols-4">
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

                <div className="mt-4 space-y-3">
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
              </details>
            </section>

              <section className="panel border-line/90 px-6 py-6">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <h2 className="text-lg font-semibold text-ink">Source automation</h2>
                    <p className="mt-1 text-sm text-ink-soft">Monitor the CSV drop-folder pipeline and see whether autonomous lead discovery is ready.</p>
                </div>
                  <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                    <DatabaseZap className="size-5" />
                  </div>
                </div>
                <details className="mt-5">
                  <summary className="cursor-pointer list-none text-sm font-semibold text-ink">View automation status</summary>
                  {sourceAutomationStatusQuery.data ? (
                    <div className="mt-4 space-y-4">
                    <div className="grid gap-4 md:grid-cols-4">
                      <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                        <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Drop folder</p>
                      <p className="mt-2 text-lg font-semibold text-ink">
                        {sourceAutomationStatusQuery.data.dropFolderEnabled ? 'Enabled' : 'Disabled'}
                      </p>
                    </div>
                    <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                      <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Pending files</p>
                      <p className="mt-2 text-2xl font-semibold text-ink">{sourceAutomationStatusQuery.data.pendingFileCount}</p>
                    </div>
                    <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                      <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Processed files</p>
                      <p className="mt-2 text-2xl font-semibold text-ink">{sourceAutomationStatusQuery.data.processedFileCount}</p>
                    </div>
                    <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                      <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Failed files</p>
                      <p className="mt-2 text-2xl font-semibold text-ink">{sourceAutomationStatusQuery.data.failedFileCount}</p>
                    </div>
                  </div>

                  <div className="rounded-[20px] border border-line bg-white px-4 py-4 text-sm text-ink-soft">
                    <p><span className="font-semibold text-ink">Inbox:</span> {sourceAutomationStatusQuery.data.inboxPath}</p>
                    <p className="mt-2"><span className="font-semibold text-ink">Processed:</span> {sourceAutomationStatusQuery.data.processedPath}</p>
                    <p className="mt-2"><span className="font-semibold text-ink">Failed:</span> {sourceAutomationStatusQuery.data.failedPath}</p>
                    <p className="mt-2">
                      <span className="font-semibold text-ink">Default behavior:</span> source `{sourceAutomationStatusQuery.data.defaultSource}`, profile `{sourceAutomationStatusQuery.data.defaultImportProfile}`,
                      {sourceAutomationStatusQuery.data.analyzeImportedLeads ? ' auto-analyze on import' : ' import only'}
                    </p>
                  </div>

                  <div className="rounded-[20px] border border-line bg-white px-4 py-4">
                    <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Paid media sync</p>
                    {paidMediaSyncStatusQuery.data ? (
                      <>
                        <div className="mt-3 grid gap-3 md:grid-cols-3">
                          <div className="rounded-[14px] border border-line bg-slate-50 px-3 py-3">
                            <p className="text-[11px] uppercase tracking-[0.14em] text-ink-soft">Status</p>
                            <p className="mt-1 text-sm font-semibold text-ink">{paidMediaSyncStatusQuery.data.enabled ? 'Enabled' : 'Disabled'}</p>
                          </div>
                          <div className="rounded-[14px] border border-line bg-slate-50 px-3 py-3">
                            <p className="text-[11px] uppercase tracking-[0.14em] text-ink-soft">Batch size</p>
                            <p className="mt-1 text-sm font-semibold text-ink">{paidMediaSyncStatusQuery.data.batchSize}</p>
                          </div>
                          <div className="rounded-[14px] border border-line bg-slate-50 px-3 py-3">
                            <p className="text-[11px] uppercase tracking-[0.14em] text-ink-soft">Interval</p>
                            <p className="mt-1 text-sm font-semibold text-ink">{paidMediaSyncStatusQuery.data.intervalMinutes} min</p>
                          </div>
                        </div>

                        {paidMediaSyncStatusQuery.data.lastRun ? (
                          <div className="mt-3 rounded-[14px] border border-line bg-slate-50 px-3 py-3 text-xs text-ink-soft">
                            <p className="font-semibold text-ink">Last run: {fmtDate(paidMediaSyncStatusQuery.data.lastRun.finishedAtUtc)}</p>
                            {paidMediaSyncStatusQuery.data.lastRun.skipped ? (
                              <p className="mt-1">
                                Skipped: {paidMediaSyncStatusQuery.data.lastRun.skipReason ?? 'no reason provided'}.
                              </p>
                            ) : (
                              <p className="mt-1">
                                Processed {paidMediaSyncStatusQuery.data.lastRun.processedLeadCount}/{paidMediaSyncStatusQuery.data.lastRun.totalLeadCount},
                                failures {paidMediaSyncStatusQuery.data.lastRun.failedLeadCount}, evidence rows {paidMediaSyncStatusQuery.data.lastRun.evidenceRowCount}.
                              </p>
                            )}
                            {paidMediaSyncStatusQuery.data.lastRun.enabledProviders.length > 0 ? (
                              <p className="mt-1">
                                Enabled providers: {paidMediaSyncStatusQuery.data.lastRun.enabledProviders.join(', ')}.
                              </p>
                            ) : null}
                            {Object.keys(paidMediaSyncStatusQuery.data.lastRun.providerEvidenceCounts).length > 0 ? (
                              <p className="mt-1">
                                Providers:{' '}
                                {Object.entries(paidMediaSyncStatusQuery.data.lastRun.providerEvidenceCounts)
                                  .map(([source, count]) => `${source} ${count}`)
                                  .join(', ')}
                              </p>
                            ) : null}
                          </div>
                        ) : (
                          <p className="mt-3 text-xs text-ink-soft">No paid media sync run has been recorded yet.</p>
                        )}
                      </>
                    ) : (
                      <p className="mt-3 text-xs text-ink-soft">Loading paid media sync status...</p>
                    )}
                  </div>

                  <div className="flex flex-wrap items-center gap-3">
                    <button
                      type="button"
                      onClick={() => processSourceAutomationMutation.mutate()}
                      disabled={processSourceAutomationMutation.isPending}
                      className="button-secondary inline-flex items-center gap-2 px-4 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {processSourceAutomationMutation.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Radar className="size-4" />}
                      {processSourceAutomationMutation.isPending ? 'Processing files...' : 'Process now'}
                    </button>
                      {processSourceAutomationMutation.data ? (
                        <p className="text-xs text-ink-soft">
                          Last run: {processSourceAutomationMutation.data.processedFileCount} processed, {processSourceAutomationMutation.data.failedFileCount} failed,
                        {` ${processSourceAutomationMutation.data.importedLeadCount} imported, ${processSourceAutomationMutation.data.analyzedLeadCount} analyzed.`}
                        </p>
                      ) : null}
                    <button
                      type="button"
                      onClick={() => runPaidMediaSyncMutation.mutate()}
                      disabled={runPaidMediaSyncMutation.isPending}
                      className="button-secondary inline-flex items-center gap-2 px-4 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {runPaidMediaSyncMutation.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <BrainCircuit className="size-4" />}
                      {runPaidMediaSyncMutation.isPending ? 'Syncing paid media...' : 'Run paid media sync now'}
                    </button>
                    {runPaidMediaSyncMutation.data ? (
                      <p className="text-xs text-ink-soft">
                        Paid media run: {runPaidMediaSyncMutation.data.processedLeadCount}/{runPaidMediaSyncMutation.data.totalLeadCount} processed,
                        {` ${runPaidMediaSyncMutation.data.failedLeadCount} failed, ${runPaidMediaSyncMutation.data.evidenceRowCount} evidence rows.`}
                      </p>
                    ) : null}
                    </div>
                    </div>
                  ) : (
                    <div className="mt-4 rounded-[20px] border border-dashed border-line px-4 py-6 text-sm text-ink-soft">
                      Loading source automation status...
                    </div>
                  )}
                </details>
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

                <details className="mt-5">
                  <summary className="cursor-pointer list-none text-sm font-semibold text-ink">Import a lead list</summary>
                  <div className="mt-4 grid gap-4 md:grid-cols-[0.35fr_0.65fr]">
                  <label className="block">
                    <span className="label-base">Import profile</span>
                    <select
                    value={importProfile}
                    onChange={(event) => {
                      const nextProfile = event.target.value as 'standard' | 'google_maps';
                      setImportProfile(nextProfile);
                      setImportSource((current) =>
                        current.trim().length > 0
                          ? current
                          : nextProfile === 'google_maps'
                            ? 'google_maps'
                            : 'csv_import');
                      setImportCsv((current) => {
                        if (current === sampleImport || current === googleMapsSampleImport || !current.trim()) {
                          return nextProfile === 'google_maps' ? googleMapsSampleImport : sampleImport;
                        }

                        return current;
                      });
                    }}
                    className="input-base"
                  >
                    <option value="standard">Standard CSV</option>
                    <option value="google_maps">Google Maps export</option>
                  </select>
                </label>
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
                  <p className="text-xs text-ink-soft">
                    {importProfile === 'google_maps'
                      ? 'Expected Google Maps-style headers: `business_name/title, website, city/address, main_category/category, place_id`.'
                      : 'Expected headers: `name, website, location, category, source, source_reference`.'}
                  </p>
                  </div>
                </details>
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
                              {(item.lead.autoInferredFields?.length ?? 0) > 0 ? (
                                <span className="mt-1 inline-flex rounded-full border border-sky-200 bg-sky-50 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-sky-700">
                                  Auto-inferred: {item.lead.autoInferredFields?.join(', ')}
                                </span>
                              ) : null}
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
                <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
                  <div className="rounded-[24px] border border-brand/15 bg-brand-soft/30 px-5 py-5">
                    <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Decision summary</p>
                    <h3 className="mt-2 text-2xl font-semibold text-ink">{selectedLead.data.lead.name}</h3>
                    <p className="mt-1 text-sm text-ink-soft">{selectedLead.data.lead.location} | {selectedLead.data.lead.category}</p>
                    {selectedLeadArchetype ? (
                      <p className="mt-1 text-xs uppercase tracking-[0.16em] text-ink-soft">Growth pattern: {selectedLeadArchetype.name}</p>
                    ) : null}
                    <div className="mt-5 flex flex-wrap items-center gap-3">
                      <p className="text-4xl font-semibold text-ink">Opportunity Score: {selectedLead.data.score.score}</p>
                      <p className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${intentTone(selectedLead.data.score.intentLevel)}`}>
                        {selectedLead.data.score.intentLevel} growth potential
                      </p>
                    </div>
                    <p className="mt-4 text-sm leading-7 text-ink">{selectedLead.data.insight || 'No insight yet. Run analysis first.'}</p>
                  </div>

                  <div className="space-y-4">
                    <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                      <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">KPI snapshot</p>
                      <p className="mt-3 text-sm text-ink-soft">Estimated impact: <span className="font-semibold text-ink">{buildEstimatedImpactRange(selectedLead.data.score.score)}</span></p>
                      <p className="mt-1 text-sm text-ink-soft">
                        Enrichment confidence: <span className="font-semibold text-ink">{Math.round((selectedLead.data.enrichment.confidenceScore ?? 0) * 100)}%</span>
                      </p>
                      <p className="mt-1 text-sm text-ink-soft">Last analysis: {selectedLead.data.latestSignal ? fmtDate(selectedLead.data.latestSignal.createdAt) : 'Not analyzed yet'}</p>
                      <p className="mt-1 text-sm text-ink-soft">Promo signal: {selectedLead.data.latestSignal?.hasPromo ? 'Detected' : 'Not detected'}</p>
                      {(selectedLead.data.enrichment.missingFields ?? []).length > 0 ? (
                        <p className="mt-1 text-sm text-ink-soft">
                          Missing fields: {selectedLead.data.enrichment.missingFields.join(', ')}
                        </p>
                      ) : null}
                      <div className="mt-4 space-y-3">
                        {visibleChannelCoverage.map((channel) => {
                          const status = formatCoverageStatus(channel.score);
                          return (
                            <div key={channel.key} className="flex items-center justify-between gap-3 rounded-[18px] border border-line bg-slate-50 px-4 py-3">
                              <div>
                                <p className="text-sm font-semibold text-ink">{channel.label}</p>
                                <p className="mt-1 text-xs text-ink-soft">{channel.score}/100</p>
                              </div>
                              <p className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${status.tone}`}>
                                {status.label}
                              </p>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  </div>
                </div>

                <div className="grid gap-4 md:grid-cols-2">
                  <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                    <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">What is already working</p>
                    <div className="mt-3 space-y-2">
                      {buildWorkingSignals(selectedLead.data).map((line) => (
                        <p key={line} className="rounded-[16px] border border-line bg-slate-50 px-3 py-2 text-sm text-ink">{line}</p>
                      ))}
                    </div>
                  </div>
                  <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                    <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Where growth can be unlocked</p>
                    <div className="mt-3 space-y-2">
                      {(selectedLeadArchetype?.detectedGaps ?? []).slice(0, 3).map((gap) => (
                        <p key={gap} className="rounded-[16px] border border-line bg-slate-50 px-3 py-2 text-sm text-ink">{gap}</p>
                      ))}
                    </div>
                    <p className="mt-3 text-sm text-ink-soft">{selectedLeadArchetype?.expectedOutcome}</p>
                  </div>
                </div>

                <div className="rounded-[24px] border border-brand/20 bg-brand-soft/30 px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Proposal setup</p>
                  <h3 className="mt-2 text-lg font-semibold text-ink">Turn this opportunity into a campaign proposal.</h3>
                  <p className="mt-3 text-sm text-ink-soft">Use the contact details below and open the existing recommendation engine with this lead already framed as a growth opportunity.</p>
                  <div className="mt-4 flex flex-wrap gap-3">
                    <button
                      type="button"
                      onClick={() => setShowProposalForm((current) => !current)}
                      className="button-primary px-4 py-3"
                    >
                      {showProposalForm ? 'Hide proposal setup' : 'Generate Campaign Proposal'}
                    </button>
                  </div>

                  {selectedLead.data.enrichment.confidenceGate.isBlocked ? (
                    <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                      <p className="font-semibold">Confidence gate blocked</p>
                      <p className="mt-1">{selectedLead.data.enrichment.confidenceGate.message}</p>
                      {(selectedLead.data.enrichment.confidenceGate.missingRequiredFields ?? []).length > 0 ? (
                        <p className="mt-1">
                          Missing: {selectedLead.data.enrichment.confidenceGate.missingRequiredFields.join(', ')}
                        </p>
                      ) : null}
                    </div>
                  ) : (
                    <div className="mt-4 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
                      Confidence gate passed. Required enrichment fields are available.
                    </div>
                  )}

                  {showProposalForm ? (
                    <div className="mt-4 grid gap-4 md:grid-cols-2">
                      <label className="block">
                        <span className="label-base">Contact name</span>
                        <input
                          value={proposalForm.fullName}
                          onChange={(event) => setProposalForm((current) => ({ ...current, fullName: event.target.value }))}
                          className="input-base"
                          placeholder="Business contact"
                        />
                      </label>
                      <label className="block">
                        <span className="label-base">Contact email</span>
                        <input
                          value={proposalForm.email}
                          onChange={(event) => setProposalForm((current) => ({ ...current, email: event.target.value }))}
                          className="input-base"
                          placeholder="contact@business.co.za"
                        />
                      </label>
                      <label className="block">
                        <span className="label-base">Contact phone</span>
                        <input
                          value={proposalForm.phone}
                          onChange={(event) => setProposalForm((current) => ({ ...current, phone: event.target.value }))}
                          className="input-base"
                          placeholder="071 234 5678"
                        />
                      </label>
                      <label className="block">
                        <span className="label-base">Package band</span>
                        <select
                          value={proposalForm.packageBandId}
                          onChange={(event) => setProposalForm((current) => ({ ...current, packageBandId: event.target.value }))}
                          className="input-base"
                        >
                          <option value="">Select package</option>
                          {(packagesQuery.data ?? []).map((packageBand) => (
                            <option key={packageBand.id} value={packageBand.id}>
                              {packageBand.name} | {formatCurrency(packageBand.minBudget)} to {formatCurrency(packageBand.maxBudget)}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label className="block md:col-span-2">
                        <span className="label-base">Campaign name</span>
                        <input
                          value={proposalForm.campaignName}
                          onChange={(event) => setProposalForm((current) => ({ ...current, campaignName: event.target.value }))}
                          className="input-base"
                          placeholder="Growth Opportunity Campaign"
                        />
                      </label>
                      <div className="md:col-span-2">
                        <button
                          type="button"
                          onClick={() => generateProposalMutation.mutate()}
                          disabled={!canGenerateProposal || generateProposalMutation.isPending}
                          className="button-primary px-4 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          {generateProposalMutation.isPending ? 'Creating proposal draft...' : 'Create prospect campaign and generate proposal draft'}
                        </button>
                        {generateProposalMutation.isError ? (
                          <p className="mt-2 text-sm text-rose-700">
                            {(generateProposalMutation.error as Error)?.message || 'Failed to create proposal draft.'}
                          </p>
                        ) : null}
                      </div>
                    </div>
                  ) : null}
                </div>

                <details className="rounded-[24px] border border-line bg-white px-4 py-4">
                  <summary className="cursor-pointer list-none text-sm font-semibold text-ink">View detailed diagnostics</summary>
                  <div className="mt-4 space-y-3">
                    <div className="rounded-[18px] border border-line bg-slate-50 px-4 py-3">
                      <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Enrichment evidence trace</p>
                      <div className="mt-3 space-y-2">
                        {(selectedLead.data.enrichment.fields ?? []).map((field) => (
                          <div key={field.key} className="rounded-xl border border-line bg-white px-3 py-2">
                            <div className="flex items-center justify-between gap-3">
                              <p className="text-sm font-semibold text-ink">{field.label}</p>
                              <span className={`inline-flex rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.12em] ${channelConfidenceTone(field.confidence)}`}>
                                {field.confidence}
                              </span>
                            </div>
                            <p className="mt-1 text-sm text-ink-soft">{field.value || 'Unknown'}</p>
                            <p className="mt-1 text-xs text-ink-soft">Source: {field.source} | {field.reason}</p>
                          </div>
                        ))}
                      </div>
                    </div>

                    {(selectedLead.data.channelDetections ?? []).length > 0 ? (
                      selectedLead.data.channelDetections.map((channel) => (
                        <div key={channel.channel} className="rounded-[18px] border border-line bg-slate-50 px-4 py-3">
                          <div className="flex flex-wrap items-start justify-between gap-3">
                            <div>
                              <p className="text-sm font-semibold text-ink">{formatChannelLabel(channel.channel)}</p>
                              <p className="mt-1 text-sm text-ink-soft">{channel.dominantReason}</p>
                            </div>
                            <div className="text-right">
                              <p className="text-sm font-semibold text-ink">{channel.score}/100</p>
                              <p className={`mt-2 inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${channelConfidenceTone(channel.confidence)}`}>
                                {channel.confidence.replaceAll('_', ' ')}
                              </p>
                            </div>
                          </div>
                          {(channel.signals ?? []).length > 0 ? (
                            <div className="mt-3 flex flex-wrap gap-2">
                              {channel.signals.slice(0, 3).map((signal) => (
                                <span key={`${channel.channel}-${signal.type}-${signal.value}`} className="rounded-full border border-line bg-white px-3 py-1 text-[11px] text-ink-soft">
                                  {signal.type.replaceAll('_', ' ')} {signal.effectiveWeight >= 0 ? `+${signal.effectiveWeight}` : signal.effectiveWeight}
                                </span>
                              ))}
                            </div>
                          ) : null}
                        </div>
                      ))
                    ) : (
                      <p className="text-sm text-ink-soft">No channel detection yet. Run analysis first.</p>
                    )}
                  </div>
                </details>

                <div className="rounded-[24px] border border-line bg-white px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Recommended actions</p>
                  <div className="mt-4 space-y-3">
                    {priorityActions.length > 0 ? (
                      priorityActions.map((action) => (
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

                <details className="rounded-[24px] border border-line bg-white px-4 py-4">
                  <summary className="cursor-pointer list-none text-sm font-semibold text-ink">Log interaction and history</summary>
                  <div className="mt-4 rounded-[20px] border border-line bg-slate-50 px-4 py-4">
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

                  <div className="mt-4 rounded-[20px] border border-line bg-slate-50 px-4 py-4">
                    <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Interaction history</p>
                    <div className="mt-4 space-y-3">
                      {(selectedLead.data.interactionHistory ?? []).length > 0 ? (
                        selectedLead.data.interactionHistory.map((interaction) => (
                          <div key={interaction.id} className="rounded-[18px] border border-line bg-white px-4 py-3">
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

                  <div className="mt-4 rounded-[20px] border border-line bg-slate-50 px-4 py-4">
                    <p className="text-xs uppercase tracking-[0.16em] text-ink-soft">Insight timeline</p>
                    <div className="mt-4 space-y-3">
                      {(selectedLead.data.insightHistory ?? []).length > 0 ? (
                        selectedLead.data.insightHistory.map((item) => (
                          <div key={item.id} className="rounded-[18px] border border-line bg-white px-4 py-3">
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
                </details>
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


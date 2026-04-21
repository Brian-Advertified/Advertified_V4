import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, PlusCircle, Trash2, X } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type {
  AdminIndustryStrategyProfile,
  AdminLeadIndustryPolicy,
  AdminUpsertIndustryStrategyProfileInput,
  AdminUpsertLeadIndustryPolicyInput,
} from '../../types/domain';
import { ActionButton, ReadOnlyNotice, hasText } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, useAdminDashboardQuery } from './adminWorkspace';

const splitLines = (value: string) => value.split('\n').map((item) => item.trim()).filter(Boolean);
const joinLines = (items: string[]) => items.join('\n');

const parseBudgetSplit = (value: string): Record<string, number> => {
  return value
    .split('\n')
    .map((item) => item.trim())
    .filter(Boolean)
    .reduce<Record<string, number>>((acc, item) => {
      const separatorIndex = item.indexOf(':');
      if (separatorIndex <= 0) {
        return acc;
      }

      const key = item.slice(0, separatorIndex).trim();
      const rawValue = item.slice(separatorIndex + 1).trim();
      const parsed = Number(rawValue);
      if (!key || !Number.isFinite(parsed)) {
        return acc;
      }

      acc[key] = parsed;
      return acc;
    }, {});
};

const formatBudgetSplit = (value: Record<string, number>) => (
  Object.entries(value)
    .map(([key, amount]) => `${key}: ${amount}`)
    .join('\n')
);

const createDefaultPolicyForm = (sortOrder = 10): AdminUpsertLeadIndustryPolicyInput => ({
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
  sortOrder,
  isActive: true,
});

const createDefaultStrategyForm = (): AdminUpsertIndustryStrategyProfileInput => ({
  industryCode: '',
  industryLabel: '',
  primaryPersona: '',
  buyingJourney: '',
  trustSensitivity: '',
  defaultLanguageBiases: [],
  defaultObjective: '',
  funnelShape: '',
  primaryKpis: [],
  salesCycle: '',
  preferredChannels: [],
  baseBudgetSplit: {},
  geographyBias: '',
  preferredTone: '',
  messagingAngle: '',
  recommendedCta: '',
  proofPoints: [],
  guardrails: [],
  restrictedClaimTypes: [],
  researchSummary: '',
  researchSources: [],
});

export function AdminIndustryPoliciesPage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [policyDialog, setPolicyDialog] = useState<{ mode: 'create' | 'view' | 'edit'; key?: string } | null>(null);
  const [strategyDialog, setStrategyDialog] = useState<{ mode: 'create' | 'view' | 'edit'; industryCode?: string } | null>(null);
  const [policyForm, setPolicyForm] = useState<AdminUpsertLeadIndustryPolicyInput>(createDefaultPolicyForm());
  const [strategyForm, setStrategyForm] = useState<AdminUpsertIndustryStrategyProfileInput>(createDefaultStrategyForm());

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
  };

  const createPolicyMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminLeadIndustryPolicy(policyForm),
    onSuccess: async () => {
      await refresh();
      setPolicyDialog(null);
      pushToast({ title: 'Industry policy created.', description: 'The lead strategy policy is now stored as live operator-managed data.' });
    },
    onError: (error) => pushToast({ title: 'Could not create industry policy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updatePolicyMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminLeadIndustryPolicy(policyDialog?.key ?? '', policyForm),
    onSuccess: async () => {
      await refresh();
      setPolicyDialog(null);
      pushToast({ title: 'Industry policy updated.', description: 'The lead strategy policy changes are now live.' });
    },
    onError: (error) => pushToast({ title: 'Could not update industry policy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deletePolicyMutation = useMutation({
    mutationFn: (key: string) => advertifiedApi.deleteAdminLeadIndustryPolicy(key),
    onSuccess: async () => {
      await refresh();
      setPolicyDialog(null);
      pushToast({ title: 'Industry policy deleted.', description: 'The policy was removed from the live strategy catalog.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete industry policy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const createStrategyMutation = useMutation({
    mutationFn: () => advertifiedApi.createAdminIndustryStrategyProfile(strategyForm),
    onSuccess: async () => {
      await refresh();
      setStrategyDialog(null);
      pushToast({ title: 'Industry strategy created.', description: 'The richer industry defaults are now operator-editable and live.' });
    },
    onError: (error) => pushToast({ title: 'Could not create industry strategy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const updateStrategyMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminIndustryStrategyProfile(strategyDialog?.industryCode ?? '', strategyForm),
    onSuccess: async () => {
      await refresh();
      setStrategyDialog(null);
      pushToast({ title: 'Industry strategy updated.', description: 'The richer industry defaults are now live.' });
    },
    onError: (error) => pushToast({ title: 'Could not update industry strategy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteStrategyMutation = useMutation({
    mutationFn: (industryCode: string) => advertifiedApi.deleteAdminIndustryStrategyProfile(industryCode),
    onSuccess: async () => {
      await refresh();
      setStrategyDialog(null);
      pushToast({ title: 'Industry strategy deleted.', description: 'The strategy profile was removed from the live industry catalog.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete industry strategy.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const selectedPolicy = policyDialog?.key
          ? dashboard.leadIndustryPolicies.find((item) => item.key === policyDialog.key) ?? null
          : null;
        const selectedStrategy = strategyDialog?.industryCode
          ? dashboard.industryStrategyProfiles.find((item) => item.industryCode === strategyDialog.industryCode) ?? null
          : null;

        const hydratePolicyForm = (item: AdminLeadIndustryPolicy) => {
          setPolicyForm({
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

        const hydrateStrategyForm = (item: AdminIndustryStrategyProfile) => {
          setStrategyForm({
            industryCode: item.industryCode,
            industryLabel: item.industryLabel,
            primaryPersona: item.primaryPersona,
            buyingJourney: item.buyingJourney,
            trustSensitivity: item.trustSensitivity,
            defaultLanguageBiases: item.defaultLanguageBiases,
            defaultObjective: item.defaultObjective,
            funnelShape: item.funnelShape,
            primaryKpis: item.primaryKpis,
            salesCycle: item.salesCycle,
            preferredChannels: item.preferredChannels,
            baseBudgetSplit: item.baseBudgetSplit,
            geographyBias: item.geographyBias,
            preferredTone: item.preferredTone,
            messagingAngle: item.messagingAngle,
            recommendedCta: item.recommendedCta,
            proofPoints: item.proofPoints,
            guardrails: item.guardrails,
            restrictedClaimTypes: item.restrictedClaimTypes,
            researchSummary: item.researchSummary,
            researchSources: item.researchSources,
          });
        };

        const openPolicyDialog = (mode: 'create' | 'view' | 'edit', item?: AdminLeadIndustryPolicy) => {
          if (item) {
            hydratePolicyForm(item);
            setPolicyDialog({ mode, key: item.key });
            return;
          }

          const nextSortOrder = dashboard.leadIndustryPolicies.length > 0
            ? Math.max(...dashboard.leadIndustryPolicies.map((entry) => entry.sortOrder)) + 10
            : 10;

          setPolicyForm(createDefaultPolicyForm(nextSortOrder));
          setPolicyDialog({ mode });
        };

        const openStrategyDialog = (mode: 'create' | 'view' | 'edit', item?: AdminIndustryStrategyProfile) => {
          if (item) {
            hydrateStrategyForm(item);
            setStrategyDialog({ mode, industryCode: item.industryCode });
            return;
          }

          setStrategyForm(createDefaultStrategyForm());
          setStrategyDialog({ mode });
        };

        const policyFormIsValid = hasText(policyForm.key) && hasText(policyForm.name) && hasText(policyForm.cta) && hasText(policyForm.messagingAngle);
        const strategyFormIsValid = hasText(strategyForm.industryCode)
          && hasText(strategyForm.industryLabel)
          && hasText(strategyForm.primaryPersona)
          && hasText(strategyForm.defaultObjective)
          && hasText(strategyForm.messagingAngle);

        return (
          <AdminPageShell title="Industry policies and strategy" description="Manage both the live lead-policy overrides and the richer industry strategy catalog that drives default objectives, audience targets, channels, messaging, compliance, and research guidance.">
            <section className="space-y-8">
              <div className="panel p-6">
                <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                  <div>
                    <h3 className="text-lg font-semibold text-ink">Lead policy catalog</h3>
                    <p className="mt-2 text-sm text-ink-soft">These runtime policies override campaign framing, preferred channels, and messaging defaults for lead intelligence.</p>
                  </div>
                  <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3" onClick={() => openPolicyDialog('create')}>
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
                        <ActionButton label={`View ${policy.name}`} icon={Eye} onClick={() => openPolicyDialog('view', policy)} />
                        <ActionButton label={`Edit ${policy.name}`} icon={Pencil} onClick={() => openPolicyDialog('edit', policy)} />
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

              <div className="panel p-6">
                <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                  <div>
                    <h3 className="text-lg font-semibold text-ink">Strategy catalog</h3>
                    <p className="mt-2 text-sm text-ink-soft">These richer profiles define the target audience, journey, KPIs, channel mix, creative guidance, compliance constraints, and research basis for each industry.</p>
                  </div>
                  <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3" onClick={() => openStrategyDialog('create')}>
                    <PlusCircle className="size-4" />
                    Add strategy profile
                  </button>
                </div>
              </div>

              <div className="grid gap-4 xl:grid-cols-2">
                {dashboard.industryStrategyProfiles.map((profile) => (
                  <div key={profile.industryCode} className="panel p-6">
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="pill">{profile.industryCode}</span>
                          <span className="pill border-brand/15 bg-white text-brand">{profile.defaultObjective || 'No objective'}</span>
                        </div>
                        <h3 className="mt-4 text-xl font-semibold text-ink">{profile.industryLabel}</h3>
                        <p className="mt-2 text-sm text-ink-soft">{profile.primaryPersona}</p>
                      </div>
                      <div className="flex items-center gap-2">
                        <ActionButton label={`View ${profile.industryLabel}`} icon={Eye} onClick={() => openStrategyDialog('view', profile)} />
                        <ActionButton label={`Edit ${profile.industryLabel}`} icon={Pencil} onClick={() => openStrategyDialog('edit', profile)} />
                      </div>
                    </div>
                    <div className="mt-5 grid gap-2 text-sm text-ink-soft">
                      <div><span className="font-semibold text-ink">Journey:</span> {profile.buyingJourney || 'Not set'}</div>
                      <div><span className="font-semibold text-ink">Channels:</span> {profile.preferredChannels.length > 0 ? profile.preferredChannels.join(', ') : 'No preferred channels'}</div>
                      <div><span className="font-semibold text-ink">CTA:</span> {profile.recommendedCta || 'Not set'}</div>
                      <div><span className="font-semibold text-ink">Research sources:</span> {profile.researchSources.length}</div>
                    </div>
                  </div>
                ))}
              </div>

              {policyDialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-5xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4">
                      <h3 className="text-xl font-semibold text-ink">
                        {policyDialog.mode === 'create' ? 'Add industry policy' : policyDialog.mode === 'view' ? 'View industry policy' : 'Edit industry policy'}
                      </h3>
                      <button type="button" className="button-secondary p-3" onClick={() => setPolicyDialog(null)}>
                        <X className="size-4" />
                      </button>
                    </div>
                    {policyDialog.mode === 'view' && selectedPolicy ? (
                      <ReadOnlyNotice label={`Viewing ${selectedPolicy.name}. Switch to edit mode when you need to change runtime lead strategy behaviour.`} />
                    ) : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                      <input disabled={policyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Policy key" value={policyForm.key} onChange={(event) => setPolicyForm((current) => ({ ...current, key: event.target.value }))} />
                      <input disabled={policyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Display name" value={policyForm.name} onChange={(event) => setPolicyForm((current) => ({ ...current, name: event.target.value }))} />
                      <input disabled={policyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" type="number" placeholder="Sort order" value={policyForm.sortOrder} onChange={(event) => setPolicyForm((current) => ({ ...current, sortOrder: Number(event.target.value) }))} />
                      <input disabled={policyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Objective override" value={policyForm.objectiveOverride ?? ''} onChange={(event) => setPolicyForm((current) => ({ ...current, objectiveOverride: event.target.value }))} />
                      <input disabled={policyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Preferred tone" value={policyForm.preferredTone ?? ''} onChange={(event) => setPolicyForm((current) => ({ ...current, preferredTone: event.target.value }))} />
                      <label className="inline-flex items-center gap-2 rounded-full border border-line px-4 py-3 text-sm text-ink-soft">
                        <input disabled={policyDialog.mode === 'view'} type="checkbox" checked={policyForm.isActive} onChange={(event) => setPolicyForm((current) => ({ ...current, isActive: event.target.checked }))} />
                        Active
                      </label>
                    </div>
                    <div className="mt-4 grid gap-4 md:grid-cols-2">
                      <textarea disabled={policyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Preferred channels, one per line" value={joinLines(policyForm.preferredChannels)} onChange={(event) => setPolicyForm((current) => ({ ...current, preferredChannels: splitLines(event.target.value) }))} />
                      <textarea disabled={policyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Guardrails, one per line" value={joinLines(policyForm.guardrails)} onChange={(event) => setPolicyForm((current) => ({ ...current, guardrails: splitLines(event.target.value) }))} />
                    </div>
                    <input disabled={policyDialog.mode === 'view'} className="input-base mt-4 disabled:bg-slate-50" placeholder="CTA" value={policyForm.cta} onChange={(event) => setPolicyForm((current) => ({ ...current, cta: event.target.value }))} />
                    <textarea disabled={policyDialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Messaging angle" value={policyForm.messagingAngle} onChange={(event) => setPolicyForm((current) => ({ ...current, messagingAngle: event.target.value }))} />
                    <textarea disabled={policyDialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Additional gap" value={policyForm.additionalGap} onChange={(event) => setPolicyForm((current) => ({ ...current, additionalGap: event.target.value }))} />
                    <textarea disabled={policyDialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Additional outcome" value={policyForm.additionalOutcome} onChange={(event) => setPolicyForm((current) => ({ ...current, additionalOutcome: event.target.value }))} />
                    {!policyFormIsValid ? <p className="mt-3 text-sm text-rose-600">Key, name, CTA, and messaging angle are required before saving.</p> : null}
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => setPolicyDialog(null)}>Close</button>
                      {policyDialog.mode === 'view' && selectedPolicy ? (
                        <button type="button" className="button-secondary px-5 py-3" onClick={() => openPolicyDialog('edit', selectedPolicy)}>
                          Edit policy
                        </button>
                      ) : null}
                      {policyDialog.mode === 'edit' && selectedPolicy ? (
                        <button
                          type="button"
                          className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50"
                          disabled={deletePolicyMutation.isPending}
                          onClick={() => {
                            if (window.confirm(`Delete industry policy ${selectedPolicy.name}? This changes live lead strategy behaviour.`)) {
                              deletePolicyMutation.mutate(selectedPolicy.key);
                            }
                          }}
                        >
                          <Trash2 className="mr-2 inline size-4" />
                          Delete policy
                        </button>
                      ) : null}
                      {policyDialog.mode === 'create' ? (
                        <button type="button" className="button-primary px-5 py-3" disabled={!policyFormIsValid || createPolicyMutation.isPending} onClick={() => createPolicyMutation.mutate()}>
                          Save policy
                        </button>
                      ) : null}
                      {policyDialog.mode === 'edit' ? (
                        <button type="button" className="button-primary px-5 py-3" disabled={!policyFormIsValid || updatePolicyMutation.isPending} onClick={() => updatePolicyMutation.mutate()}>
                          Update policy
                        </button>
                      ) : null}
                    </div>
                  </div>
                </div>
              ) : null}

              {strategyDialog ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                  <div className="w-full max-w-6xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                    <div className="flex items-center justify-between gap-4">
                      <h3 className="text-xl font-semibold text-ink">
                        {strategyDialog.mode === 'create' ? 'Add strategy profile' : strategyDialog.mode === 'view' ? 'View strategy profile' : 'Edit strategy profile'}
                      </h3>
                      <button type="button" className="button-secondary p-3" onClick={() => setStrategyDialog(null)}>
                        <X className="size-4" />
                      </button>
                    </div>
                    {strategyDialog.mode === 'view' && selectedStrategy ? (
                      <ReadOnlyNotice label={`Viewing ${selectedStrategy.industryLabel}. Switch to edit mode when you need to change the richer default strategy for this industry.`} />
                    ) : null}
                    <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                      <input disabled={strategyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Industry code" value={strategyForm.industryCode} onChange={(event) => setStrategyForm((current) => ({ ...current, industryCode: event.target.value }))} />
                      <input disabled={strategyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Industry label" value={strategyForm.industryLabel} onChange={(event) => setStrategyForm((current) => ({ ...current, industryLabel: event.target.value }))} />
                      <input disabled={strategyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Trust sensitivity" value={strategyForm.trustSensitivity} onChange={(event) => setStrategyForm((current) => ({ ...current, trustSensitivity: event.target.value }))} />
                      <input disabled={strategyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Default objective" value={strategyForm.defaultObjective} onChange={(event) => setStrategyForm((current) => ({ ...current, defaultObjective: event.target.value }))} />
                      <input disabled={strategyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Funnel shape" value={strategyForm.funnelShape} onChange={(event) => setStrategyForm((current) => ({ ...current, funnelShape: event.target.value }))} />
                      <input disabled={strategyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Sales cycle" value={strategyForm.salesCycle} onChange={(event) => setStrategyForm((current) => ({ ...current, salesCycle: event.target.value }))} />
                      <input disabled={strategyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Geography bias" value={strategyForm.geographyBias} onChange={(event) => setStrategyForm((current) => ({ ...current, geographyBias: event.target.value }))} />
                      <input disabled={strategyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Preferred tone" value={strategyForm.preferredTone} onChange={(event) => setStrategyForm((current) => ({ ...current, preferredTone: event.target.value }))} />
                      <input disabled={strategyDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Recommended CTA" value={strategyForm.recommendedCta} onChange={(event) => setStrategyForm((current) => ({ ...current, recommendedCta: event.target.value }))} />
                    </div>
                    <textarea disabled={strategyDialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Primary persona" value={strategyForm.primaryPersona} onChange={(event) => setStrategyForm((current) => ({ ...current, primaryPersona: event.target.value }))} />
                    <textarea disabled={strategyDialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Buying journey" value={strategyForm.buyingJourney} onChange={(event) => setStrategyForm((current) => ({ ...current, buyingJourney: event.target.value }))} />
                    <textarea disabled={strategyDialog.mode === 'view'} className="input-base mt-4 min-h-[110px] disabled:bg-slate-50" placeholder="Messaging angle" value={strategyForm.messagingAngle} onChange={(event) => setStrategyForm((current) => ({ ...current, messagingAngle: event.target.value }))} />
                    <textarea disabled={strategyDialog.mode === 'view'} className="input-base mt-4 min-h-[140px] disabled:bg-slate-50" placeholder="Research summary" value={strategyForm.researchSummary} onChange={(event) => setStrategyForm((current) => ({ ...current, researchSummary: event.target.value }))} />
                    <div className="mt-4 grid gap-4 md:grid-cols-2">
                      <textarea disabled={strategyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Default language biases, one per line" value={joinLines(strategyForm.defaultLanguageBiases)} onChange={(event) => setStrategyForm((current) => ({ ...current, defaultLanguageBiases: splitLines(event.target.value) }))} />
                      <textarea disabled={strategyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Primary KPIs, one per line" value={joinLines(strategyForm.primaryKpis)} onChange={(event) => setStrategyForm((current) => ({ ...current, primaryKpis: splitLines(event.target.value) }))} />
                      <textarea disabled={strategyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Preferred channels, one per line" value={joinLines(strategyForm.preferredChannels)} onChange={(event) => setStrategyForm((current) => ({ ...current, preferredChannels: splitLines(event.target.value) }))} />
                      <textarea disabled={strategyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Base budget split, one per line, e.g. Digital: 60" value={formatBudgetSplit(strategyForm.baseBudgetSplit)} onChange={(event) => setStrategyForm((current) => ({ ...current, baseBudgetSplit: parseBudgetSplit(event.target.value) }))} />
                      <textarea disabled={strategyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Proof points, one per line" value={joinLines(strategyForm.proofPoints)} onChange={(event) => setStrategyForm((current) => ({ ...current, proofPoints: splitLines(event.target.value) }))} />
                      <textarea disabled={strategyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Guardrails, one per line" value={joinLines(strategyForm.guardrails)} onChange={(event) => setStrategyForm((current) => ({ ...current, guardrails: splitLines(event.target.value) }))} />
                      <textarea disabled={strategyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Restricted claim types, one per line" value={joinLines(strategyForm.restrictedClaimTypes)} onChange={(event) => setStrategyForm((current) => ({ ...current, restrictedClaimTypes: splitLines(event.target.value) }))} />
                      <textarea disabled={strategyDialog.mode === 'view'} className="input-base min-h-[110px] disabled:bg-slate-50" placeholder="Research sources, one per line" value={joinLines(strategyForm.researchSources)} onChange={(event) => setStrategyForm((current) => ({ ...current, researchSources: splitLines(event.target.value) }))} />
                    </div>
                    {!strategyFormIsValid ? <p className="mt-3 text-sm text-rose-600">Industry code, label, primary persona, default objective, and messaging angle are required before saving.</p> : null}
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => setStrategyDialog(null)}>Close</button>
                      {strategyDialog.mode === 'view' && selectedStrategy ? (
                        <button type="button" className="button-secondary px-5 py-3" onClick={() => openStrategyDialog('edit', selectedStrategy)}>
                          Edit strategy
                        </button>
                      ) : null}
                      {strategyDialog.mode === 'edit' && selectedStrategy ? (
                        <button
                          type="button"
                          className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50"
                          disabled={deleteStrategyMutation.isPending}
                          onClick={() => {
                            if (window.confirm(`Delete strategy profile ${selectedStrategy.industryLabel}? This removes the richer defaults for that industry.`)) {
                              deleteStrategyMutation.mutate(selectedStrategy.industryCode);
                            }
                          }}
                        >
                          <Trash2 className="mr-2 inline size-4" />
                          Delete strategy
                        </button>
                      ) : null}
                      {strategyDialog.mode === 'create' ? (
                        <button type="button" className="button-primary px-5 py-3" disabled={!strategyFormIsValid || createStrategyMutation.isPending} onClick={() => createStrategyMutation.mutate()}>
                          Save strategy
                        </button>
                      ) : null}
                      {strategyDialog.mode === 'edit' ? (
                        <button type="button" className="button-primary px-5 py-3" disabled={!strategyFormIsValid || updateStrategyMutation.isPending} onClick={() => updateStrategyMutation.mutate()}>
                          Update strategy
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

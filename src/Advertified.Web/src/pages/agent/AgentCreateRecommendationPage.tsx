import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ChevronRight, Sparkles, Wand2 } from 'lucide-react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { CampaignBrief } from '../../types/domain';

type ChannelOption = 'Radio' | 'OOH' | 'TV';

const STEP_CONFIG = [
  { id: 1, label: 'Order' },
  { id: 2, label: 'Campaign' },
  { id: 3, label: 'AI Draft' },
] as const;

const CHANNEL_OPTIONS: ChannelOption[] = ['Radio', 'OOH', 'TV'];
const CHANNEL_LABELS: Record<ChannelOption, string> = {
  Radio: 'Radio',
  OOH: 'Billboards and digital screens',
  TV: 'TV',
};

function getAllowedChannels(campaign?: {
  includeRadio: 'yes' | 'optional' | 'no';
  includeTv: 'yes' | 'optional' | 'no';
} | null): ChannelOption[] {
  const allowed: ChannelOption[] = ['OOH'];
  if (campaign?.includeRadio !== 'no') {
    allowed.unshift('Radio');
  }

  if (campaign?.includeTv !== 'no') {
    allowed.push('TV');
  }

  return allowed;
}

type CampaignFormState = {
  objective: string;
  audience: string;
  scope: string;
  geography: string;
  brandName: string;
  tone: string;
  brief: string;
  channels: ChannelOption[];
};

function inferInitialForm(campaign: {
  clientName: string;
  packageBandName: string;
  selectedBudget: number;
  queueStage: string;
  nextAction: string;
  includeRadio: 'yes' | 'optional' | 'no';
  includeTv: 'yes' | 'optional' | 'no';
}) : CampaignFormState {
  const defaultChannels = getAllowedChannels(campaign);

  return {
    objective: campaign.queueStage === 'newly_paid' ? 'launch' : 'awareness',
    audience: 'retail',
    scope: campaign.selectedBudget >= 500000 ? 'national' : campaign.selectedBudget >= 150000 ? 'regional' : 'local',
    geography: campaign.selectedBudget >= 500000 ? 'national' : 'gauteng',
    brandName: `${campaign.clientName} ${campaign.packageBandName} Campaign`,
    tone: campaign.packageBandName === 'Dominance' ? 'premium' : 'high-visibility',
    brief: `Build a ${campaign.packageBandName.toLowerCase()} recommendation for ${campaign.clientName} within ${formatCurrency(campaign.selectedBudget)}. ${campaign.nextAction}`,
    channels: defaultChannels,
  };
}

export function AgentCreateRecommendationPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const inboxQuery = useQuery({ queryKey: ['agent-inbox'], queryFn: advertifiedApi.getAgentInbox });
  const packagesQuery = useQuery({ queryKey: ['packages'], queryFn: advertifiedApi.getPackages });
  const requestedCampaignId = searchParams.get('campaignId') ?? '';
  const [selectedCampaignIdState, setSelectedCampaignIdState] = useState<string>('');
  const [selectedClientIdState, setSelectedClientIdState] = useState<string>('');
  const [pendingAction, setPendingAction] = useState<'draft' | 'generate' | null>(null);
  const [aiInterpretationSummary, setAiInterpretationSummary] = useState('');
  const hydratedCampaignIdRef = useRef<string | null>(null);
  const [form, setForm] = useState<CampaignFormState>({
    objective: '',
    audience: '',
    scope: '',
    geography: '',
    brandName: '',
    tone: '',
    brief: '',
    channels: ['Radio', 'OOH'],
  });

  const availableCampaigns = useMemo(() => (inboxQuery.data?.items ?? []).filter((item) => (
    item.queueStage !== 'waiting_on_client'
    && item.queueStage !== 'completed'
  )), [inboxQuery.data]);

  const clientOptions = useMemo(() => {
    const uniqueClients = new Map<string, { id: string; name: string; email: string }>();
    for (const item of availableCampaigns) {
      if (!uniqueClients.has(item.userId)) {
        uniqueClients.set(item.userId, { id: item.userId, name: item.clientName, email: item.clientEmail });
      }
    }

    return Array.from(uniqueClients.values());
  }, [availableCampaigns]);
  const requestedCampaign = requestedCampaignId
    ? availableCampaigns.find((item) => item.id === requestedCampaignId) ?? null
    : null;
  const selectedClientId = requestedCampaign?.userId
    ?? selectedClientIdState
    ?? availableCampaigns[0]?.userId
    ?? '';

  const filteredCampaigns = useMemo(() => (
    selectedClientId
      ? availableCampaigns.filter((item) => item.userId === selectedClientId)
      : availableCampaigns
  ), [availableCampaigns, selectedClientId]);
  const selectedCampaignId = requestedCampaign?.id
    ?? selectedCampaignIdState
    ?? filteredCampaigns[0]?.id
    ?? availableCampaigns[0]?.id
    ?? '';

  const selectedCampaign = filteredCampaigns.find((item) => item.id === selectedCampaignId)
    ?? availableCampaigns.find((item) => item.id === selectedCampaignId)
    ?? filteredCampaigns[0]
    ?? availableCampaigns[0]
    ?? null;
  const selectedPackageBand = useMemo(
    () => packagesQuery.data?.find((item) => item.id === selectedCampaign?.packageBandId) ?? null,
    [packagesQuery.data, selectedCampaign?.packageBandId],
  );
  const allowedChannels = useMemo(
    () => getAllowedChannels(selectedPackageBand),
    [selectedPackageBand],
  );
  const selectedCampaignHydrationKey = selectedCampaign
    ? `${selectedCampaign.id}:${selectedPackageBand?.includeRadio ?? 'optional'}:${selectedPackageBand?.includeTv ?? 'no'}`
    : null;

  useEffect(() => {
    if (!selectedCampaign || !selectedCampaignHydrationKey) {
      return;
    }

    if (hydratedCampaignIdRef.current === selectedCampaignHydrationKey) {
      return;
    }

    hydratedCampaignIdRef.current = selectedCampaignHydrationKey;
    setForm(inferInitialForm({
      ...selectedCampaign,
      includeRadio: selectedPackageBand?.includeRadio ?? 'optional',
      includeTv: selectedPackageBand?.includeTv ?? 'no',
    }));
    setAiInterpretationSummary('');
  }, [selectedCampaign, selectedCampaignHydrationKey, selectedPackageBand?.includeRadio, selectedPackageBand?.includeTv]);

  const handleFormChange = <K extends keyof CampaignFormState>(key: K, value: CampaignFormState[K]) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const toggleChannel = (channel: ChannelOption) => {
    if (!allowedChannels.includes(channel)) {
      return;
    }

    setForm((current) => ({
      ...current,
      channels: current.channels.includes(channel)
        ? current.channels.filter((item) => item !== channel)
        : [...current.channels, channel],
    }));
  };

  const handleClientChange = (userId: string) => {
    setSelectedClientIdState(userId);
    const nextCampaign = availableCampaigns.find((item) => item.userId === userId);
    setSelectedCampaignIdState(nextCampaign?.id ?? '');
  };

  function buildBriefPayload(): CampaignBrief {
    const provinceMap: Record<string, string> = {
      gauteng: 'Gauteng',
      'western-cape': 'Western Cape',
      'kwazulu-natal': 'KwaZulu-Natal',
    };

    return {
      objective: form.objective || 'awareness',
      geographyScope: form.scope || 'regional',
      provinces: form.geography && form.geography !== 'national' ? [provinceMap[form.geography] ?? form.geography] : undefined,
      areas: form.geography && form.geography !== 'national' ? [form.geography] : undefined,
      targetAudienceNotes: [form.audience, form.tone].filter(Boolean).join(' · '),
      preferredMediaTypes: form.channels
        .filter((channel) => allowedChannels.includes(channel))
        .map((channel) => channel.toLowerCase()),
      creativeNotes: form.brandName || undefined,
      openToUpsell: false,
      specialRequirements: form.brief,
    };
  }

  const initializeMutation = useMutation({
    mutationFn: async ({ submitBrief }: { submitBrief: boolean }) => {
      if (!selectedCampaign) {
        throw new Error('Choose an order first.');
      }

      const campaign = await advertifiedApi.initializeAgentRecommendation(selectedCampaign.id, {
        campaignName: form.brandName,
        planningMode: 'hybrid',
        submitBrief,
        brief: buildBriefPayload(),
      });

      if (submitBrief) {
        return advertifiedApi.generateAgentRecommendation(selectedCampaign.id);
      }

      return campaign;
    },
    onSuccess: (campaign, variables) => {
      setPendingAction(null);
      pushToast({
        title: variables.submitBrief ? 'Recommendation generated.' : 'Draft saved.',
        description: variables.submitBrief
          ? 'The AI draft is ready in the planning workspace for agent review.'
          : 'The campaign brief has been saved for the agent workflow.',
      });

      if (variables.submitBrief) {
        navigate(`/agent/campaigns/${campaign.id}`);
      }

      void Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', campaign.id] }),
        queryClient.invalidateQueries({ queryKey: ['campaign', campaign.id] }),
      ]);
    },
    onError: (error) => {
      setPendingAction(null);
      pushToast({
        title: 'We could not create the recommendation flow.',
        description: error instanceof Error ? error.message : 'Please try again in a moment.',
      }, 'error');
    },
  });

  const interpretMutation = useMutation({
    mutationFn: async () => {
      if (!selectedCampaign) {
        throw new Error('Choose an order first.');
      }

      return advertifiedApi.interpretAgentBrief(selectedCampaign.id, {
        brief: form.brief,
        campaignName: form.brandName,
        selectedBudget: selectedCampaign.selectedBudget,
      });
    },
    onSuccess: (result) => {
      const interpretedChannels = result.channels
        .filter((channel): channel is ChannelOption => CHANNEL_OPTIONS.includes(channel as ChannelOption));

      setForm((current) => ({
        ...current,
        objective: result.objective || current.objective,
        audience: result.audience || current.audience,
        scope: result.scope || current.scope,
        geography: result.geography || current.geography,
        tone: result.tone || current.tone,
        brandName: result.campaignName || current.brandName,
        channels: (interpretedChannels.length > 0 ? interpretedChannels : current.channels)
          .filter((channel) => allowedChannels.includes(channel)),
      }));
      setAiInterpretationSummary(result.summary);
      pushToast({
        title: 'AI inputs updated.',
        description: 'The brief has been interpreted into structured campaign inputs you can refine before generating the draft.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'We could not interpret that brief.',
        description: error instanceof Error ? error.message : 'Please try again in a moment.',
      }, 'error');
    },
  });

  const isStep1Complete = Boolean(selectedClientId && selectedCampaign);
  const isStep2Complete = isStep1Complete
    && Boolean(form.objective && form.audience && form.scope && form.geography && form.brief.trim() && form.channels.length > 0);
  const isGenerating = pendingAction === 'generate' && initializeMutation.isPending;
  const isWorking = initializeMutation.isPending || interpretMutation.isPending;
  const canGenerate = isStep2Complete && !isWorking;
  const canSaveDraft = isStep1Complete && !isWorking;

  const stepPresentation = STEP_CONFIG.map((step) => {
    if (step.id === 1) {
      return {
        ...step,
        toneClass: isStep1Complete ? 'bg-highlight-soft text-highlight' : 'bg-brand text-white',
        displayValue: isStep1Complete ? '✓' : '1',
      };
    }

    if (step.id === 2) {
      const isActive = isStep1Complete && !isGenerating;
      return {
        ...step,
        toneClass: isStep2Complete
          ? 'bg-highlight-soft text-highlight'
          : isActive
            ? 'bg-brand text-white'
            : 'bg-slate-100 text-slate-500',
        displayValue: isStep2Complete ? '✓' : '2',
      };
    }

    return {
      ...step,
      label: isGenerating ? 'Generating draft...' : step.label,
      toneClass: isGenerating ? 'bg-brand text-white' : 'bg-slate-100 text-slate-400',
      displayValue: isGenerating ? '…' : '3',
    };
  });

  const handleSaveDraft = async () => {
    setPendingAction('draft');
    await initializeMutation.mutateAsync({ submitBrief: false });
  };

  const handleGenerate = async () => {
    if (!selectedCampaign) {
      pushToast({
        title: 'Choose an order first.',
        description: 'Select a client and paid package before generating a recommendation.',
      }, 'info');
      return;
    }

    if (!isStep2Complete) {
      pushToast({
        title: 'Complete the campaign details first.',
        description: 'Add the campaign objective, audience, scope, geography, and brief before generating the draft.',
      }, 'info');
      return;
    }

    setPendingAction('generate');
    await initializeMutation.mutateAsync({ submitBrief: true });
  };

  if (inboxQuery.isLoading || packagesQuery.isLoading) {
    return <LoadingState label="Loading recommendation flow..." />;
  }

  return (
    <section className="min-h-screen bg-surface text-ink">
      {initializeMutation.isPending ? (
        <ProcessingOverlay
          label={pendingAction === 'generate' ? 'Generating AI draft and opening the workspace...' : 'Saving recommendation draft...'}
        />
      ) : null}
      <div className="border-b border-line bg-white/90 backdrop-blur">
        <div className="page-shell flex flex-col gap-4 py-4 lg:flex-row lg:items-center lg:justify-between">
          <button type="button" onClick={() => navigate('/agent')} className="button-secondary inline-flex items-center gap-2 px-4 py-2">
            <ArrowLeft className="size-4" />
            Back to dashboard
          </button>

          <div className="flex flex-wrap items-center gap-2 sm:gap-3">
            {stepPresentation.map((step, index) => {
              const toneClass = step.toneClass;
              return (
                <div key={step.id} className="flex items-center gap-2">
                  <div className={`flex h-8 w-8 items-center justify-center rounded-full text-sm font-semibold ${toneClass}`}>
                    {step.displayValue}
                  </div>
                  <span className="hidden text-sm text-ink-soft md:inline">{step.label}</span>
                  {index < STEP_CONFIG.length - 1 ? <ChevronRight className="size-4 text-slate-300" /> : null}
                </div>
              );
            })}
          </div>
        </div>
      </div>

      <div className="page-shell grid gap-6 py-8 lg:grid-cols-[1.05fr_380px]">
        <div className="space-y-6">
          <div>
            <div className="pill border-brand/10 bg-brand text-white">Create recommendation</div>
            <h1 className="mt-4 text-3xl font-semibold tracking-tight text-ink">Build a new client recommendation</h1>
            <p className="mt-2 max-w-3xl text-sm leading-6 text-ink-soft md:text-base">
              Link the recommendation to a purchased package, capture campaign intent, and let AI generate a draft the agent can refine.
            </p>
          </div>

          <div className="panel border-line/90 px-6 py-6 md:px-7">
            <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="text-lg font-semibold text-ink">1. Select order</h2>
                <p className="mt-1 text-sm text-ink-soft">Start from a paid package or existing client order.</p>
              </div>
              <span className="pill bg-white text-ink-soft">Required</span>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <label className="block">
                <span className="label-base">Client</span>
                <select value={selectedClientId} onChange={(event) => handleClientChange(event.target.value)} className="input-base">
                  <option value="">Select client</option>
                  {clientOptions.map((client) => (
                    <option key={client.id} value={client.id}>{client.name}</option>
                  ))}
                </select>
              </label>

              <label className="block">
                <span className="label-base">Paid package / order</span>
                <select value={selectedCampaign?.id ?? ''} onChange={(event) => setSelectedCampaignIdState(event.target.value)} className="input-base">
                  <option value="">Select paid package</option>
                  {filteredCampaigns.map((campaign) => (
                    <option key={campaign.id} value={campaign.id}>
                      {campaign.packageBandName} · {formatCurrency(campaign.selectedBudget)} · {campaign.queueLabel}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          </div>

          <div className="panel border-line/90 px-6 py-6 md:px-7">
            <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="text-lg font-semibold text-ink">2. Capture campaign details</h2>
                <p className="mt-1 text-sm text-ink-soft">Collect structured inputs. Geography is defined here, not at package purchase.</p>
              </div>
              <span className="pill bg-white text-ink-soft">Editable by AI</span>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <label className="block">
                <span className="label-base">Campaign objective</span>
                <select value={form.objective} onChange={(event) => handleFormChange('objective', event.target.value)} className="input-base">
                  <option value="">Choose objective</option>
                  <option value="awareness">Awareness</option>
                  <option value="launch">Launch</option>
                  <option value="promotion">Promotion</option>
                  <option value="brand_presence">Brand presence</option>
                  <option value="leads">Leads</option>
                </select>
              </label>

              <label className="block">
                <span className="label-base">Audience</span>
                <select value={form.audience} onChange={(event) => handleFormChange('audience', event.target.value)} className="input-base">
                  <option value="">Select audience</option>
                  <option value="mass-market">Mass market</option>
                  <option value="youth">Youth</option>
                  <option value="business">Business professionals</option>
                  <option value="retail">Retail shoppers</option>
                </select>
              </label>

              <label className="block">
                <span className="label-base">Campaign scope</span>
                <select value={form.scope} onChange={(event) => handleFormChange('scope', event.target.value)} className="input-base">
                  <option value="">Choose scope</option>
                  <option value="local">Local</option>
                  <option value="regional">Regional</option>
                  <option value="national">National</option>
                </select>
              </label>

              <label className="block">
                <span className="label-base">Primary geography</span>
                <select value={form.geography} onChange={(event) => handleFormChange('geography', event.target.value)} className="input-base">
                  <option value="">Select geography</option>
                  <option value="gauteng">Gauteng</option>
                  <option value="western-cape">Western Cape</option>
                  <option value="kwazulu-natal">KwaZulu-Natal</option>
                  <option value="national">National</option>
                </select>
              </label>
            </div>

            <div className="mt-5 space-y-3">
              <span className="label-base">Preferred channels</span>
              <div className="flex flex-wrap gap-3">
                {CHANNEL_OPTIONS.map((channel) => {
                  const checked = form.channels.includes(channel);
                  const isAllowed = allowedChannels.includes(channel);
                  return (
                    <label
                      key={channel}
                      className={`flex items-center gap-3 rounded-2xl border px-4 py-3 text-sm shadow-sm transition ${
                        !isAllowed
                          ? 'cursor-not-allowed border-slate-200 bg-slate-100 text-slate-400'
                          : checked
                            ? 'border-brand/25 bg-brand-soft/50 text-ink'
                            : 'border-slate-200 bg-white text-ink-soft'
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={checked}
                        disabled={!isAllowed}
                        onChange={() => toggleChannel(channel)}
                        className="size-4 rounded border-slate-300 accent-brand"
                      />
                      <span>{CHANNEL_LABELS[channel]}</span>
                      {!isAllowed ? <span className="text-[11px] font-semibold uppercase tracking-[0.14em]">Not in package</span> : null}
                    </label>
                  );
                })}
              </div>
              <p className="helper-text">Only channels included in the purchased package can be selected here.</p>
            </div>

            <div className="mt-5 grid gap-4 md:grid-cols-2">
              <label className="block">
                <span className="label-base">Brand / campaign name</span>
                <input value={form.brandName} onChange={(event) => handleFormChange('brandName', event.target.value)} className="input-base" placeholder="e.g. ABC Retail Winter Launch" />
              </label>

              <label className="block">
                <span className="label-base">Tone</span>
                <select value={form.tone} onChange={(event) => handleFormChange('tone', event.target.value)} className="input-base">
                  <option value="">Select tone</option>
                  <option value="premium">Premium</option>
                  <option value="balanced">Balanced</option>
                  <option value="high-visibility">High visibility</option>
                  <option value="performance">Performance focused</option>
                </select>
              </label>
            </div>

            <label className="mt-5 block">
              <span className="label-base">Campaign brief</span>
              <textarea
                value={form.brief}
                onChange={(event) => handleFormChange('brief', event.target.value)}
                rows={6}
                className="input-base min-h-[150px] resize-y"
                placeholder="Describe the campaign in plain language."
              />
              <p className="helper-text">
                The agent can enter a brief manually or paste a client request. AI can convert this into structured planning inputs before draft generation.
              </p>
              <div className="mt-3 flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  onClick={() => interpretMutation.mutate()}
                  disabled={!selectedCampaign || !form.brief.trim() || isWorking}
                  className="button-secondary inline-flex items-center gap-2 px-4 py-2 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <Sparkles className="size-4" />
                  {interpretMutation.isPending ? 'Interpreting brief...' : 'Interpret with AI'}
                </button>
                {aiInterpretationSummary ? (
                  <p className="text-sm text-ink-soft">{aiInterpretationSummary}</p>
                ) : null}
              </div>
            </label>
          </div>

          <div className="panel border-line/90 px-6 py-6 md:px-7">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
              <div>
                <h2 className="text-lg font-semibold text-ink">3. Generate AI draft</h2>
                <p className="mt-1 text-sm text-ink-soft">AI will create a recommendation draft inside package rules. Final inventory still comes from the planning workspace.</p>
              </div>
              <div className="flex flex-wrap gap-3">
                <button
                  type="button"
                  onClick={handleSaveDraft}
                  disabled={!canSaveDraft}
                  className="button-secondary px-4 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {pendingAction === 'draft' && initializeMutation.isPending ? 'Saving draft...' : 'Save as draft'}
                </button>
                <button
                  type="button"
                  onClick={handleGenerate}
                  disabled={!canGenerate}
                  className="inline-flex items-center gap-2 rounded-full bg-brand px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand-dark disabled:cursor-not-allowed disabled:bg-brand/55"
                >
                  <Sparkles className="size-4" />
                  {isGenerating ? 'Generating draft...' : 'Generate recommendation'}
                </button>
              </div>
            </div>
          </div>
        </div>

        <div className="space-y-6">
          <div className="panel border-line/90 bg-white px-6 py-6">
            <div className="mb-5 flex items-center gap-3">
              <div className="rounded-2xl bg-brand-soft p-2 text-brand">
                <Wand2 className="size-5" />
              </div>
              <div>
                <h3 className="font-semibold text-ink">AI draft preview</h3>
                <p className="text-sm text-ink-soft">What the system will infer before the workspace opens.</p>
              </div>
            </div>

            <div className="space-y-3">
              <div className="rounded-[22px] border border-line bg-slate-50 px-4 py-4">
                <p className="text-xs uppercase tracking-[0.18em] text-ink-soft">Interpreted inputs</p>
                <div className="mt-3 grid gap-3 text-sm md:grid-cols-2">
                  <div>
                    <p className="text-ink-soft">Objective</p>
                    <p className="font-medium text-ink">{form.objective || 'Not selected'}</p>
                  </div>
                  <div>
                    <p className="text-ink-soft">Audience</p>
                    <p className="font-medium text-ink">{form.audience || 'Not selected'}</p>
                  </div>
                  <div>
                    <p className="text-ink-soft">Scope</p>
                    <p className="font-medium text-ink">{form.scope || 'Not selected'}</p>
                  </div>
                  <div>
                    <p className="text-ink-soft">Geography</p>
                    <p className="font-medium text-ink">{form.geography || 'Not selected'}</p>
                  </div>
                  <div>
                    <p className="text-ink-soft">Tone</p>
                    <p className="font-medium text-ink">{form.tone || 'Not selected'}</p>
                  </div>
                  <div>
                    <p className="text-ink-soft">Channels</p>
                    <p className="font-medium text-ink">{form.channels.join(' + ') || 'None selected'}</p>
                  </div>
                </div>
              </div>

              <div className="rounded-[22px] border border-line bg-white px-4 py-4">
                <p className="text-xs uppercase tracking-[0.18em] text-ink-soft">Package rules applied</p>
                <ul className="mt-3 space-y-2 text-sm text-ink-soft">
                  <li>• Recommendation must stay within the paid package budget.</li>
                  <li>• Geography comes from campaign input, not package purchase.</li>
                  <li>• National radio is only available for Scale or Dominance when policy allows it.</li>
                  <li>• Final inventory selection is validated in the planning workspace.</li>
                </ul>
              </div>
            </div>
          </div>

          <div className="panel hero-glow border-white/10 px-6 py-6 text-white">
            <p className="text-xs uppercase tracking-[0.18em] text-white/60">Selected order</p>
            <h3 className="mt-2 text-2xl font-semibold">
              {selectedCampaign ? `${selectedCampaign.packageBandName} package` : 'No package selected'}
            </h3>
            <p className="mt-1 text-sm text-white/75">
              {selectedCampaign ? `${selectedCampaign.queueLabel} · ${formatCurrency(selectedCampaign.selectedBudget)} budget band` : 'Choose a campaign to continue'}
            </p>

            <div className="mt-5 space-y-3 text-sm text-white/75">
              <div className="flex items-center justify-between border-b border-white/10 pb-3">
                <span>Client</span>
                <span className="font-medium text-white">{selectedCampaign?.clientName ?? '-'}</span>
              </div>
              <div className="flex items-center justify-between border-b border-white/10 pb-3">
                <span>Order reference</span>
                <span className="font-medium text-white">{selectedCampaign?.id.slice(0, 8).toUpperCase() ?? '-'}</span>
              </div>
              <div className="flex items-center justify-between border-b border-white/10 pb-3">
                <span>AI review</span>
                <span className="font-medium text-white">Required before approval</span>
              </div>
              <div className="flex items-center justify-between">
                <span>Allowed channels</span>
                <span className="font-medium text-white">{allowedChannels.join(', ') || 'Select channels'}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

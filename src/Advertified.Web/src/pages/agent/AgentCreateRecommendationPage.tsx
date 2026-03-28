import { useEffect, useMemo, useState } from 'react';
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
}) : CampaignFormState {
  const defaultChannels: ChannelOption[] =
    campaign.packageBandName === 'Launch'
      ? ['Radio', 'OOH']
      : campaign.packageBandName === 'Scale'
        ? ['Radio', 'OOH']
        : ['Radio', 'OOH', 'TV'];

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
  const requestedCampaignId = searchParams.get('campaignId') ?? '';
  const [selectedCampaignId, setSelectedCampaignId] = useState<string>('');
  const [selectedClientId, setSelectedClientId] = useState<string>('');
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
    item.queueStage === 'planning_ready'
    || item.queueStage === 'newly_paid'
    || item.queueStage === 'brief_waiting'
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

  const filteredCampaigns = useMemo(() => (
    selectedClientId
      ? availableCampaigns.filter((item) => item.userId === selectedClientId)
      : availableCampaigns
  ), [availableCampaigns, selectedClientId]);

  const selectedCampaign = filteredCampaigns.find((item) => item.id === selectedCampaignId)
    ?? availableCampaigns.find((item) => item.id === selectedCampaignId)
    ?? filteredCampaigns[0]
    ?? availableCampaigns[0]
    ?? null;

  useEffect(() => {
    if (!selectedCampaign) {
      return;
    }

    setSelectedClientId((current) => current || selectedCampaign.userId);
    setSelectedCampaignId((current) => current || selectedCampaign.id);
  }, [selectedCampaign]);

  useEffect(() => {
    if (!requestedCampaignId || availableCampaigns.length === 0) {
      return;
    }

    const requestedCampaign = availableCampaigns.find((item) => item.id === requestedCampaignId);
    if (!requestedCampaign) {
      return;
    }

    setSelectedClientId(requestedCampaign.userId);
    setSelectedCampaignId(requestedCampaign.id);
  }, [availableCampaigns, requestedCampaignId]);

  useEffect(() => {
    if (!selectedCampaign) {
      return;
    }

    setForm(inferInitialForm(selectedCampaign));
  }, [selectedCampaign?.id]);

  if (inboxQuery.isLoading) {
    return <LoadingState label="Loading recommendation flow..." />;
  }

  const handleFormChange = <K extends keyof CampaignFormState>(key: K, value: CampaignFormState[K]) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const toggleChannel = (channel: ChannelOption) => {
    setForm((current) => ({
      ...current,
      channels: current.channels.includes(channel)
        ? current.channels.filter((item) => item !== channel)
        : [...current.channels, channel],
    }));
  };

  const handleClientChange = (userId: string) => {
    setSelectedClientId(userId);
    const nextCampaign = availableCampaigns.find((item) => item.userId === userId);
    setSelectedCampaignId(nextCampaign?.id ?? '');
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
      preferredMediaTypes: form.channels.map((channel) => channel.toLowerCase()),
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
    onSuccess: async (campaign, variables) => {
      await queryClient.invalidateQueries({ queryKey: ['agent-inbox'] });
      await queryClient.invalidateQueries({ queryKey: ['agent-campaign', campaign.id] });
      pushToast({
        title: variables.submitBrief ? 'Recommendation generated.' : 'Draft saved.',
        description: variables.submitBrief
          ? 'The AI draft is ready in the planning workspace for agent review.'
          : 'The campaign brief has been saved for the agent workflow.',
      });

      if (variables.submitBrief) {
        navigate(`/agent/campaigns/${campaign.id}`);
      }
    },
    onError: (error) => {
      pushToast({
        title: 'We could not create the recommendation flow.',
        description: error instanceof Error ? error.message : 'Please try again in a moment.',
      }, 'error');
    },
  });

  const handleSaveDraft = async () => {
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

    await initializeMutation.mutateAsync({ submitBrief: true });
  };

  return (
    <section className="min-h-screen bg-surface text-ink">
      {initializeMutation.isPending ? <ProcessingOverlay label="Creating recommendation flow..." /> : null}
      <div className="border-b border-line bg-white/90 backdrop-blur">
        <div className="page-shell flex flex-col gap-4 py-4 lg:flex-row lg:items-center lg:justify-between">
          <button type="button" onClick={() => navigate('/agent')} className="button-secondary inline-flex items-center gap-2 px-4 py-2">
            <ArrowLeft className="size-4" />
            Back to dashboard
          </button>

          <div className="flex flex-wrap items-center gap-2 sm:gap-3">
            {STEP_CONFIG.map((step, index) => {
              const toneClass = step.id === 2
                ? 'bg-brand text-white'
                : step.id < 2
                  ? 'bg-highlight-soft text-highlight'
                  : 'bg-slate-100 text-slate-500';

              return (
                <div key={step.id} className="flex items-center gap-2">
                  <div className={`flex h-8 w-8 items-center justify-center rounded-full text-sm font-semibold ${toneClass}`}>
                    {step.id}
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
                <select value={selectedCampaign?.id ?? ''} onChange={(event) => setSelectedCampaignId(event.target.value)} className="input-base">
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
                  return (
                    <label
                      key={channel}
                      className={`flex items-center gap-3 rounded-2xl border px-4 py-3 text-sm shadow-sm transition ${checked ? 'border-brand/25 bg-brand-soft/50 text-ink' : 'border-slate-200 bg-white text-ink-soft'}`}
                    >
                      <input type="checkbox" checked={checked} onChange={() => toggleChannel(channel)} className="size-4 rounded border-slate-300 accent-brand" />
                      {channel}
                    </label>
                  );
                })}
              </div>
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
                The agent can enter a brief manually or paste a client request. AI will convert this into structured planning inputs.
              </p>
            </label>
          </div>

          <div className="panel border-line/90 px-6 py-6 md:px-7">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
              <div>
                <h2 className="text-lg font-semibold text-ink">3. Generate AI draft</h2>
                <p className="mt-1 text-sm text-ink-soft">AI will create a recommendation draft inside package rules. Final inventory still comes from the planning workspace.</p>
              </div>
              <div className="flex flex-wrap gap-3">
                <button type="button" onClick={handleSaveDraft} className="button-secondary px-4 py-3">
                  Save as draft
                </button>
                <button type="button" onClick={handleGenerate} className="inline-flex items-center gap-2 rounded-full bg-brand px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand-dark">
                  <Sparkles className="size-4" />
                  Generate recommendation
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
                <span className="font-medium text-white">{form.channels.join(', ') || 'Select channels'}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

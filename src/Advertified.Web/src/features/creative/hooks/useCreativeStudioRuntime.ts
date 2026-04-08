import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useToast } from '../../../components/ui/toast';
import { queryKeys } from '../../../lib/queryKeys';
import { advertifiedApi } from '../../../services/advertifiedApi';
import { buildDefaultCreativePrompt, parseDelimitedInput } from '../creativeStudioUtils';

type CampaignLike = Awaited<ReturnType<typeof advertifiedApi.getCampaign>>;
type BriefLike = CampaignLike['brief'];

interface UseCreativeStudioRuntimeOptions {
  campaign: CampaignLike;
  brief: BriefLike;
  channelMood: string[];
  isPreview: boolean;
}

export function useCreativeStudioRuntime({
  campaign,
  brief,
  channelMood,
  isPreview,
}: UseCreativeStudioRuntimeOptions) {
  const { pushToast } = useToast();
  const queryClient = useQueryClient();

  const [prompt, setPrompt] = useState(() => buildDefaultCreativePrompt({
    campaignName: campaign.campaignName,
    businessName: campaign.businessName ?? campaign.clientName,
    packageBandName: campaign.packageBandName,
    briefObjective: brief?.objective,
    audience: brief?.targetAudienceNotes,
    creativeNotes: brief?.creativeNotes,
    channelMood,
  }));
  const [brandInput, setBrandInput] = useState(campaign.businessName ?? campaign.clientName ?? '');
  const [productInput, setProductInput] = useState(campaign.campaignName ?? '');
  const [audienceInput, setAudienceInput] = useState(brief?.targetAudienceNotes ?? '');
  const [objectiveInput, setObjectiveInput] = useState(brief?.objective ?? '');
  const [toneInput, setToneInput] = useState(brief?.creativeNotes ?? '');
  const [channelsInput, setChannelsInput] = useState(channelMood.join(', '));
  const [ctaInput, setCtaInput] = useState('');
  const [constraintsInput, setConstraintsInput] = useState(brief?.specialRequirements ?? '');
  const [activeAiJobId, setActiveAiJobId] = useState('');
  const [regenCreativeId, setRegenCreativeId] = useState('');
  const [regenFeedback, setRegenFeedback] = useState('');
  const [scopedCreativeState, setScopedCreativeState] = useState<{
    key: string;
    creativeSystem: Awaited<ReturnType<typeof advertifiedApi.generateCreativeSystem>> | null;
    lastIterationLabel: string | null;
  } | null>(null);

  const creativeStateKey = campaign.latestCreativeSystem?.id ?? `campaign:${campaign.id}:none`;
  const creativeSystem = scopedCreativeState?.key === creativeStateKey
    ? scopedCreativeState.creativeSystem
    : (campaign.latestCreativeSystem?.output ?? null);
  const lastIterationLabel = scopedCreativeState?.key === creativeStateKey
    ? scopedCreativeState.lastIterationLabel
    : (campaign.latestCreativeSystem?.iterationLabel ?? null);

  const creativeSystemMutation = useMutation({
    mutationFn: async (variables: { iterationLabel?: string; iterationInstruction?: string } = {}) => {
      const { iterationInstruction } = variables;
      const normalizedPrompt = iterationInstruction
        ? `${prompt.trim()}\n\nIteration direction: ${iterationInstruction}`
        : prompt.trim();

      return advertifiedApi.generateCreativeSystem(campaign.id, {
        prompt: normalizedPrompt,
        iterationLabel: variables.iterationLabel,
        brand: brandInput.trim() || undefined,
        product: productInput.trim() || undefined,
        audience: audienceInput.trim() || undefined,
        objective: objectiveInput.trim() || undefined,
        tone: toneInput.trim() || undefined,
        channels: parseDelimitedInput(channelsInput),
        cta: ctaInput.trim() || undefined,
        constraints: parseDelimitedInput(constraintsInput),
      });
    },
    onSuccess: (result, variables) => {
      setScopedCreativeState({
        key: creativeStateKey,
        creativeSystem: result,
        lastIterationLabel: variables.iterationLabel ?? null,
      });
      queryClient.invalidateQueries({ queryKey: queryKeys.creative.campaign(campaign.id) }).catch(() => {});
      pushToast({
        title: variables.iterationLabel ? `Creative system updated: ${variables.iterationLabel}.` : 'Creative system generated.',
        description: 'The studio output is ready for review and handoff.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Creative system could not be generated.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const campaignCreativesQuery = useQuery({
    queryKey: ['ai-platform-campaign-creatives', campaign.id],
    queryFn: () => advertifiedApi.getAiPlatformCampaignCreatives(campaign.id),
    enabled: !isPreview,
  });

  const adMetricsSummaryQuery = useQuery({
    queryKey: ['ai-platform-campaign-metrics-summary', campaign.id],
    queryFn: () => advertifiedApi.getAiCampaignAdMetricsSummary(campaign.id),
    enabled: !isPreview,
  });

  const adVariantsQuery = useQuery({
    queryKey: ['ai-platform-ad-variants', campaign.id],
    queryFn: () => advertifiedApi.getAiAdVariants(campaign.id),
    enabled: !isPreview,
  });

  const submitAiJobMutation = useMutation({
    mutationFn: async () => advertifiedApi.submitAiPlatformJob({
      campaignId: campaign.id,
      promptOverride: prompt.trim() || undefined,
    }),
    onSuccess: (response) => {
      setActiveAiJobId(response.jobId);
      pushToast({
        title: 'AI platform job queued.',
        description: `Job ${response.jobId.slice(0, 8)} is running in the queue.`,
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not queue AI platform job.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const aiJobStatusQuery = useQuery({
    queryKey: ['ai-platform-job-status', activeAiJobId],
    queryFn: () => advertifiedApi.getAiPlatformJobStatus(activeAiJobId),
    enabled: activeAiJobId.trim().length > 0,
    refetchInterval: (query) => {
      const status = query.state.data?.status?.toLowerCase();
      return status === 'completed' || status === 'failed' ? false : 3000;
    },
  });

  const syncMetricsMutation = useMutation({
    mutationFn: () => advertifiedApi.syncAiCampaignAdMetrics(campaign.id),
    onSuccess: () => {
      adMetricsSummaryQuery.refetch().catch(() => {});
      adVariantsQuery.refetch().catch(() => {});
      pushToast({
        title: 'Metrics synced.',
        description: 'Campaign ad metrics are now updated.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not sync metrics.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const regenerateAiMutation = useMutation({
    mutationFn: async () => advertifiedApi.regenerateAiPlatformCreative({
      creativeId: regenCreativeId.trim(),
      campaignId: campaign.id,
      feedback: regenFeedback.trim(),
    }),
    onSuccess: (response) => {
      campaignCreativesQuery.refetch().catch(() => {});
      pushToast({
        title: 'AI creative regenerated.',
        description: `Created ${response.creativeCount} creatives and ${response.assetCount} assets.`,
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Regeneration failed.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  return {
    prompt,
    setPrompt,
    brandInput,
    setBrandInput,
    productInput,
    setProductInput,
    audienceInput,
    setAudienceInput,
    objectiveInput,
    setObjectiveInput,
    toneInput,
    setToneInput,
    channelsInput,
    setChannelsInput,
    ctaInput,
    setCtaInput,
    constraintsInput,
    setConstraintsInput,
    activeAiJobId,
    setActiveAiJobId,
    regenCreativeId,
    setRegenCreativeId,
    regenFeedback,
    setRegenFeedback,
    creativeStateKey,
    creativeSystem,
    lastIterationLabel,
    setScopedCreativeState,
    creativeSystemMutation,
    campaignCreativesQuery,
    adMetricsSummaryQuery,
    adVariantsQuery,
    submitAiJobMutation,
    aiJobStatusQuery,
    syncMetricsMutation,
    regenerateAiMutation,
  };
}

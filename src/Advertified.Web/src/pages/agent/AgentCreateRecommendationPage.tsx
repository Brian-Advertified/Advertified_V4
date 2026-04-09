import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ChevronRight, Sparkles, Wand2 } from 'lucide-react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
import {
  buildBriefQuestionnaireSummary,
  buildRecommendationDraftBrief,
  formatAgeRange,
  inferRecommendationAudienceFromBrief,
  inferRecommendationGeographyFromBrief,
  inferRecommendationToneFromBrief,
  type RecommendationDraftFormState,
} from '../../features/campaigns/briefModel';
import { catalogQueryOptions } from '../../lib/catalogQueryOptions';
import { useSharedFormOptions } from '../../lib/useSharedFormOptions';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { formatChannelLabel, normalizeChannelKey } from '../../features/channels/channelUtils';
import {
  AGE_RANGE_OPTIONS,
  AUDIENCE_OPTIONS,
  CHANNEL_OPTIONS,
  ensureRequiredChannels,
  GEOGRAPHY_OPTIONS,
  GENDER_OPTIONS,
  LANGUAGE_OPTIONS,
  mergeUniqueChannels,
  normalizeChannelOption,
  normalizeOption,
  OBJECTIVE_OPTIONS,
  SCOPE_OPTIONS,
  TONE_OPTIONS,
  type ChannelOption,
} from './recommendationDraftOptions';
import {
  AgentDetailGrid,
  AgentDetailStack,
  AgentInsightCard,
} from './agentSectionShared';
import type { AutoBriefConfidence, AutoBriefField, AutoBriefPayload } from '../../features/leads/leadAutoBrief';
import type { AgentInboxItem, Campaign, LeadIndustryPolicy, PackageBand } from '../../types/domain';
import { pushAgentMutationError } from './agentMutationToast';

const STEP_CONFIG = [
  { id: 1, label: 'Choose campaign' },
  { id: 2, label: 'Add details' },
  { id: 3, label: 'Create draft' },
] as const;

function isProspectiveCampaign(campaign?: Pick<Campaign, 'paymentStatus' | 'status'> | Pick<AgentInboxItem, 'paymentStatus' | 'status'> | null): boolean {
  if (!campaign) {
    return false;
  }

  return campaign.paymentStatus !== 'paid' || campaign.status === 'awaiting_purchase';
}

function resolvePackageReferenceBudget(packageBand: PackageBand): number {
  const recommendedSpend = packageBand.recommendedSpend;

  if (
    typeof recommendedSpend === 'number'
    && recommendedSpend >= packageBand.minBudget
    && recommendedSpend <= packageBand.maxBudget
  ) {
    return recommendedSpend;
  }

  return Math.round((packageBand.minBudget + packageBand.maxBudget) / 2);
}

function formatPackageRange(packageBand: PackageBand): string {
  return `${formatCurrency(packageBand.minBudget)} to ${formatCurrency(packageBand.maxBudget)}`;
}

function selectIndefiniteArticle(value: string): 'a' | 'an' {
  const normalized = value.trim().toLowerCase();
  return /^[aeiou]/.test(normalized) ? 'an' : 'a';
}

function normalizeNextAction(nextAction: string, hasPackageRange: boolean): string {
  if (!hasPackageRange) {
    return nextAction;
  }

  return nextAction.replace('within the paid budget', 'within the selected package band');
}

function getAllowedChannels(campaign?: {
  includeRadio: 'yes' | 'optional' | 'no';
  includeTv: 'yes' | 'optional' | 'no';
} | null): ChannelOption[] {
  const allowed: ChannelOption[] = ['OOH', 'Digital'];
  if (campaign?.includeRadio !== 'no') {
    allowed.unshift('Radio');
  }

  if (campaign?.includeTv !== 'no') {
    allowed.push('TV');
  }

  return allowed;
}

function applyIndustryPolicyToForm(
  form: CampaignFormState,
  industryPolicy: LeadIndustryPolicy | undefined,
  allowedChannels: ChannelOption[],
): CampaignFormState {
  if (!industryPolicy) {
    return form;
  }

  const policyChannels = (industryPolicy.preferredChannels ?? [])
    .map(normalizeChannelOption)
    .filter((channel): channel is ChannelOption => channel !== undefined && allowedChannels.includes(channel));
  const mergedChannels = ensureRequiredChannels(mergeUniqueChannels(form.channels, policyChannels));

  const objective = form.objective || normalizeOption(industryPolicy.objectiveOverride, OBJECTIVE_OPTIONS);
  const tone = form.tone || normalizeOption(industryPolicy.preferredTone, TONE_OPTIONS);

  const industryNotes = [
    `Industry profile: ${industryPolicy.name}`,
    `Industry messaging angle:\n${industryPolicy.messagingAngle}`,
    (industryPolicy.guardrails ?? []).length > 0
      ? `Industry guardrails:\n${industryPolicy.guardrails
        .filter((item) => item.trim().length > 0)
        .map((item) => `- ${item.trim()}`)
        .join('\n')}`
      : '',
    industryPolicy.cta?.trim()
      ? `Industry recommended CTA:\n${industryPolicy.cta.trim()}`
      : '',
  ].filter((value) => value.trim().length > 0);

  const hasIndustryBlock = form.brief.includes('Industry profile:');
  const brief = hasIndustryBlock
    ? form.brief
    : [industryNotes.join('\n\n'), form.brief].filter((value) => value.trim().length > 0).join('\n\n');

  return {
    ...form,
    objective,
    tone,
    channels: mergedChannels,
    brief,
  };
}

type CampaignFormState = RecommendationDraftFormState;

type ProspectFormState = {
  fullName: string;
  email: string;
  phone: string;
  packageBandId: string;
  campaignName: string;
};

type CampaignConversionPrefillState = {
  convertToCampaign?: {
    leadId?: number;
    businessName?: string;
    location?: string;
    archetypeName?: string;
    suggestedCampaignType?: string;
    detectedGaps?: string[];
    insightSummary?: string;
    expectedOutcome?: string;
    recommendedChannels?: string[];
    strategyChannels?: Array<{
      channel: string;
      budgetSharePercent: number;
      reason?: string;
    }>;
    industryProfileName?: string;
    industryMessagingAngle?: string;
    industryCta?: string;
    industryGuardrails?: string[];
    whoWeAre?: string;
    researchBasis?: string[];
    lastResearchedAtUtc?: string;
    socialQualityNote?: string;
    flexibleRollout?: string;
    whyActNow?: string;
    nextStep?: string;
    autoGenerateDraft?: boolean;
    autoBrief?: AutoBriefPayload;
  };
};

type StrategySharePayload = {
  targetRadioShare: number;
  targetOohShare: number;
  targetTvShare: number;
  targetDigitalShare: number;
};

function buildStrategySharePayload(
  strategyChannels?: Array<{ channel: string; budgetSharePercent: number }>,
): StrategySharePayload | undefined {
  if (!strategyChannels || strategyChannels.length === 0) {
    return undefined;
  }

  const targets = {
    targetRadioShare: 0,
    targetOohShare: 0,
    targetTvShare: 0,
    targetDigitalShare: 0,
  };

  for (const item of strategyChannels) {
    if (!Number.isFinite(item.budgetSharePercent) || item.budgetSharePercent <= 0) {
      continue;
    }

    const normalizedChannel = normalizeChannelKey(item.channel);
    if (normalizedChannel === 'RADIO') {
      targets.targetRadioShare += item.budgetSharePercent;
      continue;
    }

    if (normalizedChannel === 'OOH') {
      targets.targetOohShare += item.budgetSharePercent;
      continue;
    }

    if (normalizedChannel === 'TV') {
      targets.targetTvShare += item.budgetSharePercent;
      continue;
    }

    if (normalizedChannel === 'DIGITAL') {
      targets.targetDigitalShare += item.budgetSharePercent;
    }
  }

  const total = targets.targetRadioShare + targets.targetOohShare + targets.targetTvShare + targets.targetDigitalShare;
  if (total <= 0) {
    return undefined;
  }

  const normalizedTargets: StrategySharePayload = {
    targetRadioShare: Math.round((targets.targetRadioShare * 100) / total),
    targetOohShare: Math.round((targets.targetOohShare * 100) / total),
    targetTvShare: Math.round((targets.targetTvShare * 100) / total),
    targetDigitalShare: Math.round((targets.targetDigitalShare * 100) / total),
  };

  const delta = 100 - (
    normalizedTargets.targetRadioShare
    + normalizedTargets.targetOohShare
    + normalizedTargets.targetTvShare
    + normalizedTargets.targetDigitalShare
  );

  if (delta !== 0) {
    const rankedKeys: Array<[keyof StrategySharePayload, number]> = [
      ['targetRadioShare', normalizedTargets.targetRadioShare],
      ['targetOohShare', normalizedTargets.targetOohShare],
      ['targetTvShare', normalizedTargets.targetTvShare],
      ['targetDigitalShare', normalizedTargets.targetDigitalShare],
    ];
    rankedKeys.sort((a, b) => b[1] - a[1]);
    const topKey = rankedKeys[0]?.[0];
    if (topKey) {
      normalizedTargets[topKey] += delta;
    }
  }

  return normalizedTargets;
}

function normalizeConfidence(value?: string): AutoBriefConfidence {
  if (value === 'detected' || value === 'strongly_inferred' || value === 'weakly_inferred' || value === 'no_evidence') {
    return value;
  }

  return 'no_evidence';
}

function confidencePillTone(confidence: AutoBriefConfidence): string {
  switch (confidence) {
    case 'detected':
      return 'border-emerald-200 bg-emerald-50 text-emerald-700';
    case 'strongly_inferred':
      return 'border-sky-200 bg-sky-50 text-sky-700';
    case 'weakly_inferred':
      return 'border-amber-200 bg-amber-50 text-amber-700';
    default:
      return 'border-slate-200 bg-slate-50 text-slate-600';
  }
}

function confidenceLabel(confidence: AutoBriefConfidence): string {
  return confidence.replaceAll('_', ' ');
}

function hasOpportunityDraftRequirements(form: CampaignFormState): boolean {
  return Boolean(
    form.objective
    && form.audience
    && form.scope
    && (form.scope === 'national' || form.geography)
    && form.brief.trim()
    && form.channels.length > 0,
  );
}

function hasDetailedDraftRequirements(form: CampaignFormState): boolean {
  return hasOpportunityDraftRequirements(form)
    && Boolean(
      form.salesModel
      && form.customerType
      && form.buyingBehaviour
      && form.decisionCycle
      && form.pricePositioning
      && form.growthTarget
      && form.urgencyLevel
      && form.audienceClarity,
    );
}

function listOpportunityDraftMissingFields(form: CampaignFormState): string[] {
  const missing: string[] = [];

  if (!form.objective) {
    missing.push('campaign objective');
  }

  if (!form.audience) {
    missing.push('audience direction');
  }

  if (!form.scope) {
    missing.push('coverage scope');
  }

  if (form.scope !== 'national' && !form.geography) {
    missing.push('target geography');
  }

  if (!form.brief.trim()) {
    missing.push('campaign brief');
  }

  if (form.channels.length === 0) {
    missing.push('channel selection');
  }

  return missing;
}

type ScopedFormState = {
  key: string;
  value: CampaignFormState;
};

function inferInitialForm(campaign: {
  clientName: string;
  packageBandName: string;
  selectedBudget: number;
  packageRangeLabel?: string;
  isProspective?: boolean;
  queueStage: string;
  nextAction: string;
  includeRadio: 'yes' | 'optional' | 'no';
  includeTv: 'yes' | 'optional' | 'no';
}) : CampaignFormState {
  const defaultChannels = getAllowedChannels(campaign);
  const hasPackageRange = Boolean(campaign.packageRangeLabel);
  const nextAction = normalizeNextAction(campaign.nextAction, hasPackageRange);

  return {
    objective: campaign.queueStage === 'newly_paid' ? 'launch' : 'awareness',
    audience: 'retail',
    scope: campaign.selectedBudget >= 500000 ? 'national' : 'provincial',
    geography: campaign.selectedBudget >= 500000 ? '' : 'gauteng',
    ageRange: '',
    language: '',
    targetGender: '',
    targetInterests: '',
    salesModel: '',
    customerType: '',
    buyingBehaviour: '',
    decisionCycle: '',
    pricePositioning: '',
    growthTarget: '',
    urgencyLevel: '',
    audienceClarity: '',
    valuePropositionFocus: '',
    brandName: `${campaign.clientName} ${campaign.packageBandName} Campaign`,
    tone: campaign.packageBandName === 'Dominance' ? 'premium' : 'high-visibility',
    brief: hasPackageRange
      ? `Build a ${campaign.packageBandName.toLowerCase()} recommendation for ${campaign.clientName} within the ${campaign.packageRangeLabel} package range. ${nextAction}`
      : `Build a ${campaign.packageBandName.toLowerCase()} recommendation for ${campaign.clientName} within ${formatCurrency(campaign.selectedBudget)}. ${nextAction}`,
    channels: ensureRequiredChannels(defaultChannels),
  };
}

function inferFormFromCampaign(
  campaign: {
    clientName: string;
    packageBandName: string;
    selectedBudget: number;
    packageRangeLabel?: string;
    isProspective?: boolean;
    queueStage: string;
    nextAction: string;
    includeRadio: 'yes' | 'optional' | 'no';
    includeTv: 'yes' | 'optional' | 'no';
  },
  detailedCampaign?: Campaign | null,
): CampaignFormState {
  const fallback = inferInitialForm(campaign);
  const brief = detailedCampaign?.brief;
  if (!brief) {
    return fallback;
  }

  const briefChannels = (brief.preferredMediaTypes ?? [])
    .map(normalizeChannelOption)
    .filter((channel): channel is ChannelOption => Boolean(channel));

  return {
    objective: normalizeOption(brief.objective, OBJECTIVE_OPTIONS) || fallback.objective,
    audience: inferRecommendationAudienceFromBrief(brief),
    scope: normalizeOption(brief.geographyScope, SCOPE_OPTIONS) || fallback.scope,
    geography: inferRecommendationGeographyFromBrief(brief) || fallback.geography,
    ageRange: formatAgeRange(brief.targetAgeMin, brief.targetAgeMax),
    language: brief.targetLanguages?.[0] ?? fallback.language,
    targetGender: normalizeOption(brief.targetGender, GENDER_OPTIONS) || fallback.targetGender,
    targetInterests: (brief.targetInterests ?? []).join(', '),
    salesModel: brief.salesModel ?? fallback.salesModel,
    customerType: brief.customerType ?? fallback.customerType,
    buyingBehaviour: brief.buyingBehaviour ?? fallback.buyingBehaviour,
    decisionCycle: brief.decisionCycle ?? fallback.decisionCycle,
    pricePositioning: brief.pricePositioning ?? fallback.pricePositioning,
    growthTarget: brief.growthTarget ?? fallback.growthTarget,
    urgencyLevel: brief.urgencyLevel ?? fallback.urgencyLevel,
    audienceClarity: brief.audienceClarity ?? fallback.audienceClarity,
    valuePropositionFocus: brief.valuePropositionFocus ?? fallback.valuePropositionFocus,
    brandName: detailedCampaign?.campaignName || fallback.brandName,
    tone: inferRecommendationToneFromBrief(brief, campaign.packageBandName),
    brief: brief.specialRequirements?.trim()
      || brief.targetAudienceNotes?.trim()
      || fallback.brief,
    channels: ensureRequiredChannels(briefChannels.length > 0 ? briefChannels : fallback.channels),
  };
}

async function waitForGeneratedRecommendation(campaignId: string, attempts = 6, delayMs = 400): Promise<Campaign> {
  for (let attempt = 0; attempt < attempts; attempt += 1) {
    const campaign = await advertifiedApi.getAgentCampaign(campaignId);
    if (campaign.recommendations.length > 0 || campaign.recommendation) {
      return campaign;
    }

    if (attempt < attempts - 1) {
      await new Promise((resolve) => window.setTimeout(resolve, delayMs));
    }
  }

  return advertifiedApi.getAgentCampaign(campaignId);
}

export function AgentCreateRecommendationPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const inboxQuery = useQuery({ queryKey: ['agent-inbox'], queryFn: advertifiedApi.getAgentInbox });
  const packagesQuery = useQuery({ queryKey: ['packages'], queryFn: advertifiedApi.getPackages, ...catalogQueryOptions });
  const formOptionsQuery = useSharedFormOptions();
  const requestedCampaignId = searchParams.get('campaignId') ?? '';
  const [selectedCampaignIdState, setSelectedCampaignIdState] = useState<string>('');
  const [selectedClientIdState, setSelectedClientIdState] = useState<string>('');
  const [pendingAction, setPendingAction] = useState<'draft' | 'generate' | null>(null);
  const autoGenerateKeyRef = useRef<string | null>(null);
  const [scopedAiInterpretationSummary, setScopedAiInterpretationSummary] = useState<{ key: string; summary: string } | null>(null);
  const [scopedForm, setScopedForm] = useState<ScopedFormState | null>(null);
  const [showDetailEditing, setShowDetailEditing] = useState(false);
  const [selectedProspectPackageBandState, setSelectedProspectPackageBandState] = useState<{ campaignId: string; packageBandId: string } | null>(null);
  const emptyForm: CampaignFormState = {
    objective: '',
    audience: '',
    scope: '',
    geography: '',
    ageRange: '',
    language: '',
    targetGender: '',
    targetInterests: '',
    salesModel: '',
    customerType: '',
    buyingBehaviour: '',
    decisionCycle: '',
    pricePositioning: '',
    growthTarget: '',
    urgencyLevel: '',
    audienceClarity: '',
    valuePropositionFocus: '',
    brandName: '',
    tone: '',
    brief: '',
    channels: ['OOH', 'Radio'],
  };
  const [showProspectForm, setShowProspectForm] = useState(false);
  const [prospectForm, setProspectForm] = useState<ProspectFormState>({
    fullName: '',
    email: '',
    phone: '',
    packageBandId: '',
    campaignName: '',
  });

  const availableCampaigns = useMemo(() => (inboxQuery.data?.items ?? []).filter((item) => (
    item.queueStage !== 'waiting_on_client'
    && item.queueStage !== 'completed'
  )), [inboxQuery.data]);

  const clientOptions = useMemo(() => {
    const uniqueClients = new Map<string, { id: string; name: string; email: string }>();
    for (const item of availableCampaigns) {
      const ownerKey = item.userId ?? item.clientEmail ?? item.id;
      if (!uniqueClients.has(ownerKey)) {
        uniqueClients.set(ownerKey, { id: ownerKey, name: item.clientName, email: item.clientEmail });
      }
    }

    return Array.from(uniqueClients.values());
  }, [availableCampaigns]);
  const requestedCampaign = requestedCampaignId
    ? availableCampaigns.find((item) => item.id === requestedCampaignId) ?? null
    : null;
  const selectedClientId = (requestedCampaign?.userId ?? requestedCampaign?.clientEmail ?? requestedCampaign?.id)
    ?? selectedClientIdState
    ?? '';

  const filteredCampaigns = useMemo(() => (
    selectedClientId
      ? availableCampaigns.filter((item) => (item.userId ?? item.clientEmail ?? item.id) === selectedClientId)
      : availableCampaigns
  ), [availableCampaigns, selectedClientId]);
  const selectedCampaignId = requestedCampaign?.id
    ?? selectedCampaignIdState
    ?? '';

  const selectedCampaign = filteredCampaigns.find((item) => item.id === selectedCampaignId)
    ?? availableCampaigns.find((item) => item.id === selectedCampaignId)
    ?? null;
  const selectedCampaignDetailsQuery = useQuery({
    queryKey: ['agent-campaign', selectedCampaign?.id],
    queryFn: () => advertifiedApi.getAgentCampaign(selectedCampaign!.id),
    enabled: Boolean(selectedCampaign?.id),
  });
  const selectedCampaignDetails = selectedCampaignDetailsQuery.data;
  const industryPolicyQuery = useQuery({
    queryKey: ['lead-industry-policy', selectedCampaignDetails?.industry ?? ''],
    queryFn: () => advertifiedApi.resolveLeadIndustryPolicy(selectedCampaignDetails?.industry ?? ''),
    enabled: Boolean(selectedCampaignDetails?.industry?.trim()),
  });
  const selectedIndustryPolicy = industryPolicyQuery.data;
  const selectedCampaignBrief = selectedCampaignDetails?.brief;
  const selectedCampaignIsProspective = isProspectiveCampaign(selectedCampaignDetails ?? selectedCampaign);
  const selectedProspectPackageBandId = selectedCampaignIsProspective
    ? ((selectedProspectPackageBandState?.campaignId === selectedCampaign?.id
      ? selectedProspectPackageBandState?.packageBandId
      : selectedCampaign?.packageBandId) || '')
    : (selectedCampaign?.packageBandId || '');
  const selectedPackageBand = useMemo(
    () => packagesQuery.data?.find((item) => item.id === selectedProspectPackageBandId) ?? null,
    [packagesQuery.data, selectedProspectPackageBandId],
  );
  const selectedCampaignReferenceBudget = selectedCampaignIsProspective && selectedPackageBand
    ? resolvePackageReferenceBudget(selectedPackageBand)
    : (selectedCampaign?.selectedBudget ?? 0);
  const packageBandsById = useMemo(
    () => new Map((packagesQuery.data ?? []).map((item) => [item.id, item])),
    [packagesQuery.data],
  );
  const allowedChannels = useMemo(
    () => getAllowedChannels(selectedPackageBand),
    [selectedPackageBand],
  );
  const selectedCampaignHydrationKey = selectedCampaign
    ? `${selectedCampaign.id}:${selectedProspectPackageBandId}:${selectedPackageBand?.includeRadio ?? 'optional'}:${selectedPackageBand?.includeTv ?? 'no'}`
    : null;
  const activeFormKey = selectedCampaignHydrationKey ?? '__no-campaign__';
  const routePrefill = (location.state as CampaignConversionPrefillState | null)?.convertToCampaign;
  const strategyShareTargets = useMemo(
    () => buildStrategySharePayload(routePrefill?.strategyChannels),
    [routePrefill?.strategyChannels],
  );
  const inferredForm = selectedCampaign
    ? inferFormFromCampaign({
      ...selectedCampaign,
      selectedBudget: selectedCampaignReferenceBudget,
      packageRangeLabel: selectedPackageBand ? formatPackageRange(selectedPackageBand) : undefined,
      isProspective: selectedCampaignIsProspective,
      includeRadio: selectedPackageBand?.includeRadio ?? 'optional',
      includeTv: selectedPackageBand?.includeTv ?? 'no',
    }, selectedCampaignDetailsQuery.data)
    : emptyForm;
  const prefilledForm = useMemo(() => {
    if (!routePrefill) {
      return applyIndustryPolicyToForm(inferredForm, selectedIndustryPolicy, allowedChannels);
    }

    const autoBriefFields = routePrefill.autoBrief?.fields;
    const autoBriefChannels = routePrefill.autoBrief?.channels?.values ?? [];
    const normalizedObjective = normalizeOption(routePrefill.suggestedCampaignType, OBJECTIVE_OPTIONS);
    const normalizedLocation = routePrefill.location?.trim() ?? '';
    const recommendedChannels = (routePrefill.recommendedChannels ?? [])
      .map(normalizeChannelOption)
      .filter((channel): channel is ChannelOption => channel !== undefined && allowedChannels.includes(channel));
    const inferredChannels = autoBriefChannels
      .map(normalizeChannelOption)
      .filter((channel): channel is ChannelOption => channel !== undefined && allowedChannels.includes(channel));
    const detectedGapBlock = (routePrefill.detectedGaps ?? [])
      .filter((item) => item.trim().length > 0)
      .map((item) => `- ${item.trim()}`)
      .join('\n');
    const researchBasisBlock = (routePrefill.researchBasis ?? [])
      .filter((item) => item.trim().length > 0)
      .map((item) => `- ${item.trim()}`)
      .join('\n');
    const objectiveLabel = normalizedObjective || 'awareness';
    const planningGoalLine = routePrefill.businessName?.trim()
      ? `Build ${selectIndefiniteArticle(objectiveLabel)} ${objectiveLabel} campaign for ${routePrefill.businessName.trim()}${normalizedLocation ? ` in ${normalizedLocation}` : ''}.`
      : '';
    const packageDirectionLine = inferredForm.brief
      .split('. ')
      .find((item) => item.includes('package range') || item.includes('within '))
      ?.trim() ?? '';
    const briefSections = [
      typeof routePrefill.leadId === 'number' && routePrefill.leadId > 0
        ? `Lead source id: ${routePrefill.leadId}`
        : '',
      planningGoalLine,
      packageDirectionLine,
      routePrefill.archetypeName?.trim()
        ? `Archetype: ${routePrefill.archetypeName.trim()}`
        : '',
      routePrefill.industryProfileName?.trim()
        ? `Industry profile: ${routePrefill.industryProfileName.trim()}`
        : '',
      routePrefill.industryMessagingAngle?.trim()
        ? `Industry messaging angle:\n${routePrefill.industryMessagingAngle.trim()}`
        : '',
      (routePrefill.industryGuardrails ?? []).length > 0
        ? `Industry guardrails:\n${(routePrefill.industryGuardrails ?? [])
          .filter((item) => item.trim().length > 0)
          .map((item) => `- ${item.trim()}`)
          .join('\n')}`
        : '',
      routePrefill.industryCta?.trim()
        ? `Industry recommended CTA:\n${routePrefill.industryCta.trim()}`
        : '',
      researchBasisBlock
        ? `Research basis:\n${researchBasisBlock}`
        : '',
      routePrefill.lastResearchedAtUtc?.trim()
        ? `Last researched:\n${routePrefill.lastResearchedAtUtc.trim()}`
        : '',
      routePrefill.socialQualityNote?.trim()
        ? `Social quality note:\n${routePrefill.socialQualityNote.trim()}`
        : '',
      detectedGapBlock
        ? `Why you are receiving this:\n${detectedGapBlock}`
        : '',
      routePrefill.insightSummary?.trim()
        ? `Lead intelligence summary:\n${routePrefill.insightSummary.trim()}`
        : '',
      routePrefill.expectedOutcome?.trim()
        ? routePrefill.expectedOutcome.trim()
        : '',
      routePrefill.whyActNow?.trim()
        ? `Why act now:\n${routePrefill.whyActNow.trim()}`
        : '',
    ].filter((item) => item.trim().length > 0);

    const prefilled = {
      ...inferredForm,
      brandName: routePrefill.businessName?.trim() || inferredForm.brandName,
      objective: normalizeOption(autoBriefFields?.objective?.value, OBJECTIVE_OPTIONS) || normalizedObjective || inferredForm.objective,
      audience: normalizeOption(autoBriefFields?.audience?.value, AUDIENCE_OPTIONS) || inferredForm.audience,
      scope: normalizeOption(autoBriefFields?.scope?.value, SCOPE_OPTIONS)
        || (normalizedLocation && inferredForm.scope !== 'national' ? inferredForm.scope || 'provincial' : inferredForm.scope),
      geography: autoBriefFields?.geography?.value?.trim() || normalizedLocation || inferredForm.geography,
      tone: normalizeOption(autoBriefFields?.tone?.value, TONE_OPTIONS) || inferredForm.tone,
      salesModel: autoBriefFields?.salesModel?.value?.trim() || inferredForm.salesModel,
      customerType: autoBriefFields?.customerType?.value?.trim() || inferredForm.customerType,
      buyingBehaviour: autoBriefFields?.buyingBehaviour?.value?.trim() || inferredForm.buyingBehaviour,
      decisionCycle: autoBriefFields?.decisionCycle?.value?.trim() || inferredForm.decisionCycle,
      urgencyLevel: autoBriefFields?.urgencyLevel?.value?.trim() || inferredForm.urgencyLevel,
      language: autoBriefFields?.language?.value?.trim() || inferredForm.language,
      targetInterests: autoBriefFields?.targetInterests?.value?.trim() || inferredForm.targetInterests,
      targetGender: normalizeOption(autoBriefFields?.targetGender?.value, GENDER_OPTIONS) || inferredForm.targetGender,
      ageRange: normalizeOption(autoBriefFields?.ageRange?.value, AGE_RANGE_OPTIONS) || inferredForm.ageRange,
      brief: briefSections.join('\n\n'),
      channels: ensureRequiredChannels(
        inferredChannels.length > 0
          ? inferredChannels
          : (recommendedChannels.length > 0 ? recommendedChannels : inferredForm.channels),
      ),
    };
    return applyIndustryPolicyToForm(prefilled, selectedIndustryPolicy, allowedChannels);
  }, [allowedChannels, inferredForm, routePrefill, selectedIndustryPolicy]);
  const form = scopedForm?.key === activeFormKey ? scopedForm.value : prefilledForm;
  const aiInterpretationSummary = scopedAiInterpretationSummary?.key === activeFormKey
    ? scopedAiInterpretationSummary.summary
    : '';
  const questionnaireSummary = useMemo(() => {
    return buildBriefQuestionnaireSummary(selectedCampaignBrief, selectedCampaignDetails
      ? {
        businessName: selectedCampaignDetails.businessName,
        industry: selectedCampaignDetails.industry,
      }
      : undefined);
  }, [selectedCampaignBrief, selectedCampaignDetails]);
  const sharedFormOptions = formOptionsQuery.data;
  const hasCapturedBrief = questionnaireSummary.length > 0 || Boolean(routePrefill?.autoBrief);
  const autoBrief = routePrefill?.autoBrief;
  const autoBriefUncertainFields = autoBrief?.uncertainFields?.filter((field) => field.trim().length > 0) ?? [];
  const autoBriefField = <K extends keyof NonNullable<AutoBriefPayload['fields']>>(key: K): AutoBriefField | undefined => {
    const field = autoBrief?.fields?.[key];
    if (!field) {
      return undefined;
    }

    return {
      value: field.value,
      confidence: normalizeConfidence(field.confidence),
      reason: field.reason,
    };
  };
  const routePrefillGapLines = routePrefill?.detectedGaps?.filter((item) => item.trim().length > 0) ?? [];
  const isOpportunityFlow = Boolean(routePrefill);
  const opportunityMissingFields = isOpportunityFlow ? listOpportunityDraftMissingFields(form) : [];

  useEffect(() => {
    setShowDetailEditing(false);
  }, [activeFormKey]);

  const handleFormChange = <K extends keyof CampaignFormState>(key: K, value: CampaignFormState[K]) => {
    if (key === 'scope') {
      const nextScope = value as string;
      setScopedForm({
        key: activeFormKey,
        value: {
          ...form,
          scope: nextScope,
          geography: nextScope === 'national' ? '' : form.geography,
        },
      });
      return;
    }

    setScopedForm({
      key: activeFormKey,
      value: {
        ...form,
        [key]: value,
      },
    });
  };

  const toggleChannel = (channel: ChannelOption) => {
    if (!allowedChannels.includes(channel)) {
      return;
    }

    setScopedForm({
      key: activeFormKey,
      value: {
        ...form,
        channels: ensureRequiredChannels(
          form.channels.includes(channel)
            ? form.channels.filter((item) => item !== channel)
            : [...form.channels, channel],
        ),
      },
    });
  };

  const handleClientChange = (userId: string) => {
    setSelectedClientIdState(userId);
    setSelectedCampaignIdState('');
  };

  const handleCampaignChange = (campaignId: string) => {
    setSelectedCampaignIdState(campaignId);
    const nextCampaign = availableCampaigns.find((item) => item.id === campaignId) ?? null;
    setSelectedClientIdState(nextCampaign ? (nextCampaign.userId ?? nextCampaign.clientEmail ?? nextCampaign.id) : '');
    setSelectedProspectPackageBandState(
      nextCampaign?.status === 'awaiting_purchase'
        ? { campaignId: nextCampaign.id, packageBandId: nextCampaign.packageBandId }
        : null,
    );
  };

  const createProspectMutation = useMutation({
    mutationFn: async () => {
      return advertifiedApi.createAgentProspectCampaign({
        fullName: prospectForm.fullName.trim(),
        email: prospectForm.email.trim(),
        phone: prospectForm.phone.trim(),
        packageBandId: prospectForm.packageBandId,
        campaignName: prospectForm.campaignName.trim() || undefined,
      });
    },
    onSuccess: async (campaign) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
      ]);
      await inboxQuery.refetch();

      setSelectedClientIdState(campaign.userId ?? campaign.clientEmail ?? campaign.id);
      setSelectedCampaignIdState(campaign.id);
      setShowProspectForm(false);
      setProspectForm({
        fullName: '',
        email: '',
        phone: '',
        packageBandId: '',
        campaignName: '',
      });
      pushToast({
        title: 'Prospect lead created.',
        description: 'This creates a lead record and campaign workspace, not a registered client account.',
      });
    },
    onError: (error) => {
      pushAgentMutationError(pushToast, 'Could not create prospect campaign.', error);
    },
  });

  const canCreateProspectCampaign = Boolean(
    prospectForm.fullName.trim()
    && prospectForm.email.trim()
    && prospectForm.phone.trim()
    && prospectForm.packageBandId,
  );

  const initializeMutation = useMutation({
    mutationFn: async ({ submitBrief }: { submitBrief: boolean }) => {
      if (!selectedCampaign) {
        throw new Error('Choose a client campaign first.');
      }

      let campaignForRecommendationId = selectedCampaign.id;
      if (
        selectedCampaign.status === 'awaiting_purchase'
        && selectedProspectPackageBandId
        && selectedProspectPackageBandId !== selectedCampaign.packageBandId
      ) {
        const updatedCampaign = await advertifiedApi.updateProspectPricing(selectedCampaign.id, {
          packageBandId: selectedProspectPackageBandId,
        });
        campaignForRecommendationId = updatedCampaign.id;
      }

      const campaign = await advertifiedApi.initializeAgentRecommendation(campaignForRecommendationId, {
        campaignName: form.brandName,
        planningMode: 'hybrid',
        submitBrief,
        brief: buildRecommendationDraftBrief(form, allowedChannels),
      });

      if (submitBrief) {
        return advertifiedApi.generateAgentRecommendation(campaignForRecommendationId, strategyShareTargets);
      }

      return campaign;
    },
    onSuccess: async (campaign, variables) => {
      setPendingAction(null);
      pushToast({
        title: variables.submitBrief ? 'Recommendation generated.' : 'Draft saved.',
        description: variables.submitBrief
          ? 'The AI draft is ready in the planning workspace for agent review.'
          : 'The campaign brief has been saved for the agent workflow.',
      });

      const campaignId = (campaign as { campaignId: string }).campaignId;

      if (variables.submitBrief) {
        const refreshedCampaign = await waitForGeneratedRecommendation(campaignId);
        queryClient.setQueryData(['agent-campaign', campaignId], refreshedCampaign);
        queryClient.setQueryData(['campaign', campaignId], refreshedCampaign);
        navigate(`/agent/campaigns/${campaignId}`);
      }

      void Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', campaignId] }),
        queryClient.invalidateQueries({ queryKey: ['campaign', campaignId] }),
      ]);
    },
    onError: (error) => {
      setPendingAction(null);
      pushAgentMutationError(pushToast, 'We could not create the recommendation flow.', error, 'Please try again in a moment.');
    },
  });

  const interpretMutation = useMutation({
    mutationFn: async () => {
      if (!selectedCampaign) {
        throw new Error('Choose a client campaign first.');
      }

      return advertifiedApi.interpretAgentBrief(selectedCampaign.id, {
        brief: form.brief,
        campaignName: form.brandName,
        selectedBudget: selectedCampaignIsProspective && selectedPackageBand
          ? resolvePackageReferenceBudget(selectedPackageBand)
          : selectedCampaign.selectedBudget,
      });
    },
    onSuccess: (result) => {
      const interpretedChannels = result.channels
        .map(normalizeChannelOption)
        .filter((channel): channel is ChannelOption => Boolean(channel));

      setScopedForm({
        key: activeFormKey,
        value: {
          ...form,
          objective: normalizeOption(result.objective, OBJECTIVE_OPTIONS) || form.objective,
          audience: normalizeOption(result.audience, AUDIENCE_OPTIONS) || form.audience,
          scope: normalizeOption(result.scope === 'regional' ? 'provincial' : result.scope, SCOPE_OPTIONS) || form.scope,
          geography: normalizeOption(result.geography, GEOGRAPHY_OPTIONS) || form.geography,
          tone: normalizeOption(result.tone, TONE_OPTIONS) || form.tone,
          brandName: result.campaignName || form.brandName,
          channels: ensureRequiredChannels(
            (interpretedChannels.length > 0 ? interpretedChannels : form.channels)
              .filter((channel) => allowedChannels.includes(channel)),
          ),
        },
      });
      setScopedAiInterpretationSummary({ key: activeFormKey, summary: result.summary });
      pushToast({
        title: 'AI inputs updated.',
        description: 'The brief has been interpreted into structured campaign inputs you can refine before generating the draft.',
      });
    },
    onError: (error) => {
      pushAgentMutationError(pushToast, 'We could not interpret that brief.', error, 'Please try again in a moment.');
    },
  });

  const isStep1Complete = Boolean(selectedClientId && selectedCampaign);
  const isStep2Complete = isStep1Complete
    && (isOpportunityFlow ? hasOpportunityDraftRequirements(form) : hasDetailedDraftRequirements(form));
  const isGenerating = pendingAction === 'generate' && initializeMutation.isPending;
  const isWorking = initializeMutation.isPending || interpretMutation.isPending;
  const canGenerate = isStep2Complete && !isWorking;
  const canSaveDraft = isStep1Complete && !isWorking;

  useEffect(() => {
    if (!routePrefill?.autoGenerateDraft || !selectedCampaign || !canGenerate || initializeMutation.isPending) {
      return;
    }

    const autoGenerateKey = `${selectedCampaign.id}:${activeFormKey}`;
    if (autoGenerateKeyRef.current === autoGenerateKey) {
      return;
    }

    autoGenerateKeyRef.current = autoGenerateKey;
    setPendingAction('generate');
    void initializeMutation.mutateAsync({ submitBrief: true });
  }, [activeFormKey, canGenerate, initializeMutation, routePrefill?.autoGenerateDraft, selectedCampaign]);

  const stepPresentation = STEP_CONFIG.map((step) => {
    if (step.id === 1) {
      return {
        ...step,
        toneClass: isStep1Complete
          ? 'border border-brand/20 bg-brand-soft text-brand'
          : 'bg-brand text-white shadow-[0_10px_24px_rgba(15,118,110,0.24)]',
        displayValue: isStep1Complete ? '✓' : '1',
      };
    }

    if (step.id === 2) {
      const isActive = isStep1Complete && !isGenerating;
      return {
        ...step,
        toneClass: isStep2Complete
          ? 'border border-brand/20 bg-brand-soft text-brand'
          : isActive
            ? 'bg-brand text-white shadow-[0_10px_24px_rgba(15,118,110,0.24)]'
            : 'border border-line bg-white text-ink-soft',
        displayValue: isStep2Complete ? '✓' : '2',
      };
    }

    return {
      ...step,
      label: isGenerating ? 'Generating draft...' : step.label,
      toneClass: isGenerating
        ? 'bg-brand text-white shadow-[0_10px_24px_rgba(15,118,110,0.24)]'
        : 'border border-line bg-white text-ink-soft',
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
        title: 'Choose a campaign first.',
        description: 'Select a client campaign before generating a recommendation.',
      }, 'info');
      return;
    }

    if (!isStep2Complete) {
      pushToast({
        title: 'Complete the campaign details first.',
        description: 'Add the campaign objective, audience, coverage, strategy fields, and client brief before generating the draft.',
      }, 'info');
      return;
    }

    setPendingAction('generate');
    await initializeMutation.mutateAsync({ submitBrief: true });
  };

  if (inboxQuery.isLoading || packagesQuery.isLoading || formOptionsQuery.isLoading) {
    return <LoadingState label="Loading recommendation flow..." />;
  }

  if (formOptionsQuery.isError || !sharedFormOptions) {
    return <LoadingState label="We could not load recommendation form options right now." />;
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
                  {index < STEP_CONFIG.length - 1 ? <ChevronRight className="size-4 text-brand/35" /> : null}
                </div>
              );
            })}
          </div>
        </div>
      </div>

      <div className="page-shell grid gap-6 py-8 lg:grid-cols-[1.05fr_380px]">
        <div className="space-y-6">
          <div>
            <div className="hero-kicker">Start recommendation</div>
            <h1 className="mt-4 text-3xl font-semibold tracking-tight text-ink">Create a recommendation</h1>
            <p className="mt-2 max-w-3xl text-sm leading-6 text-ink-soft md:text-base">
              Pick the campaign, add the key details, and let AI prepare a draft you can review and refine.
            </p>
          </div>

          <div className="panel border-line/90 px-6 py-6 md:px-7">
            <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="text-lg font-semibold text-ink">1. Choose the campaign</h2>
                <p className="mt-1 text-sm text-ink-soft">Start from an existing campaign, or create a prospect lead if the person has not registered yet.</p>
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
                <span className="label-base">Package / campaign</span>
                <select value={selectedCampaign?.id ?? ''} onChange={(event) => handleCampaignChange(event.target.value)} className="input-base">
                  <option value="">Select campaign</option>
                  {filteredCampaigns.map((campaign) => (
                    <option key={campaign.id} value={campaign.id}>
                      {campaign.packageBandName} | {isProspectiveCampaign(campaign) && packageBandsById.get(campaign.packageBandId)
                        ? formatPackageRange(packageBandsById.get(campaign.packageBandId)!)
                        : formatCurrency(campaign.selectedBudget)} | {campaign.queueLabel}
                      {isProspectiveCampaign(campaign) ? ' | Prospective (unpaid)' : ''}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            {selectedCampaignIsProspective ? (
              <div className="mt-4 max-w-md">
                <label className="block">
                  <span className="label-base">Price band</span>
                  <select
                    value={selectedProspectPackageBandId}
                    onChange={(event) => setSelectedProspectPackageBandState(
                      selectedCampaign
                        ? { campaignId: selectedCampaign.id, packageBandId: event.target.value }
                        : null,
                    )}
                    className="input-base"
                  >
                    <option value="">Select price band</option>
                    {(packagesQuery.data ?? []).map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.name} | {formatPackageRange(item)}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
            ) : null}
            {selectedCampaignDetailsQuery.isSuccess && questionnaireSummary.length > 0 ? (
              <div className="mt-4 rounded-2xl border border-line bg-slate-50 p-4">
                <p className="text-sm font-semibold text-ink">Captured questionnaire</p>
                <p className="mt-1 text-xs text-ink-soft">This is the brief information already captured. Use it as the starting point for the recommendation.</p>
                <div className="mt-4 grid gap-3 md:grid-cols-2">
                  {questionnaireSummary.map((item) => (
                    <div key={item.label} className="rounded-xl border border-white bg-white px-3 py-3">
                      <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-ink-soft">{item.label}</p>
                      <p className="mt-1 text-sm leading-6 text-ink">{item.value}</p>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}
            <div className="mt-4">
              <button
                type="button"
                onClick={() => setShowProspectForm((current) => !current)}
                className="button-secondary px-4 py-2"
              >
                {showProspectForm ? 'Hide prospect lead form' : 'Add prospect lead'}
              </button>
            </div>
            {showProspectForm ? (
              <div className="mt-4 rounded-2xl border border-line bg-slate-50 p-4">
                <p className="text-sm font-semibold text-ink">Create a prospect lead</p>
                <p className="mt-1 text-xs text-ink-soft">Use this when the person has not registered yet. It creates a lead record and campaign workspace without creating a client login account.</p>
                <div className="mt-4 grid gap-3 md:grid-cols-2">
                  <label className="block">
                    <span className="label-base">Full name</span>
                    <input value={prospectForm.fullName} onChange={(event) => setProspectForm((current) => ({ ...current, fullName: event.target.value }))} className="input-base" />
                  </label>
                  <label className="block">
                    <span className="label-base">Email</span>
                    <input value={prospectForm.email} onChange={(event) => setProspectForm((current) => ({ ...current, email: event.target.value }))} className="input-base" type="email" />
                  </label>
                  <label className="block">
                    <span className="label-base">Phone</span>
                    <input value={prospectForm.phone} onChange={(event) => setProspectForm((current) => ({ ...current, phone: event.target.value }))} className="input-base" />
                  </label>
                  <label className="block">
                    <span className="label-base">Price band</span>
                    <select value={prospectForm.packageBandId} onChange={(event) => setProspectForm((current) => ({ ...current, packageBandId: event.target.value }))} className="input-base">
                      <option value="">Select package</option>
                      {(packagesQuery.data ?? []).map((item) => (
                        <option key={item.id} value={item.id}>{item.name} | {formatPackageRange(item)}</option>
                      ))}
                    </select>
                  </label>
                  <label className="block">
                    <span className="label-base">Campaign name (optional)</span>
                    <input value={prospectForm.campaignName} onChange={(event) => setProspectForm((current) => ({ ...current, campaignName: event.target.value }))} className="input-base" />
                  </label>
                </div>
                <div className="mt-4">
                  <button
                    type="button"
                    onClick={() => createProspectMutation.mutate()}
                    disabled={createProspectMutation.isPending || !canCreateProspectCampaign}
                    className="button-primary px-4 py-2 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {createProspectMutation.isPending ? 'Creating...' : 'Create lead'}
                  </button>
                </div>
              </div>
            ) : null}
          </div>

          <div className="panel border-line/90 px-6 py-6 md:px-7">
            <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="text-lg font-semibold text-ink">{hasCapturedBrief ? '2. Review captured brief' : '2. Add campaign details'}</h2>
              </div>
              <div className="flex flex-wrap items-center gap-3">
                <span className="pill bg-white text-ink-soft">AI can help</span>
                {hasCapturedBrief ? (
                  <button
                    type="button"
                    onClick={() => setShowDetailEditing((current) => !current)}
                    className="button-secondary px-4 py-2"
                  >
                    {showDetailEditing ? 'Hide planning inputs' : 'Refine planning inputs'}
                  </button>
                ) : null}
              </div>
            </div>

            {hasCapturedBrief && !showDetailEditing ? (
              <div className="rounded-[24px] border border-brand/15 bg-brand-soft/30 px-5 py-5">
                <div className="mb-5 rounded-[18px] border border-brand/10 bg-white/70 px-4 py-3 text-sm text-ink-soft">
                  We are using the captured brief as the recommendation source. Open planning inputs only if you want to override or refine them before drafting.
                </div>
                {autoBrief ? (
                  <div className="mb-5 rounded-[18px] border border-brand/15 bg-white/80 px-4 py-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Auto-brief from lead intelligence</p>
                      {autoBrief.generatedAtUtc ? (
                        <p className="text-xs text-ink-soft">Generated {new Date(autoBrief.generatedAtUtc).toLocaleString()}</p>
                      ) : null}
                    </div>
                    {autoBriefUncertainFields.length > 0 ? (
                      <div className="mt-3">
                        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Needs confirmation</p>
                        <div className="mt-2 flex flex-wrap gap-2">
                          {autoBriefUncertainFields.map((field) => (
                            <span key={field} className="rounded-full border border-amber-200 bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-700">
                              {field}
                            </span>
                          ))}
                        </div>
                      </div>
                    ) : null}
                  </div>
                ) : null}
                <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Objective</p>
                    <div className="mt-2 flex items-center gap-2">
                      <p className="text-sm font-medium text-ink">{form.objective || 'Not set'}</p>
                      {autoBriefField('objective') ? (
                        <span className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.12em] ${confidencePillTone(autoBriefField('objective')!.confidence)}`}>
                          {confidenceLabel(autoBriefField('objective')!.confidence)}
                        </span>
                      ) : null}
                    </div>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Audience</p>
                    <div className="mt-2 flex items-center gap-2">
                      <p className="text-sm font-medium text-ink">{form.audience || 'Not set'}</p>
                      {autoBriefField('audience') ? (
                        <span className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.12em] ${confidencePillTone(autoBriefField('audience')!.confidence)}`}>
                          {confidenceLabel(autoBriefField('audience')!.confidence)}
                        </span>
                      ) : null}
                    </div>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Coverage</p>
                    <div className="mt-2 flex items-center gap-2">
                      <p className="text-sm font-medium text-ink">{form.scope || 'Not set'}</p>
                      {autoBriefField('scope') ? (
                        <span className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.12em] ${confidencePillTone(autoBriefField('scope')!.confidence)}`}>
                          {confidenceLabel(autoBriefField('scope')!.confidence)}
                        </span>
                      ) : null}
                    </div>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Main area</p>
                    <div className="mt-2 flex items-center gap-2">
                      <p className="text-sm font-medium text-ink">{form.geography || 'Not needed'}</p>
                      {autoBriefField('geography') ? (
                        <span className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.12em] ${confidencePillTone(autoBriefField('geography')!.confidence)}`}>
                          {confidenceLabel(autoBriefField('geography')!.confidence)}
                        </span>
                      ) : null}
                    </div>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Sales model</p>
                    <p className="mt-2 text-sm font-medium text-ink">{form.salesModel || 'Not set'}</p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Customer type</p>
                    <p className="mt-2 text-sm font-medium text-ink">{form.customerType || 'Not set'}</p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Buying behaviour</p>
                    <p className="mt-2 text-sm font-medium text-ink">{form.buyingBehaviour || 'Not set'}</p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Decision cycle</p>
                    <p className="mt-2 text-sm font-medium text-ink">{form.decisionCycle || 'Not set'}</p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Price positioning</p>
                    <p className="mt-2 text-sm font-medium text-ink">{form.pricePositioning || 'Not set'}</p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Growth target</p>
                    <p className="mt-2 text-sm font-medium text-ink">{form.growthTarget || 'Not set'}</p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Urgency</p>
                    <p className="mt-2 text-sm font-medium text-ink">{form.urgencyLevel || 'Not set'}</p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Audience clarity</p>
                    <p className="mt-2 text-sm font-medium text-ink">{form.audienceClarity || 'Not set'}</p>
                  </div>
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Tone</p>
                    <div className="mt-2 flex items-center gap-2">
                      <p className="text-sm font-medium text-ink">{form.tone || 'Not set'}</p>
                      {autoBriefField('tone') ? (
                        <span className={`rounded-full border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.12em] ${confidencePillTone(autoBriefField('tone')!.confidence)}`}>
                          {confidenceLabel(autoBriefField('tone')!.confidence)}
                        </span>
                      ) : null}
                    </div>
                  </div>
                </div>
              </div>
            ) : null}

            <div className={`${hasCapturedBrief && !showDetailEditing ? 'hidden' : 'grid'} gap-4 md:grid-cols-2`}>
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
                <span className="label-base">Coverage</span>
                <select value={form.scope} onChange={(event) => handleFormChange('scope', event.target.value)} className="input-base">
                  <option value="">Choose scope</option>
                  <option value="local">Local</option>
                  <option value="provincial">Provincial</option>
                  <option value="national">National</option>
                </select>
              </label>

              <label className="block">
                <span className="label-base">Main area</span>
                {form.scope === 'national' ? (
                  <input value="Not needed for national coverage" className="input-base bg-slate-50 text-slate-500" disabled />
                ) : form.scope === 'local' ? (
                  <select value={form.geography} onChange={(event) => handleFormChange('geography', event.target.value)} className="input-base">
                    <option value="">Select city</option>
                    <option value="johannesburg">Johannesburg</option>
                    <option value="cape-town">Cape Town</option>
                    <option value="durban">Durban</option>
                    <option value="pretoria">Pretoria</option>
                    <option value="port-elizabeth">Port Elizabeth</option>
                  </select>
                ) : (
                  <select value={form.geography} onChange={(event) => handleFormChange('geography', event.target.value)} className="input-base">
                    <option value="">Select province</option>
                    <option value="gauteng">Gauteng</option>
                    <option value="western_cape">Western Cape</option>
                    <option value="kwazulu_natal">KwaZulu-Natal</option>
                  </select>
                )}
              </label>
            </div>

            <div className={`${hasCapturedBrief && !showDetailEditing ? 'hidden ' : ''}mt-5 rounded-[28px] border border-brand/10 bg-brand-soft/30 p-4 sm:p-5`}>
              <div className="mb-4">
                <p className="text-sm font-semibold text-ink">Audience profile</p>
              </div>
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                <label className="block">
                  <span className="label-base">Age group</span>
                  <select value={form.ageRange} onChange={(event) => handleFormChange('ageRange', event.target.value)} className="input-base">
                    <option value="">Select age group</option>
                    {AGE_RANGE_OPTIONS.map((option) => (
                      <option key={option} value={option}>{option === '55-100' ? '55+' : option}</option>
                    ))}
                  </select>
                </label>

                <label className="block">
                  <span className="label-base">Gender focus</span>
                  <select value={form.targetGender} onChange={(event) => handleFormChange('targetGender', event.target.value)} className="input-base">
                    <option value="">Select gender focus</option>
                    <option value="all">All</option>
                    <option value="female">Female</option>
                    <option value="male">Male</option>
                    <option value="mixed">Mixed</option>
                  </select>
                </label>

                <label className="block">
                  <span className="label-base">Primary language</span>
                  <select value={form.language} onChange={(event) => handleFormChange('language', event.target.value)} className="input-base">
                    <option value="">Select language</option>
                    {LANGUAGE_OPTIONS.map((option) => (
                      <option key={option} value={option}>{option}</option>
                    ))}
                  </select>
                </label>

                <label className="block md:col-span-2 xl:col-span-3">
                  <span className="label-base">Audience interests</span>
                  <input
                    value={form.targetInterests}
                    onChange={(event) => handleFormChange('targetInterests', event.target.value)}
                    className="input-base"
                    placeholder="Retail, family, commuters"
                  />
                </label>
              </div>
            </div>

            <div className={`${hasCapturedBrief && !showDetailEditing ? 'hidden ' : ''}mt-5 rounded-[28px] border border-brand/10 bg-brand-soft/30 p-4 sm:p-5`}>
              {routePrefill ? (
                <div className="mb-4 rounded-[22px] border border-brand/15 bg-white/80 px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.18em] text-ink-soft">Why you are receiving this</p>
                  <p className="mt-2 text-sm text-ink">
                    Our system detected growth gaps for {routePrefill.businessName || 'this business'} and prepared this campaign direction to fix them.
                  </p>
                  {routePrefillGapLines.length > 0 ? (
                    <div className="mt-3 space-y-2 text-sm text-ink-soft">
                      {routePrefillGapLines.map((item) => (
                        <p key={item}>- {item}</p>
                      ))}
                    </div>
                  ) : null}
                  {routePrefill.expectedOutcome ? (
                    <p className="mt-3 text-sm font-medium text-ink">{routePrefill.expectedOutcome}</p>
                  ) : null}
                  <div className="mt-3 rounded-2xl border border-brand/10 bg-brand-soft/30 px-3 py-3 text-sm text-ink-soft">
                    {opportunityMissingFields.length === 0 ? (
                      <p>This scan already provides enough direction to generate a first proposal draft. You can refine the commercial fields afterward.</p>
                    ) : (
                      <p>Complete {opportunityMissingFields.join(', ')} before generating the first proposal draft.</p>
                    )}
                  </div>
                </div>
              ) : null}

              <div className="mb-4">
                <p className="text-sm font-semibold text-ink">Commercial fit</p>
              </div>
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                <label className="block">
                  <span className="label-base">Sales model</span>
                  <select value={form.salesModel} onChange={(event) => handleFormChange('salesModel', event.target.value)} className="input-base">
                    <option value="">Select sales model</option>
                    {sharedFormOptions.salesModels.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>

                <label className="block">
                  <span className="label-base">Customer type</span>
                  <select value={form.customerType} onChange={(event) => handleFormChange('customerType', event.target.value)} className="input-base">
                    <option value="">Select customer type</option>
                    {sharedFormOptions.customerTypes.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>

                <label className="block">
                  <span className="label-base">Price positioning</span>
                  <select value={form.pricePositioning} onChange={(event) => handleFormChange('pricePositioning', event.target.value)} className="input-base">
                    <option value="">Select price positioning</option>
                    {sharedFormOptions.pricePositioning.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>
              </div>
            </div>

            <div className={`${hasCapturedBrief && !showDetailEditing ? 'hidden ' : ''}mt-4 rounded-[28px] border border-line/80 bg-white/80 p-4 sm:p-5`}>
              <div className="mb-4">
                <p className="text-sm font-semibold text-ink">Decision signals</p>
              </div>
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                <label className="block">
                  <span className="label-base">Buying behaviour</span>
                  <select value={form.buyingBehaviour} onChange={(event) => handleFormChange('buyingBehaviour', event.target.value)} className="input-base">
                    <option value="">Select buying behaviour</option>
                    {sharedFormOptions.buyingBehaviours.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>

                <label className="block">
                  <span className="label-base">Decision cycle</span>
                  <select value={form.decisionCycle} onChange={(event) => handleFormChange('decisionCycle', event.target.value)} className="input-base">
                    <option value="">Select decision cycle</option>
                    {sharedFormOptions.decisionCycles.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>

                <label className="block">
                  <span className="label-base">Urgency</span>
                  <select value={form.urgencyLevel} onChange={(event) => handleFormChange('urgencyLevel', event.target.value)} className="input-base">
                    <option value="">Select urgency</option>
                    {sharedFormOptions.urgencyLevels.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>
              </div>
            </div>

            <div className={`${hasCapturedBrief && !showDetailEditing ? 'hidden ' : ''}mt-4 rounded-[28px] border border-line/80 bg-white/80 p-4 sm:p-5`}>
              <div className="mb-4">
                <p className="text-sm font-semibold text-ink">Growth and clarity</p>
              </div>
              <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                <label className="block">
                  <span className="label-base">Growth target</span>
                  <select value={form.growthTarget} onChange={(event) => handleFormChange('growthTarget', event.target.value)} className="input-base">
                    <option value="">Select growth target</option>
                    {sharedFormOptions.growthTargets.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>

                <label className="block">
                  <span className="label-base">Audience clarity</span>
                  <select value={form.audienceClarity} onChange={(event) => handleFormChange('audienceClarity', event.target.value)} className="input-base">
                    <option value="">Select audience clarity</option>
                    {sharedFormOptions.audienceClarity.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>

                <label className="block">
                  <span className="label-base">Value proposition</span>
                  <select value={form.valuePropositionFocus} onChange={(event) => handleFormChange('valuePropositionFocus', event.target.value)} className="input-base">
                    <option value="">Select value proposition</option>
                    {sharedFormOptions.valuePropositionFocus.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>
              </div>
            </div>

            <div className={`${hasCapturedBrief && !showDetailEditing ? 'hidden ' : ''}mt-5 space-y-3`}>
              <span className="label-base">Channels to include</span>
              <div className="flex flex-wrap gap-3">
                {CHANNEL_OPTIONS.map((channel) => {
                  const checked = form.channels.includes(channel);
                  const isAllowed = allowedChannels.includes(channel);
                  const isRequired = channel === 'OOH';
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
                        checked={isRequired ? true : checked}
                        disabled={!isAllowed || isRequired}
                        onChange={() => toggleChannel(channel)}
                        className="size-4 rounded border-slate-300 accent-brand"
                      />
                      <span>{formatChannelLabel(channel)}</span>
                      {isRequired ? <span className="text-[11px] font-semibold uppercase tracking-[0.14em]">Required</span> : null}
                      {!isAllowed ? <span className="text-[11px] font-semibold uppercase tracking-[0.14em]">Not in package</span> : null}
                    </label>
                  );
                })}
              </div>
            </div>

            <div className={`${hasCapturedBrief && !showDetailEditing ? 'hidden ' : ''}mt-5 grid gap-4 md:grid-cols-2`}>
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
              <span className="label-base">Client brief</span>
              <textarea
                value={form.brief}
                onChange={(event) => handleFormChange('brief', event.target.value)}
                rows={6}
                className="input-base min-h-[150px] resize-y"
                placeholder="Describe the campaign in plain language."
              />
              <div className="mt-3 flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  onClick={() => interpretMutation.mutate()}
                  disabled={!selectedCampaign || !form.brief.trim() || isWorking}
                  className="button-secondary inline-flex items-center gap-2 px-4 py-2 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <Sparkles className="size-4" />
                  {interpretMutation.isPending ? 'Reading brief...' : 'Use AI to fill this in'}
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
                <h2 className="text-lg font-semibold text-ink">3. Create the draft</h2>
                <p className="mt-1 text-sm text-ink-soft">Create a draft recommendation now, then review and refine it in the campaign workspace.</p>
              </div>
              <div className="flex flex-wrap gap-3">
                <button
                  type="button"
                  onClick={handleSaveDraft}
                  disabled={!canSaveDraft}
                  className="button-secondary px-4 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {pendingAction === 'draft' && initializeMutation.isPending ? 'Saving draft...' : 'Save for later'}
                </button>
                <button
                  type="button"
                  onClick={handleGenerate}
                  disabled={!canGenerate}
                  className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:cursor-not-allowed disabled:opacity-55"
                >
                  <Sparkles className="size-4" />
                  {isGenerating ? 'Creating draft...' : 'Create recommendation draft'}
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
                <h3 className="font-semibold text-ink">Draft preview</h3>
                <p className="text-sm text-ink-soft">A quick summary of what the system will use before the workspace opens.</p>
              </div>
            </div>

            <div className="space-y-3">
              {routePrefill ? (
                <AgentInsightCard eyebrow="Proposal context" tone="soft">
                  <AgentDetailGrid
                    items={[
                      { label: 'Business', value: routePrefill.businessName || 'Not captured' },
                      { label: 'Suggested campaign type', value: routePrefill.suggestedCampaignType || 'Not captured' },
                      { label: 'Archetype', value: routePrefill.archetypeName || 'Not captured' },
                    ]}
                  />
                  {routePrefillGapLines.length > 0 ? (
                    <div className="mt-3 space-y-2 text-sm text-ink-soft">
                      {routePrefillGapLines.map((item) => (
                        <p key={item}>- {item}</p>
                      ))}
                    </div>
                  ) : null}
                </AgentInsightCard>
              ) : null}

              <AgentInsightCard eyebrow="What the draft will use">
                <AgentDetailGrid
                  items={[
                    { label: 'Objective', value: form.objective || 'Not selected' },
                    { label: 'Audience', value: form.audience || 'Not selected' },
                    { label: 'Scope', value: form.scope || 'Not selected' },
                    { label: 'Geography', value: form.geography || 'Not selected' },
                    { label: 'Age group', value: form.ageRange ? (form.ageRange === '55-100' ? '55+' : form.ageRange) : 'Not selected' },
                    { label: 'Gender focus', value: form.targetGender || 'Not selected' },
                    { label: 'Language', value: form.language || 'Not selected' },
                    { label: 'Interests', value: form.targetInterests || 'Not selected' },
                    { label: 'Tone', value: form.tone || 'Not selected' },
                    { label: 'Sales model', value: form.salesModel || 'Not selected' },
                    { label: 'Customer type', value: form.customerType || 'Not selected' },
                    { label: 'Buying behaviour', value: form.buyingBehaviour || 'Not selected' },
                    { label: 'Decision cycle', value: form.decisionCycle || 'Not selected' },
                    { label: 'Urgency', value: form.urgencyLevel || 'Not selected' },
                    { label: 'Channels', value: form.channels.map((channel) => formatChannelLabel(channel)).join(' + ') || 'None selected' },
                  ]}
                />
              </AgentInsightCard>

              <div className="rounded-[22px] border border-line bg-white px-4 py-4">
                <p className="text-xs uppercase tracking-[0.18em] text-ink-soft">Rules being applied</p>
                <ul className="mt-3 space-y-2 text-sm text-ink-soft">
                  <li>• Recommendation must stay within the selected package budget.</li>
                  <li>• Geography comes from campaign input, not package purchase.</li>
                  <li>• National radio is only available for Scale or Dominance when policy allows it.</li>
                  <li>• Final inventory selection is validated in the planning workspace.</li>
                </ul>
              </div>
            </div>
          </div>

          <div className="panel hero-mint px-6 py-6 text-ink">
            <p className="text-xs uppercase tracking-[0.18em] text-ink-soft">Selected campaign</p>
            <h3 className="mt-2 text-2xl font-semibold">
              {selectedPackageBand ? `${selectedPackageBand.name} package` : 'No package selected'}
            </h3>
            <p className="mt-1 text-sm text-ink-soft">
              {selectedCampaign
                ? `${selectedCampaign.queueLabel} · ${selectedPackageBand ? `${formatPackageRange(selectedPackageBand)} price band` : formatCurrency(selectedCampaign.selectedBudget)}`
                : 'Choose a campaign to continue'}
            </p>
            {selectedCampaignIsProspective ? (
              <p className="mt-2 text-xs text-amber-700">
                This is a prospective campaign, so you can prepare and share the recommendation before payment.
              </p>
            ) : null}

            <div className="mt-5">
              <AgentDetailStack
                items={[
                  { label: 'Client', value: selectedCampaign?.clientName ?? '-' },
                  { label: 'Order reference', value: selectedCampaign?.id.slice(0, 8).toUpperCase() ?? '-' },
                  { label: 'AI review', value: 'Recommended before sending' },
                  { label: 'Allowed channels', value: allowedChannels.map((channel) => formatChannelLabel(channel)).join(', ') || 'Select channels' },
                ]}
              />
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

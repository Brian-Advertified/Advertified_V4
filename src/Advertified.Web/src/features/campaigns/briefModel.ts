import { titleCase } from '../../lib/utils';
import { formatChannelLabel } from '../channels/channelUtils';
import type { CampaignBrief } from '../../types/domain';

type QuestionnaireBriefFieldKey =
  | 'objective'
  | 'businessStage'
  | 'monthlyRevenueBand'
  | 'salesModel'
  | 'geographyScope'
  | 'targetGender'
  | 'customerType'
  | 'buyingBehaviour'
  | 'decisionCycle'
  | 'pricePositioning'
  | 'averageCustomerSpendBand'
  | 'growthTarget'
  | 'urgencyLevel'
  | 'audienceClarity'
  | 'valuePropositionFocus'
  | 'preferredMediaTypes'
  | 'specialRequirements';

export type QuestionnaireBriefFields = {
  [K in QuestionnaireBriefFieldKey]-?: CampaignBrief[K] extends string[] | undefined ? string[] : string;
};

export type RecommendationDraftChannel = 'Radio' | 'OOH' | 'TV' | 'Digital';

export type RecommendationDraftFormState = {
  objective: string;
  audience: string;
  scope: string;
  geography: string;
  ageRange: string;
  language: string;
  targetGender: string;
  targetInterests: string;
  salesModel: string;
  customerType: string;
  buyingBehaviour: string;
  decisionCycle: string;
  pricePositioning: string;
  growthTarget: string;
  urgencyLevel: string;
  audienceClarity: string;
  valuePropositionFocus: string;
  brandName: string;
  tone: string;
  brief: string;
  channels: RecommendationDraftChannel[];
};

export type CampaignOpportunityContext = {
  detectedGaps: string[];
  insightSummary?: string;
  expectedOutcome?: string;
  campaignNotes?: string;
};

export function createDefaultCampaignBrief(overrides?: Partial<CampaignBrief>): CampaignBrief {
  return {
    objective: '',
    geographyScope: '',
    openToUpsell: true,
    ...overrides,
  };
}

export function normalizeCampaignBrief(brief?: CampaignBrief | null): CampaignBrief | undefined {
  if (!brief) {
    return undefined;
  }

  return {
    ...brief,
    provinces: brief.provinces ?? undefined,
    cities: brief.cities ?? undefined,
    suburbs: brief.suburbs ?? undefined,
    areas: brief.areas ?? undefined,
    targetLanguages: brief.targetLanguages ?? undefined,
    targetInterests: brief.targetInterests ?? undefined,
    preferredMediaTypes: brief.preferredMediaTypes ?? undefined,
    excludedMediaTypes: brief.excludedMediaTypes ?? undefined,
    mustHaveAreas: brief.mustHaveAreas ?? undefined,
    excludedAreas: brief.excludedAreas ?? undefined,
  };
}

export function createDefaultQuestionnaireBriefFields(
  overrides?: Partial<QuestionnaireBriefFields>,
): QuestionnaireBriefFields {
  const brief = createDefaultCampaignBrief({
    objective: 'awareness',
    geographyScope: 'provincial',
    openToUpsell: false,
    preferredMediaTypes: ['ooh', 'radio'],
    ...overrides,
  });

  return {
    objective: brief.objective,
    businessStage: brief.businessStage ?? '',
    monthlyRevenueBand: brief.monthlyRevenueBand ?? '',
    salesModel: brief.salesModel ?? '',
    geographyScope: brief.geographyScope,
    targetGender: brief.targetGender ?? '',
    customerType: brief.customerType ?? '',
    buyingBehaviour: brief.buyingBehaviour ?? '',
    decisionCycle: brief.decisionCycle ?? '',
    pricePositioning: brief.pricePositioning ?? '',
    averageCustomerSpendBand: brief.averageCustomerSpendBand ?? '',
    growthTarget: brief.growthTarget ?? '',
    urgencyLevel: brief.urgencyLevel ?? '',
    audienceClarity: brief.audienceClarity ?? '',
    valuePropositionFocus: brief.valuePropositionFocus ?? '',
    preferredMediaTypes: brief.preferredMediaTypes ?? [],
    specialRequirements: brief.specialRequirements ?? '',
  };
}

export function splitCommaList(value?: string) {
  return value?.split(',').map((item) => item.trim()).filter(Boolean);
}

export function joinCommaList(value?: string[]) {
  return value?.join(', ');
}

export function optionalNumber(value?: string) {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isNaN(parsed) ? undefined : parsed;
}

export function parseAgeRange(value: string): { min?: number; max?: number } {
  if (!value) {
    return {};
  }

  const [min, max] = value.split('-').map((item) => Number.parseInt(item, 10));
  return {
    min: Number.isFinite(min) ? min : undefined,
    max: Number.isFinite(max) ? max : undefined,
  };
}

export function formatAgeRange(min?: number, max?: number): string {
  if (!min && !max) {
    return '';
  }

  return `${min ?? '?'}-${max ?? '?'}`;
}

export function inferRecommendationAudienceFromBrief(brief?: CampaignBrief): string {
  const haystack = `${brief?.targetAudienceNotes ?? ''} ${(brief?.targetInterests ?? []).join(' ')}`.toLowerCase();
  if (haystack.includes('youth') || haystack.includes('young')) {
    return 'youth';
  }

  if (haystack.includes('business') || haystack.includes('professional')) {
    return 'business';
  }

  if (haystack.includes('retail') || haystack.includes('shopper')) {
    return 'retail';
  }

  return 'mass-market';
}

export function inferRecommendationToneFromBrief(brief?: CampaignBrief, packageBandName?: string): string {
  const haystack = `${brief?.targetAudienceNotes ?? ''} ${brief?.specialRequirements ?? ''}`.toLowerCase();
  if (haystack.includes('premium') || haystack.includes('luxury')) {
    return 'premium';
  }

  if (haystack.includes('performance') || haystack.includes('lead')) {
    return 'performance';
  }

  if (packageBandName === 'Dominance') {
    return 'premium';
  }

  return 'high-visibility';
}

export function inferRecommendationGeographyFromBrief(brief?: CampaignBrief): string {
  const rawValue = brief?.cities?.[0]
    ?? brief?.areas?.[0]
    ?? brief?.provinces?.[0]
    ?? '';

  return rawValue.trim().toLowerCase().replace(/\s+/g, '-');
}

export function buildBriefGeographySummary(brief?: CampaignBrief): string {
  if (!brief) {
    return 'Not captured yet';
  }

  const areas = [...(brief.areas ?? []), ...(brief.cities ?? []), ...(brief.provinces ?? [])];
  if (areas.length > 0) {
    return areas.join(', ');
  }

  return titleCase(brief.geographyScope || 'not set');
}

export function buildBriefCoverageSummary(
  brief?: CampaignBrief,
  fallback = 'Not added yet',
): string {
  const summary = buildBriefGeographySummary(brief);
  return summary === 'Not captured yet' || summary === 'Not Set' ? fallback : summary;
}

export function buildBriefAgeRangeSummary(brief?: CampaignBrief): string | undefined {
  if (!brief) {
    return undefined;
  }

  if (!brief.targetAgeMin && !brief.targetAgeMax) {
    return undefined;
  }

  return `${brief.targetAgeMin ?? '?'}-${brief.targetAgeMax ?? '?'}`;
}

export function buildBriefQuestionnaireSummary(
  brief?: CampaignBrief,
  campaignContext?: { businessName?: string; industry?: string },
) {
  if (!brief) {
    return [];
  }

  const opportunityContext = parseCampaignOpportunityContext(brief);

  return [
    { label: 'Business', value: campaignContext?.businessName },
    { label: 'Industry', value: campaignContext?.industry },
    { label: 'Business stage', value: brief.businessStage },
    { label: 'Monthly revenue', value: brief.monthlyRevenueBand },
    { label: 'Sales model', value: brief.salesModel },
    { label: 'Objective', value: brief.objective },
    { label: 'Geography', value: buildBriefGeographySummary(brief) },
    { label: 'Age range', value: buildBriefAgeRangeSummary(brief) },
    { label: 'Gender', value: brief.targetGender },
    { label: 'Languages', value: brief.targetLanguages?.join(', ') },
    { label: 'Customer type', value: brief.customerType },
    { label: 'Buying behaviour', value: brief.buyingBehaviour },
    { label: 'Decision cycle', value: brief.decisionCycle },
    { label: 'Price positioning', value: brief.pricePositioning },
    { label: 'Average spend', value: brief.averageCustomerSpendBand },
    { label: 'Growth target', value: brief.growthTarget },
    { label: 'Urgency', value: brief.urgencyLevel },
    { label: 'Audience clarity', value: brief.audienceClarity },
    { label: 'Value proposition', value: brief.valuePropositionFocus },
    { label: 'Preferred channels', value: brief.preferredMediaTypes?.join(', ') },
    { label: 'Interests', value: brief.targetInterests?.join(', ') },
    { label: 'Audience notes', value: brief.targetAudienceNotes },
    { label: 'Special requirements', value: opportunityContext?.campaignNotes ?? brief.specialRequirements },
  ].filter((item): item is { label: string; value: string } => Boolean(item.value && item.value.trim()));
}

export function buildBriefAudienceSummary(
  brief?: CampaignBrief,
  fallback = 'Audience direction has not been captured yet.',
): string {
  if (!brief) {
    return fallback;
  }

  const parts: string[] = [];
  if (brief.targetAudienceNotes?.trim()) {
    parts.push(brief.targetAudienceNotes.trim());
  }
  if (brief.targetInterests?.length) {
    parts.push(brief.targetInterests.join(', '));
  }
  if (brief.targetAgeMin || brief.targetAgeMax) {
    parts.push(`Age ${brief.targetAgeMin ?? '?'}-${brief.targetAgeMax ?? '?'}`);
  }

  return parts[0] ?? fallback;
}

export function buildBriefChannelSummary(
  brief?: CampaignBrief,
  fallback = 'No channels set',
): string {
  if (!brief?.preferredMediaTypes?.length) {
    return fallback;
  }

  return brief.preferredMediaTypes
    .map((channel) => formatChannelLabel(channel))
    .join(', ');
}

export function buildBriefProductionNotes(
  brief?: CampaignBrief,
  fallback = 'No production notes yet. Build from the approved recommendation and package rules.',
): string {
  const opportunityContext = parseCampaignOpportunityContext(brief);

  return opportunityContext?.campaignNotes?.trim()
    || brief?.specialRequirements?.trim()
    || brief?.creativeNotes?.trim()
    || fallback;
}

export function buildBriefClientNotes(
  brief?: CampaignBrief,
  fallback = 'No client notes captured yet.',
): string {
  const opportunityContext = parseCampaignOpportunityContext(brief);

  return opportunityContext?.campaignNotes?.trim()
    || brief?.specialRequirements?.trim()
    || brief?.creativeNotes?.trim()
    || brief?.targetAudienceNotes?.trim()
    || fallback;
}

export function buildBriefOriginalPrompt(
  brief?: CampaignBrief,
  fallback = 'No original prompt has been captured yet.',
): string {
  const opportunityContext = parseCampaignOpportunityContext(brief);

  return opportunityContext?.campaignNotes?.trim()
    || brief?.specialRequirements?.trim()
    || brief?.creativeNotes?.trim()
    || brief?.targetAudienceNotes?.trim()
    || fallback;
}

export function parseCampaignOpportunityContext(brief?: CampaignBrief): CampaignOpportunityContext | undefined {
  const rawNotes = brief?.specialRequirements?.trim() || brief?.creativeNotes?.trim();
  if (!rawNotes) {
    return undefined;
  }

  const sections = rawNotes
    .split(/\r?\n\r?\n/)
    .map((section) => section.trim())
    .filter(Boolean);

  const detectedGaps: string[] = [];
  let insightSummary: string | undefined;
  let expectedOutcome: string | undefined;
  const remainingSections: string[] = [];

  for (const section of sections) {
    if (section.toLowerCase().startsWith('why you are receiving this:')) {
      const body = section.slice('Why you are receiving this:'.length).trim();
      body
        .split(/\r?\n/)
        .map((line) => line.trim())
        .filter((line) => line.startsWith('-'))
        .forEach((line) => {
          const normalized = line.slice(1).trim();
          if (normalized) {
            detectedGaps.push(normalized);
          }
        });
      continue;
    }

    if (section.toLowerCase().startsWith('lead intelligence summary:')) {
      insightSummary = section.slice('Lead intelligence summary:'.length).trim() || undefined;
      continue;
    }

    if (section.toLowerCase().startsWith('expected impact:')) {
      expectedOutcome = section;
      continue;
    }

    remainingSections.push(section);
  }

  if (detectedGaps.length === 0 && !insightSummary && !expectedOutcome) {
    return undefined;
  }

  return {
    detectedGaps,
    insightSummary,
    expectedOutcome,
    campaignNotes: remainingSections.join('\n\n') || undefined,
  };
}

export function buildRecommendationDraftBrief(
  form: RecommendationDraftFormState,
  allowedChannels: RecommendationDraftChannel[],
): CampaignBrief {
  const provinceMap: Record<string, string> = {
    gauteng: 'Gauteng',
    western_cape: 'Western Cape',
    kwazulu_natal: 'KwaZulu-Natal',
  };
  const cityMap: Record<string, string> = {
    johannesburg: 'Johannesburg',
    'cape-town': 'Cape Town',
    durban: 'Durban',
    pretoria: 'Pretoria',
    'port-elizabeth': 'Port Elizabeth',
  };
  const normalizedScope = form.scope === 'regional' ? 'provincial' : (form.scope || 'provincial');
  const geography = form.geography;
  const provinces = normalizedScope === 'provincial' && geography
    ? [provinceMap[geography] ?? geography]
    : undefined;
  const cities = normalizedScope === 'local' && geography
    ? [cityMap[geography] ?? geography]
    : undefined;
  const ageRange = parseAgeRange(form.ageRange);

  return {
    objective: form.objective || 'awareness',
    geographyScope: normalizedScope,
    provinces,
    cities,
    areas: undefined,
    targetAgeMin: ageRange.min,
    targetAgeMax: ageRange.max,
    targetGender: form.targetGender || undefined,
    targetLanguages: form.language.trim() ? [form.language.trim()] : undefined,
    targetInterests: splitCommaList(form.targetInterests),
    salesModel: form.salesModel || undefined,
    customerType: form.customerType || undefined,
    buyingBehaviour: form.buyingBehaviour || undefined,
    decisionCycle: form.decisionCycle || undefined,
    pricePositioning: form.pricePositioning || undefined,
    growthTarget: form.growthTarget || undefined,
    urgencyLevel: form.urgencyLevel || undefined,
    audienceClarity: form.audienceClarity || undefined,
    valuePropositionFocus: form.valuePropositionFocus || undefined,
    targetAudienceNotes: [form.audience, form.tone].filter(Boolean).join(' · '),
    preferredMediaTypes: form.channels
      .filter((channel) => allowedChannels.includes(channel))
      .map((channel) => channel.toLowerCase()),
    creativeNotes: form.brandName || undefined,
    openToUpsell: false,
    specialRequirements: form.brief,
  };
}

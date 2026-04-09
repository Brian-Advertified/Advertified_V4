import type { AutoBriefConfidence, AutoBriefFieldKey, AutoBriefPayload } from './leadAutoBrief';
import type { LeadIndustryPolicy, LeadIntelligence, LeadOpportunityProfile } from '../../types/domain';

const STRONG_CHANNEL_MIN = 60;

function toAutoBriefConfidence(value?: string): AutoBriefConfidence {
  const normalized = (value ?? '').trim().toLowerCase();
  if (normalized === 'detected') {
    return 'detected';
  }

  if (normalized === 'inferred') {
    return 'weakly_inferred';
  }

  return 'no_evidence';
}

function getEnrichmentField(lead: LeadIntelligence, key: string) {
  return lead.enrichment.fields.find((field) => field.key === key);
}

function inferGenderValue(value: string): string {
  const normalized = value.toLowerCase();
  if (normalized.includes('female')) {
    return 'female';
  }

  if (normalized.includes('male')) {
    return 'male';
  }

  return 'mixed';
}

export function getChannelScore(lead: LeadIntelligence, channel: string) {
  return lead.channelDetections.find((item) => item.channel === channel)?.score ?? 0;
}

export function buildResearchEvidenceLines(lead: LeadIntelligence): string[] {
  const social = lead.channelDetections.find((item) => item.channel === 'social');
  const search = lead.channelDetections.find((item) => item.channel === 'search');
  const ooh = lead.channelDetections.find((item) => item.channel === 'billboards_ooh');
  const observedAt = lead.latestSignal?.createdAt
    ? new Date(lead.latestSignal.createdAt).toISOString().slice(0, 10)
    : new Date().toISOString().slice(0, 10);
  const confidenceLabel = (confidence?: string) => (confidence ? confidence.replaceAll('_', ' ') : 'not available');

  const lines: string[] = [];
  lines.push(`Observed active promotions: ${lead.latestSignal?.hasPromo ? 'Yes' : 'No'} | Source: website signal scan | Confidence: medium | Observed: ${observedAt}`);
  lines.push(`Observed Meta ad indicators: ${lead.latestSignal?.hasMetaAds ? 'Present' : 'Not found'} | Source: website stack markers | Confidence: ${lead.latestSignal?.hasMetaAds ? 'medium' : 'low'} | Observed: ${observedAt}`);

  if (search) {
    lines.push(`Search channel score: ${search.score}/100 | Source: channel scoring model | Confidence: ${confidenceLabel(search.confidence)} | Observed: ${observedAt}`);
  }

  if (ooh) {
    lines.push(`Billboards and Digital Screens channel score: ${ooh.score}/100 | Source: channel scoring model | Confidence: ${confidenceLabel(ooh.confidence)} | Observed: ${observedAt}`);
  }

  if (social) {
    lines.push(`Social channel score: ${social.score}/100 | Source: channel scoring model | Confidence: ${confidenceLabel(social.confidence)} | Observed: ${observedAt}`);
  }

  return lines.slice(0, 5);
}

export function buildSocialQualityNote(lead: LeadIntelligence): string {
  const socialScore = getChannelScore(lead, 'social');

  if (socialScore >= STRONG_CHANNEL_MIN) {
    return 'Social activity appears strong based on public signals. Creative quality, posting frequency quality, spend efficiency, and conversion quality are not directly verified without platform-level account data.';
  }

  if (socialScore >= 40) {
    return 'Some social activity signals are present, but campaign quality and consistency are unclear from public signals alone.';
  }

  return 'Limited social campaign evidence was found. Current assessment is based on public signals and should be validated with direct platform data.';
}

export function buildWorkingSignals(lead: LeadIntelligence): string[] {
  const signals: string[] = [];
  const social = getChannelScore(lead, 'social');
  const search = getChannelScore(lead, 'search');
  const hasPromo = lead.latestSignal?.hasPromo ?? false;
  const websiteActive = lead.latestSignal?.websiteUpdatedRecently ?? false;

  if (hasPromo) {
    signals.push('Active promotional movement was detected.');
  }

  if (websiteActive || lead.lead.website) {
    signals.push('Digital presence is active with a usable website foundation.');
  }

  if (social >= STRONG_CHANNEL_MIN || search >= STRONG_CHANNEL_MIN) {
    signals.push('At least one demand channel is already showing strong momentum.');
  }

  if (signals.length === 0) {
    signals.push('Baseline market presence exists, with room to build consistent demand capture.');
  }

  return signals.slice(0, 3);
}

function normalizeGeographyValue(location: string): string {
  const normalized = location.trim().toLowerCase();
  const mappings: Array<{ pattern: RegExp; value: string }> = [
    { pattern: /johannesburg|joburg|jhb/, value: 'johannesburg' },
    { pattern: /cape[\s-]?town|capetown/, value: 'cape-town' },
    { pattern: /durban|ethekwini/, value: 'durban' },
    { pattern: /pretoria|tshwane/, value: 'pretoria' },
    { pattern: /port[\s-]?elizabeth|gqeberha/, value: 'port-elizabeth' },
    { pattern: /gauteng/, value: 'gauteng' },
    { pattern: /western[\s-]?cape/, value: 'western_cape' },
    { pattern: /kwazulu[\s-]?natal|kzn/, value: 'kwazulu_natal' },
  ];

  const mapped = mappings.find((item) => item.pattern.test(normalized));
  if (mapped) {
    return mapped.value;
  }

  return normalized.replace(/\s+/g, '-');
}

function inferScopeFromLeadLocation(location: string): { value: string; confidence: AutoBriefConfidence; reason: string } {
  const normalized = location.trim().toLowerCase();
  if (/south africa|nationwide|national/.test(normalized)) {
    return {
      value: 'national',
      confidence: 'strongly_inferred',
      reason: 'Location signal indicates a national footprint.',
    };
  }

  return {
    value: 'provincial',
    confidence: 'weakly_inferred',
    reason: 'No explicit national footprint signal was found, so provincial is the safer default.',
  };
}

export function buildAutoBriefFromLead(
  lead: LeadIntelligence,
  archetype: LeadOpportunityProfile,
  industryPolicy: LeadIndustryPolicy): AutoBriefPayload {
  const scope = inferScopeFromLeadLocation(lead.lead.location);
  const audienceValue = /retail|grocery|supermarket|shop/i.test(lead.lead.category) ? 'retail' : 'mass-market';
  const toneValue = industryPolicy.preferredTone
    ?? (lead.latestSignal?.hasPromo || lead.score.score >= 60 ? 'performance' : 'balanced');
  const languageField = getEnrichmentField(lead, 'language');
  const targetAudienceField = getEnrichmentField(lead, 'target_audience');
  const genderField = getEnrichmentField(lead, 'gender');

  const fields: AutoBriefPayload['fields'] = {
    objective: {
      value: industryPolicy.objectiveOverride ?? archetype.suggestedCampaignType,
      confidence: 'strongly_inferred',
      reason: `Mapped from detected lead archetype and ${industryPolicy.name} policy.`,
    },
    audience: {
      value: audienceValue,
      confidence: 'strongly_inferred',
      reason: 'Inferred from business category and lead profile.',
    },
    scope: {
      value: scope.value,
      confidence: scope.confidence,
      reason: scope.reason,
    },
    geography: {
      value: normalizeGeographyValue(lead.lead.location),
      confidence: 'strongly_inferred',
      reason: 'Mapped from lead location signal.',
    },
    tone: {
      value: toneValue,
      confidence: 'weakly_inferred',
      reason: 'Inferred from campaign momentum and promotional signal mix.',
    },
    salesModel: {
      value: '',
      confidence: 'no_evidence',
      reason: 'No reliable public signal available.',
    },
    customerType: {
      value: '',
      confidence: 'no_evidence',
      reason: 'No reliable public signal available.',
    },
    buyingBehaviour: {
      value: '',
      confidence: 'no_evidence',
      reason: 'No reliable public signal available.',
    },
    decisionCycle: {
      value: '',
      confidence: 'no_evidence',
      reason: 'No reliable public signal available.',
    },
    urgencyLevel: {
      value: '',
      confidence: 'no_evidence',
      reason: 'No reliable public signal available.',
    },
    language: languageField?.value
      ? {
          value: languageField.value,
          confidence: toAutoBriefConfidence(languageField.confidence),
          reason: `${languageField.reason} (source: ${languageField.source})`,
        }
      : {
          value: '',
          confidence: 'no_evidence',
          reason: 'No reliable public signal available.',
        },
    targetInterests: {
      value: '',
      confidence: 'no_evidence',
      reason: 'No reliable public signal available.',
    },
    targetGender: genderField?.value
      ? {
          value: inferGenderValue(genderField.value),
          confidence: toAutoBriefConfidence(genderField.confidence),
          reason: `${genderField.reason} (source: ${genderField.source})`,
        }
      : {
          value: '',
          confidence: 'no_evidence',
          reason: 'No reliable public signal available.',
        },
    ageRange: targetAudienceField?.value && /\b35\s*-\s*65\b/.test(targetAudienceField.value)
      ? {
          value: '45-54',
          confidence: 'weakly_inferred',
          reason: 'Age range approximated from inferred audience profile.',
        }
      : {
          value: '',
          confidence: 'no_evidence',
          reason: 'No reliable public signal available.',
        },
  };

  const uncertainLabelByField: Array<{ key: AutoBriefFieldKey; label: string }> = [
    { key: 'ageRange', label: 'Age group' },
    { key: 'targetGender', label: 'Gender focus' },
    { key: 'language', label: 'Language' },
    { key: 'targetInterests', label: 'Interests' },
    { key: 'salesModel', label: 'Sales model' },
    { key: 'customerType', label: 'Customer type' },
    { key: 'buyingBehaviour', label: 'Buying behaviour' },
    { key: 'decisionCycle', label: 'Decision cycle' },
    { key: 'urgencyLevel', label: 'Urgency' },
  ];
  const uncertainFields = uncertainLabelByField
    .filter((item) => fields[item.key]?.confidence === 'no_evidence')
    .map((item) => item.label);
  const strategyChannels = (lead.strategy?.channels ?? [])
    .map((item) => item.channel)
    .filter((value) => value.trim().length > 0);
  const recommendedChannels = strategyChannels.length > 0
    ? strategyChannels
    : archetype.recommendedChannels;

  return {
    fields,
    channels: {
      values: recommendedChannels,
      confidence: 'strongly_inferred',
      reason: strategyChannels.length > 0
        ? `Aligned to strategy engine channel mix for ${archetype.name}.`
        : `Aligned to detected archetype, channel gap profile, and ${industryPolicy.name} channel preference.`,
    },
    uncertainFields,
    generatedAtUtc: new Date().toISOString(),
  };
}

export function buildEstimatedImpactRange(score: number): string {
  if (score >= 85) {
    return 'R80k - R180k / month';
  }

  if (score >= 70) {
    return 'R45k - R120k / month';
  }

  if (score >= 50) {
    return 'R20k - R65k / month';
  }

  return 'R5k - R25k / month';
}

export function formatCoverageStatus(score: number): { label: string; tone: string } {
  if (score >= 80) {
    return { label: 'Detected', tone: 'border-emerald-200 bg-emerald-50 text-emerald-700' };
  }

  if (score >= 60) {
    return { label: 'Strong', tone: 'border-sky-200 bg-sky-50 text-sky-700' };
  }

  if (score >= 40) {
    return { label: 'Weak', tone: 'border-amber-200 bg-amber-50 text-amber-700' };
  }

  return { label: 'Missing', tone: 'border-rose-200 bg-rose-50 text-rose-700' };
}

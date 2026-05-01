import type { LeadIndustryContext } from '../../types/domain';

export function normalizeIndustryObjective(value?: string | null) {
  const normalized = value?.trim().toLowerCase();
  return normalized === 'foottraffic' ? 'foot_traffic' : normalized ?? '';
}

export function industryPreferredChannels(context: LeadIndustryContext): string[] {
  return (context.channels.preferredChannels ?? [])
    .map((channel) => channel.trim())
    .filter((channel) => channel.length > 0);
}

export function firstIndustryLanguage(context: LeadIndustryContext): string {
  return context.audience.defaultLanguageBiases.find((language) => language.trim().length > 0)?.trim() ?? '';
}

export function industryLanguageList(context: LeadIndustryContext): string {
  return context.audience.defaultLanguageBiases
    .map((language) => language.trim())
    .filter((language) => language.length > 0)
    .join(', ');
}

export function buildIndustryAudienceNotes(context: LeadIndustryContext): string {
  return [
    context.audience.primaryPersona ? `Primary audience: ${context.audience.primaryPersona}` : '',
    context.audience.buyingJourney ? `Buying journey: ${context.audience.buyingJourney}` : '',
  ].filter((value) => value.trim().length > 0).join('\n');
}

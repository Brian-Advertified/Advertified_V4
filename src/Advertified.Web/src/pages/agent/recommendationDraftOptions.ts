import { normalizeChannelKey } from '../../features/channels/channelUtils';
import type { RecommendationDraftChannel } from '../../features/campaigns/briefModel';

export type ChannelOption = RecommendationDraftChannel;

export const CHANNEL_OPTIONS: ChannelOption[] = ['OOH', 'Radio', 'TV', 'Digital'];
export const OBJECTIVE_OPTIONS = ['awareness', 'launch', 'promotion', 'brand_presence', 'leads'] as const;
export const AUDIENCE_OPTIONS = ['mass-market', 'youth', 'business', 'retail'] as const;
export const AGE_RANGE_OPTIONS = ['18-24', '25-34', '35-44', '45-54', '55-100'] as const;
export const GENDER_OPTIONS = ['all', 'female', 'male', 'mixed'] as const;
export const SCOPE_OPTIONS = ['local', 'provincial', 'national'] as const;
export const GEOGRAPHY_OPTIONS = [
  'johannesburg',
  'soweto',
  'cape-town',
  'durban',
  'pretoria',
  'port-elizabeth',
  'gauteng',
  'western_cape',
  'kwazulu_natal',
] as const;
export const TONE_OPTIONS = ['premium', 'balanced', 'high-visibility', 'performance'] as const;
export const LANGUAGE_OPTIONS = [
  'English',
  'isiZulu',
  'isiXhosa',
  'Afrikaans',
  'Sesotho',
  'Setswana',
  'Sepedi',
  'Xitsonga',
  'Tshivenda',
  'Siswati',
  'isiNdebele',
  'Multilingual',
] as const;

export function normalizeOption<T extends readonly string[]>(value: string | null | undefined, allowed: T): T[number] | '' {
  if (!value) {
    return '';
  }

  return allowed.includes(value) ? value as T[number] : '';
}

export function normalizeChannelOption(channel: string | null | undefined): ChannelOption | undefined {
  if (!channel) {
    return undefined;
  }
  const normalized = normalizeChannelKey(channel);
  return normalized === 'TV'
    ? 'TV'
    : normalized === 'OOH'
      ? 'OOH'
      : normalized === 'RADIO'
        ? 'Radio'
        : normalized === 'DIGITAL'
          ? 'Digital'
          : undefined;
}

export function ensureRequiredChannels(channels: ChannelOption[]): ChannelOption[] {
  const ordered: ChannelOption[] = CHANNEL_OPTIONS.filter((channel) => channel === 'OOH' || channels.includes(channel));
  return ordered.includes('OOH') ? ordered : ['OOH', ...ordered];
}

export function mergeUniqueChannels(base: ChannelOption[], additions: ChannelOption[]): ChannelOption[] {
  return [...base, ...additions]
    .filter((channel, index, allChannels) => allChannels.indexOf(channel) === index);
}

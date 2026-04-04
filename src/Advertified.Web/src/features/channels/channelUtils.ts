import { titleCase } from '../../lib/utils';

export type NormalizedChannelKey = 'RADIO' | 'OOH' | 'TV' | 'DIGITAL' | 'NEWSPAPER' | string;

const CHANNEL_LABELS: Record<string, string> = {
  RADIO: 'Radio',
  OOH: 'Billboards and Digital Screens',
  TV: 'TV',
  DIGITAL: 'Digital',
  NEWSPAPER: 'Newspaper',
};

const CHANNEL_ALIASES: Record<string, NormalizedChannelKey> = {
  radio: 'RADIO',
  ooh: 'OOH',
  billboard: 'OOH',
  billboards: 'OOH',
  out_of_home: 'OOH',
  outofhome: 'OOH',
  television: 'TV',
  tv: 'TV',
  digital: 'DIGITAL',
  meta: 'DIGITAL',
  tiktok: 'DIGITAL',
  google: 'DIGITAL',
  newspaper: 'NEWSPAPER',
  print: 'NEWSPAPER',
};

export function normalizeChannelKey(channel: string): NormalizedChannelKey {
  const raw = channel.trim();
  if (!raw) {
    return '';
  }

  const normalized = raw.toLowerCase().replace(/[ -]+/g, '_');
  if (CHANNEL_ALIASES[normalized]) {
    return CHANNEL_ALIASES[normalized];
  }

  const tokens = normalized.split(/[^a-z0-9]+/).filter(Boolean);
  const matches = Array.from(new Set(tokens
    .map((token) => CHANNEL_ALIASES[token])
    .filter((value): value is NormalizedChannelKey => Boolean(value))));

  return matches.length === 1 ? matches[0] : raw.toUpperCase();
}

export function formatChannelLabel(channel: string): string {
  const normalized = normalizeChannelKey(channel);
  if (CHANNEL_LABELS[normalized]) {
    return CHANNEL_LABELS[normalized];
  }

  return titleCase(channel.trim().replace(/[_-]+/g, ' ').toLowerCase());
}

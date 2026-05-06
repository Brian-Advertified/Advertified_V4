import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatCurrency(value: number, currency = 'ZAR') {
  return new Intl.NumberFormat('en-ZA', {
    style: 'currency',
    currency,
    maximumFractionDigits: 0,
  }).format(value);
}

export function formatCompactBudget(value: number) {
  const rounded = Math.round(value);
  if (Math.abs(rounded) >= 1_000_000) {
    const millions = Math.round((rounded / 1_000_000) * 10) / 10;
    return `${Number.isInteger(millions) ? millions.toFixed(0) : millions.toFixed(1)}M`;
  }

  if (Math.abs(rounded) >= 1_000) {
    const thousands = Math.round(rounded / 1_000);
    return `${thousands}K`;
  }

  return String(rounded);
}

export function formatDate(value: string | Date) {
  return new Intl.DateTimeFormat('en-ZA', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(typeof value === 'string' ? new Date(value) : value);
}

export function formatDateTime(value: string | Date) {
  return new Intl.DateTimeFormat('en-ZA', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(typeof value === 'string' ? new Date(value) : value);
}

export function titleCase(value: string) {
  return value
    .split('_')
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

type RecommendationTimingLike = {
  flighting?: string;
  duration?: string;
  startDate?: string;
  endDate?: string;
  requestedStartDate?: string;
  requestedEndDate?: string;
  resolvedStartDate?: string;
  resolvedEndDate?: string;
  appliedDuration?: string;
};

function formatDateRangeLabel(start?: string, end?: string) {
  if (start && end) {
    return `${formatDate(start)} to ${formatDate(end)}`;
  }

  if (start) {
    return `From ${formatDate(start)}`;
  }

  if (end) {
    return `Until ${formatDate(end)}`;
  }

  return undefined;
}

export function buildRecommendationTimingLabel(item: RecommendationTimingLike) {
  const resolvedRange = formatDateRangeLabel(item.resolvedStartDate, item.resolvedEndDate);
  if (resolvedRange) {
    return resolvedRange;
  }

  const requestedRange = formatDateRangeLabel(item.requestedStartDate, item.requestedEndDate);
  if (requestedRange) {
    return requestedRange;
  }

  const legacyRange = formatDateRangeLabel(item.startDate, item.endDate);
  if (legacyRange) {
    return legacyRange;
  }

  return item.flighting ?? item.appliedDuration ?? item.duration ?? undefined;
}

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

export function titleCase(value: string) {
  return value
    .split('_')
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

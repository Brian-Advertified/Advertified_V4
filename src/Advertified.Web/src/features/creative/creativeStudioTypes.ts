import type { LucideIcon } from 'lucide-react';

export interface CreativeBookingDraft {
  supplierOrStation: string;
  channel: string;
  bookingStatus: string;
  committedAmount: string;
  liveFrom: string;
  liveTo: string;
  notes: string;
}

export interface CreativeStudioSignal {
  label: string;
  value: string;
  helper: string;
}

export interface CreativeStudioCollection {
  icon: LucideIcon;
  title: string;
  body: string;
  accent: string;
}

import type { Campaign, CampaignDeliveryReport, CampaignSupplierBooking } from '../../../types/domain';
import { formatChannelLabel, normalizeChannelKey } from '../../channels/channelUtils';

export type CampaignPerformanceMetricKey = 'impressions' | 'playsOrSpots' | 'spendDelivered';

export interface CampaignPerformanceTimelinePoint {
  date: string;
  value: number;
}

export interface CampaignPerformanceChannelSnapshot {
  channel: string;
  label: string;
  bookedSpend: number;
  deliveredSpend: number;
  impressions: number;
  playsOrSpots: number;
  reportCount: number;
  status: 'on_track' | 'live' | 'booked' | 'no_data';
}

export interface CampaignPerformanceSnapshot {
  totalBookedSpend: number;
  totalDeliveredSpend: number;
  totalImpressions: number;
  totalPlaysOrSpots: number;
  reportCount: number;
  bookingCount: number;
  spendDeliveryPercent: number;
  primaryMetric: CampaignPerformanceMetricKey;
  timeline: CampaignPerformanceTimelinePoint[];
  channels: CampaignPerformanceChannelSnapshot[];
}

function clampPercent(value: number) {
  if (!Number.isFinite(value) || value <= 0) {
    return 0;
  }

  if (value >= 100) {
    return 100;
  }

  return Math.round(value);
}

function createEmptyChannel(channel: string): CampaignPerformanceChannelSnapshot {
  return {
    channel,
    label: formatChannelLabel(channel),
    bookedSpend: 0,
    deliveredSpend: 0,
    impressions: 0,
    playsOrSpots: 0,
    reportCount: 0,
    status: 'no_data',
  };
}

function resolveChannelFromBooking(booking: CampaignSupplierBooking | undefined) {
  if (!booking?.channel) {
    return '';
  }

  const normalized = normalizeChannelKey(booking.channel);
  return normalized || booking.channel;
}

function resolveChannelFromReport(report: CampaignDeliveryReport, bookingById: Map<string, CampaignSupplierBooking>) {
  if (report.supplierBookingId) {
    const bookingChannel = resolveChannelFromBooking(bookingById.get(report.supplierBookingId));
    if (bookingChannel) {
      return bookingChannel;
    }
  }

  return '';
}

function resolvePrimaryMetric(totalImpressions: number, totalPlaysOrSpots: number): CampaignPerformanceMetricKey {
  if (totalImpressions > 0) {
    return 'impressions';
  }

  if (totalPlaysOrSpots > 0) {
    return 'playsOrSpots';
  }

  return 'spendDelivered';
}

function toTimelineValue(report: CampaignDeliveryReport, metric: CampaignPerformanceMetricKey) {
  if (metric === 'impressions') {
    return report.impressions ?? 0;
  }

  if (metric === 'playsOrSpots') {
    return report.playsOrSpots ?? 0;
  }

  return report.spendDelivered ?? 0;
}

function getChannelStatus(channel: CampaignPerformanceChannelSnapshot): CampaignPerformanceChannelSnapshot['status'] {
  if (channel.reportCount > 0 && (channel.deliveredSpend > 0 || channel.impressions > 0 || channel.playsOrSpots > 0)) {
    return 'on_track';
  }

  if (channel.reportCount > 0) {
    return 'live';
  }

  if (channel.bookedSpend > 0) {
    return 'booked';
  }

  return 'no_data';
}

export function buildCampaignPerformanceSnapshot(campaign: Campaign): CampaignPerformanceSnapshot {
  const bookingById = new Map(campaign.supplierBookings.map((booking) => [booking.id, booking]));
  const channels = new Map<string, CampaignPerformanceChannelSnapshot>();

  for (const booking of campaign.supplierBookings) {
    const channel = resolveChannelFromBooking(booking);
    if (!channel) {
      continue;
    }

    const current = channels.get(channel) ?? createEmptyChannel(channel);
    current.bookedSpend += booking.committedAmount ?? 0;
    channels.set(channel, current);
  }

  let totalDeliveredSpend = 0;
  let totalImpressions = 0;
  let totalPlaysOrSpots = 0;

  for (const report of campaign.deliveryReports) {
    totalDeliveredSpend += report.spendDelivered ?? 0;
    totalImpressions += report.impressions ?? 0;
    totalPlaysOrSpots += report.playsOrSpots ?? 0;

    const channel = resolveChannelFromReport(report, bookingById);
    if (!channel) {
      continue;
    }

    const current = channels.get(channel) ?? createEmptyChannel(channel);
    current.deliveredSpend += report.spendDelivered ?? 0;
    current.impressions += report.impressions ?? 0;
    current.playsOrSpots += report.playsOrSpots ?? 0;
    current.reportCount += 1;
    channels.set(channel, current);
  }

  const totalBookedSpend = campaign.supplierBookings.reduce((sum, booking) => sum + (booking.committedAmount ?? 0), 0);
  const primaryMetric = resolvePrimaryMetric(totalImpressions, totalPlaysOrSpots);

  const timeline = [...campaign.deliveryReports]
    .sort((left, right) => {
      const leftDate = Date.parse(left.reportedAt ?? '');
      const rightDate = Date.parse(right.reportedAt ?? '');
      return leftDate - rightDate;
    })
    .slice(-6)
    .map((report) => ({
      date: report.reportedAt ?? '',
      value: toTimelineValue(report, primaryMetric),
    }));

  const channelSnapshots = [...channels.values()]
    .map((channel) => ({
      ...channel,
      status: getChannelStatus(channel),
    }))
    .sort((left, right) => {
      const rightStrength = right.deliveredSpend + right.bookedSpend + right.impressions + right.playsOrSpots;
      const leftStrength = left.deliveredSpend + left.bookedSpend + left.impressions + left.playsOrSpots;
      return rightStrength - leftStrength;
    });

  return {
    totalBookedSpend,
    totalDeliveredSpend,
    totalImpressions,
    totalPlaysOrSpots,
    reportCount: campaign.deliveryReports.length,
    bookingCount: campaign.supplierBookings.length,
    spendDeliveryPercent: clampPercent(totalBookedSpend > 0 ? (totalDeliveredSpend / totalBookedSpend) * 100 : 0),
    primaryMetric,
    timeline,
    channels: channelSnapshots,
  };
}

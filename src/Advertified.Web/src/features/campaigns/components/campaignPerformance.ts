import type {
  Campaign,
  CampaignDeliveryReport,
  CampaignPerformanceSnapshot as DomainCampaignPerformanceSnapshot,
  CampaignSupplierBooking,
} from '../../../types/domain';
import { AD_PLATFORM_SYNC_REPORT_TYPE } from '../constants/performance';
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
  leads: number;
  cplZar?: number;
  roas?: number;
  activityLabel: string;
  reportCount: number;
  status: 'on_track' | 'live' | 'booked' | 'no_data';
}

export interface CampaignPerformanceSnapshot {
  totalBookedSpend: number;
  totalDeliveredSpend: number;
  totalImpressions: number;
  totalPlaysOrSpots: number;
  totalLeads: number;
  averageCplZar?: number;
  averageRoas?: number;
  totalSyncedClicks: number;
  reportCount: number;
  bookingCount: number;
  spendDeliveryPercent: number;
  primaryMetric: CampaignPerformanceMetricKey;
  timeline: CampaignPerformanceTimelinePoint[];
  channels: CampaignPerformanceChannelSnapshot[];
  latestReportDate?: string;
  topChannel?: CampaignPerformanceChannelSnapshot;
}

export interface CampaignPerformanceViewState {
  snapshot: CampaignPerformanceSnapshot;
  hasPerformanceView: boolean;
}

export function hasProjectedPerformanceData(snapshot: DomainCampaignPerformanceSnapshot | undefined) {
  if (!snapshot) {
    return false;
  }

  return snapshot.bookingCount > 0
    || snapshot.reportCount > 0
    || snapshot.totalBookedSpend > 0
    || snapshot.totalDeliveredSpend > 0
    || snapshot.totalImpressions > 0
    || snapshot.totalPlaysOrSpots > 0;
}

export function hasCampaignPerformanceData(campaign: Campaign) {
  return campaign.supplierBookings.length > 0
    || campaign.deliveryReports.length > 0
    || campaign.assets.length > 0
    || campaign.daysLeft != null;
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
    leads: 0,
    activityLabel: 'Plays / spots',
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

function resolveActivityLabel(
  channel: string,
  report: CampaignDeliveryReport | undefined,
  booking: CampaignSupplierBooking | undefined)
{
  if (report?.reportType === AD_PLATFORM_SYNC_REPORT_TYPE) {
    return 'Clicks';
  }

  const supplier = (booking?.supplierOrStation ?? '').toLowerCase();
  if (channel === 'DIGITAL' && (supplier.includes('meta ads') || supplier.includes('google ads') || supplier.includes('linkedin ads') || supplier.includes('tiktok ads'))) {
    return 'Clicks';
  }

  return 'Plays / spots';
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
    current.activityLabel = resolveActivityLabel(channel, undefined, booking);
    current.bookedSpend += booking.committedAmount ?? 0;
    channels.set(channel, current);
  }

  let totalDeliveredSpend = 0;
  let totalImpressions = 0;
  let totalPlaysOrSpots = 0;
  let totalLeads = 0;
  let totalSyncedClicks = 0;

  for (const report of campaign.deliveryReports) {
    totalDeliveredSpend += report.spendDelivered ?? 0;
    totalImpressions += report.impressions ?? 0;
    totalPlaysOrSpots += report.playsOrSpots ?? 0;
    if (report.reportType === AD_PLATFORM_SYNC_REPORT_TYPE) {
      totalSyncedClicks += report.playsOrSpots ?? 0;
    }

    const channel = resolveChannelFromReport(report, bookingById);
    if (!channel) {
      continue;
    }

    const current = channels.get(channel) ?? createEmptyChannel(channel);
    current.deliveredSpend += report.spendDelivered ?? 0;
    current.impressions += report.impressions ?? 0;
    current.playsOrSpots += report.playsOrSpots ?? 0;
    current.activityLabel = resolveActivityLabel(channel, report, bookingById.get(report.supplierBookingId ?? ''));
    current.reportCount += 1;
    channels.set(channel, current);
  }

  const totalBookedSpend = campaign.supplierBookings.reduce((sum, booking) => sum + (booking.committedAmount ?? 0), 0);
  const primaryMetric = resolvePrimaryMetric(totalImpressions, totalPlaysOrSpots);

  const performanceTimeline = campaign.performanceTimeline ?? [];

  const timeline = performanceTimeline.length > 0
    ? [...performanceTimeline]
      .sort((left, right) => Date.parse(left.date) - Date.parse(right.date))
      .slice(-8)
      .map((point) => ({
        date: point.date,
        value: primaryMetric === 'impressions'
          ? point.impressions
          : primaryMetric === 'playsOrSpots'
            ? point.playsOrSpots
            : point.spendDelivered,
      }))
    : [...campaign.deliveryReports]
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
    totalLeads,
    totalSyncedClicks,
    averageCplZar: undefined,
    averageRoas: undefined,
    reportCount: campaign.deliveryReports.length,
    bookingCount: campaign.supplierBookings.length,
    spendDeliveryPercent: clampPercent(totalBookedSpend > 0 ? (totalDeliveredSpend / totalBookedSpend) * 100 : 0),
    primaryMetric,
    timeline,
    channels: channelSnapshots,
    latestReportDate: [...campaign.deliveryReports]
      .map((report) => report.reportedAt)
      .filter((value): value is string => Boolean(value))
      .sort()
      .at(-1),
    topChannel: channelSnapshots[0],
  };
}

export function buildCampaignPerformanceSnapshotFromProjection(
  snapshot: DomainCampaignPerformanceSnapshot): CampaignPerformanceSnapshot
{
  const primaryMetric = resolvePrimaryMetric(snapshot.totalImpressions, snapshot.totalPlaysOrSpots);

  const timeline = [...snapshot.timeline]
    .sort((left, right) => Date.parse(left.date) - Date.parse(right.date))
    .slice(-8)
    .map((point) => ({
      date: point.date,
      value: primaryMetric === 'impressions'
        ? point.impressions
        : primaryMetric === 'playsOrSpots'
          ? point.playsOrSpots
          : point.spendDelivered,
    }));

  const channels = snapshot.channels
    .map((channel): CampaignPerformanceChannelSnapshot => {
      const status: CampaignPerformanceChannelSnapshot['status'] =
        channel.reportCount > 0 && (channel.deliveredSpend > 0 || channel.impressions > 0 || channel.playsOrSpots > 0)
          ? 'on_track'
          : channel.reportCount > 0
            ? 'live'
            : channel.bookedSpend > 0
              ? 'booked'
              : 'no_data';

      return {
        channel: channel.channel,
        label: channel.label,
        bookedSpend: channel.bookedSpend,
        deliveredSpend: channel.deliveredSpend,
        impressions: channel.impressions,
        playsOrSpots: channel.playsOrSpots,
        leads: channel.leads,
        cplZar: channel.cplZar ?? undefined,
        roas: channel.roas ?? undefined,
        activityLabel: channel.syncedClicks > 0 ? 'Clicks' : 'Plays / spots',
        reportCount: channel.reportCount,
        status,
      };
    })
    .sort((left, right) => {
      const rightStrength = right.deliveredSpend + right.bookedSpend + right.impressions + right.playsOrSpots;
      const leftStrength = left.deliveredSpend + left.bookedSpend + left.impressions + left.playsOrSpots;
      return rightStrength - leftStrength;
    });

  return {
    totalBookedSpend: snapshot.totalBookedSpend,
    totalDeliveredSpend: snapshot.totalDeliveredSpend,
    totalImpressions: snapshot.totalImpressions,
    totalPlaysOrSpots: snapshot.totalPlaysOrSpots,
    totalLeads: snapshot.totalLeads,
    averageCplZar: snapshot.averageCplZar,
    averageRoas: snapshot.averageRoas,
    totalSyncedClicks: snapshot.totalSyncedClicks,
    reportCount: snapshot.reportCount,
    bookingCount: snapshot.bookingCount,
    spendDeliveryPercent: snapshot.spendDeliveryPercent,
    primaryMetric,
    timeline,
    channels,
    latestReportDate: snapshot.latestReportDate,
    topChannel: channels[0],
  };
}

export function resolveCampaignPerformanceViewState(
  campaign: Campaign,
  projection?: DomainCampaignPerformanceSnapshot): CampaignPerformanceViewState
{
  const projectedSnapshot = projection
    ? buildCampaignPerformanceSnapshotFromProjection(projection)
    : undefined;

  return {
    snapshot: projectedSnapshot ?? buildCampaignPerformanceSnapshot(campaign),
    hasPerformanceView: hasProjectedPerformanceData(projection) || hasCampaignPerformanceData(campaign),
  };
}

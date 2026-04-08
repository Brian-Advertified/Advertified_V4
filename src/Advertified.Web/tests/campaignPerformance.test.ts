import { describe, expect, it } from 'vitest';
import { buildCampaignPerformanceSnapshot, hasCampaignPerformanceData } from '../src/features/campaigns/components/campaignPerformance';
import type { Campaign } from '../src/types/domain';

function buildCampaign(): Campaign {
  return {
    id: 'campaign-1',
    userId: 'user-1',
    packageOrderId: 'order-1',
    packageBandId: 'band-1',
    packageBandName: 'Scale',
    selectedBudget: 250000,
    paymentStatus: 'paid',
    status: 'launched',
    aiUnlocked: true,
    agentAssistanceRequested: false,
    campaignName: 'Retail Launch',
    nextAction: 'Next step',
    timeline: [],
    recommendations: [],
    creativeSystems: [],
    assets: [],
    supplierBookings: [
      {
        id: 'booking-radio',
        supplierOrStation: 'Ukhozi FM',
        channel: 'radio',
        bookingStatus: 'live',
        committedAmount: 50000,
      },
      {
        id: 'booking-ooh',
        supplierOrStation: 'Joburg North',
        channel: 'ooh',
        bookingStatus: 'booked',
        committedAmount: 120000,
      },
    ],
    deliveryReports: [
      {
        id: 'report-1',
        supplierBookingId: 'booking-radio',
        reportType: 'performance_snapshot',
        headline: 'Week 1',
        reportedAt: '2026-04-01T00:00:00Z',
        impressions: 250000,
        playsOrSpots: 42,
        spendDelivered: 20000,
      },
      {
        id: 'report-2',
        supplierBookingId: 'booking-ooh',
        reportType: 'performance_snapshot',
        headline: 'Week 2',
        reportedAt: '2026-04-06T00:00:00Z',
        impressions: 500000,
        spendDelivered: 45000,
      },
    ],
    createdAt: '2026-03-30T00:00:00Z',
  };
}

function buildDigitalCampaign(): Campaign {
  return {
    ...buildCampaign(),
    supplierBookings: [
      {
        id: 'booking-digital',
        supplierOrStation: 'Meta Ads',
        channel: 'digital',
        bookingStatus: 'live',
        committedAmount: 0,
        notes: 'System-managed ad platform performance sync.',
      },
    ],
    deliveryReports: [
      {
        id: 'report-digital',
        supplierBookingId: 'booking-digital',
        reportType: 'ad_platform_sync',
        headline: 'Meta Ads performance',
        reportedAt: '2026-04-07T00:00:00Z',
        impressions: 1200,
        playsOrSpots: 38,
        spendDelivered: 84,
      },
    ],
  };
}

describe('campaign performance snapshot', () => {
  it('summarizes booked and delivered performance by channel', () => {
    const snapshot = buildCampaignPerformanceSnapshot(buildCampaign());

    expect(snapshot.totalBookedSpend).toBe(170000);
    expect(snapshot.totalDeliveredSpend).toBe(65000);
    expect(snapshot.totalImpressions).toBe(750000);
    expect(snapshot.primaryMetric).toBe('impressions');
    expect(snapshot.channels.map((item) => item.channel)).toEqual(['OOH', 'RADIO']);
    expect(snapshot.channels[0]?.label).toBe('Billboards and Digital Screens');
    expect(snapshot.channels[0]?.deliveredSpend).toBe(45000);
    expect(snapshot.channels[1]?.playsOrSpots).toBe(42);
    expect(snapshot.topChannel?.channel).toBe('OOH');
    expect(snapshot.latestReportDate).toBe('2026-04-06T00:00:00Z');
  });

  it('detects when a campaign has performance-facing data', () => {
    expect(hasCampaignPerformanceData(buildCampaign())).toBe(true);

    expect(hasCampaignPerformanceData({
      ...buildCampaign(),
      supplierBookings: [],
      deliveryReports: [],
      assets: [],
      daysLeft: undefined,
    })).toBe(false);
  });

  it('labels synced ad-platform activity as clicks', () => {
    const snapshot = buildCampaignPerformanceSnapshot(buildDigitalCampaign());

    expect(snapshot.channels).toHaveLength(1);
    expect(snapshot.channels[0]?.channel).toBe('DIGITAL');
    expect(snapshot.channels[0]?.activityLabel).toBe('Clicks');
    expect(snapshot.channels[0]?.playsOrSpots).toBe(38);
    expect(snapshot.totalSyncedClicks).toBe(38);
  });
});

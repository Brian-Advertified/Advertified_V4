import { describe, expect, it } from 'vitest';
import { buildCampaignPerformanceSnapshot } from '../src/features/campaigns/components/campaignPerformance';
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
  });
});

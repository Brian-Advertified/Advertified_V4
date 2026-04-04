import { type ReactNode } from 'react';
import type { useAgentCampaignsQuery, useAgentInboxQuery } from './agentWorkspace';

export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? 'http://localhost:5050';

export function ActionIconButton({
  title,
  onClick,
  tone = 'default',
  children,
  disabled = false,
}: {
  title: string;
  onClick?: () => void;
  tone?: 'default' | 'danger';
  children: ReactNode;
  disabled?: boolean;
}) {
  const className = tone === 'danger'
    ? 'rounded-full border border-rose-200 bg-white p-2 text-rose-600 transition hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-50'
    : 'button-secondary p-2 disabled:cursor-not-allowed disabled:opacity-50';

  return (
    <button type="button" className={className} onClick={onClick} title={title} disabled={disabled}>
      {children}
    </button>
  );
}

export function buildClientRows(campaigns: Awaited<ReturnType<typeof useAgentCampaignsQuery>['data']>, search: string) {
  const grouped = new Map<string, {
    userId: string;
    clientName: string;
    clientEmail: string;
    campaignCount: number;
    activeCount: number;
    awaitingApprovalCount: number;
    latestActivityAt?: string;
    latestActivity: string;
    topRegion: string;
    topPackage: string;
    latestCampaignId?: string;
  }>();

  for (const campaign of campaigns ?? []) {
    const hasApprovedRecommendation = campaign.recommendations.some((item) => item.status === 'approved');
    const hasPendingClientDecision = !hasApprovedRecommendation && campaign.recommendations.some((item) => item.status === 'sent_to_client');
    const current = grouped.get(campaign.userId) ?? {
      userId: campaign.userId,
      clientName: campaign.clientName ?? campaign.businessName ?? 'Client account',
      clientEmail: campaign.clientEmail ?? 'No email captured',
      campaignCount: 0,
      activeCount: 0,
      awaitingApprovalCount: 0,
      latestActivityAt: campaign.createdAt,
      latestActivity: campaign.nextAction,
      topRegion: campaign.brief?.provinces?.[0] ?? campaign.brief?.areas?.[0] ?? 'Not set',
      topPackage: campaign.packageBandName,
      latestCampaignId: campaign.id,
    };

    current.campaignCount += 1;
    if (campaign.status !== 'approved') current.activeCount += 1;
    if (hasPendingClientDecision) current.awaitingApprovalCount += 1;
    if (!current.latestActivityAt || new Date(campaign.createdAt).getTime() > new Date(current.latestActivityAt).getTime()) {
      current.latestActivityAt = campaign.createdAt;
      current.latestActivity = campaign.nextAction;
      current.topRegion = campaign.brief?.provinces?.[0] ?? campaign.brief?.areas?.[0] ?? current.topRegion;
      current.topPackage = campaign.packageBandName;
      current.latestCampaignId = campaign.id;
    }

    grouped.set(campaign.userId, current);
  }

  return Array.from(grouped.values())
    .filter((item) => `${item.clientName} ${item.clientEmail} ${item.topRegion} ${item.topPackage}`.toLowerCase().includes(search.toLowerCase()))
    .sort((left, right) => (right.awaitingApprovalCount - left.awaitingApprovalCount) || (right.activeCount - left.activeCount) || left.clientName.localeCompare(right.clientName));
}

export function getAverageTurnaroundDays(campaigns: Awaited<ReturnType<typeof useAgentCampaignsQuery>['data']>) {
  const completed = (campaigns ?? []).filter((campaign) => campaign.recommendations.some((item) => item.status === 'approved'));
  if (completed.length === 0) return null;
  const totalDays = completed.reduce((sum, campaign) => {
    const createdAt = new Date(campaign.createdAt).getTime();
    const approvedAt = campaign.timeline.find((step) => step.key === 'approval' && step.state === 'complete') ? Date.now() : createdAt;
    return sum + Math.max(0, (approvedAt - createdAt) / (1000 * 60 * 60 * 24));
  }, 0);
  return (totalDays / completed.length).toFixed(1);
}

export function buildTasks(inbox: NonNullable<ReturnType<typeof useAgentInboxQuery>['data']>) {
  return {
    urgent: inbox.items.filter((item) => item.isUrgent).slice(0, 5),
    review: inbox.items.filter((item) => item.queueStage === 'agent_review' || item.queueStage === 'ready_to_send').slice(0, 5),
    waiting: inbox.items.filter((item) => item.queueStage === 'waiting_on_client').slice(0, 5),
  };
}

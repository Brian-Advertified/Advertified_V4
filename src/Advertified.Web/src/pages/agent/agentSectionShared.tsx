import { ArrowRight } from 'lucide-react';
import { type ReactNode } from 'react';
import { Link } from 'react-router-dom';
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

export function AgentSummaryCard({
  label,
  value,
  helper,
  href,
  actionLabel = 'Open',
}: {
  label: string;
  value: string | number;
  helper?: string;
  href?: string;
  actionLabel?: string;
}) {
  const content = (
    <>
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">{label}</p>
      <p className="mt-3 text-3xl font-semibold text-ink">{value}</p>
      {helper ? <p className="mt-2 text-sm text-ink-soft">{helper}</p> : null}
      {href ? (
        <div className="mt-4 inline-flex items-center gap-2 text-sm font-semibold text-brand">
          {actionLabel}
          <ArrowRight className="size-4 transition-transform group-hover:translate-x-0.5 group-focus-visible:translate-x-0.5" />
        </div>
      ) : null}
    </>
  );

  if (!href) {
    return (
      <div className="rounded-[22px] border border-line bg-white px-5 py-4">
        {content}
      </div>
    );
  }

  return (
    <Link
      to={href}
      className="group block rounded-[22px] border border-line bg-white px-5 py-4 transition hover:-translate-y-0.5 hover:border-brand/30 hover:shadow-[0_18px_40px_rgba(31,143,99,0.08)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand/30"
    >
      {content}
    </Link>
  );
}

export function AgentSectionIntro({
  title,
  description,
  action,
}: {
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <h3 className="text-xl font-semibold text-ink">{title}</h3>
        <p className="mt-1 text-sm text-ink-soft">{description}</p>
      </div>
      {action ? <div className="shrink-0">{action}</div> : null}
    </div>
  );
}

export function AgentPageLead({
  eyebrow,
  title,
  description,
  aside,
}: {
  eyebrow: string;
  title: string;
  description: string;
  aside?: ReactNode;
}) {
  return (
    <div className="rounded-[28px] border border-line bg-[linear-gradient(180deg,#ffffff_0%,#f6faf8_100%)] p-6">
      <div className="flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
        <div className="max-w-3xl">
          <p className="text-[11px] font-semibold uppercase tracking-[0.24em] text-brand">{eyebrow}</p>
          <h3 className="mt-3 text-2xl font-semibold text-ink">{title}</h3>
          <p className="mt-2 text-sm leading-6 text-ink-soft">{description}</p>
        </div>
        {aside ? <div className="xl:min-w-[280px]">{aside}</div> : null}
      </div>
    </div>
  );
}

export type AgentDetailItem = {
  label: string;
  value: ReactNode;
};

export function AgentInsightCard({
  eyebrow,
  title,
  description,
  tone = 'default',
  children,
}: {
  eyebrow: string;
  title?: string;
  description?: string;
  tone?: 'default' | 'soft' | 'mint';
  children?: ReactNode;
}) {
  const toneClassName = tone === 'mint'
    ? 'hero-mint text-ink'
    : tone === 'soft'
      ? 'border border-brand/15 bg-brand-soft/30'
      : 'border border-line bg-white';

  return (
    <div className={`rounded-[22px] px-4 py-4 ${toneClassName}`}>
      <p className="text-xs uppercase tracking-[0.18em] text-ink-soft">{eyebrow}</p>
      {title ? <h3 className="mt-2 text-lg font-semibold text-ink">{title}</h3> : null}
      {description ? <p className="mt-2 text-sm text-ink-soft">{description}</p> : null}
      {children ? <div className="mt-3">{children}</div> : null}
    </div>
  );
}

export function AgentDetailGrid({
  items,
  columns = 2,
}: {
  items: AgentDetailItem[];
  columns?: 1 | 2;
}) {
  return (
    <div className={`grid gap-3 text-sm ${columns === 2 ? 'md:grid-cols-2' : ''}`}>
      {items.map((item) => (
        <div key={item.label}>
          <p className="text-ink-soft">{item.label}</p>
          <div className="font-medium text-ink">{item.value}</div>
        </div>
      ))}
    </div>
  );
}

export function AgentDetailStack({
  items,
}: {
  items: AgentDetailItem[];
}) {
  return (
    <div className="space-y-3 text-sm text-ink-soft">
      {items.map((item, index) => (
        <div
          key={item.label}
          className={index < items.length - 1 ? 'flex items-center justify-between border-b border-brand/10 pb-3' : 'flex items-center justify-between'}
        >
          <span>{item.label}</span>
          <span className="font-medium text-ink">{item.value}</span>
        </div>
      ))}
    </div>
  );
}

export function AgentBulletCard({
  eyebrow,
  items,
}: {
  eyebrow: string;
  items: ReactNode[];
}) {
  return (
    <AgentInsightCard eyebrow={eyebrow}>
      <ul className="space-y-2 text-sm text-ink-soft">
        {items.map((item, index) => (
          <li key={`${eyebrow}-${index}`}>{item}</li>
        ))}
      </ul>
    </AgentInsightCard>
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
    const ownerKey = campaign.userId ?? campaign.clientEmail ?? campaign.id;
    const hasApprovedRecommendation = campaign.recommendations.some((item) => item.status === 'approved');
    const hasPendingClientDecision = campaign.status === 'creative_sent_to_client_for_approval'
      || (!hasApprovedRecommendation && campaign.recommendations.some((item) => item.status === 'sent_to_client'));
    const current = grouped.get(ownerKey) ?? {
      userId: ownerKey,
      clientName: campaign.clientName ?? campaign.businessName ?? campaign.campaignName ?? 'Client',
      clientEmail: campaign.clientEmail ?? campaign.businessName ?? 'Email not available',
      campaignCount: 0,
      activeCount: 0,
      awaitingApprovalCount: 0,
      latestActivityAt: campaign.createdAt,
      latestActivity: campaign.nextAction,
      topRegion: campaign.brief?.provinces?.[0] ?? campaign.brief?.areas?.[0] ?? 'Not set',
      topPackage: campaign.packageBandName || campaign.campaignName || 'Campaign',
      latestCampaignId: campaign.id,
    };

    current.campaignCount += 1;
    if (campaign.status !== 'launched') current.activeCount += 1;
    if (hasPendingClientDecision) current.awaitingApprovalCount += 1;
    if (!current.latestActivityAt || new Date(campaign.createdAt).getTime() > new Date(current.latestActivityAt).getTime()) {
      current.latestActivityAt = campaign.createdAt;
      current.latestActivity = campaign.nextAction;
      current.topRegion = campaign.brief?.provinces?.[0] ?? campaign.brief?.areas?.[0] ?? current.topRegion;
      current.topPackage = campaign.packageBandName || campaign.campaignName || current.topPackage;
      current.latestCampaignId = campaign.id;
    }

    grouped.set(ownerKey, current);
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
    review: inbox.items.filter((item) => item.queueStage === 'agent_review' || item.manualReviewRequired).slice(0, 5),
    waiting: inbox.items.filter((item) => item.queueStage === 'waiting_on_client').slice(0, 5),
  };
}

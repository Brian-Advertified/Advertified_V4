import { CheckCircle2, Circle, Clock3 } from 'lucide-react';
import type { PropsWithChildren, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { hasCampaignClearedPayment } from '../../lib/access';
import { buildBriefCoverageSummary } from '../../features/campaigns/briefModel';
import {
  CAMPAIGN_STATUSES_AFTER_CREATIVE_APPROVAL,
  getPrimaryRecommendation,
  hasRecommendationApprovalCompleted,
  isCampaignInSet,
} from '../../lib/campaignStatus';
import { cn, formatCurrency, formatDate, titleCase } from '../../lib/utils';
import type { Campaign, CampaignRecommendation, PackageBand, PackageOrder } from '../../types/domain';

function formatChannelLabel(value: string) {
  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}

export function getClientFacingBudget(campaign: Campaign) {
  const recommendation = getPrimaryRecommendation(campaign);
  if (!hasCampaignClearedPayment(campaign) && recommendation?.status === 'sent_to_client') {
    return recommendation.totalCost;
  }

  return campaign.selectedBudget;
}

export function getCampaignProgressPercent(campaign: Campaign) {
  if (campaign.timeline.length > 0) {
    const completed = campaign.timeline.filter((step) => step.state === 'complete').length;
    return Math.round((completed / campaign.timeline.length) * 100);
  }

  switch (campaign.status) {
    case 'paid':
      return 20;
    case 'brief_in_progress':
      return 35;
    case 'brief_submitted':
      return 50;
    case 'planning_in_progress':
      return 70;
    case 'review_ready':
      return 85;
    case 'approved':
      return 92;
    case 'creative_changes_requested':
      return 94;
    case 'creative_sent_to_client_for_approval':
      return 96;
    case 'creative_approved':
      return 97;
    case 'booking_in_progress':
      return 98;
    case 'launched':
      return 100;
    default:
      return 0;
  }
}

export function getCampaignQuickSteps(campaign: Campaign) {
  const recommendation = getPrimaryRecommendation(campaign);
  const recommendationApproved = hasRecommendationApprovalCompleted(campaign.status, recommendation);
  const creativeInRevision = campaign.status === 'creative_changes_requested';
  const creativeSentForApproval = campaign.status === 'creative_sent_to_client_for_approval';
  const creativeApproved = isCampaignInSet(campaign.status, CAMPAIGN_STATUSES_AFTER_CREATIVE_APPROVAL);
  const bookingInProgress = campaign.status === 'booking_in_progress';
  const campaignLive = campaign.status === 'launched';
  const creativeProductionStarted = recommendationApproved && !creativeSentForApproval && !creativeApproved;

  return [
    {
      key: 'purchase',
      label: 'Package purchased',
      description: 'Payment completed and campaign shell created.',
      state: 'complete' as const,
    },
    {
      key: 'recommendation',
      label: recommendationApproved ? 'Recommendation approved' : 'Approve placement recommendation',
      description: recommendation
        ? 'Confirm the channels and placements in your live plan.'
        : 'A recommendation will appear here once planning completes.',
      state: recommendationApproved
        ? 'complete' as const
        : recommendation
          ? 'current' as const
          : 'upcoming' as const,
    },
    {
      key: 'creative-production',
      label: creativeInRevision ? 'Creative revision in progress' : 'Creative production',
      description: creativeInRevision
        ? 'Advertified is revising the creative after your feedback.'
        : 'Approved recommendations move into creative director production.',
      state: creativeSentForApproval || creativeApproved
        ? 'complete' as const
        : creativeProductionStarted
          ? 'current' as const
          : 'upcoming' as const,
    },
    {
      key: 'creative-review',
      label: 'Approve content',
      description: 'Approve the finished campaign content before booking starts.',
      state: creativeApproved
        ? 'complete' as const
        : creativeSentForApproval
          ? 'current' as const
          : 'upcoming' as const,
    },
    {
      key: 'launch',
      label: 'Supplier booking',
      description: 'Advertified is confirming placements and supplier availability before launch.',
      state: campaignLive
        ? 'complete' as const
        : bookingInProgress || creativeApproved
          ? 'current' as const
          : 'upcoming' as const,
    },
    {
      key: 'live',
      label: 'Campaign live',
      description: 'Operations has activated the campaign and it is now live.',
      state: campaignLive ? 'complete' as const : 'upcoming' as const,
    },
  ];
}

export function buildPackageSummary(campaign: Campaign, order?: PackageOrder, packageBand?: PackageBand) {
  const recommendation = getPrimaryRecommendation(campaign);
  const recommendedChannels = Array.from(new Set(recommendation?.items.map((item) => item.channel) ?? []));

  return [
    { label: 'Package', value: campaign.packageBandName },
    { label: 'Budget', value: formatCurrency(getClientFacingBudget(campaign)) },
    { label: 'Coverage', value: buildBriefCoverageSummary(campaign.brief) },
    { label: 'Objective', value: campaign.brief?.objective || packageBand?.packagePurpose || 'Not added yet' },
    { label: 'Included channels', value: recommendedChannels[0] ? recommendedChannels.map(formatChannelLabel).join(', ') : 'Recommendation not ready yet' },
    { label: 'Payment status', value: titleCase(order?.paymentStatus ?? 'paid') },
  ];
}

export function buildRecommendationRows(campaign: Campaign) {
  const recommendation = getPrimaryRecommendation(campaign);

  return (recommendation?.items ?? []).map((item) => ({
    channel: formatChannelLabel(item.channel),
    placement: item.title,
    flight: item.flighting ?? item.duration ?? item.startDate ?? 'In current plan',
    reason: item.selectionReasons[0] ?? item.rationale,
  }));
}

export function buildCreativeProgressRows(campaign: Campaign) {
  const recommendation = getPrimaryRecommendation(campaign);
  const status = recommendation?.status ?? campaign.status;

  return (recommendation?.items ?? []).map((item) => ({
    format: item.title,
    status: titleCase(status),
    source: formatChannelLabel(item.channel),
    nextStep: campaign.status === 'creative_sent_to_client_for_approval' ? 'Approve content' : campaign.nextAction,
  }));
}

export function ClientCampaignShell({
  campaign,
  title,
  description,
  children,
  actions,
  activeView = 'overview',
}: PropsWithChildren<{
  campaign: Campaign;
  title: string;
  description: string;
  actions?: ReactNode;
  activeView?: 'overview' | 'approvals' | 'messages';
}>) {
  const workspaceBasePath = `/campaigns/${campaign.id}`;

  return (
    <section className="page-shell">
      <div className="user-portal-layout">
        <aside className="user-portal-sidebar">
          <div className="user-nav-group">
            <div className="user-nav-title">Workspace</div>
            <Link to={`${workspaceBasePath}/overview`} className={cn('user-nav-item', activeView === 'overview' && 'active')}>Overview</Link>
            <Link to={`${workspaceBasePath}/approvals`} className={cn('user-nav-item', activeView === 'approvals' && 'active')}>Approvals</Link>
            <Link to={`${workspaceBasePath}/messages`} className={cn('user-nav-item', activeView === 'messages' && 'active')}>Messages</Link>
          </div>
          <div className="user-nav-group">
            <div className="user-nav-title">Help</div>
            <Link to={`${workspaceBasePath}/messages`} className="user-nav-item">Ask your agent</Link>
          </div>
          <Link to="/dashboard" className="user-nav-item">
            Back to Client Portal
          </Link>
        </aside>

        <main className="user-portal-content">
          <div className="user-page">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
              <div>
                <h2>{title}</h2>
                <p className="user-subtitle">{description}</p>
              </div>
              <div className="flex flex-wrap gap-2">
                <span className="user-pill">{campaign.campaignName}</span>
                <span className="user-pill">{campaign.packageBandName}</span>
                <span className="user-pill">{formatCurrency(getClientFacingBudget(campaign))}</span>
              </div>
            </div>
            {children}
            {actions ? <div className="mt-4">{actions}</div> : null}
          </div>
        </main>
      </div>
    </section>
  );
}

export function ClientPortalShell({
  campaigns,
  title,
  description,
  activeNav = 'dashboard',
  children,
}: PropsWithChildren<{
  campaigns: Campaign[];
  title: string;
  description: string;
  activeNav?: 'dashboard' | 'orders';
}>) {
  return (
    <section className="page-shell">
      <div className="user-portal-layout">
        <aside className="user-portal-sidebar">
          <div className="user-nav-group">
            <div className="user-nav-title">Overview</div>
            <Link to="/dashboard" className={cn('user-nav-item', activeNav === 'dashboard' && 'active')}>
              Dashboard
            </Link>
            <Link to="/orders" className={cn('user-nav-item', activeNav === 'orders' && 'active')}>
              Orders
            </Link>
          </div>
          <div className="user-nav-group">
            <div className="user-nav-title">Campaigns</div>
            {campaigns.map((campaign) => (
              <Link key={campaign.id} to={`/campaigns/${campaign.id}`} className="user-nav-item">
                {campaign.campaignName}
              </Link>
            ))}
          </div>
          <div className="user-nav-group">
            <div className="user-nav-title">Explore</div>
            <Link to="/packages" className="user-nav-item">Packages</Link>
          </div>
        </aside>

        <main className="user-portal-content">
          <div className="user-page">
            <h2>{title}</h2>
            <p className="user-subtitle">{description}</p>
            {children}
          </div>
        </main>
      </div>
    </section>
  );
}

export function WireBanner({ children }: PropsWithChildren) {
  return <div className="user-banner">{children}</div>;
}

export function WireCard({
  title,
  children,
}: PropsWithChildren<{
  title: string;
}>) {
  return (
    <div className="user-card">
      <h3>{title}</h3>
      {children}
    </div>
  );
}

export function MetricGrid({ items }: { items: Array<{ label: string; value: string; helper: string }> }) {
  return (
    <div className="user-grid-4">
      {items.map((item) => (
        <WireCard key={item.label} title={item.label}>
          <div className="user-metric">{item.value}</div>
          <div className="user-muted">{item.helper}</div>
        </WireCard>
      ))}
    </div>
  );
}

export function InfoTable({
  rows,
  columns,
}: {
  columns: string[];
  rows: string[][];
}) {
  return (
    <table className="user-table">
      <thead>
        <tr>
          {columns.map((column) => <th key={column}>{column}</th>)}
        </tr>
      </thead>
      <tbody>
        {rows.map((row, index) => (
          <tr key={`${row[0]}-${index}`}>
            {row.map((value, valueIndex) => <td key={`${value}-${valueIndex}`}>{value}</td>)}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export function StepList({ campaign }: { campaign: Campaign }) {
  return (
    <div className="user-timeline">
      {getCampaignQuickSteps(campaign).map((step, index) => (
        <div key={step.key} className="user-step">
          <div className="user-dot">{index + 1}</div>
          <div>
            <strong>{step.label}</strong>
            <div className="user-muted">{step.description}</div>
          </div>
          <span className={cn('user-status', step.state === 'complete' && 'soft', step.state === 'current' && 'accent')}>
            {step.state === 'complete' ? 'Done' : step.state === 'current' ? 'Needs action' : 'Upcoming'}
          </span>
        </div>
      ))}
    </div>
  );
}

export function CompactStatusList({ items }: { items: Array<{ label: string; value: string }> }) {
  return (
    <div className="space-y-3">
      {items.map((item) => (
        <div key={item.label} className="user-wire">
          <strong>{item.label}</strong>
          <div>{item.value}</div>
        </div>
      ))}
    </div>
  );
}

export function DataGrid({ items }: { items: Array<{ label: string; value: string }> }) {
  return (
    <div className="user-grid-2">
      {items.map((item) => (
        <div key={item.label} className="user-card">
          <h3>{item.label}</h3>
          <div className="text-sm font-semibold text-ink">{item.value}</div>
        </div>
      ))}
    </div>
  );
}

export function RecommendationMetrics({
  campaign,
  recommendation,
}: {
  campaign: Campaign;
  recommendation: CampaignRecommendation;
}) {
  const withinBudget = recommendation.totalCost <= campaign.selectedBudget;

  return (
    <MetricGrid
      items={[
        { label: 'Package Status', value: 'Purchased', helper: 'Your package is active and ready for planning.' },
        { label: 'Recommendation', value: titleCase(recommendation.status), helper: 'Live recommendation status from the campaign.' },
        { label: 'Creative Status', value: campaign.planningMode ? titleCase(campaign.planningMode) : 'Not started', helper: 'Current path selected for this campaign.' },
        { label: 'Launch Readiness', value: `${getCampaignProgressPercent(campaign)}%`, helper: withinBudget ? 'Current recommendation fits within budget.' : 'Recommendation still needs budget review.' },
      ]}
    />
  );
}

export function StatusKanban({ campaign }: { campaign: Campaign }) {
  const recommendation = getPrimaryRecommendation(campaign);
  const items = [
    {
      title: 'Package Purchased',
      lines: [`Package: ${campaign.packageBandName}`, `Budget: ${formatCurrency(getClientFacingBudget(campaign))}`, `Campaign opened: ${formatDate(campaign.createdAt)}`],
    },
    {
      title: 'Recommendation Approved',
      lines: recommendation ? [`Status: ${titleCase(recommendation.status)}`, `Lines: ${recommendation.items.length}`, `Source: ${recommendation.proposalLabel ?? 'Current plan'}`] : ['Recommendation not ready yet'],
    },
    {
      title: 'Creative In Progress',
      lines: [`Path: ${campaign.planningMode ? titleCase(campaign.planningMode) : 'Not selected yet'}`, `Brief: ${campaign.brief ? 'Submitted' : 'Not submitted'}`, `Next: ${campaign.nextAction}`],
    },
    {
      title: 'Approved for Launch',
      lines: campaign.status === 'launched'
        ? ['Campaign is live', 'Operations activation has been completed']
        : campaign.status === 'booking_in_progress'
          ? ['Supplier booking is in progress', 'Placements and live dates are being confirmed']
        : campaign.status === 'creative_approved'
          ? ['Final creative approved', 'Supplier booking starts next']
        : campaign.status === 'creative_sent_to_client_for_approval'
            ? ['Content sent to client', 'Waiting for content approval']
            : campaign.status === 'approved'
              ? ['Creative production is underway', 'Finished media has not been sent yet']
              : ['Waiting for approval before launch handoff'],
    },
  ];

  return (
    <div className="user-kanban">
      {items.map((column) => (
        <div key={column.title} className="user-column">
          <h4>{column.title}</h4>
          {column.lines.map((line) => (
            <div key={line} className="user-ticket">{line}</div>
          ))}
        </div>
      ))}
    </div>
  );
}

export function StepIcon({ state }: { state: 'complete' | 'current' | 'upcoming' }) {
  if (state === 'complete') {
    return <CheckCircle2 className="size-5 text-emerald-600" />;
  }

  if (state === 'current') {
    return <Clock3 className="size-5 text-sky-600" />;
  }

  return <Circle className="size-5 text-slate-300" />;
}


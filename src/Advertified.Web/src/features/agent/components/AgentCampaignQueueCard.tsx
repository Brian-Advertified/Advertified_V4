import { ArrowRight, Eye, Pencil, UserPlus2, UserX2, WalletCards } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { AgentInboxItem } from '../../../types/domain';
import { buildAgentCampaignQueueHref, buildAgentMessagesHref, buildQueueFiltersForInboxItem } from '../../../pages/agent/agentCampaignQueueFilters';
import { ActionIconButton } from '../../../pages/agent/agentSectionShared';
import { fmtCurrency, queueTone, titleize } from '../../../pages/agent/agentWorkspace';

function formatBuildSource(planningMode?: 'ai_assisted' | 'agent_assisted' | 'hybrid') {
  switch (planningMode) {
    case 'ai_assisted':
      return 'AI draft';
    case 'agent_assisted':
      return 'Agent built';
    case 'hybrid':
      return 'Hybrid';
    default:
      return 'Not selected';
  }
}

function getOwnershipLabel(campaign: AgentInboxItem) {
  if (campaign.isAssignedToCurrentUser) {
    return 'Assigned to me';
  }

  if (campaign.isUnassigned) {
    return 'Unassigned';
  }

  return `Assigned to ${campaign.assignedAgentName ?? 'another agent'}`;
}

export function getPrimaryCampaignAction(campaign: AgentInboxItem) {
  if (campaign.status === 'awaiting_purchase') {
    return {
      label: 'Open prospect',
      href: `/agent/campaigns/${campaign.id}`,
    };
  }

  if (campaign.queueStage === 'waiting_on_client') {
    return {
      label: 'View client reply',
      href: `/agent/campaigns/${campaign.id}`,
    };
  }

  if (campaign.queueStage === 'brief_waiting') {
    return {
      label: 'Check brief',
      href: `/agent/campaigns/${campaign.id}`,
    };
  }

  if (campaign.queueStage === 'planning_ready' || campaign.queueStage === 'agent_review') {
    return {
      label: 'Work on recommendation',
      href: `/agent/recommendations/new?campaignId=${campaign.id}`,
    };
  }

  return {
    label: 'Open campaign',
    href: `/agent/campaigns/${campaign.id}`,
  };
}

export function AgentCampaignQueueCard({
  campaign,
  onAssign,
  onUnassign,
  onConvertToSale,
  assignDisabled,
  unassignDisabled,
  convertDisabled,
}: {
  campaign: AgentInboxItem;
  onAssign: (campaignId: string) => void;
  onUnassign: (campaignId: string) => void;
  onConvertToSale: (campaignId: string) => void;
  assignDisabled: boolean;
  unassignDisabled: boolean;
  convertDisabled: boolean;
}) {
  const primaryAction = getPrimaryCampaignAction(campaign);

  return (
    <article className="rounded-[24px] border border-line bg-white p-5 shadow-[0_18px_40px_rgba(17,24,39,0.04)]">
      <div className="flex flex-col gap-5 xl:flex-row xl:items-start xl:justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <Link
              to={buildAgentCampaignQueueHref(buildQueueFiltersForInboxItem(campaign))}
              className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold transition hover:-translate-y-0.5 ${queueTone(campaign.queueStage)}`}
            >
              {campaign.queueLabel}
            </Link>
            <span className="pill bg-slate-50 text-ink-soft">{getOwnershipLabel(campaign)}</span>
            {campaign.isUrgent ? (
              <Link to={buildAgentCampaignQueueHref({ stage: 'all', ownership: 'all', focus: 'urgent' })} className="pill border-rose-200 bg-rose-50 text-rose-700 transition hover:border-rose-300 hover:bg-rose-100">
                Urgent
              </Link>
            ) : null}
            {campaign.manualReviewRequired ? (
              <Link to={buildAgentCampaignQueueHref({ stage: 'ready_to_work', ownership: 'all', focus: 'needs_review' })} className="pill border-amber-200 bg-amber-50 text-amber-700 transition hover:border-amber-300 hover:bg-amber-100">
                Manual review
              </Link>
            ) : null}
            {campaign.isOverBudget ? (
              <Link to={buildAgentCampaignQueueHref({ stage: 'all', ownership: 'all', focus: 'budget_issues' })} className="pill border-rose-200 bg-rose-50 text-rose-700 transition hover:border-rose-300 hover:bg-rose-100">
                Over budget
              </Link>
            ) : null}
          </div>

          <h3 className="mt-3 text-xl font-semibold text-ink">{campaign.campaignName}</h3>
          <p className="mt-1 text-sm text-ink-soft">{campaign.clientName} | {campaign.clientEmail}</p>

          <div className="mt-4 grid gap-3 lg:grid-cols-2">
            <div className="rounded-[20px] border border-line bg-slate-50/75 px-4 py-4">
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Action needed</p>
              <p className="mt-2 text-sm leading-6 text-ink">{campaign.nextAction}</p>
              <p className="mt-3 text-xs text-ink-soft">
                Status: {titleize(campaign.status)} | Build style: {formatBuildSource(campaign.planningMode)}
              </p>
            </div>

            <div className="rounded-[20px] border border-line bg-slate-50/75 px-4 py-4">
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Campaign context</p>
              <p className="mt-2 text-sm font-semibold text-ink">{campaign.packageBandName}</p>
              <p className="mt-1 text-sm text-ink-soft">{fmtCurrency(campaign.selectedBudget)}</p>
              <div className="mt-3 flex flex-wrap gap-2 text-xs text-ink-soft">
                <span className="pill bg-white text-ink-soft">{campaign.hasBrief ? 'Brief ready' : 'Brief missing'}</span>
                <span className="pill bg-white text-ink-soft">{campaign.hasRecommendation ? 'Recommendation started' : 'No recommendation yet'}</span>
                {campaign.isStale ? <span className="pill border-amber-200 bg-amber-50 text-amber-700">Stale for {campaign.ageInDays} days</span> : null}
              </div>
            </div>
          </div>
        </div>

        <div className="flex w-full shrink-0 flex-col gap-3 xl:w-[230px]">
          <Link to={primaryAction.href} className="button-primary inline-flex items-center justify-center gap-2 px-4 py-3 text-sm font-semibold">
            {primaryAction.label}
            <ArrowRight className="size-4" />
          </Link>
          <Link to={`/agent/campaigns/${campaign.id}`} className="button-secondary inline-flex items-center justify-center gap-2 px-4 py-3 text-sm font-semibold">
            View campaign
            <Eye className="size-4" />
          </Link>
          <Link to={buildAgentMessagesHref(campaign.id)} className="button-secondary inline-flex items-center justify-center px-4 py-3 text-sm font-semibold">
            Message client
          </Link>

          <div className="rounded-[18px] border border-line bg-slate-50/75 px-3 py-3">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Queue actions</p>
            <div className="mt-3 flex flex-wrap gap-2">
              <Link to={`/agent/recommendations/new?campaignId=${campaign.id}`} className="button-secondary p-2" title={`Open recommendation for ${campaign.campaignName}`}>
                <Pencil className="size-4" />
              </Link>
              {campaign.status === 'awaiting_purchase' && campaign.isAssignedToCurrentUser ? (
                <ActionIconButton title={`Convert ${campaign.campaignName} to sale`} onClick={() => onConvertToSale(campaign.id)} disabled={convertDisabled}>
                  <WalletCards className="size-4" />
                </ActionIconButton>
              ) : null}
              {campaign.isAssignedToCurrentUser ? (
                <ActionIconButton title={`Unassign ${campaign.campaignName}`} onClick={() => onUnassign(campaign.id)} disabled={unassignDisabled}>
                  <UserX2 className="size-4" />
                </ActionIconButton>
              ) : (
                <ActionIconButton title={`Assign ${campaign.campaignName}`} onClick={() => onAssign(campaign.id)} disabled={assignDisabled}>
                  <UserPlus2 className="size-4" />
                </ActionIconButton>
              )}
            </div>
          </div>
        </div>
      </div>
    </article>
  );
}

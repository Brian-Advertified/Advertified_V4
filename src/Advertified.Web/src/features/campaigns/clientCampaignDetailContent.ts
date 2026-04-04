import { formatCurrency, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { getPrimaryRecommendation } from '../../pages/client/clientWorkspace';

type Campaign = Awaited<ReturnType<typeof advertifiedApi.getCampaign>>;

function formatChannelLabel(value: string) {
  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}

export function getHeroContent(campaign: Campaign, recommendationStatus?: string) {
  if (campaign.status === 'launched') {
    return {
      title: 'Your campaign is now live',
      description: 'Operations has activated your campaign. There are no more approvals waiting for you in this workspace right now.',
      primaryAction: 'Review live status',
      timeLabel: 'No action required',
      reassurance: 'We will keep using this workspace for important updates and support',
    };
  }

  if (campaign.status === 'creative_approved') {
    return {
      title: 'Creative approval is complete',
      description: 'Your finished campaign content has been approved. Our team is preparing the next handoff into supplier booking and launch planning.',
      primaryAction: 'Review final status',
      timeLabel: 'No action required',
      reassurance: 'We will notify you if anything new needs attention',
    };
  }

  if (campaign.status === 'booking_in_progress') {
    return {
      title: 'We are booking your campaign now',
      description: 'Your creative is approved and our team is now confirming placements, dates, and supplier bookings before launch.',
      primaryAction: 'Review final status',
      timeLabel: 'No action required',
      reassurance: 'We will notify you if anything new needs attention',
    };
  }

  if (campaign.status === 'creative_changes_requested') {
    return {
      title: 'Creative revisions are in progress',
      description: 'Your feedback has been sent back to Advertified and the creative team is preparing a revised handoff for you.',
      primaryAction: 'Check revision status',
      timeLabel: 'No action required',
      reassurance: 'You will see the next approval here once the revision is ready',
    };
  }

  if (campaign.status === 'creative_sent_to_client_for_approval') {
    return {
      title: 'Review the finished campaign handoff',
      description: 'Your finished media has been returned for final client approval. Use the approval section below to review the current campaign state and message the team if anything needs to change.',
      primaryAction: 'Review approval status',
      timeLabel: 'Short review',
      reassurance: 'Support is one message away',
    };
  }

  if (campaign.status === 'approved') {
    return {
      title: 'Your recommendation is approved',
      description: 'Advertified is now taking the next step for you. Creative production is in motion, so you do not need to manage the rest of the workflow from separate campaign pages.',
      primaryAction: 'View campaign progress',
      timeLabel: 'No action required',
      reassurance: 'We will notify you when something needs attention',
    };
  }

  if (recommendationStatus === 'sent_to_client' || campaign.status === 'review_ready' || campaign.status === 'planning_in_progress') {
    return {
      title: 'Approve your campaign recommendation',
      description: 'We have simplified the workspace so you only see what matters now: one approval, one way to ask for help, and a calm handoff to the Advertified team after that.',
      primaryAction: 'Review recommendation',
      timeLabel: 'Takes about 2 minutes',
      reassurance: 'You can still request changes later',
    };
  }

  return {
    title: 'Your campaign is moving through setup',
    description: campaign.nextAction,
    primaryAction: 'Open campaign status',
    timeLabel: 'Quick check',
    reassurance: 'Ask your agent if anything feels unclear',
  };
}

export function getApprovalContent(campaign: Campaign, recommendationStatus?: string) {
  if (campaign.status === 'launched') {
    return {
      title: 'Your campaign is live',
      body: 'Operations has activated the campaign and it is now live.',
      badge: 'Live',
      badgeClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      highlightClass: 'border-emerald-200 bg-[linear-gradient(180deg,#f4fbf8_0%,#eef8f4_100%)]',
      guidance: 'There is nothing left to approve here. Use messages if you need support or want to check in with the team.',
      reassurance: 'Client approval and live activation are now tracked separately, so you can see exactly when the campaign moved from approval into execution.',
      statusText: 'Campaign live',
      nextPhaseText: 'Campaign execution is underway',
    };
  }

  if (campaign.status === 'creative_approved') {
    return {
      title: 'Creative approval is complete',
      body: 'Your final creative is approved. Our team is now moving the campaign into booking and launch preparation.',
      badge: 'Done',
      badgeClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      highlightClass: 'border-emerald-200 bg-[linear-gradient(180deg,#f4fbf8_0%,#eef8f4_100%)]',
      guidance: 'There is nothing else you need to approve right now. Use messages if you want to speak to the team.',
      reassurance: 'The final creative approval is captured, and the team now uses that approval to move into booking and launch preparation.',
      statusText: 'Creative approved',
      nextPhaseText: 'Our team starts supplier booking',
    };
  }

  if (campaign.status === 'booking_in_progress') {
    return {
      title: 'Supplier booking is in progress',
      body: 'Your campaign is now in the booking stage. We are confirming placements, live dates, and supplier readiness before launch.',
      badge: 'In progress',
      badgeClass: 'border-sky-200 bg-sky-50 text-sky-700',
      highlightClass: 'border-sky-200 bg-[linear-gradient(180deg,#f4fbff_0%,#eef6ff_100%)]',
      guidance: 'There is nothing you need to approve right now. We will keep this page updated as bookings are confirmed.',
      reassurance: 'Saved supplier bookings and delivery updates will appear in this workspace so you can see what has been confirmed.',
      statusText: 'Booking in progress',
      nextPhaseText: 'Booked placements move toward launch',
    };
  }

  if (campaign.status === 'creative_changes_requested') {
    return {
      title: 'Creative changes requested',
      body: 'Your creative feedback has been sent back to Advertified and the team is revising the finished media.',
      badge: 'Sent back',
      badgeClass: 'border-amber-200 bg-amber-50 text-amber-700',
      highlightClass: 'border-amber-200 bg-[linear-gradient(180deg,#fffaf0_0%,#fff5dc_100%)]',
      guidance: 'There is nothing else you need to do right now. The revised creative handoff will return here once it is ready.',
      reassurance: 'Your revision request is now a formal backend state, so the workflow will not skip ahead while the team is updating the creative.',
      statusText: 'Waiting on revised creative',
      nextPhaseText: 'Advertified prepares a revised creative handoff',
    };
  }

  if (campaign.status === 'creative_sent_to_client_for_approval') {
    return {
      title: 'Finished media sent to client',
      body: 'Advertified has moved the campaign into the final client-approval state. Approve the finished creative or send it back with revision notes.',
      badge: 'Needs approval',
      badgeClass: 'border-sky-200 bg-sky-50 text-sky-700',
      highlightClass: 'border-sky-200 bg-[linear-gradient(180deg,#f4fbff_0%,#eef6ff_100%)]',
      guidance: 'Review the finished creative here. Approve it if it is ready, or request changes with specific notes if you want the team to revise it.',
      reassurance: 'This is a real persisted approval step in the backend, so your decision here controls the next workflow state.',
      statusText: 'Waiting for final creative approval',
      nextPhaseText: 'Launch preparation continues after final sign-off',
    };
  }

  if (campaign.status === 'approved') {
    return {
      title: 'You are all set for now',
      body: 'Thanks. Your recommendation approval has already been captured and the campaign is moving through creative production.',
      badge: 'Done',
      badgeClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      highlightClass: 'border-emerald-200 bg-[linear-gradient(180deg,#f4fbf8_0%,#eef8f4_100%)]',
      guidance: 'There is nothing else you need to approve right now. If something feels unclear, send a message and the team will help.',
      reassurance: 'This campaign has already moved past recommendation review and into the next backend step.',
      statusText: 'Recommendation approved',
      nextPhaseText: 'Our team starts creative production',
    };
  }

  if (recommendationStatus === 'sent_to_client' || campaign.status === 'review_ready' || campaign.status === 'planning_in_progress') {
    return {
      title: 'Approve recommendation',
      body: 'Review the recommended media plan and approve it so the Advertified team can continue. If anything feels unclear, ask your agent before deciding.',
      badge: 'Needs approval',
      badgeClass: 'border-blue-200 bg-blue-50 text-blue-700',
      highlightClass: 'border-brand/25 bg-[linear-gradient(180deg,#f4fbf8_0%,#eef8f4_100%)]',
      guidance: 'Approve this plan so we can continue, or send it back with notes if you want changes before creative production starts.',
      reassurance: 'This recommendation has already been reviewed by the Advertified team, and you can still request adjustments after approving.',
      statusText: 'Waiting for your approval',
      nextPhaseText: 'Our team starts creative production',
    };
  }

  return {
    title: 'No approval needed right now',
    body: campaign.nextAction,
    badge: 'Up to date',
    badgeClass: 'border-slate-200 bg-slate-50 text-slate-700',
    highlightClass: 'border-line bg-slate-50/70',
    guidance: 'You do not need to complete any approval on this screen at the moment.',
    reassurance: 'When something truly needs your attention, it will appear here as the main task.',
    statusText: titleCase(campaign.status),
    nextPhaseText: campaign.nextAction,
  };
}

export function buildApprovalDetails(campaign: Campaign, recommendationOverride?: Campaign['recommendations'][number]) {
  const recommendation = recommendationOverride ?? getPrimaryRecommendation(campaign);
  const channels = Array.from(new Set(recommendation?.items.map((item) => item.channel).filter(Boolean) ?? []));
  const details: string[] = [];

  if (recommendation?.items.length) {
    details.push(`${recommendation.items.length} recommended placement${recommendation.items.length === 1 ? '' : 's'}`);
  }

  if (channels.length) {
    details.push(`Channels: ${channels.map(formatChannelLabel).join(', ')}`);
  }

  if (campaign.brief?.durationWeeks) {
    details.push(`Timeline: ${campaign.brief.durationWeeks} weeks`);
  }

  if (campaign.brief?.objective) {
    details.push(`Goal: ${titleCase(campaign.brief.objective)}`);
  }

  if (campaign.selectedBudget) {
    details.push(`Budget: ${formatCurrency(campaign.selectedBudget)}`);
  }

  return details;
}


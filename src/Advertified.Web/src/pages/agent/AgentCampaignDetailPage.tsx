import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CircleCheckBig,
  Download,
  Send,
  X,
} from 'lucide-react';
import { useMemo, useRef, useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
import {
  type BudgetConstraintContext,
  buildGeneratedInventoryFallback,
  buildOriginalPrompt,
  buildTargetChannelMix,
  formatConfidenceLabel,
  getConstraintChecks,
  groupGeneratedRecommendationItems,
  groupPlanItems,
  isInventoryRelevant,
  normalizeChannelKey,
} from '../../features/agent/agentCampaignDetailUtils';
import { AgentCampaignWorkspaceOverview } from '../../features/agent/components/AgentCampaignWorkspaceOverview';
import { AgentInventorySelectionModal } from '../../features/agent/components/AgentInventorySelectionModal';
import { AgentRecommendationPanel } from '../../features/agent/components/AgentRecommendationPanel';
import { useAuth } from '../../features/auth/auth-context';
import { formatChannelLabel } from '../../features/channels/channelUtils';
import { catalogQueryOptions } from '../../lib/catalogQueryOptions';
import { invalidateAgentCampaignQueries, queryKeys } from '../../lib/queryKeys';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { RecommendationItem, SelectedPlanInventoryItem, SelectOption } from '../../types/domain';
import { AgentPageShell } from './agentWorkspace';
import { pushAgentMutationError } from './agentMutationToast';

type DisplayPlanItem = SelectedPlanInventoryItem | RecommendationItem;

function formatMixSummary(mix: { ooh: number; radio: number; tv: number; digital: number }) {
  return `Billboards and Digital Screens ${mix.ooh}% | Radio ${mix.radio}% | TV ${mix.tv}% | Digital ${mix.digital}%`;
}

function formatFallbackFlag(flag: string) {
  const normalized = flag.trim().toLowerCase();
  if (normalized.startsWith('preferred_media_unfulfilled:')) {
    const rawChannel = normalized.split(':')[1] ?? 'A preferred channel';
    const channel = normalizeChannelKey(rawChannel) === 'OOH' ? 'Billboards and Digital Screens' : rawChannel.toUpperCase();
    return `${channel} was requested, but this package or the available inventory could not support it in the draft.`;
  }

  return flag.replace(/_/g, ' ');
}

function formatPackageRange(minBudget: number, maxBudget: number) {
  return `${formatCurrency(minBudget)} to ${formatCurrency(maxBudget)}`;
}

function AuditLine({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[14px] border border-line bg-slate-50 px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-ink-soft">{label}</p>
      <p className="mt-2 text-sm leading-6 text-ink">{value}</p>
    </div>
  );
}

export function AgentCampaignDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [selectedPlanState, setSelectedPlanState] = useState<{ key: string; items: SelectedPlanInventoryItem[] } | null>(null);
  const [draftApprovalState, setDraftApprovalState] = useState<{ key: string; captured: boolean } | null>(null);
  const [mixBalance, setMixBalance] = useState(60);
  const [selectedRecommendationIdState, setSelectedRecommendationIdState] = useState('');
  const [inventoryModalOpen, setInventoryModalOpen] = useState(false);
  const [replacementTargetItemId, setReplacementTargetItemId] = useState<string | null>(null);
  const [closeProspectModalOpen, setCloseProspectModalOpen] = useState(false);
  const [closeReasonCode, setCloseReasonCode] = useState('');
  const [closeReasonNotes, setCloseReasonNotes] = useState('');
  const [requestChangesModalOpen, setRequestChangesModalOpen] = useState(false);
  const [requestChangesNotes, setRequestChangesNotes] = useState('');
  const mixPanelRef = useRef<HTMLDivElement | null>(null);

  const campaignQuery = useQuery({
    queryKey: queryKeys.agent.campaign(id),
    queryFn: () => advertifiedApi.getAgentCampaign(id),
    retry: false,
  });
  const inventoryQuery = useQuery({
    queryKey: queryKeys.agent.inventory(id),
    queryFn: () => advertifiedApi.getInventory(id),
    enabled: campaignQuery.isSuccess,
    retry: false,
  });
  const packagesQuery = useQuery({
    queryKey: queryKeys.packages.all,
    queryFn: () => advertifiedApi.getPackages(),
    ...catalogQueryOptions,
  });
  const prospectDispositionReasonsQuery = useQuery<SelectOption[]>({
    queryKey: ['agent', 'prospect-disposition-reasons'],
    queryFn: () => advertifiedApi.getProspectDispositionReasons(),
    retry: false,
  });

  const saveMutation = useMutation({
    mutationFn: () => advertifiedApi.updateRecommendation(id, activeRecommendation?.id, '', selectedPlanItems),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: 'Recommendation draft saved.',
        description: 'The latest plan and strategy summary are now part of this campaign draft.',
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not save recommendation draft.', error),
  });

  const sendMutation = useMutation({
    mutationFn: () => advertifiedApi.sendRecommendationToClient(id),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: 'Recommendation sent to client.',
        description: 'The campaign has moved into the client review stage.',
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not send recommendation to client.', error),
  });

  const resendEmailMutation = useMutation({
    mutationFn: (payload: { toEmail: string; message?: string | null; successTitle: string; successDescription: string }) =>
      advertifiedApi.resendProposalEmail(id, {
        toEmail: payload.toEmail.trim(),
        message: payload.message?.trim() ? payload.message.trim() : null,
      }),
    onSuccess: async (_, variables) => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: variables.successTitle,
        description: variables.successDescription,
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not resend proposal email.', error),
  });
  const closeProspectMutation = useMutation({
    mutationFn: () => advertifiedApi.closeProspect(id, {
      reasonCode: closeReasonCode,
      notes: closeReasonNotes.trim() ? closeReasonNotes.trim() : null,
    }),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      setCloseProspectModalOpen(false);
      setCloseReasonCode('');
      setCloseReasonNotes('');
      pushToast({
        title: 'Prospect closed.',
        description: 'The close reason was saved on this campaign.',
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not close prospect.', error),
  });
  const reopenProspectMutation = useMutation({
    mutationFn: () => advertifiedApi.reopenProspect(id),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: 'Prospect reopened.',
        description: 'The campaign is active again.',
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not reopen prospect.', error),
  });
  const requestRecommendationChangesMutation = useMutation({
    mutationFn: () => advertifiedApi.requestRecommendationChanges(id, requestChangesNotes.trim()),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      setRequestChangesModalOpen(false);
      setRequestChangesNotes('');
      setSelectedRecommendationIdState('');
      pushToast({
        title: 'Change request captured.',
        description: 'A new draft revision is ready for the requested proposal updates.',
      });
      navigate(`/agent/recommendations/new?campaignId=${id}`);
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not reopen the recommendation for changes.', error),
  });

  const regenerateMutation = useMutation({
    mutationFn: () => advertifiedApi.generateAgentRecommendation(id, {
      targetRadioShare: targetMix.radio,
      targetOohShare: targetMix.ooh,
      targetTvShare: targetMix.tv,
      targetDigitalShare: targetMix.digital,
    }),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: 'Recommendation regenerated.',
        description: `A fresh AI draft was prepared using target mix Radio ${targetMix.radio}% | Billboards and Digital Screens ${targetMix.ooh}% | TV ${targetMix.tv}% | Digital ${targetMix.digital}%.`,
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'We could not regenerate the recommendation.', error, 'Please try again in a moment.'),
  });

  const campaign = campaignQuery.data;
  const inventoryItems = inventoryQuery.data ?? [];
  const selectedPackageBand = packagesQuery.data?.find((item) => item.id === campaign?.packageBandId) ?? null;
  const recommendations = campaign?.recommendations.length
    ? campaign.recommendations
    : (campaign?.recommendation ? [campaign.recommendation] : []);
  const preferredRecommendationId = recommendations.find((item) => item.status === 'approved')?.id
    ?? recommendations.find((item) => item.status === 'sent_to_client')?.id
    ?? recommendations[0]?.id
    ?? '';
  const selectedRecommendationId = selectedRecommendationIdState || preferredRecommendationId;
  const activeRecommendation = recommendations.find((item) => item.id === selectedRecommendationId) ?? recommendations[0];
  const recommendationItemsKey = activeRecommendation?.items
    .map((item) => `${item.id}:${item.sourceInventoryId ?? ''}:${item.quantity ?? 1}:${item.cost}`)
    .join('|') ?? '';
  const inventoryItemsKey = inventoryItems.map((item) => item.id).join('|');
  const hydrationKey = `${campaign?.id ?? 'no-campaign'}:${activeRecommendation?.id ?? 'no-recommendation'}:${inventoryItemsKey}:${recommendationItemsKey}`;
  const hydratedSelectedPlanItems = useMemo(() => {
    if (!campaign || !activeRecommendation) {
      return [] as SelectedPlanInventoryItem[];
    }

    const byId = new Map(inventoryItems.map((item) => [item.id, item]));
    return activeRecommendation.items
      .map((item) => {
        const quantity = item.quantity || 1;
        const inventoryMatch = item.sourceInventoryId ? byId.get(item.sourceInventoryId) : undefined;

        if (inventoryMatch) {
          return {
            ...inventoryMatch,
            quantity,
            flighting: item.flighting,
            notes: item.itemNotes,
            startDate: item.startDate,
            endDate: item.endDate,
          } as SelectedPlanInventoryItem;
        }

        const normalizedChannel = normalizeChannelKey(item.channel);
        const normalizedType = normalizedChannel === 'RADIO'
          ? 'radio'
          : normalizedChannel === 'OOH'
            ? 'ooh'
            : normalizedChannel === 'TV'
              ? 'tv'
              : 'digital';
        const fallbackDetails = buildGeneratedInventoryFallback(item, campaign.brief);

        return {
          id: item.sourceInventoryId ?? item.id,
          type: normalizedType,
          station: item.title,
          region: fallbackDetails.region,
          language: fallbackDetails.language,
          showDaypart: fallbackDetails.showDaypart,
          timeBand: fallbackDetails.timeBand,
          slotType: fallbackDetails.slotType,
          duration: fallbackDetails.duration,
          rate: quantity > 0 ? item.cost / quantity : item.cost,
          restrictions: fallbackDetails.restrictions,
          quantity,
          flighting: item.flighting,
          notes: item.itemNotes,
          startDate: item.startDate,
          endDate: item.endDate,
        } as SelectedPlanInventoryItem;
      })
      .filter((item): item is SelectedPlanInventoryItem => item !== null);
  }, [campaign, activeRecommendation, inventoryItems]);
  const selectedPlanItems = selectedPlanState?.key === hydrationKey
    ? selectedPlanState.items
    : hydratedSelectedPlanItems;
  const draftApprovalCaptured = draftApprovalState?.key === hydrationKey
    ? draftApprovalState.captured
    : false;
  const proposalDisplayTotals = useMemo(() => {
    const totals: Record<string, number> = {};
    for (const recommendation of recommendations) {
      totals[recommendation.id] = recommendation.id === activeRecommendation?.id
        ? selectedPlanItems.reduce((sum, item) => sum + item.rate * item.quantity, 0) || activeRecommendation?.totalCost || 0
        : recommendation.totalCost;
    }

    return totals;
  }, [activeRecommendation?.id, activeRecommendation?.totalCost, recommendations, selectedPlanItems]);
  const proposalDisplayItemCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const recommendation of recommendations) {
      counts[recommendation.id] = recommendation.id === activeRecommendation?.id
        ? selectedPlanItems.length
        : recommendation.items.length;
    }

    return counts;
  }, [activeRecommendation?.id, recommendations, selectedPlanItems.length]);

  if (campaignQuery.isLoading || (campaign && inventoryQuery.isLoading)) {
    return <LoadingState label="Loading agent campaign detail..." />;
  }

  if (campaignQuery.isError || !campaign) {
    return (
      <EmptyState
        title="Campaign not found"
        description="We could not load this agent campaign. The link may be stale or the campaign may no longer exist in DEV."
        ctaHref="/agent/campaigns"
        ctaLabel="Back to campaigns"
      />
    );
  }

  const needsRecommendationSetup = !campaign.brief || recommendations.length === 0;
  if (needsRecommendationSetup) {
    return <Navigate to={`/agent/recommendations/new?campaignId=${campaign.id}`} replace />;
  }

  const selectedInventoryIds = selectedPlanItems.map((item) => item.id);
  const relevantInventoryItems = inventoryItems.filter((item) => isInventoryRelevant(item as SelectedPlanInventoryItem, campaign.brief));
  const visibleInventoryItems = [
    ...selectedPlanItems.filter((item) => !inventoryItems.some((inventoryRow) => inventoryRow.id === item.id)),
    ...relevantInventoryItems,
  ];
  const groupedItems = groupPlanItems(selectedPlanItems);
  const generatedRecommendationItems = activeRecommendation?.items ?? [];
  const groupedGeneratedItems = groupGeneratedRecommendationItems(generatedRecommendationItems);
  const hasManualPlanItems = selectedPlanItems.length > 0;
  const displayedGroups: Record<string, DisplayPlanItem[]> = hasManualPlanItems ? groupedItems : groupedGeneratedItems;
  const groupedTotals = (hasManualPlanItems
    ? Object.entries(groupedItems).map(([channel, items]) => ({
      channel: normalizeChannelKey(channel),
      total: items.reduce((sum, item) => sum + item.rate * item.quantity, 0),
    }))
    : Object.entries(groupedGeneratedItems).map(([channel, items]) => ({
    channel: normalizeChannelKey(channel),
    total: items.reduce((sum, item) => sum + item.cost, 0),
  })));
  const plannedTotal = selectedPlanItems.reduce((sum, item) => sum + item.rate * item.quantity, 0);
  const effectivePlannedTotal = plannedTotal > 0 ? plannedTotal : activeRecommendation?.totalCost ?? 0;
  const isProspectiveCampaign = campaign.paymentStatus !== 'paid' || campaign.status === 'awaiting_purchase';
  const isClosedProspect = isProspectiveCampaign && campaign.prospectDisposition?.status === 'closed';
  const budgetDelta = campaign.selectedBudget - effectivePlannedTotal;
  const hasSelectedPackageBand = Boolean(selectedPackageBand);
  const prospectiveBelowBandDelta = selectedPackageBand
    ? selectedPackageBand.minBudget - effectivePlannedTotal
    : 0;
  const prospectiveAboveBandDelta = selectedPackageBand
    ? effectivePlannedTotal - selectedPackageBand.maxBudget
    : 0;
  const isOutsideProspectBand = selectedPackageBand
    ? effectivePlannedTotal < selectedPackageBand.minBudget || effectivePlannedTotal > selectedPackageBand.maxBudget
    : false;
  const isOverBudget = hasSelectedPackageBand ? isOutsideProspectBand : budgetDelta < 0;
  const activeProposalLabel = activeRecommendation?.proposalLabel ?? 'Current proposal';
  const campaignPastRecommendationStage = campaign.status === 'creative_approved'
    || campaign.status === 'booking_in_progress'
    || campaign.status === 'launched';
  const radioShare = groupedTotals.reduce((sum, entry) => entry.channel === 'RADIO' ? sum + entry.total : sum, 0);
  const oohShare = groupedTotals.reduce((sum, entry) => entry.channel === 'OOH' ? sum + entry.total : sum, 0);
  const digitalShare = groupedTotals.reduce((sum, entry) => entry.channel === 'DIGITAL' ? sum + entry.total : sum, 0);
  const tvShare = groupedTotals.reduce((sum, entry) => entry.channel === 'TV' ? sum + entry.total : sum, 0);
  const totalGroupedSpend = groupedTotals.reduce((sum, entry) => sum + entry.total, 0);
  const currentRadioShare = totalGroupedSpend > 0 ? Math.round((radioShare / totalGroupedSpend) * 100) : 0;
  const currentOohShare = totalGroupedSpend > 0 ? Math.round((oohShare / totalGroupedSpend) * 100) : 0;
  const currentDigitalShare = totalGroupedSpend > 0 ? Math.round((digitalShare / totalGroupedSpend) * 100) : 0;
  const currentTvShare = totalGroupedSpend > 0 ? Math.round((tvShare / totalGroupedSpend) * 100) : 0;
  const targetMix = buildTargetChannelMix(groupedTotals, mixBalance, campaign.brief?.preferredMediaTypes);
  const originalPrompt = buildOriginalPrompt(campaign.brief);
  const recommendationTitle = activeRecommendation?.summary || 'Draft recommendation';
  const recommendationStatus = activeRecommendation?.status?.toLowerCase() ?? '';
  const recommendationApproved = recommendationStatus === 'approved' || Boolean(campaign.workflow?.recommendationApprovalCompleted);
  const recommendationSentToClient = campaign.status === 'review_ready' || recommendationStatus === 'sent_to_client';
  const awaitingClientReview = campaign.status === 'review_ready' || recommendationStatus === 'sent_to_client';
  const recommendationWorkflowLocked = isClosedProspect || campaignPastRecommendationStage || recommendationStatus === 'approved' || awaitingClientReview;
  const showRecommendationEditing = !recommendationWorkflowLocked;
  const canEditDraftRecommendation = !recommendationWorkflowLocked && activeRecommendation?.status?.toLowerCase() === 'draft';
  const canModifyPlan = canEditDraftRecommendation && !draftApprovalCaptured;
  const hasSendableProposal = !recommendationWorkflowLocked && recommendations.length >= 1;
  const canResendProposalEmail = recommendations.length >= 1;
  const canCloseProspect = isProspectiveCampaign && !isClosedProspect;
  const canRequestRecommendationChanges = awaitingClientReview && !recommendationApproved && !isClosedProspect;
  const hasOohRecommendation = selectedPlanItems.some((item) => normalizeChannelKey(item.type) === 'OOH');
  const lockedNextStep = isClosedProspect
    ? 'This prospect is commercially closed. Reopen it only if the client starts engaging again or the opportunity changes.'
    : campaign.status === 'approved'
    ? 'The recommendation is approved and paid. The next real step belongs to the creative director team: create the campaign content and prepare it for client approval.'
    : campaign.status === 'creative_changes_requested'
      ? 'The client has asked for creative changes. The creative director team is updating the content and preparing the next handoff.'
      : campaign.status === 'creative_sent_to_client_for_approval'
        ? 'The content handoff is back with the client for final review. Watch messages and feedback while waiting for sign-off.'
      : campaign.status === 'creative_approved'
          ? 'Creative approval is complete. The next step belongs to the creative director team: start supplier booking and launch planning.'
      : campaign.status === 'booking_in_progress'
            ? 'Supplier booking is already under way in the creative director workflow. Use messages if the team needs anything else.'
          : 'The campaign is live. Use this page for delivery tracking, reports, and operational follow-up.';
  const budgetConstraint: BudgetConstraintContext = selectedPackageBand
    ? {
      label: 'Within selected band',
      ok: !isOutsideProspectBand,
      detail: !isOutsideProspectBand
        ? `Inside the selected ${formatPackageRange(selectedPackageBand.minBudget, selectedPackageBand.maxBudget)} package band.`
        : effectivePlannedTotal > selectedPackageBand.maxBudget
          ? 'This draft is currently above the selected package band.'
          : 'This draft is currently below the selected package band.',
    }
    : {
      label: 'Within budget',
      ok: budgetDelta >= 0,
      detail: budgetDelta >= 0
        ? `Inside the paid ${formatCurrency(campaign.selectedBudget)} package.`
        : 'This draft is currently over budget.',
    };
  const constraintChecks = getConstraintChecks(campaign.brief, selectedPlanItems, budgetConstraint);
  const budgetWarning = isOverBudget
    ? (isProspectiveCampaign && selectedPackageBand
      ? effectivePlannedTotal > selectedPackageBand.maxBudget
        ? `This draft is ${formatCurrency(Math.abs(prospectiveAboveBandDelta))} over the selected package band maximum.`
        : `This draft is ${formatCurrency(Math.abs(prospectiveBelowBandDelta))} below the selected package band minimum.`
      : selectedPackageBand
        ? effectivePlannedTotal > selectedPackageBand.maxBudget
          ? `This draft is ${formatCurrency(Math.abs(prospectiveAboveBandDelta))} over the selected package band maximum.`
          : `This draft is ${formatCurrency(Math.abs(prospectiveBelowBandDelta))} below the selected package band minimum.`
        : `This draft is ${formatCurrency(Math.abs(budgetDelta))} over the client's budget.`)
    : undefined;
  const auditSummary = activeRecommendation?.audit
    ? {
      request: activeRecommendation.audit.requestSummary,
      selected: activeRecommendation.audit.selectionSummary,
      rejected: activeRecommendation.audit.rejectionSummary,
      policy: activeRecommendation.audit.policySummary,
      budget: activeRecommendation.audit.budgetSummary,
      fallback: activeRecommendation.audit.fallbackSummary
        ?? (activeRecommendation.fallbackFlags.length > 0
          ? activeRecommendation.fallbackFlags.map(formatFallbackFlag).join(' ')
          : activeRecommendation.manualReviewRequired
            ? 'Manual review was required for this recommendation.'
            : undefined),
    }
    : (activeRecommendation?.fallbackFlags.length || activeRecommendation?.manualReviewRequired)
      ? {
        request: originalPrompt,
        selected: activeRecommendation?.summary ?? 'Recommendation selected.',
        rejected: 'Detailed rejection traces are not available for this recommendation.',
        policy: activeRecommendation.manualReviewRequired
          ? 'Manual review safeguards were triggered during planning.'
          : 'No special policy overrides were required.',
        budget: selectedPackageBand
          ? `${formatCurrency(effectivePlannedTotal)} inside ${formatPackageRange(selectedPackageBand.minBudget, selectedPackageBand.maxBudget)}.`
          : `${formatCurrency(effectivePlannedTotal)} against ${formatCurrency(campaign.selectedBudget)}.`,
        fallback: activeRecommendation.fallbackFlags.length > 0
          ? activeRecommendation.fallbackFlags.map(formatFallbackFlag).join(' ')
          : 'No fallback conditions were recorded.',
      }
      : null;
  const proposalEmailSent = recommendations.some((recommendation) => (recommendation.emailDeliveries?.length ?? 0) > 0);
  const clientRecipientEmail = campaign.clientEmail?.trim() ?? '';
  const agentPreviewEmail = user?.email?.trim() ?? '';
  const lockedProposalLabel = recommendationApproved
    ? 'Approved proposal'
    : recommendationSentToClient
      ? 'Proposal sent to client'
      : 'Current proposal';
  const lockedProposalDescription = recommendationApproved
    ? 'Recommendation work is complete. Keep this page focused on production, delivery, and client follow-up.'
    : recommendationSentToClient
      ? 'The current proposal was sent to the client and is waiting for a response. Keep the proposal stable and use messages for follow-up until they reply.'
      : 'This proposal is locked in the current workflow stage.';

  async function handleDownloadRecommendationPdf() {
    const pdfUrl = campaign?.recommendationPdfUrl;
    const campaignId = campaign?.id;
    if (!pdfUrl || !campaignId) {
      return;
    }

    try {
      await advertifiedApi.downloadProtectedFile(pdfUrl, `recommendation-${campaignId}.pdf`);
    } catch (error) {
      pushToast({
        title: 'Could not open recommendation PDF.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    }
  }

  function handleSendToClient() {
    if (recommendationWorkflowLocked) {
      pushToast({
        title: 'Recommendation stage is complete.',
        description: 'This campaign has already moved past recommendation sending and is now in production or activation.',
      }, 'info');
      return;
    }

    if (!hasSendableProposal) {
      pushToast({
        title: 'No proposal available yet.',
        description: 'Create at least one proposal option before sending to the client.',
      }, 'info');
      return;
    }

    if (!hasOohRecommendation) {
      pushToast({
        title: 'Billboards and Digital Screens are required.',
        description: 'Add at least one Billboards and Digital Screens line before sending this recommendation to the client.',
      }, 'error');
      return;
    }

    sendMutation.mutate();
  }

  function handleResendClientEmail() {
    if (!clientRecipientEmail) {
      pushToast({
        title: 'Recipient email required.',
        description: 'This campaign does not have a client email captured yet.',
      }, 'info');
      return;
    }

    resendEmailMutation.mutate({
      toEmail: clientRecipientEmail,
      message: null,
      successTitle: 'Proposal email resent.',
      successDescription: `Sent to ${clientRecipientEmail}.`,
    });
  }

  function handleSendPreviewToMyself() {
    if (!agentPreviewEmail) {
      pushToast({
        title: 'Agent email not available.',
        description: 'We could not find your signed-in email address for the preview send.',
      }, 'info');
      return;
    }

    resendEmailMutation.mutate({
      toEmail: agentPreviewEmail,
      message: 'Preview copy for agent review.',
      successTitle: 'Preview email sent.',
      successDescription: `Sent to ${agentPreviewEmail}.`,
    });
  }

  function handleOpenCloseProspectModal() {
    setCloseProspectModalOpen(true);
  }

  function handleOpenRequestChangesModal() {
    setRequestChangesModalOpen(true);
  }

  function handleCloseProspectModal() {
    if (closeProspectMutation.isPending) {
      return;
    }

    setCloseProspectModalOpen(false);
    setCloseReasonCode('');
    setCloseReasonNotes('');
  }

  function handleCloseRequestChangesModal() {
    if (requestRecommendationChangesMutation.isPending) {
      return;
    }

    setRequestChangesModalOpen(false);
    setRequestChangesNotes('');
  }

  function handleSubmitCloseProspect() {
    if (!closeReasonCode) {
      pushToast({
        title: 'Close reason required.',
        description: 'Choose the reason for closing this prospect before continuing.',
      }, 'info');
      return;
    }

    closeProspectMutation.mutate();
  }

  function handleSubmitRecommendationChanges() {
    if (!requestChangesNotes.trim()) {
      pushToast({
        title: 'Change notes required.',
        description: 'Capture the client feedback before reopening this proposal set for changes.',
      }, 'info');
      return;
    }

    requestRecommendationChangesMutation.mutate();
  }

  function toggleInventoryItem(item: SelectedPlanInventoryItem) {
    if (!canModifyPlan) {
      pushToast({
        title: 'Recommendation locked after approval.',
        description: 'Create a new draft revision if you need to change plan lines.',
      }, 'info');
      return;
    }

    setSelectedPlanState((currentState) => {
      const currentItems = currentState?.key === hydrationKey
        ? currentState.items
        : hydratedSelectedPlanItems;
      const existing = currentItems.find((value) => value.id === item.id);
      if (existing) {
        return {
          key: hydrationKey,
          items: currentItems.filter((value) => value.id !== item.id),
        };
      }

      return {
        key: hydrationKey,
        items: [
          ...currentItems,
          {
            ...item,
            quantity: 1,
            flighting: '',
            notes: '',
            startDate: '',
            endDate: '',
          },
        ],
      };
    });
  }

  function handleReplace(itemId: string) {
    const currentItem = selectedPlanItems.find((item) => item.id === itemId);
    if (!currentItem) return;

    setReplacementTargetItemId(itemId);
    setInventoryModalOpen(true);
  }

  function handleToggleInventoryItem(item: SelectedPlanInventoryItem) {
    if (replacementTargetItemId) {
      const currentItem = selectedPlanItems.find((value) => value.id === replacementTargetItemId);
      if (!currentItem) {
        setReplacementTargetItemId(null);
        return;
      }

      setSelectedPlanState((currentState) => {
        const currentItems = currentState?.key === hydrationKey
          ? currentState.items
          : hydratedSelectedPlanItems;
        const replacementExists = currentItems.some((value) => value.id === item.id);
        const nextItems = currentItems
          .filter((value) => value.id !== currentItem.id)
          .map((value) => (
            value.id === item.id
              ? {
                ...value,
                quantity: currentItem.quantity,
                flighting: currentItem.flighting,
                notes: currentItem.notes,
                startDate: currentItem.startDate,
                endDate: currentItem.endDate,
              }
              : value
          ));

        if (!replacementExists) {
          nextItems.push({
            ...item,
            quantity: currentItem.quantity,
            flighting: currentItem.flighting,
            notes: currentItem.notes,
            startDate: currentItem.startDate,
            endDate: currentItem.endDate,
          });
        }

        return {
          key: hydrationKey,
          items: nextItems,
        };
      });

      setReplacementTargetItemId(null);
      setInventoryModalOpen(false);
      pushToast({
        title: 'Line replaced.',
        description: `${currentItem.station} was replaced with ${item.station}.`,
      });
      return;
    }

    toggleInventoryItem(item);
  }

  async function handleApproveRecommendation() {
    if (!canEditDraftRecommendation) {
      pushToast({
        title: 'Recommendation is locked.',
        description: recommendationWorkflowLocked
          ? 'This campaign has already moved past recommendation work and is now read-only here.'
          : 'Only draft recommendations can be edited. Create a new revision to make changes.',
      }, 'info');
      return;
    }

    if (draftApprovalCaptured) {
      pushToast({
        title: 'Recommendation already approved.',
        description: 'This draft has already been finalized for client review.',
      }, 'info');
      return;
    }

    if (!hasOohRecommendation) {
      pushToast({
        title: 'Billboards and Digital Screens are required.',
        description: 'Add at least one Billboards and Digital Screens line before finalizing this draft.',
      }, 'error');
      return;
    }

    await saveMutation.mutateAsync();
    setDraftApprovalState({ key: hydrationKey, captured: true });
    pushToast({
      title: 'Draft finalized.',
      description: 'The selected proposal draft is locked and ready for client review handoff.',
    });
    navigate('/agent/review-send');
  }

  function handleRegenerate() {
    if (recommendationWorkflowLocked) {
      pushToast({
        title: 'Recommendation stage is complete.',
        description: 'Approved campaigns can no longer be regenerated from this workspace.',
      }, 'info');
      return;
    }

    regenerateMutation.mutate();
  }

  function handleAdjustMix() {
    if (recommendationWorkflowLocked) {
      pushToast({
        title: 'Recommendation stage is complete.',
        description: 'Approved campaigns can no longer be adjusted from this workspace.',
      }, 'info');
      return;
    }

    pushToast({
      title: 'Adjust the target mix below.',
      description: 'Use the budget split slider, then regenerate when you want a new draft from the same saved campaign inputs.',
    }, 'info');
  }

  return (
    <AgentPageShell
      title={campaign.campaignName}
      description="Review the campaign, move the next action forward, and keep recommendation and execution work in one controlled workspace."
    >
      <section className="space-y-8">
        {saveMutation.isPending || sendMutation.isPending || resendEmailMutation.isPending || regenerateMutation.isPending || closeProspectMutation.isPending || reopenProspectMutation.isPending || requestRecommendationChangesMutation.isPending ? (
          <ProcessingOverlay
            label={
            sendMutation.isPending
              ? 'Sending recommendation to the client...'
              : resendEmailMutation.isPending
                ? 'Resending the proposal email...'
              : requestRecommendationChangesMutation.isPending
                ? 'Reopening recommendation for changes...'
              : closeProspectMutation.isPending
                ? 'Closing prospect...'
              : reopenProspectMutation.isPending
                ? 'Reopening prospect...'
              : regenerateMutation.isPending
                ? 'Regenerating the recommendation from the latest campaign inputs...'
                : 'Saving recommendation draft...'
          }
        />
      ) : null}

      <div className="space-y-6">
        <AgentCampaignWorkspaceOverview
          campaignName={campaign.campaignName}
          timeline={campaign.timeline}
          actions={(
            <>
              <Link
                to={`/agent/recommendations/new?campaignId=${campaign.id}`}
                className={`button-secondary inline-flex items-center gap-2 px-5 py-3 ${recommendationWorkflowLocked ? 'pointer-events-none opacity-60' : ''}`}
                aria-disabled={recommendationWorkflowLocked}
              >
                Edit inputs
              </Link>
              {campaign.recommendationPdfUrl ? (
                <button
                  type="button"
                  onClick={() => void handleDownloadRecommendationPdf()}
                  className="button-primary inline-flex items-center gap-2 px-5 py-3"
                >
                  <Download className="size-4" />
                  Preview client PDF
                </button>
              ) : null}
              {canResendProposalEmail && !isClosedProspect ? (
                <button
                  type="button"
                  disabled={resendEmailMutation.isPending}
                  onClick={proposalEmailSent ? handleResendClientEmail : handleSendToClient}
                  className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
                >
                  <Send className="size-4" />
                  {proposalEmailSent ? 'Resend Email' : 'Send Email'}
                </button>
              ) : null}
              {canResendProposalEmail ? (
                <button
                  type="button"
                  disabled={resendEmailMutation.isPending || !agentPreviewEmail}
                  onClick={handleSendPreviewToMyself}
                  className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
                >
                  <Send className="size-4" />
                  Send preview email to myself
                </button>
              ) : null}
              {canRequestRecommendationChanges ? (
                <button
                  type="button"
                  onClick={handleOpenRequestChangesModal}
                  className="button-secondary inline-flex items-center gap-2 px-5 py-3"
                >
                  Request changes
                </button>
              ) : null}
              {canCloseProspect ? (
                <button
                  type="button"
                  onClick={handleOpenCloseProspectModal}
                  className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50"
                >
                  Close prospect
                </button>
              ) : null}
              {isClosedProspect ? (
                <button
                  type="button"
                  onClick={() => reopenProspectMutation.mutate()}
                  disabled={reopenProspectMutation.isPending}
                  className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
                >
                  Reopen prospect
                </button>
              ) : null}
              {!isProspectiveCampaign && !recommendationWorkflowLocked ? (
                <button
                  type="button"
                  disabled={saveMutation.isPending || !canEditDraftRecommendation || draftApprovalCaptured || !hasOohRecommendation}
                  onClick={() => void handleApproveRecommendation()}
                  className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
                >
                  <CircleCheckBig className="size-4" />
                  {draftApprovalCaptured ? 'Draft finalized' : 'Finalize draft'}
                </button>
              ) : null}
            </>
          )}

        />

        <div className="space-y-6">
          <AgentRecommendationPanel
            activeRecommendation={activeRecommendation}
            recommendations={recommendations}
            showRecommendationEditing={showRecommendationEditing}
            recommendationWorkflowLocked={recommendationWorkflowLocked}
            showAuditSummary={false}
            showEmailDelivery={false}
            recommendationTitle={recommendationTitle}
            lockedNextStep={lockedNextStep}
            activeProposalLabel={activeProposalLabel}
            activeProposalTotal={effectivePlannedTotal}
            lockedProposalLabel={lockedProposalLabel}
            lockedProposalDescription={lockedProposalDescription}
            mixPanelRef={mixPanelRef}
            mixBalance={mixBalance}
            setMixBalance={setMixBalance}
            targetMix={targetMix}
            currentMix={{
              ooh: currentOohShare,
              radio: currentRadioShare,
              tv: currentTvShare,
              digital: currentDigitalShare,
            }}
            displayedGroups={displayedGroups}
            constraintChecks={constraintChecks}
            isOverBudget={isOverBudget}
            budgetWarning={budgetWarning}
            canModifyPlan={canModifyPlan}
            selectedPlanItemsCount={selectedPlanItems.length}
            proposalDisplayTotals={proposalDisplayTotals}
            proposalDisplayItemCounts={proposalDisplayItemCounts}
            onSelectRecommendation={setSelectedRecommendationIdState}
            onRegenerate={handleRegenerate}
            onAdjustMix={handleAdjustMix}
            onReplace={handleReplace}
            onOpenInventory={() => setInventoryModalOpen(true)}
            formatChannelLabel={formatChannelLabel}
            formatCurrency={formatCurrency}
            formatMixSummary={formatMixSummary}
            formatConfidenceLabel={formatConfidenceLabel}
            formatFallbackFlag={formatFallbackFlag}
          />

          {auditSummary ? (
            <div className="panel border-brand/15 bg-white px-6 py-5">
              <p className="text-sm font-semibold text-ink">Engine audit</p>
              <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                <AuditLine label="Request" value={auditSummary.request} />
                <AuditLine label="Selected" value={auditSummary.selected} />
                <AuditLine label="Rejected" value={auditSummary.rejected} />
                <AuditLine label="Policy" value={auditSummary.policy} />
                {auditSummary.budget ? <AuditLine label="Budget" value={auditSummary.budget} /> : null}
                {auditSummary.fallback ? <AuditLine label="Fallback" value={auditSummary.fallback} /> : null}
              </div>
            </div>
          ) : null}

          <AgentInventorySelectionModal
            isOpen={showRecommendationEditing && inventoryModalOpen}
            items={visibleInventoryItems}
            selectedItemIds={selectedInventoryIds}
            canModifyPlan={canModifyPlan}
            replacementItemId={replacementTargetItemId}
            replacementInventoryType={selectedPlanItems.find((item) => item.id === replacementTargetItemId)?.type ?? null}
            onClose={() => {
              setInventoryModalOpen(false);
              setReplacementTargetItemId(null);
            }}
            onToggleItem={(item) => handleToggleInventoryItem(item as SelectedPlanInventoryItem)}
            formatChannelLabel={formatChannelLabel}
          />
          {closeProspectModalOpen ? (
            <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 px-4">
              <div className="w-full max-w-2xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.24)]">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-sm font-semibold uppercase tracking-[0.18em] text-rose-600">Close prospect</p>
                    <h3 className="mt-2 text-2xl font-semibold text-ink">Why are we closing this prospect?</h3>
                    <p className="mt-3 text-sm leading-7 text-ink-soft">
                      Save the close reason so the team can understand why this opportunity is no longer moving forward.
                    </p>
                  </div>
                  <button
                    type="button"
                    className="button-secondary p-3"
                    onClick={handleCloseProspectModal}
                    disabled={closeProspectMutation.isPending}
                    aria-label="Close modal"
                  >
                    <X className="size-4" />
                  </button>
                </div>

                <div className="mt-6 space-y-5">
                  <label className="block">
                    <span className="text-sm font-semibold text-ink">Reason</span>
                    <select
                      className="input-base mt-2"
                      value={closeReasonCode}
                      onChange={(event) => setCloseReasonCode(event.target.value)}
                      disabled={closeProspectMutation.isPending}
                    >
                      <option value="">Select a close reason</option>
                      {prospectDispositionReasonsQuery.data?.map((option) => (
                        <option key={option.value} value={option.value}>{option.label}</option>
                      ))}
                    </select>
                  </label>

                  <label className="block">
                    <span className="text-sm font-semibold text-ink">Notes</span>
                    <textarea
                      className="input-base mt-2 min-h-[140px]"
                      placeholder="Add any context that explains why this prospect is being closed."
                      value={closeReasonNotes}
                      onChange={(event) => setCloseReasonNotes(event.target.value)}
                      disabled={closeProspectMutation.isPending}
                    />
                  </label>
                </div>

                <div className="mt-6 flex flex-wrap justify-end gap-3">
                  <button
                    type="button"
                    className="button-secondary px-5 py-3"
                    onClick={handleCloseProspectModal}
                    disabled={closeProspectMutation.isPending}
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="rounded-full bg-rose-600 px-5 py-3 font-semibold text-white transition hover:bg-rose-700 disabled:opacity-60"
                    onClick={handleSubmitCloseProspect}
                    disabled={closeProspectMutation.isPending || !closeReasonCode}
                  >
                    Save and close prospect
                  </button>
                </div>
              </div>
            </div>
          ) : null}
          {requestChangesModalOpen ? (
            <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 px-4">
              <div className="w-full max-w-2xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.24)]">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Request changes</p>
                    <h3 className="mt-2 text-2xl font-semibold text-ink">What changes did the client ask for?</h3>
                    <p className="mt-3 text-sm leading-7 text-ink-soft">
                      Save the client feedback here and we will reopen this recommendation set as a new draft revision.
                    </p>
                  </div>
                  <button
                    type="button"
                    className="button-secondary p-3"
                    onClick={handleCloseRequestChangesModal}
                    disabled={requestRecommendationChangesMutation.isPending}
                    aria-label="Close modal"
                  >
                    <X className="size-4" />
                  </button>
                </div>

                <div className="mt-6 space-y-5">
                  <label className="block">
                    <span className="text-sm font-semibold text-ink">Client feedback</span>
                    <textarea
                      className="input-base mt-2 min-h-[180px]"
                      placeholder="Example: Client likes Proposal B, but wants less radio and more Pretoria billboards."
                      value={requestChangesNotes}
                      onChange={(event) => setRequestChangesNotes(event.target.value)}
                      disabled={requestRecommendationChangesMutation.isPending}
                    />
                  </label>
                </div>

                <div className="mt-6 flex flex-wrap justify-end gap-3">
                  <button
                    type="button"
                    className="button-secondary px-5 py-3"
                    onClick={handleCloseRequestChangesModal}
                    disabled={requestRecommendationChangesMutation.isPending}
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="button-primary px-5 py-3 disabled:opacity-60"
                    onClick={handleSubmitRecommendationChanges}
                    disabled={requestRecommendationChangesMutation.isPending || !requestChangesNotes.trim()}
                  >
                    Save changes and reopen draft
                  </button>
                </div>
              </div>
            </div>
          ) : null}
        </div>
      </div>
      </section>
    </AgentPageShell>
  );
}



import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  BrainCircuit,
  Building2,
  CircleCheckBig,
  Download,
  MessageSquareQuote,
  Send,
  UserPlus2,
  UserX2,
} from 'lucide-react';
import { useMemo, useRef, useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
import { AgentStepper } from '../../components/agent/AgentStepper';
import {
  type BudgetConstraintContext,
  buildAudienceSummary,
  buildChannelSummary,
  buildGeneratedInventoryFallback,
  buildGeoSummary,
  buildOriginalPrompt,
  buildTargetChannelMix,
  buildToneSummary,
  calculateConfidence,
  formatConfidenceLabel,
  getConstraintChecks,
  groupGeneratedRecommendationItems,
  groupPlanItems,
  isInventoryRelevant,
  normalizeChannelKey,
} from '../../features/agent/agentCampaignDetailUtils';
import { AgentBookingPanel } from '../../features/agent/components/AgentBookingPanel';
import { AgentDeliveryReportPanel } from '../../features/agent/components/AgentDeliveryReportPanel';
import { AgentInventorySelectionModal } from '../../features/agent/components/AgentInventorySelectionModal';
import { AgentOpsAssetsPanel } from '../../features/agent/components/AgentOpsAssetsPanel';
import { AgentRecommendationPanel } from '../../features/agent/components/AgentRecommendationPanel';
import { formatChannelLabel } from '../../features/channels/channelUtils';
import { catalogQueryOptions } from '../../lib/catalogQueryOptions';
import { invalidateAgentCampaignQueries, queryKeys } from '../../lib/queryKeys';
import { formatCurrency, formatDate, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { RecommendationItem, SelectedPlanInventoryItem } from '../../types/domain';
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

export function AgentCampaignDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const [selectedPlanState, setSelectedPlanState] = useState<{ key: string; items: SelectedPlanInventoryItem[] } | null>(null);
  const [draftApprovalState, setDraftApprovalState] = useState<{ key: string; captured: boolean } | null>(null);
  const [mixBalance, setMixBalance] = useState(60);
  const [selectedRecommendationIdState, setSelectedRecommendationIdState] = useState('');
  const [inventoryModalOpen, setInventoryModalOpen] = useState(false);
  const [prospectPackageBandState, setProspectPackageBandState] = useState<{ campaignId: string; packageBandId: string } | null>(null);
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
  const updateProspectPricingMutation = useMutation({
    mutationFn: () => advertifiedApi.updateProspectPricing(id, {
      packageBandId: prospectPackageBandId,
    }),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: 'Prospect pricing updated.',
        description: 'The package band was saved and the budget was recalculated for this prospective campaign.',
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not update prospect pricing.', error),
  });

  const markLiveMutation = useMutation({
    mutationFn: () => advertifiedApi.markCampaignLaunched(id),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: 'Campaign marked live.',
        description: 'Operations activation is now captured separately from client approval.',
      });
      navigate('/agent/campaigns');
    },
    onError: (error) => {
      pushAgentMutationError(pushToast, 'Could not mark campaign live.', error);
    },
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

  const assignMutation = useMutation({
    mutationFn: () => advertifiedApi.assignCampaignToMe(id),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: 'Campaign assigned to you.',
        description: 'This campaign is now in your active working queue.',
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not assign campaign.', error),
  });

  const unassignMutation = useMutation({
    mutationFn: () => advertifiedApi.unassignCampaign(id),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: 'Campaign unassigned.',
        description: 'This campaign has been returned to the shared queue.',
      }, 'info');
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not unassign campaign.', error),
  });

  const uploadOpsAssetMutation = useMutation({
    mutationFn: ({ file, type }: { file: File; type: string }) => advertifiedApi.uploadAgentCampaignAsset(id, file, type),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.agent.campaign(id) });
      pushToast({
        title: 'Campaign asset uploaded.',
        description: 'The file is now available for supplier proof, delivery reporting, or execution support.',
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not upload campaign asset.', error),
  });

  const saveBookingMutation = useMutation({
    mutationFn: (draft: {
      supplierOrStation: string;
      channel: string;
      bookingStatus: string;
      committedAmount: string;
      liveFrom: string;
      liveTo: string;
      notes: string;
      proofAssetId: string;
    }) => advertifiedApi.saveSupplierBooking(id, {
      supplierOrStation: draft.supplierOrStation,
      channel: draft.channel,
      bookingStatus: draft.bookingStatus,
      committedAmount: Number(draft.committedAmount || 0),
      liveFrom: draft.liveFrom || undefined,
      liveTo: draft.liveTo || undefined,
      notes: draft.notes || undefined,
      proofAssetId: draft.proofAssetId || undefined,
    }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.agent.campaign(id) });
      pushToast({
        title: 'Supplier booking saved.',
        description: 'Execution details are now attached to this campaign.',
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not save supplier booking.', error),
  });

  const saveReportMutation = useMutation({
    mutationFn: (draft: {
      supplierBookingId: string;
      reportType: string;
      headline: string;
      summary: string;
      impressions: string;
      playsOrSpots: string;
      spendDelivered: string;
      evidenceAssetId: string;
    }) => advertifiedApi.saveDeliveryReport(id, {
      supplierBookingId: draft.supplierBookingId || undefined,
      reportType: draft.reportType,
      headline: draft.headline,
      summary: draft.summary || undefined,
      impressions: draft.impressions ? Number(draft.impressions) : undefined,
      playsOrSpots: draft.playsOrSpots ? Number(draft.playsOrSpots) : undefined,
      spendDelivered: draft.spendDelivered ? Number(draft.spendDelivered) : undefined,
      evidenceAssetId: draft.evidenceAssetId || undefined,
    }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.agent.campaign(id) });
      pushToast({
        title: 'Delivery report saved.',
        description: 'The latest live reporting update is now visible on the campaign.',
      });
    },
    onError: (error) => pushAgentMutationError(pushToast, 'Could not save delivery report.', error),
  });

  const campaign = campaignQuery.data;
  const prospectPackageBandId = prospectPackageBandState && prospectPackageBandState.campaignId === campaign?.id
    ? prospectPackageBandState.packageBandId
    : (campaign?.packageBandId ?? '');
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
  const isProspectiveCampaign = campaign.status === 'awaiting_purchase';
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
  const showExecutionOperations = false;
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
  const confidenceScore = calculateConfidence(campaign.brief);
  const audienceSummary = buildAudienceSummary(campaign.brief);
  const geoSummary = buildGeoSummary(campaign.brief);
  const channelSummary = buildChannelSummary(campaign.brief, selectedPlanItems);
  const toneSummary = buildToneSummary(campaign.brief);
  const originalPrompt = buildOriginalPrompt(campaign.brief);
  const clientNotes = campaign.brief?.specialRequirements ?? campaign.brief?.creativeNotes ?? campaign.brief?.targetAudienceNotes ?? 'No client notes captured yet.';
  const statusLabel = campaign.status === 'creative_approved' || campaign.status === 'booking_in_progress' || campaign.status === 'launched'
    ? titleCase(campaign.status)
    : activeRecommendation?.status
      ? titleCase(activeRecommendation.status)
      : titleCase(campaign.status);
  const recommendationTitle = activeRecommendation?.summary || 'Draft recommendation';
  const canMarkLive = false;
  const recommendationStatus = activeRecommendation?.status?.toLowerCase() ?? '';
  const awaitingClientReview = campaign.status === 'review_ready' || recommendationStatus === 'sent_to_client';
  const recommendationWorkflowLocked = showExecutionOperations || recommendationStatus === 'approved' || awaitingClientReview;
  const showRecommendationEditing = !recommendationWorkflowLocked;
  const showAiStudioHandoff = campaign.status === 'approved'
    || campaign.status === 'creative_changes_requested'
    || campaign.status === 'creative_sent_to_client_for_approval'
    || campaign.status === 'creative_approved'
    || campaign.status === 'booking_in_progress'
    || campaign.status === 'launched';
  const canEditDraftRecommendation = !recommendationWorkflowLocked && activeRecommendation?.status?.toLowerCase() === 'draft';
  const canModifyPlan = canEditDraftRecommendation && !draftApprovalCaptured;
  const hasSendableProposal = !recommendationWorkflowLocked && recommendations.length >= 1;
  const hasOohRecommendation = selectedPlanItems.some((item) => normalizeChannelKey(item.type) === 'OOH');
  const lockedNextStep = campaign.status === 'approved'
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
  const plannedStartLabel = campaign.brief?.startDate ? formatDate(campaign.brief.startDate) : 'Not set';
  const plannedEndLabel = campaign.brief?.endDate ? formatDate(campaign.brief.endDate) : 'Not set';
  const effectiveEndLabel = campaign.effectiveEndDate ? formatDate(`${campaign.effectiveEndDate}T00:00:00`) : 'Not set';
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
  const executionAssets = campaign.assets ?? [];
  const supplierBookings = campaign.supplierBookings ?? [];
  const deliveryReports = campaign.deliveryReports ?? [];

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

    const suggestions = inventoryItems
      .filter((item) => item.id !== currentItem.id && item.type === currentItem.type)
      .slice(0, 2)
      .map((item) => item.station);

    pushToast({
      title: 'Pick a replacement below.',
      description: suggestions.length > 0
        ? `Suggested swaps: ${suggestions.join(', ')}.`
        : 'Browse the inventory library below to replace this line.',
    }, 'info');
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

  function handleUpdateProspectPricing() {
    if (!prospectPackageBandId) {
      pushToast({
        title: 'Select a package band.',
        description: 'Choose the package band for this prospect first.',
      }, 'error');
      return;
    }

    updateProspectPricingMutation.mutate();
  }

  return (
    <section className="page-shell space-y-8">
      {saveMutation.isPending || sendMutation.isPending || assignMutation.isPending || unassignMutation.isPending || regenerateMutation.isPending || markLiveMutation.isPending ? (
        <ProcessingOverlay
          label={
            sendMutation.isPending
              ? 'Sending recommendation to the client...'
              : markLiveMutation.isPending
                ? 'Marking the campaign live...'
              : assignMutation.isPending
                ? 'Assigning this campaign to you...'
                : unassignMutation.isPending
                  ? 'Returning this campaign to the shared queue...'
                  : regenerateMutation.isPending
                    ? 'Regenerating the recommendation from the latest campaign inputs...'
                  : 'Saving recommendation draft...'
          }
        />
      ) : null}

      <div className="panel border-brand/10 bg-white/80 px-6 py-6 sm:px-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="hero-kicker">Agent review workspace</div>
            <h1 className="mt-3 text-3xl font-semibold tracking-tight text-ink">{campaign.campaignName}</h1>
            <p className="mt-3 max-w-3xl text-sm leading-7 text-ink-soft">
              User expresses intent, AI drafts the direction, and the agent validates and elevates the final recommendation.
            </p>
          </div>
          <div className="rounded-[20px] border border-line bg-slate-50 px-4 py-4 text-sm">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Ownership</p>
            <p className="mt-2 font-semibold text-ink">
              {campaign.isAssignedToCurrentUser
                ? 'Assigned to you'
                : campaign.isUnassigned
                  ? 'Unassigned'
                  : `Assigned to ${campaign.assignedAgentName ?? 'another agent'}`}
            </p>
            <p className="mt-2 leading-7 text-ink-soft">{campaign.nextAction}</p>
          </div>
        </div>
      </div>

      <div className="panel border-brand/10 bg-white/80 px-6 py-6 sm:px-8">
        <AgentStepper campaign={campaign} />
      </div>

      <div className="grid gap-6 lg:grid-cols-[360px_1fr] xl:grid-cols-[380px_1fr]">
        <div className="space-y-5 lg:sticky lg:top-24 lg:self-start">
          <div className="panel px-5 py-5">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Order</p>
            <h2 className="mt-3 text-xl font-semibold text-ink">{campaign.packageBandName}</h2>
            <p className="mt-2 text-sm text-ink-soft">
              {selectedPackageBand
                ? `Package range: ${formatPackageRange(selectedPackageBand.minBudget, selectedPackageBand.maxBudget)}`
                : `Package target: ${formatCurrency(campaign.selectedBudget)}`}
            </p>
            {activeRecommendation ? (
              <p className="mt-2 text-sm font-semibold text-ink">{activeProposalLabel}: {formatCurrency(activeRecommendation.totalCost)}</p>
            ) : null}
            <div className="mt-4 inline-flex rounded-full bg-brand-soft px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-brand">
              {statusLabel}
            </div>
          </div>

          {isProspectiveCampaign && campaign.isAssignedToCurrentUser ? (
            <div className="panel px-5 py-5">
              <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Prospect pricing</p>
              <p className="mt-2 text-sm leading-7 text-ink-soft">
                If this prospect was captured on the wrong package band, update it here before conversion.
              </p>
              <div className="mt-4 space-y-3">
                <label className="block">
                  <span className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Package band</span>
                  <select
                    value={prospectPackageBandId}
                    onChange={(event) => setProspectPackageBandState({ campaignId: campaign.id, packageBandId: event.target.value })}
                    className="input-base mt-2"
                  >
                    <option value="">Select package</option>
                    {(packagesQuery.data ?? []).map((item) => (
                      <option key={item.id} value={item.id}>{item.name}</option>
                    ))}
                  </select>
                </label>
                <p className="text-xs text-ink-soft">
                  The budget is now derived automatically from the selected package band.
                </p>
                <button
                  type="button"
                  disabled={updateProspectPricingMutation.isPending}
                  onClick={handleUpdateProspectPricing}
                  className="button-primary inline-flex w-full items-center justify-center gap-2 px-4 py-2 disabled:opacity-60"
                >
                  {updateProspectPricingMutation.isPending ? 'Updating...' : 'Update package'}
                </button>
              </div>
            </div>
          ) : null}

          <div className="panel px-5 py-5">
            <div className="flex items-start gap-3">
              <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                <Building2 className="size-4" />
              </div>
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Client</p>
                <p className="mt-3 text-lg font-semibold text-ink">{campaign.businessName ?? campaign.clientName ?? 'Client account'}</p>
                <p className="mt-2 text-sm text-ink-soft">{campaign.industry ?? 'Industry not captured'} | {geoSummary}</p>
                <p className="mt-2 line-clamp-4 text-sm text-ink-soft">{clientNotes}</p>
              </div>
            </div>
          </div>

          <div className="panel px-5 py-5">
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-start gap-3">
                <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                  <BrainCircuit className="size-4" />
                </div>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">AI inputs</p>
                  <div className="mt-3 space-y-1.5 text-sm text-ink">
                    <p><span className="font-semibold">Objective:</span> {campaign.brief?.objective ?? 'Not set'}</p>
                    <p><span className="font-semibold">Audience:</span> {audienceSummary}</p>
                    <p><span className="font-semibold">Geo:</span> {geoSummary}</p>
                    <p><span className="font-semibold">Channels:</span> {channelSummary}</p>
                    <p><span className="font-semibold">Tone:</span> {toneSummary}</p>
                    <p><span className="font-semibold">Confidence:</span> {confidenceScore.toFixed(2)}</p>
                  </div>
                </div>
              </div>
            </div>
            {!recommendationWorkflowLocked ? (
              <Link to={`/agent/recommendations/new?campaignId=${campaign.id}`} className="button-secondary mt-4 inline-flex px-4 py-2">
                Edit inputs
              </Link>
            ) : (
              <p className="mt-4 text-sm leading-6 text-ink-soft">
                Inputs are locked here because the recommendation phase is complete.
              </p>
            )}
          </div>

          {showAiStudioHandoff ? (
            <div className="panel px-5 py-5">
              <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Campaign timeline</p>
              <div className="mt-3 space-y-2 text-sm text-ink">
                <p><span className="font-semibold">Planned start:</span> {plannedStartLabel}</p>
                <p><span className="font-semibold">Planned end:</span> {plannedEndLabel}</p>
                <p><span className="font-semibold">Duration:</span> {campaign.brief?.durationWeeks ? `${campaign.brief.durationWeeks} week(s)` : 'Not set'}</p>
                {campaign.daysLeft != null ? <p><span className="font-semibold">Days left:</span> {campaign.daysLeft}</p> : null}
                {campaign.effectiveEndDate ? <p><span className="font-semibold">Current end date:</span> {effectiveEndLabel}</p> : null}
              </div>
            </div>
          ) : null}

          <div className="panel px-5 py-5">
            <div className="flex items-start gap-3">
              <div className="rounded-2xl bg-brand-soft p-3 text-brand">
                <MessageSquareQuote className="size-4" />
              </div>
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">User prompt</p>
                <p className="mt-3 text-sm leading-7 text-ink">{originalPrompt}</p>
              </div>
            </div>
          </div>
        </div>

        <div className="space-y-6">
          <AgentRecommendationPanel
            activeRecommendation={activeRecommendation}
            recommendations={recommendations}
            showRecommendationEditing={showRecommendationEditing}
            recommendationWorkflowLocked={recommendationWorkflowLocked}
            recommendationTitle={recommendationTitle}
            lockedNextStep={lockedNextStep}
            activeProposalLabel={activeProposalLabel}
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

          {showExecutionOperations ? (
            <div className="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
              <AgentOpsAssetsPanel
                executionAssets={executionAssets}
                isUploading={uploadOpsAssetMutation.isPending}
                onUpload={(input) => uploadOpsAssetMutation.mutate(input)}
              />

              <div className="space-y-6">
                <AgentBookingPanel
                  supplierBookings={supplierBookings}
                  executionAssets={executionAssets}
                  isSaving={saveBookingMutation.isPending}
                  onSave={(draft) => saveBookingMutation.mutate(draft)}
                  formatChannelLabel={formatChannelLabel}
                  formatCurrency={formatCurrency}
                  titleCase={titleCase}
                />

                <AgentDeliveryReportPanel
                  deliveryReports={deliveryReports}
                  supplierBookings={supplierBookings}
                  executionAssets={executionAssets}
                  isSaving={saveReportMutation.isPending}
                  onSave={(draft) => saveReportMutation.mutate(draft)}
                  titleCase={titleCase}
                />
              </div>
            </div>
          ) : null}

          <div className="flex flex-wrap justify-end gap-3">
            {campaign.isAssignedToCurrentUser ? (
              <button
                type="button"
                disabled={unassignMutation.isPending}
                onClick={() => unassignMutation.mutate()}
                className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
              >
                <UserX2 className="size-4" />
                Unassign
              </button>
            ) : (
              <button
                type="button"
                disabled={assignMutation.isPending}
                onClick={() => assignMutation.mutate()}
                className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
              >
                <UserPlus2 className="size-4" />
                Assign to me
              </button>
            )}
            {campaign.recommendationPdfUrl ? (
              <button
                type="button"
                onClick={() => void handleDownloadRecommendationPdf()}
                className="button-secondary inline-flex items-center gap-2 px-5 py-3"
              >
                <Download className="size-4" />
                Preview client PDF
              </button>
            ) : null}
            {awaitingClientReview ? (
              <button
                type="button"
                disabled
                className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:cursor-not-allowed disabled:opacity-60"
              >
                <Send className="size-4" />
                Sent to client
              </button>
            ) : null}
            {!recommendationWorkflowLocked ? (
              <button
                type="button"
                disabled={sendMutation.isPending || isOverBudget || !hasSendableProposal || !hasOohRecommendation}
                onClick={handleSendToClient}
                className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
              >
                <Send className="size-4" />
                Send to client
              </button>
            ) : null}
            {!isProspectiveCampaign && !recommendationWorkflowLocked ? (
              <button
                type="button"
                disabled={saveMutation.isPending || !canEditDraftRecommendation || draftApprovalCaptured || !hasOohRecommendation}
                onClick={() => void handleApproveRecommendation()}
                className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
              >
                <CircleCheckBig className="size-4" />
                {draftApprovalCaptured ? 'Draft finalized' : 'Finalize draft'}
              </button>
            ) : null}
            {canMarkLive ? (
              <button
                type="button"
                disabled={markLiveMutation.isPending}
                onClick={() => markLiveMutation.mutate()}
                className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
              >
                <CircleCheckBig className="size-4" />
                Mark campaign live
              </button>
            ) : null}
          </div>

          <AgentInventorySelectionModal
            isOpen={showRecommendationEditing && inventoryModalOpen}
            items={visibleInventoryItems}
            selectedItemIds={selectedInventoryIds}
            canModifyPlan={canModifyPlan}
            onClose={() => setInventoryModalOpen(false)}
            onToggleItem={(item) => toggleInventoryItem(item as SelectedPlanInventoryItem)}
            formatChannelLabel={formatChannelLabel}
          />
        </div>
      </div>
    </section>
  );
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowRightLeft,
  BrainCircuit,
  Building2,
  CircleAlert,
  CircleCheckBig,
  Download,
  MessageSquareQuote,
  RefreshCcw,
  Search,
  Send,
  SlidersHorizontal,
  UserPlus2,
  UserX2,
  X,
} from 'lucide-react';
import { useDeferredValue, useEffect, useMemo, useRef, useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
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
import { canAccessAiStudioForStatus } from '../../features/campaigns/aiStudioAccess';
import { InventoryTable } from '../../features/agent/components/InventoryTable';
import { invalidateAgentCampaignQueries, queryKeys } from '../../lib/queryKeys';
import { formatCurrency, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { RecommendationItem, SelectedPlanInventoryItem } from '../../types/domain';

type DisplayPlanItem = SelectedPlanInventoryItem | RecommendationItem;

function formatChannelLabel(channel: string) {
  return normalizeChannelKey(channel) === 'OOH' ? 'Billboards and Digital Screens' : titleCase(channel.toLowerCase());
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
  const [inventoryTypeFilter, setInventoryTypeFilter] = useState('all');
  const [inventoryRegionFilter, setInventoryRegionFilter] = useState('all');
  const [inventoryLanguageFilter, setInventoryLanguageFilter] = useState('all');
  const [inventorySearchInput, setInventorySearchInput] = useState('');
  const [opsAssetFile, setOpsAssetFile] = useState<File | null>(null);
  const [opsAssetType, setOpsAssetType] = useState('proof_of_booking');
  const [bookingDraft, setBookingDraft] = useState({
    supplierOrStation: '',
    channel: 'radio',
    bookingStatus: 'planned',
    committedAmount: '',
    liveFrom: '',
    liveTo: '',
    notes: '',
    proofAssetId: '',
  });
  const [reportDraft, setReportDraft] = useState({
    supplierBookingId: '',
    reportType: 'delivery_update',
    headline: '',
    summary: '',
    impressions: '',
    playsOrSpots: '',
    spendDelivered: '',
    evidenceAssetId: '',
  });
  const [prospectPackageBandId, setProspectPackageBandId] = useState('');
  const mixPanelRef = useRef<HTMLDivElement | null>(null);

  const campaignQuery = useQuery({ queryKey: queryKeys.agent.campaign(id), queryFn: () => advertifiedApi.getAgentCampaign(id) });
  const inventoryQuery = useQuery({
    queryKey: queryKeys.agent.inventory(id),
    queryFn: () => advertifiedApi.getInventory(id),
  });
  const packagesQuery = useQuery({
    queryKey: queryKeys.packages.all,
    queryFn: () => advertifiedApi.getPackages(),
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
  });

  const sendMutation = useMutation({
    mutationFn: () => advertifiedApi.sendRecommendationToClient(id),
    onSuccess: async () => {
      await invalidateAgentCampaignQueries(queryClient, id);
      pushToast({
        title: 'Recommendation sent to client.',
        description: 'The campaign has moved into the client review stage.',
      });
      navigate('/agent/approvals');
    },
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
    onError: (error) => pushToast({
      title: 'Could not update prospect pricing.',
      description: error instanceof Error ? error.message : 'Please try again.',
    }, 'error'),
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
      pushToast({
        title: 'Could not mark campaign live.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
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
    onError: (error) => pushToast({
      title: 'We could not regenerate the recommendation.',
      description: error instanceof Error ? error.message : 'Please try again in a moment.',
    }, 'error'),
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
  });

  const uploadOpsAssetMutation = useMutation({
    mutationFn: ({ file, type }: { file: File; type: string }) => advertifiedApi.uploadAgentCampaignAsset(id, file, type),
    onSuccess: async () => {
      setOpsAssetFile(null);
      await queryClient.invalidateQueries({ queryKey: queryKeys.agent.campaign(id) });
      pushToast({
        title: 'Campaign asset uploaded.',
        description: 'The file is now available for supplier proof, delivery reporting, or execution support.',
      });
    },
    onError: (error) => pushToast({
      title: 'Could not upload campaign asset.',
      description: error instanceof Error ? error.message : 'Please try again.',
    }, 'error'),
  });

  const saveBookingMutation = useMutation({
    mutationFn: () => advertifiedApi.saveSupplierBooking(id, {
      supplierOrStation: bookingDraft.supplierOrStation,
      channel: bookingDraft.channel,
      bookingStatus: bookingDraft.bookingStatus,
      committedAmount: Number(bookingDraft.committedAmount || 0),
      liveFrom: bookingDraft.liveFrom || undefined,
      liveTo: bookingDraft.liveTo || undefined,
      notes: bookingDraft.notes || undefined,
      proofAssetId: bookingDraft.proofAssetId || undefined,
    }),
    onSuccess: async () => {
      setBookingDraft({
        supplierOrStation: '',
        channel: 'radio',
        bookingStatus: 'planned',
        committedAmount: '',
        liveFrom: '',
        liveTo: '',
        notes: '',
        proofAssetId: '',
      });
      await queryClient.invalidateQueries({ queryKey: queryKeys.agent.campaign(id) });
      pushToast({
        title: 'Supplier booking saved.',
        description: 'Execution details are now attached to this campaign.',
      });
    },
    onError: (error) => pushToast({
      title: 'Could not save supplier booking.',
      description: error instanceof Error ? error.message : 'Please try again.',
    }, 'error'),
  });

  const saveReportMutation = useMutation({
    mutationFn: () => advertifiedApi.saveDeliveryReport(id, {
      supplierBookingId: reportDraft.supplierBookingId || undefined,
      reportType: reportDraft.reportType,
      headline: reportDraft.headline,
      summary: reportDraft.summary || undefined,
      impressions: reportDraft.impressions ? Number(reportDraft.impressions) : undefined,
      playsOrSpots: reportDraft.playsOrSpots ? Number(reportDraft.playsOrSpots) : undefined,
      spendDelivered: reportDraft.spendDelivered ? Number(reportDraft.spendDelivered) : undefined,
      evidenceAssetId: reportDraft.evidenceAssetId || undefined,
    }),
    onSuccess: async () => {
      setReportDraft({
        supplierBookingId: '',
        reportType: 'delivery_update',
        headline: '',
        summary: '',
        impressions: '',
        playsOrSpots: '',
        spendDelivered: '',
        evidenceAssetId: '',
      });
      await queryClient.invalidateQueries({ queryKey: queryKeys.agent.campaign(id) });
      pushToast({
        title: 'Delivery report saved.',
        description: 'The latest live reporting update is now visible on the campaign.',
      });
    },
    onError: (error) => pushToast({
      title: 'Could not save delivery report.',
      description: error instanceof Error ? error.message : 'Please try again.',
    }, 'error'),
  });

  const campaign = campaignQuery.data;
  const inventoryItems = inventoryQuery.data ?? [];
  const selectedPackageBand = packagesQuery.data?.find((item) => item.id === campaign?.packageBandId) ?? null;
  const recommendations = campaign?.recommendations.length
    ? campaign.recommendations
    : (campaign?.recommendation ? [campaign.recommendation] : []);
  const selectedRecommendationId = selectedRecommendationIdState || recommendations[0]?.id || '';
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
  const deferredInventorySearchInput = useDeferredValue(inventorySearchInput);

  useEffect(() => {
    if (!campaign) {
      return;
    }

    setProspectPackageBandId(campaign.packageBandId);
  }, [campaign?.id, campaign?.packageBandId]);

  if (campaignQuery.isLoading || inventoryQuery.isLoading || !campaign) {
    return <LoadingState label="Loading agent campaign detail..." />;
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
  const inventoryTypeOptions = Array.from(new Set(visibleInventoryItems.map((item) => item.type))).sort();
  const inventoryRegionOptions = Array.from(new Set(visibleInventoryItems.map((item) => item.region).filter((value) => value?.trim()))).sort();
  const inventoryLanguageOptions = Array.from(new Set(visibleInventoryItems.map((item) => item.language).filter((value) => value?.trim()))).sort();
  const inventorySearchQuery = deferredInventorySearchInput.trim();
  const inventorySearchActive = inventorySearchQuery.length >= 3;
  const filteredInventoryItems = visibleInventoryItems.filter((item) => {
    if (inventoryTypeFilter !== 'all' && item.type !== inventoryTypeFilter) {
      return false;
    }

    if (inventoryRegionFilter !== 'all' && item.region !== inventoryRegionFilter) {
      return false;
    }

    if (inventoryLanguageFilter !== 'all' && item.language !== inventoryLanguageFilter) {
      return false;
    }

    if (!inventorySearchActive) {
      return true;
    }

    const haystack = [
      item.type,
      item.station,
      item.region,
      item.language,
      item.showDaypart,
      item.timeBand,
      item.slotType,
      item.duration,
      item.restrictions,
    ].join(' ').toLowerCase();

    return haystack.includes(inventorySearchQuery.trim().toLowerCase());
  });
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
  const prospectiveBelowBandDelta = isProspectiveCampaign && selectedPackageBand
    ? selectedPackageBand.minBudget - effectivePlannedTotal
    : 0;
  const prospectiveAboveBandDelta = isProspectiveCampaign && selectedPackageBand
    ? effectivePlannedTotal - selectedPackageBand.maxBudget
    : 0;
  const isOutsideProspectBand = isProspectiveCampaign && selectedPackageBand
    ? effectivePlannedTotal < selectedPackageBand.minBudget || effectivePlannedTotal > selectedPackageBand.maxBudget
    : false;
  const isOverBudget = isProspectiveCampaign ? isOutsideProspectBand : budgetDelta < 0;
  const activeProposalLabel = activeRecommendation?.proposalLabel ?? 'Current proposal';
  const showExecutionOperations = (
    campaign.status === 'approved'
    || campaign.status === 'creative_changes_requested'
    || campaign.status === 'creative_sent_to_client_for_approval'
    || campaign.status === 'creative_approved'
    || campaign.status === 'launched'
  );
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
  const statusLabel = campaign.status === 'creative_approved' || campaign.status === 'launched'
    ? titleCase(campaign.status)
    : activeRecommendation?.status
      ? titleCase(activeRecommendation.status)
      : titleCase(campaign.status);
  const recommendationTitle = activeRecommendation?.summary || 'Draft recommendation';
  const canMarkLive = campaign.status === 'creative_approved';
  const canEditDraftRecommendation = activeRecommendation?.status?.toLowerCase() === 'draft';
  const canModifyPlan = canEditDraftRecommendation && !draftApprovalCaptured;
  const hasSendableProposal = recommendations.length >= 1;
  const hasOohRecommendation = selectedPlanItems.some((item) => normalizeChannelKey(item.type) === 'OOH');
  const budgetConstraint: BudgetConstraintContext = isProspectiveCampaign && selectedPackageBand
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
        description: 'Only draft recommendations can be edited. Create a new revision to make changes.',
      }, 'info');
      return;
    }

    if (draftApprovalCaptured) {
      pushToast({
        title: 'Recommendation already approved.',
        description: 'Use Send to client to move this campaign to the next step.',
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
    regenerateMutation.mutate();
  }

  function handleAdjustMix() {
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

      <div className="grid gap-6 lg:grid-cols-[360px_1fr] xl:grid-cols-[380px_1fr]">
        <div className="space-y-5 lg:sticky lg:top-24 lg:self-start">
          <div className="panel px-5 py-5">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-ink-soft">Order</p>
            <h2 className="mt-3 text-xl font-semibold text-ink">{campaign.packageBandName}</h2>
            <p className="mt-2 text-sm text-ink-soft">
              {isProspectiveCampaign && selectedPackageBand
                ? `Package range: ${formatCurrency(selectedPackageBand.minBudget)} to ${formatCurrency(selectedPackageBand.maxBudget)}`
                : `Package target: ${formatCurrency(campaign.selectedBudget)}`}
            </p>
            {isProspectiveCampaign && selectedPackageBand ? (
              <p className="mt-2 text-sm text-ink-soft">
                Planning reference: {formatCurrency(campaign.selectedBudget)}
              </p>
            ) : null}
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
                    onChange={(event) => setProspectPackageBandId(event.target.value)}
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
            <Link to={`/agent/recommendations/new?campaignId=${campaign.id}`} className="button-secondary mt-4 inline-flex px-4 py-2">
              Edit inputs
            </Link>
          </div>

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
          {activeRecommendation?.clientFeedbackNotes ? (
            <div className="panel border-amber-200 bg-amber-50/80 px-6 py-5">
              <p className="text-sm font-semibold text-amber-800">Client feedback</p>
              <p className="mt-2 text-sm leading-7 text-amber-900">{activeRecommendation.clientFeedbackNotes}</p>
            </div>
          ) : null}

          {activeRecommendation?.manualReviewRequired ? (
            <div className="panel border-rose-200 bg-rose-50/80 px-6 py-5">
              <p className="text-sm font-semibold text-rose-800">Manual review required</p>
              <p className="mt-2 text-sm leading-7 text-rose-900">
                The planner could not fully satisfy package policy or inventory requirements for this draft.
              </p>
              {activeRecommendation.fallbackFlags.length > 0 ? (
                <div className="mt-3 flex flex-wrap gap-2">
                  {activeRecommendation.fallbackFlags.map((flag) => (
                    <span key={flag} className="rounded-full bg-white px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.12em] text-rose-700 ring-1 ring-rose-200">
                      {formatFallbackFlag(flag)}
                    </span>
                  ))}
                </div>
              ) : null}
            </div>
          ) : null}

          {recommendations.length > 1 ? (
            <div className="panel px-6 py-5">
              <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Proposal set</p>
              <div className="mt-4 flex flex-wrap gap-3">
                {recommendations.map((proposal) => {
                  const isActive = proposal.id === activeRecommendation?.id;
                  return (
                    <button
                      key={proposal.id}
                      type="button"
                      onClick={() => setSelectedRecommendationIdState(proposal.id)}
                      className={`rounded-[18px] border px-4 py-3 text-left transition ${
                        isActive ? 'border-brand bg-brand-soft' : 'border-line bg-slate-50 hover:border-brand/30'
                      }`}
                    >
                      <p className="text-sm font-semibold text-ink">{proposal.proposalLabel ?? 'Proposal'}</p>
                      <p className="mt-1 text-sm text-ink-soft">{proposal.proposalStrategy ?? 'Recommendation option'}</p>
                      <p className="mt-2 text-sm font-semibold text-ink">{formatCurrency(proposal.totalCost)}</p>
                      <p className="mt-1 text-xs uppercase tracking-[0.14em] text-ink-soft">{proposal.items.length} line item(s)</p>
                    </button>
                  );
                })}
              </div>
            </div>
          ) : null}

          <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
              <div>
                <h2 className="text-xl font-semibold text-ink">Recommendation</h2>
                <p className="mt-2 text-sm text-ink-soft">{recommendationTitle}</p>
              </div>
              <div className="flex flex-wrap gap-2">
                <button type="button" onClick={handleRegenerate} disabled={regenerateMutation.isPending} className="button-secondary inline-flex items-center gap-2 px-4 py-2 disabled:opacity-60">
                  <RefreshCcw className="size-4" />
                  Regenerate
                </button>
                <button type="button" onClick={handleAdjustMix} className="button-secondary inline-flex items-center gap-2 px-4 py-2">
                  <SlidersHorizontal className="size-4" />
                  Adjust mix
                </button>
              </div>
            </div>

            <div className="mt-6 space-y-5">
              <div ref={mixPanelRef} className="rounded-[16px] border border-line bg-slate-50 px-4 py-4">
                <h3 className="text-sm font-semibold text-ink">Budget split</h3>
                <input
                  type="range"
                  min={0}
                  max={100}
                  value={mixBalance}
                  onChange={(event) => setMixBalance(Number(event.target.value))}
                  className="mt-4 w-full accent-brand"
                />
                <p className="mt-3 text-sm text-ink-soft">
                  Target mix: Radio {targetMix.radio}% | Billboards and Digital Screens {targetMix.ooh}% | TV {targetMix.tv}% | Digital {targetMix.digital}%
                </p>
                <p className="mt-1 text-sm text-ink-soft">
                  Current draft: Radio {currentRadioShare}% | Billboards and Digital Screens {currentOohShare}% | TV {currentTvShare}% | Digital {currentDigitalShare}%
                </p>
              </div>

              {Object.entries(displayedGroups).length > 0 ? Object.entries(displayedGroups).map(([channel, items]) => (
                <div key={channel}>
                  <p className="mb-3 text-sm font-semibold text-ink">{formatChannelLabel(channel)}</p>
                  <div className="grid gap-2.5 md:grid-cols-2">
                    {items.map((item) => (
                      <div
                        key={item.id}
                        className="group min-w-[210px] rounded-[16px] border border-line bg-slate-50 px-3.5 py-3"
                      >
                        <div className="flex items-start justify-between gap-3">
                          <div>
                            <p className="text-sm font-semibold text-ink">{'station' in item ? item.station : item.title}</p>
                            <p className="mt-1 text-xs text-ink-soft">
                              {'rate' in item
                                ? `${formatCurrency(item.rate * item.quantity)}${item.quantity > 1 ? ` | Qty ${item.quantity}` : ''}`
                                : `${formatCurrency(item.cost)}${item.quantity > 1 ? ` | Qty ${item.quantity}` : ''}`}
                            </p>
                            {!('station' in item) && item.confidenceScore !== undefined ? (
                              <p className="mt-1 text-[11px] font-medium uppercase tracking-[0.12em] text-brand">
                                {formatConfidenceLabel(item.confidenceScore)}
                              </p>
                            ) : null}
                            {!('station' in item) ? (
                              <>
                                <p className="mt-1 text-xs text-ink-soft line-clamp-2">{item.rationale}</p>
                                {item.selectionReasons.length > 0 ? (
                                  <div className="mt-2 flex flex-wrap gap-1.5">
                                    {item.selectionReasons.slice(0, 3).map((reason) => (
                                      <span key={reason} className="rounded-full bg-white px-2 py-1 text-[11px] text-ink-soft ring-1 ring-line">
                                        {reason}
                                      </span>
                                    ))}
                                  </div>
                                ) : null}
                              </>
                            ) : null}
                          </div>
                          {'station' in item && canModifyPlan ? (
                            <button
                              type="button"
                              onClick={() => handleReplace(item.id)}
                              className="inline-flex items-center gap-1 rounded-full border border-brand/15 bg-white px-2.5 py-1 text-[11px] font-semibold text-brand transition group-hover:border-brand/30"
                            >
                              <ArrowRightLeft className="size-3" />
                              Replace
                            </button>
                          ) : (
                            <span className="inline-flex rounded-full border border-brand/15 bg-white px-2.5 py-1 text-[11px] font-semibold text-brand">
                              {'station' in item ? 'Locked' : 'AI draft'}
                            </span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )) : (
                <EmptyState
                  title="No recommendation lines yet"
                  description="Generate the recommendation first, or use the inventory table below to add radio, Billboards and Digital Screens, or digital lines manually."
                />
              )}
            </div>
          </div>

          <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
            <h2 className="text-xl font-semibold text-ink">Validation</h2>
            <div className="mt-4 grid gap-3 md:grid-cols-3">
              {constraintChecks.map((check) => (
                <div
                  key={check.label}
                  className={`rounded-[14px] border px-4 py-3 ${
                    check.ok ? 'border-emerald-200 bg-emerald-50' : 'border-rose-200 bg-rose-50'
                  }`}
                >
                  <div className="flex items-start gap-3">
                    {check.ok ? (
                      <CircleCheckBig className="mt-0.5 size-4 text-emerald-700" />
                    ) : (
                      <CircleAlert className="mt-0.5 size-4 text-rose-700" />
                    )}
                    <div>
                      <p className={`text-sm font-semibold ${check.ok ? 'text-emerald-800' : 'text-rose-800'}`}>{check.label}</p>
                      <p className={`text-sm ${check.ok ? 'text-emerald-700' : 'text-rose-700'}`}>{check.detail}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
            {isOverBudget ? (
              <p className="mt-4 text-sm text-rose-700">
                {isProspectiveCampaign && selectedPackageBand
                  ? effectivePlannedTotal > selectedPackageBand.maxBudget
                    ? `This draft is ${formatCurrency(Math.abs(prospectiveAboveBandDelta))} over the selected package band maximum.`
                    : `This draft is ${formatCurrency(Math.abs(prospectiveBelowBandDelta))} below the selected package band minimum.`
                  : `This draft is ${formatCurrency(Math.abs(budgetDelta))} over the client&apos;s budget.`}
              </p>
            ) : null}
          </div>

          {showExecutionOperations ? (
            <div className="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
              <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
                <h2 className="text-xl font-semibold text-ink">Execution files</h2>
                <div className="mt-4 space-y-3">
                  {executionAssets.length > 0 ? executionAssets.map((asset) => (
                    <div key={asset.id} className="rounded-[16px] border border-line bg-slate-50 px-4 py-3">
                      <p className="text-sm font-semibold text-ink">{asset.displayName}</p>
                      <p className="mt-1 text-xs text-ink-soft">{asset.assetType.replace(/_/g, ' ')}</p>
                      {asset.publicUrl ? (
                        <a href={asset.publicUrl} target="_blank" rel="noreferrer" className="button-secondary mt-3 inline-flex px-3 py-2 text-xs">
                          <Download className="size-3.5" />
                          Open file
                        </a>
                      ) : null}
                    </div>
                  )) : (
                    <div className="rounded-[16px] border border-line bg-slate-50 px-4 py-3 text-sm text-ink-soft">
                      No proof, delivery, or support files uploaded yet.
                    </div>
                  )}
                </div>
                <div className="mt-5 space-y-3">
                  <select value={opsAssetType} onChange={(event) => setOpsAssetType(event.target.value)} className="input-base">
                    <option value="proof_of_booking">Proof of booking</option>
                    <option value="delivery_proof">Delivery proof</option>
                    <option value="supporting_asset">Supporting asset</option>
                  </select>
                  <input type="file" onChange={(event) => setOpsAssetFile(event.target.files?.[0] ?? null)} className="input-base" />
                  <button
                    type="button"
                    disabled={!opsAssetFile || uploadOpsAssetMutation.isPending}
                    onClick={() => {
                      if (!opsAssetFile) return;
                      uploadOpsAssetMutation.mutate({ file: opsAssetFile, type: opsAssetType });
                    }}
                    className="button-secondary inline-flex w-full items-center justify-center gap-2 px-4 py-3 disabled:opacity-60"
                  >
                    Upload execution file
                  </button>
                </div>
              </div>

              <div className="space-y-6">
                <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
                  <h2 className="text-xl font-semibold text-ink">Supplier execution</h2>
                  <div className="mt-4 grid gap-3 md:grid-cols-2">
                    <input className="input-base" placeholder="Supplier or station" value={bookingDraft.supplierOrStation} onChange={(event) => setBookingDraft((current) => ({ ...current, supplierOrStation: event.target.value }))} />
                    <select className="input-base" value={bookingDraft.channel} onChange={(event) => setBookingDraft((current) => ({ ...current, channel: event.target.value }))}>
                      <option value="radio">Radio</option>
                      <option value="ooh">Billboards and Digital Screens</option>
                      <option value="tv">TV</option>
                      <option value="digital">Digital</option>
                    </select>
                    <select className="input-base" value={bookingDraft.bookingStatus} onChange={(event) => setBookingDraft((current) => ({ ...current, bookingStatus: event.target.value }))}>
                      <option value="planned">Planned</option>
                      <option value="booked">Booked</option>
                      <option value="live">Live</option>
                      <option value="completed">Completed</option>
                    </select>
                    <input className="input-base" type="number" min="0" step="0.01" placeholder="Committed amount" value={bookingDraft.committedAmount} onChange={(event) => setBookingDraft((current) => ({ ...current, committedAmount: event.target.value }))} />
                    <input className="input-base" type="date" value={bookingDraft.liveFrom} onChange={(event) => setBookingDraft((current) => ({ ...current, liveFrom: event.target.value }))} />
                    <input className="input-base" type="date" value={bookingDraft.liveTo} onChange={(event) => setBookingDraft((current) => ({ ...current, liveTo: event.target.value }))} />
                    <select className="input-base md:col-span-2" value={bookingDraft.proofAssetId} onChange={(event) => setBookingDraft((current) => ({ ...current, proofAssetId: event.target.value }))}>
                      <option value="">Proof asset (optional)</option>
                      {executionAssets.map((asset) => <option key={asset.id} value={asset.id}>{asset.displayName}</option>)}
                    </select>
                    <textarea className="input-base md:col-span-2 min-h-[96px]" placeholder="Booking notes" value={bookingDraft.notes} onChange={(event) => setBookingDraft((current) => ({ ...current, notes: event.target.value }))} />
                  </div>
                  <button
                    type="button"
                    disabled={!bookingDraft.supplierOrStation.trim() || saveBookingMutation.isPending}
                    onClick={() => saveBookingMutation.mutate()}
                    className="button-primary mt-4 inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
                  >
                    Save supplier booking
                  </button>
                  <div className="mt-4 space-y-3">
                    {supplierBookings.length > 0 ? supplierBookings.map((booking) => (
                      <div key={booking.id} className="rounded-[16px] border border-line bg-slate-50 px-4 py-3">
                        <p className="text-sm font-semibold text-ink">{booking.supplierOrStation}</p>
                        <p className="mt-1 text-xs text-ink-soft">{formatChannelLabel(booking.channel)} | {titleCase(booking.bookingStatus)} | {formatCurrency(booking.committedAmount)}</p>
                        <p className="mt-1 text-xs text-ink-soft">{booking.liveFrom ?? 'Start TBC'} to {booking.liveTo ?? 'End TBC'}</p>
                      </div>
                    )) : null}
                  </div>
                </div>

                <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
                  <h2 className="text-xl font-semibold text-ink">Live reporting</h2>
                  <div className="mt-4 grid gap-3 md:grid-cols-2">
                    <select className="input-base" value={reportDraft.reportType} onChange={(event) => setReportDraft((current) => ({ ...current, reportType: event.target.value }))}>
                      <option value="delivery_update">Delivery update</option>
                      <option value="performance_snapshot">Performance snapshot</option>
                      <option value="proof_of_flight">Proof of flight</option>
                    </select>
                    <select className="input-base" value={reportDraft.supplierBookingId} onChange={(event) => setReportDraft((current) => ({ ...current, supplierBookingId: event.target.value }))}>
                      <option value="">Related booking (optional)</option>
                      {supplierBookings.map((booking) => <option key={booking.id} value={booking.id}>{booking.supplierOrStation}</option>)}
                    </select>
                    <input className="input-base md:col-span-2" placeholder="Headline" value={reportDraft.headline} onChange={(event) => setReportDraft((current) => ({ ...current, headline: event.target.value }))} />
                    <textarea className="input-base md:col-span-2 min-h-[96px]" placeholder="Report summary" value={reportDraft.summary} onChange={(event) => setReportDraft((current) => ({ ...current, summary: event.target.value }))} />
                    <input className="input-base" type="number" min="0" step="1" placeholder="Impressions" value={reportDraft.impressions} onChange={(event) => setReportDraft((current) => ({ ...current, impressions: event.target.value }))} />
                    <input className="input-base" type="number" min="0" step="1" placeholder="Plays / spots" value={reportDraft.playsOrSpots} onChange={(event) => setReportDraft((current) => ({ ...current, playsOrSpots: event.target.value }))} />
                    <input className="input-base" type="number" min="0" step="0.01" placeholder="Spend delivered" value={reportDraft.spendDelivered} onChange={(event) => setReportDraft((current) => ({ ...current, spendDelivered: event.target.value }))} />
                    <select className="input-base" value={reportDraft.evidenceAssetId} onChange={(event) => setReportDraft((current) => ({ ...current, evidenceAssetId: event.target.value }))}>
                      <option value="">Evidence asset (optional)</option>
                      {executionAssets.map((asset) => <option key={asset.id} value={asset.id}>{asset.displayName}</option>)}
                    </select>
                  </div>
                  <button
                    type="button"
                    disabled={!reportDraft.headline.trim() || saveReportMutation.isPending}
                    onClick={() => saveReportMutation.mutate()}
                    className="button-primary mt-4 inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
                  >
                    Save delivery report
                  </button>
                  <div className="mt-4 space-y-3">
                    {deliveryReports.length > 0 ? deliveryReports.map((report) => (
                      <div key={report.id} className="rounded-[16px] border border-line bg-slate-50 px-4 py-3">
                        <p className="text-sm font-semibold text-ink">{report.headline}</p>
                        <p className="mt-1 text-xs text-ink-soft">{titleCase(report.reportType)}{report.impressions ? ` | ${report.impressions.toLocaleString()} impressions` : ''}{report.playsOrSpots ? ` | ${report.playsOrSpots} plays/spots` : ''}</p>
                        <p className="mt-1 text-xs text-ink-soft">{report.summary ?? 'No summary captured yet.'}</p>
                      </div>
                    )) : null}
                  </div>
                </div>
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
            {canAccessAiStudioForStatus(campaign.status) ? (
              <Link
                to={`/ai-studio?campaignId=${campaign.id}`}
                className="button-secondary inline-flex items-center gap-2 px-5 py-3"
              >
                <BrainCircuit className="size-4" />
                Prefill from approved recommendation
              </Link>
            ) : null}
            <button
              type="button"
              disabled={sendMutation.isPending || isOverBudget || !hasSendableProposal || !hasOohRecommendation}
              onClick={handleSendToClient}
              className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
            >
              <Send className="size-4" />
              Send to client
            </button>
            {!isProspectiveCampaign ? (
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

          <button
            type="button"
            onClick={() => setInventoryModalOpen(true)}
            className="panel flex w-full items-center justify-between gap-4 px-6 py-5 text-left transition hover:border-brand/30"
          >
            <div>
              <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Matching inventory</p>
              <p className="mt-2 text-sm leading-7 text-ink-soft">
                Click to open inventory, apply filters, and search supplier rows.
              </p>
            </div>
            <div className="rounded-full bg-brand-soft px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-brand">
              {selectedPlanItems.length} selected
            </div>
          </button>

          {inventoryModalOpen ? (
            <div className="fixed inset-0 z-[90] bg-slate-950/45 backdrop-blur-[1px]">
              <div className="mx-auto mt-8 flex h-[calc(100vh-4rem)] w-[min(1300px,95vw)] flex-col rounded-[24px] border border-line bg-white shadow-[0_25px_60px_rgba(15,23,42,0.22)]">
                <div className="flex items-center justify-between gap-3 border-b border-line px-6 py-4">
                  <div>
                    <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Matching inventory</p>
                    <p className="mt-1 text-sm text-ink-soft">Search starts filtering from the 3rd keystroke.</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => setInventoryModalOpen(false)}
                    className="button-secondary inline-flex items-center gap-2 px-3 py-2"
                  >
                    <X className="size-4" />
                    Close
                  </button>
                </div>

                <div className="grid gap-3 border-b border-line px-6 py-4 md:grid-cols-[1.3fr_0.8fr_0.8fr_0.8fr]">
                  <label className="relative">
                    <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-soft" />
                    <input
                      type="search"
                      value={inventorySearchInput}
                      onChange={(event) => setInventorySearchInput(event.target.value)}
                      placeholder="Search station, region, language, daypart..."
                      className="input-base pl-9"
                    />
                  </label>
                  <select className="input-base" value={inventoryTypeFilter} onChange={(event) => setInventoryTypeFilter(event.target.value)}>
                    <option value="all">All types</option>
                    {inventoryTypeOptions.map((value) => <option key={value} value={value}>{formatChannelLabel(value)}</option>)}
                  </select>
                  <select className="input-base" value={inventoryRegionFilter} onChange={(event) => setInventoryRegionFilter(event.target.value)}>
                    <option value="all">All regions</option>
                    {inventoryRegionOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                  </select>
                  <select className="input-base" value={inventoryLanguageFilter} onChange={(event) => setInventoryLanguageFilter(event.target.value)}>
                    <option value="all">All languages</option>
                    {inventoryLanguageOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                  </select>
                </div>

                <div className="flex items-center justify-between gap-3 px-6 py-3 text-xs text-ink-soft">
                  <span>
                    Showing {filteredInventoryItems.length} of {visibleInventoryItems.length} inventory row(s)
                  </span>
                  <span>{inventorySearchInput.trim().length > 0 && inventorySearchInput.trim().length < 3 ? 'Type at least 3 characters to start search filtering.' : ''}</span>
                </div>

                <div className="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-6 pb-6 pr-3">
                  <InventoryTable
                    items={filteredInventoryItems}
                    selectedItemIds={selectedInventoryIds}
                    onToggleItem={canModifyPlan ? ((item) => toggleInventoryItem(item as SelectedPlanInventoryItem)) : undefined}
                  />
                </div>
              </div>
            </div>
          ) : null}
        </div>
      </div>
    </section>
  );
}


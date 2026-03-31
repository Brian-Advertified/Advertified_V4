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
  Send,
  SlidersHorizontal,
  UserPlus2,
  UserX2,
} from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
import {
  buildAudienceSummary,
  buildChannelSummary,
  buildGeneratedInventoryFallback,
  buildGeoSummary,
  buildOriginalPrompt,
  buildTargetChannelMix,
  buildToneSummary,
  calculateConfidence,
  formatConfidenceLabel,
  groupGeneratedRecommendationItems,
  groupPlanItems,
  isInventoryRelevant,
  normalizeChannelKey,
} from '../../features/agent/agentCampaignDetailUtils';
import { InventoryTable } from '../../features/agent/components/InventoryTable';
import { invalidateAgentCampaignQueries, queryKeys } from '../../lib/queryKeys';
import { formatCurrency, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { RecommendationItem, SelectedPlanInventoryItem } from '../../types/domain';

type DisplayPlanItem = SelectedPlanInventoryItem | RecommendationItem;

function formatFallbackFlag(flag: string) {
  const normalized = flag.trim().toLowerCase();
  if (normalized.startsWith('preferred_media_unfulfilled:')) {
    const channel = normalized.split(':')[1]?.toUpperCase() ?? 'A preferred channel';
    return `${channel} was requested, but this package or the available inventory could not support it in the draft.`;
  }

  return flag.replace(/_/g, ' ');
}

function buildClientFacingSummary(items: SelectedPlanInventoryItem[]) {
  if (items.length === 0) {
    return 'No planned inventory lines selected yet.';
  }

  const lines = items.map((item) => {
    const lineCost = item.rate * item.quantity;
    const timeBand = item.timeBand?.trim() ? ` | ${item.timeBand}` : '';
    const qty = item.quantity > 1 ? ` | Qty ${item.quantity}` : '';
    return `- ${titleCase(item.type)}: ${item.station}${timeBand}${qty} | ${formatCurrency(lineCost)}`;
  });
  const total = items.reduce((sum, item) => sum + item.rate * item.quantity, 0);

  return [
    'Recommended inventory plan:',
    ...lines,
    `Total planned: ${formatCurrency(total)}`,
  ].join('\n');
}

export function AgentCampaignDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const [selectedPlanItems, setSelectedPlanItems] = useState<SelectedPlanInventoryItem[]>([]);
  const [strategySummary, setStrategySummary] = useState('');
  const [draftApprovalCaptured, setDraftApprovalCaptured] = useState(false);
  const [mixBalance, setMixBalance] = useState(60);
  const [selectedRecommendationIdState, setSelectedRecommendationIdState] = useState('');
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
  const mixPanelRef = useRef<HTMLDivElement | null>(null);
  const hydratedRecommendationKeyRef = useRef<string | null>(null);
  const autoSummaryRef = useRef('');

  const campaignQuery = useQuery({ queryKey: queryKeys.agent.campaign(id), queryFn: () => advertifiedApi.getAgentCampaign(id) });
  const inventoryQuery = useQuery({
    queryKey: queryKeys.agent.inventory(id),
    queryFn: () => advertifiedApi.getInventory(id),
  });

  const saveMutation = useMutation({
    mutationFn: (notes: string) => advertifiedApi.updateRecommendation(id, activeRecommendation?.id, notes, selectedPlanItems),
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
        description: `A fresh AI draft was prepared using target mix Radio ${targetMix.radio}% | OOH ${targetMix.ooh}% | TV ${targetMix.tv}% | Digital ${targetMix.digital}%.`,
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
  const recommendations = campaign?.recommendations.length
    ? campaign.recommendations
    : (campaign?.recommendation ? [campaign.recommendation] : []);
  const selectedRecommendationId = selectedRecommendationIdState || recommendations[0]?.id || '';
  const activeRecommendation = recommendations.find((item) => item.id === selectedRecommendationId) ?? recommendations[0];

  useEffect(() => {
    if (!campaign || !activeRecommendation) {
      return;
    }

    const byId = new Map(inventoryItems.map((item) => [item.id, item]));
    const selectedFromRecommendation = activeRecommendation.items
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

    const hydrationKey = `${campaign.id}:${activeRecommendation.id}:${inventoryItems.map((item) => item.id).join('|')}`;
    if (hydratedRecommendationKeyRef.current === hydrationKey) {
      return;
    }

    hydratedRecommendationKeyRef.current = hydrationKey;
    const autoSummary = buildClientFacingSummary(selectedFromRecommendation);
    autoSummaryRef.current = autoSummary;
    setStrategySummary(autoSummary);
    setSelectedPlanItems(selectedFromRecommendation);
    setDraftApprovalCaptured(false);
  }, [campaign, activeRecommendation, inventoryItems]);

  useEffect(() => {
    const autoSummary = buildClientFacingSummary(selectedPlanItems);
    const hasManualEdits = strategySummary.trim().length > 0 && strategySummary !== autoSummaryRef.current;
    if (!hasManualEdits) {
      setStrategySummary(autoSummary);
    }
    autoSummaryRef.current = autoSummary;
  }, [selectedPlanItems, strategySummary]);

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
  const budgetDelta = campaign.selectedBudget - effectivePlannedTotal;
  const isOverBudget = budgetDelta < 0;
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
  const recommendationTitle = strategySummary || activeRecommendation?.summary || 'Draft recommendation';
  const canMarkLive = campaign.status === 'creative_approved';
  const canEditDraftRecommendation = activeRecommendation?.status?.toLowerCase() === 'draft';
  const canModifyPlan = canEditDraftRecommendation && !draftApprovalCaptured;
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

  function toggleInventoryItem(item: SelectedPlanInventoryItem) {
    if (!canModifyPlan) {
      pushToast({
        title: 'Recommendation locked after approval.',
        description: 'Create a new draft revision if you need to change plan lines.',
      }, 'info');
      return;
    }

    setSelectedPlanItems((current) => {
      const existing = current.find((value) => value.id === item.id);
      if (existing) {
        return current.filter((value) => value.id !== item.id);
      }

      return [
        ...current,
        {
          ...item,
          quantity: 1,
          flighting: '',
          notes: '',
          startDate: '',
          endDate: '',
        },
      ];
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

    await saveMutation.mutateAsync(strategySummary);
    setDraftApprovalCaptured(true);
    pushToast({
      title: 'Recommendation approved.',
      description: 'The approved draft is saved. Opening Review & Send next.',
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
            <p className="mt-2 text-sm text-ink-soft">{formatCurrency(campaign.selectedBudget)} budget</p>
            <div className="mt-4 inline-flex rounded-full bg-brand-soft px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-brand">
              {statusLabel}
            </div>
          </div>

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
                      <p className="mt-2 text-xs uppercase tracking-[0.14em] text-ink-soft">{proposal.items.length} line item(s)</p>
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
                  Target mix: Radio {targetMix.radio}% | OOH {targetMix.ooh}% | TV {targetMix.tv}% | Digital {targetMix.digital}%
                </p>
                <p className="mt-1 text-sm text-ink-soft">
                  Current draft: Radio {currentRadioShare}% | OOH {currentOohShare}% | TV {currentTvShare}% | Digital {currentDigitalShare}%
                </p>
              </div>

              {Object.entries(displayedGroups).length > 0 ? Object.entries(displayedGroups).map(([channel, items]) => (
                <div key={channel}>
                  <p className="mb-3 text-sm font-semibold text-ink">{titleCase(channel.toLowerCase())}</p>
                  <div className="flex flex-wrap gap-2.5">
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
                  description="Generate the recommendation first, or use the inventory table below to add radio, OOH, or digital lines manually."
                />
              )}
            </div>
          </div>

          <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
            <h2 className="text-xl font-semibold text-ink">Client-facing summary</h2>
            <p className="mt-2 text-sm text-ink-soft">
              This summary includes all selected inventory lines. You can refine the wording before sending to the client.
            </p>
            <textarea
              value={strategySummary}
              onChange={(event) => setStrategySummary(event.target.value)}
              className="input-base mt-4 min-h-[170px]"
              placeholder="This campaign focuses on high-frequency commuter exposure across key Gauteng routes and stations aligned to the target audience."
            />
          </div>

          {isOverBudget ? (
            <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
              <h2 className="text-xl font-semibold text-ink">Validation</h2>
              <div className="mt-4 flex items-start gap-3 rounded-[14px] border border-rose-200 bg-rose-50 px-4 py-3">
                <CircleAlert className="mt-0.5 size-4 text-rose-700" />
                <div>
                  <p className="text-sm font-semibold text-rose-800">Budget exceeded</p>
                  <p className="text-sm text-rose-700">
                    This draft is {formatCurrency(Math.abs(budgetDelta))} over the client&apos;s budget.
                  </p>
                </div>
              </div>
            </div>
          ) : null}

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
                      <option value="ooh">OOH</option>
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
                        <p className="mt-1 text-xs text-ink-soft">{booking.channel.toUpperCase()} | {titleCase(booking.bookingStatus)} | {formatCurrency(booking.committedAmount)}</p>
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
          ) : (
            <div className="panel border-line/80 bg-white px-6 py-6 text-sm text-ink-soft shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
              Execution tools unlock after recommendation approval. For now, focus on recommendation quality and client sign-off.
            </div>
          )}

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
            <button
              type="button"
              disabled={sendMutation.isPending || isOverBudget}
              onClick={() => sendMutation.mutate()}
              className="button-secondary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
            >
              <Send className="size-4" />
              Send to client
            </button>
            <button
              type="button"
              disabled={saveMutation.isPending || !canEditDraftRecommendation || draftApprovalCaptured}
              onClick={() => void handleApproveRecommendation()}
              className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
            >
              <CircleCheckBig className="size-4" />
              {draftApprovalCaptured ? 'Approved' : 'Approve recommendation'}
            </button>
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

          <details id="agent-inventory-table" className="panel overflow-hidden">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-4 px-6 py-5">
              <div>
                <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Matching inventory</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">
                  These rows are matched against this campaign&apos;s budget, channels, and geography so the agent can mix and match with real supplier inventory.
                </p>
              </div>
              <div className="rounded-full bg-brand-soft px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-brand">
                {selectedPlanItems.length} selected
              </div>
            </summary>
            <div className="border-t border-line px-6 py-6">
              <InventoryTable
                items={visibleInventoryItems}
                selectedItemIds={selectedInventoryIds}
                onToggleItem={canModifyPlan ? ((item) => toggleInventoryItem(item as SelectedPlanInventoryItem)) : undefined}
              />
            </div>
          </details>
        </div>
      </div>
    </section>
  );
}

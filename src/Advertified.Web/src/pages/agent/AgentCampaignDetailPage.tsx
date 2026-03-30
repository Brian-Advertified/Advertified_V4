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
import { useRef, useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
import { InventoryTable } from '../../features/agent/components/InventoryTable';
import { formatCurrency, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { CampaignBrief, RecommendationItem, SelectedPlanInventoryItem } from '../../types/domain';

type DisplayPlanItem = SelectedPlanInventoryItem | RecommendationItem;
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? 'http://localhost:5050';

function buildAudienceSummary(brief?: CampaignBrief) {
  if (!brief) return 'Not captured yet';

  const parts: string[] = [];
  if (brief.targetAudienceNotes) parts.push(brief.targetAudienceNotes);
  if (brief.targetInterests?.length) parts.push(brief.targetInterests.join(', '));
  if (brief.targetAgeMin || brief.targetAgeMax) parts.push(`Age ${brief.targetAgeMin ?? '?'}-${brief.targetAgeMax ?? '?'}`);
  return parts[0] ?? 'General audience';
}

function buildGeoSummary(brief?: CampaignBrief) {
  if (!brief) return 'Not captured yet';

  const areas = [...(brief.areas ?? []), ...(brief.cities ?? []), ...(brief.provinces ?? [])];
  if (areas.length > 0) return areas.slice(0, 3).join(', ');
  return titleCase(brief.geographyScope || 'not set');
}

function buildChannelSummary(brief?: CampaignBrief, selectedPlanItems?: SelectedPlanInventoryItem[]) {
  if (brief?.preferredMediaTypes?.length) return brief.preferredMediaTypes.map((x) => x.toUpperCase()).join(' + ');
  const channels = Array.from(new Set((selectedPlanItems ?? []).map((item) => item.type.toUpperCase())));
  return channels.length > 0 ? channels.join(' + ') : 'Not selected yet';
}

function buildToneSummary(brief?: CampaignBrief) {
  const notes = brief?.creativeNotes?.toLowerCase() ?? '';
  if (notes.includes('premium')) return 'Premium';
  if (notes.includes('youth')) return 'Youthful';
  if (notes.includes('bold') || notes.includes('visibility')) return 'High visibility';
  return brief?.creativeNotes ? 'Campaign-led' : 'Balanced';
}

function buildOriginalPrompt(brief?: CampaignBrief) {
  return brief?.specialRequirements
    ?? brief?.creativeNotes
    ?? brief?.targetAudienceNotes
    ?? 'No original prompt has been captured yet.';
}

function inferRegionFromTitle(title: string, brief?: CampaignBrief) {
  const segments = title
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean);

  if (segments.length > 1) {
    return segments[segments.length - 1];
  }

  return buildGeoSummary(brief);
}

function buildGeneratedInventoryFallback(item: RecommendationItem, brief?: CampaignBrief) {
  const channel = normalizeChannelKey(item.channel);
  if (channel === 'RADIO') {
    return {
      region: item.region || buildGeoSummary(brief),
      language: item.language || 'Station language',
      showDaypart: item.showDaypart || 'Station schedule',
      timeBand: item.timeBand || 'To be confirmed',
      slotType: item.slotType || 'Radio spot',
      duration: item.duration || 'Standard spot',
      restrictions: item.restrictions || 'Final station slot to be confirmed',
    };
  }

  if (channel === 'OOH') {
    return {
      region: item.region || inferRegionFromTitle(item.title, brief),
      language: item.language || 'N/A',
      showDaypart: item.showDaypart || 'All day',
      timeBand: item.timeBand || 'Always on',
      slotType: item.slotType || 'Placement',
      duration: item.duration || 'Site-based',
      restrictions: item.restrictions || 'Subject to site availability',
    };
  }

  return {
    region: item.region || buildGeoSummary(brief),
    language: item.language || 'N/A',
    showDaypart: item.showDaypart || 'Flexible',
    timeBand: item.timeBand || 'Flexible',
    slotType: item.slotType || 'Placement',
    duration: item.duration || 'Campaign-based',
    restrictions: item.restrictions || 'Final delivery details pending',
  };
}

function formatConfidenceLabel(confidenceScore?: number) {
  if (confidenceScore === undefined) {
    return null;
  }

  return `${Math.round(confidenceScore * 100)}% confidence`;
}

function calculateConfidence(brief?: CampaignBrief) {
  if (!brief) return 0.42;

  let score = 0.42;
  if (brief.objective) score += 0.1;
  if (brief.geographyScope) score += 0.1;
  if (brief.targetAudienceNotes || brief.targetInterests?.length) score += 0.12;
  if (brief.preferredMediaTypes?.length) score += 0.1;
  if (brief.creativeNotes) score += 0.07;
  if (brief.specialRequirements) score += 0.05;
  return Math.min(0.96, Number(score.toFixed(2)));
}

function getConstraintChecks(brief: CampaignBrief | undefined, selectedPlanItems: SelectedPlanInventoryItem[], isOverBudget: boolean, selectedBudget: number) {
  const geoAligned = selectedPlanItems.length === 0
    || selectedPlanItems.some((item) => {
      const region = item.region.toLowerCase();
      return (
        brief?.areas?.some((area) => region.includes(area.toLowerCase()))
        || brief?.cities?.some((city) => region.includes(city.toLowerCase()))
        || brief?.provinces?.some((province) => region.includes(province.toLowerCase()))
        || region.includes((brief?.geographyScope ?? '').toLowerCase())
      );
    });

  const nationalRadioSelected = selectedPlanItems.some((item) =>
    item.type === 'radio' && /(metro|5fm|ukhozi|radio 2000)/i.test(item.station));

  return [
    {
      label: 'Within budget',
      ok: !isOverBudget,
      detail: !isOverBudget ? `Inside the paid ${formatCurrency(selectedBudget)} package.` : 'This draft is currently over budget.',
    },
    {
      label: 'No national radio',
      ok: !nationalRadioSelected,
      detail: nationalRadioSelected ? 'National-capable radio is included.' : 'No national radio selected yet. That is still acceptable here.',
    },
    {
      label: 'Geo aligned',
      ok: geoAligned,
      detail: geoAligned ? 'The plan matches the campaign geography.' : 'Some lines do not clearly match the campaign geography.',
    },
  ];
}

function groupPlanItems(items: SelectedPlanInventoryItem[]) {
  return items.reduce<Record<string, SelectedPlanInventoryItem[]>>((acc, item) => {
    const key = item.type.toUpperCase();
    acc[key] = [...(acc[key] ?? []), item];
    return acc;
  }, {});
}

function groupGeneratedRecommendationItems(items: RecommendationItem[]) {
  return items.reduce<Record<string, typeof items>>((acc, item) => {
    const key = normalizeChannelKey(item.channel);
    acc[key] = [...(acc[key] ?? []), item];
    return acc;
  }, {});
}

function normalizeChannelKey(channel: string) {
  const normalized = channel.trim().toLowerCase();
  if (normalized.includes('radio')) return 'RADIO';
  if (normalized.includes('ooh') || normalized.includes('out of home') || normalized.includes('billboard')) return 'OOH';
  if (normalized.includes('digital')) return 'DIGITAL';
  if (normalized.includes('tv')) return 'TV';
  return channel.trim().toUpperCase();
}

function isInventoryRelevant(item: SelectedPlanInventoryItem, brief?: CampaignBrief) {
  const preferredMediaTypes = brief?.preferredMediaTypes?.map((value) => value.toLowerCase()) ?? [];
  if (preferredMediaTypes.length > 0 && !preferredMediaTypes.includes(item.type.toLowerCase())) {
    return false;
  }

  if ((brief?.geographyScope ?? '').toLowerCase() === 'national') {
    return true;
  }

  const geoTerms = [
    ...(brief?.areas ?? []),
    ...(brief?.cities ?? []),
    ...(brief?.provinces ?? []),
  ]
    .map((value) => value.toLowerCase())
    .filter(Boolean);

  if (geoTerms.length === 0) {
    return true;
  }

  const haystack = [
    item.station,
    item.region,
    item.restrictions,
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();

  return geoTerms.some((term) => haystack.includes(term));
}

function buildTargetChannelMix(groupedTotals: { channel: string; total: number }[], radioShareTarget: number) {
  const activeNonRadioChannels = groupedTotals
    .map((entry) => normalizeChannelKey(entry.channel))
    .filter((channel) => channel !== 'RADIO');

  const uniqueNonRadioChannels = Array.from(new Set(activeNonRadioChannels));
  const remaining = Math.max(0, 100 - radioShareTarget);

  if (uniqueNonRadioChannels.length === 0) {
    return {
      radio: radioShareTarget,
      ooh: 0,
      digital: 0,
    };
  }

  const baseAllocation = Math.floor(remaining / uniqueNonRadioChannels.length);
  let leftover = remaining - (baseAllocation * uniqueNonRadioChannels.length);

  const allocations = new Map<string, number>();
  for (const channel of uniqueNonRadioChannels) {
    const extra = leftover > 0 ? 1 : 0;
    allocations.set(channel, baseAllocation + extra);
    leftover = Math.max(0, leftover - extra);
  }

  return {
    radio: radioShareTarget,
    ooh: allocations.get('OOH') ?? 0,
    digital: allocations.get('DIGITAL') ?? 0,
  };
}

export function AgentCampaignDetailPage() {
  const { id = '' } = useParams();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const [selectedPlanItems, setSelectedPlanItems] = useState<SelectedPlanInventoryItem[]>([]);
  const [strategySummary, setStrategySummary] = useState('');
  const [mixBalance, setMixBalance] = useState(60);
  const [selectedRecommendationIdState, setSelectedRecommendationIdState] = useState('');
  const mixPanelRef = useRef<HTMLDivElement | null>(null);
  const hydratedRecommendationKeyRef = useRef<string | null>(null);

  const campaignQuery = useQuery({ queryKey: ['agent-campaign', id], queryFn: () => advertifiedApi.getAgentCampaign(id) });
  const inventoryQuery = useQuery({
    queryKey: ['inventory', id],
    queryFn: () => advertifiedApi.getInventory(id),
  });

  const saveMutation = useMutation({
    mutationFn: (notes: string) => advertifiedApi.updateRecommendation(id, activeRecommendation?.id, notes, selectedPlanItems),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
      ]);
      pushToast({
        title: 'Recommendation draft saved.',
        description: 'The latest plan and strategy summary are now part of this campaign draft.',
      });
    },
  });

  const sendMutation = useMutation({
    mutationFn: () => advertifiedApi.sendRecommendationToClient(id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
      ]);
      pushToast({
        title: 'Recommendation sent to client.',
        description: 'The campaign has moved into the client review stage.',
      });
    },
  });

  const markLiveMutation = useMutation({
    mutationFn: () => advertifiedApi.markCampaignLaunched(id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaigns'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
      ]);
      pushToast({
        title: 'Campaign marked live.',
        description: 'Operations activation is now captured separately from client approval.',
      });
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
      targetDigitalShare: targetMix.digital,
    }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
      ]);
      pushToast({
        title: 'Recommendation regenerated.',
        description: `A fresh AI draft was prepared using target mix Radio ${targetMix.radio}% | OOH ${targetMix.ooh}% | Digital ${targetMix.digital}%.`,
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
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
      ]);
      pushToast({
        title: 'Campaign assigned to you.',
        description: 'This campaign is now in your active working queue.',
      });
    },
  });

  const unassignMutation = useMutation({
    mutationFn: () => advertifiedApi.unassignCampaign(id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['agent-campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['agent-inbox'] }),
        queryClient.invalidateQueries({ queryKey: ['agent-campaigns'] }),
      ]);
      pushToast({
        title: 'Campaign unassigned.',
        description: 'This campaign has been returned to the shared queue.',
      }, 'info');
    },
  });

  const campaign = campaignQuery.data;
  const inventoryItems = inventoryQuery.data ?? [];
  const recommendations = campaign?.recommendations.length
    ? campaign.recommendations
    : (campaign?.recommendation ? [campaign.recommendation] : []);
  const selectedRecommendationId = selectedRecommendationIdState || recommendations[0]?.id || '';
  const activeRecommendation = recommendations.find((item) => item.id === selectedRecommendationId) ?? recommendations[0];

  if (campaign && activeRecommendation) {
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

        const normalizedType = item.channel.toLowerCase().includes('radio')
          ? 'radio'
          : item.channel.toLowerCase().includes('ooh')
            ? 'ooh'
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
    if (hydratedRecommendationKeyRef.current !== hydrationKey) {
      hydratedRecommendationKeyRef.current = hydrationKey;
      setStrategySummary(activeRecommendation.summary ?? '');
      setSelectedPlanItems(selectedFromRecommendation);
    }
  }

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
  const radioShare = groupedTotals.reduce((sum, entry) => entry.channel === 'RADIO' ? sum + entry.total : sum, 0);
  const oohShare = groupedTotals.reduce((sum, entry) => entry.channel === 'OOH' ? sum + entry.total : sum, 0);
  const digitalShare = groupedTotals.reduce((sum, entry) => entry.channel === 'DIGITAL' ? sum + entry.total : sum, 0);
  const totalGroupedSpend = groupedTotals.reduce((sum, entry) => sum + entry.total, 0);
  const currentRadioShare = totalGroupedSpend > 0 ? Math.round((radioShare / totalGroupedSpend) * 100) : 0;
  const currentOohShare = totalGroupedSpend > 0 ? Math.round((oohShare / totalGroupedSpend) * 100) : 0;
  const currentDigitalShare = totalGroupedSpend > 0 ? Math.round((digitalShare / totalGroupedSpend) * 100) : 0;
  const targetMix = buildTargetChannelMix(groupedTotals, mixBalance);
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
  const constraints = getConstraintChecks(campaign.brief, selectedPlanItems, isOverBudget, campaign.selectedBudget);
  const recommendationTitle = strategySummary || activeRecommendation?.summary || 'Draft recommendation';
  const canMarkLive = campaign.status === 'creative_approved';

  function toggleInventoryItem(item: SelectedPlanInventoryItem) {
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

    document.getElementById('agent-inventory-table')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    pushToast({
      title: 'Pick a replacement below.',
      description: suggestions.length > 0
        ? `Suggested swaps: ${suggestions.join(', ')}.`
        : 'Browse the inventory library below to replace this line.',
    }, 'info');
  }

  async function handleApproveRecommendation() {
    await saveMutation.mutateAsync(strategySummary);
    pushToast({
      title: 'Recommendation approved.',
      description: 'The draft is saved and ready for the next step.',
    });
  }

  function handleRegenerate() {
    regenerateMutation.mutate();
  }

  function handleAdjustMix() {
    mixPanelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
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
                      {flag.replace(/_/g, ' ')}
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
                          {'station' in item ? (
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
                              AI draft
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
            <h2 className="text-xl font-semibold text-ink">Strategy</h2>
            <textarea
              value={strategySummary}
              onChange={(event) => setStrategySummary(event.target.value)}
              className="input-base mt-4 min-h-[170px]"
              placeholder="This campaign focuses on high-frequency commuter exposure across key Gauteng routes and stations aligned to the target audience."
            />
          </div>

          <div ref={mixPanelRef} className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
            <h2 className="text-xl font-semibold text-ink">Budget split</h2>
            <input
              type="range"
              min={0}
              max={100}
              value={mixBalance}
              onChange={(event) => setMixBalance(Number(event.target.value))}
              className="mt-5 w-full accent-brand"
            />
            <p className="mt-4 text-sm text-ink-soft">
              Target mix: Radio {targetMix.radio}% | OOH {targetMix.ooh}% | Digital {targetMix.digital}%
            </p>
            <p className="mt-2 text-sm text-ink-soft">
              Current draft: Radio {currentRadioShare}% | OOH {currentOohShare}% | Digital {currentDigitalShare}%
            </p>
            <p className="mt-3 text-sm text-ink-soft">
              Regenerate refreshes the draft from the saved campaign inputs. If those inputs did not change, the result may stay very similar.
            </p>
          </div>

          <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
            <h2 className="text-xl font-semibold text-ink">Validation</h2>
            <div className="mt-4 space-y-2.5">
              {constraints.map((constraint) => (
                <div key={constraint.label} className="flex items-start gap-3 rounded-[14px] border border-line bg-slate-50 px-4 py-3">
                  {constraint.ok ? (
                    <CircleCheckBig className={`mt-0.5 size-4 ${constraint.label === 'No national radio' ? 'text-amber-600' : 'text-emerald-600'}`} />
                  ) : (
                    <CircleAlert className="mt-0.5 size-4 text-amber-600" />
                  )}
                  <div>
                    <p className={`text-sm font-medium ${constraint.ok && constraint.label !== 'No national radio' ? 'text-emerald-700' : 'text-amber-700'}`}>
                      {constraint.label === 'No national radio'
                        ? `Optional national radio check`
                        : constraint.label}
                    </p>
                    <p className="text-sm text-ink-soft">{constraint.detail}</p>
                  </div>
                </div>
              ))}
            </div>
            {isOverBudget ? (
              <p className="mt-4 text-sm font-medium text-rose-700">
                This draft is {formatCurrency(Math.abs(budgetDelta))} over the client&apos;s budget.
              </p>
            ) : null}
          </div>

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
              <a
                href={`${API_BASE_URL}${campaign.recommendationPdfUrl}`}
                target="_blank"
                rel="noreferrer"
                className="button-secondary inline-flex items-center gap-2 px-5 py-3"
              >
                <Download className="size-4" />
                Preview client PDF
              </a>
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
              disabled={saveMutation.isPending}
              onClick={() => void handleApproveRecommendation()}
              className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
            >
              <CircleCheckBig className="size-4" />
              Approve recommendation
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
                onToggleItem={(item) => toggleInventoryItem(item as SelectedPlanInventoryItem)}
              />
            </div>
          </details>
        </div>
      </div>
    </section>
  );
}

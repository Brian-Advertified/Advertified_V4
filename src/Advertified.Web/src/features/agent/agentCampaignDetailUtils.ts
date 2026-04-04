import { titleCase } from '../../lib/utils';
import { formatChannelLabel, normalizeChannelKey as sharedNormalizeChannelKey } from '../channels/channelUtils';
import type { CampaignBrief, RecommendationItem, SelectedPlanInventoryItem } from '../../types/domain';

export type BudgetConstraintContext = {
  label: string;
  ok: boolean;
  detail: string;
};

export function buildAudienceSummary(brief?: CampaignBrief) {
  if (!brief) return 'Not captured yet';

  const parts: string[] = [];
  if (brief.targetAudienceNotes) parts.push(brief.targetAudienceNotes);
  if (brief.targetInterests?.length) parts.push(brief.targetInterests.join(', '));
  if (brief.targetAgeMin || brief.targetAgeMax) parts.push(`Age ${brief.targetAgeMin ?? '?'}-${brief.targetAgeMax ?? '?'}`);
  return parts[0] ?? 'General audience';
}

export function buildGeoSummary(brief?: CampaignBrief) {
  if (!brief) return 'Not captured yet';

  const areas = [...(brief.areas ?? []), ...(brief.cities ?? []), ...(brief.provinces ?? [])];
  if (areas.length > 0) return areas.slice(0, 3).join(', ');
  return titleCase(brief.geographyScope || 'not set');
}

export function buildChannelSummary(brief?: CampaignBrief, selectedPlanItems?: SelectedPlanInventoryItem[]) {
  if (brief?.preferredMediaTypes?.length) {
    return brief.preferredMediaTypes
      .map((x) => formatChannelLabel(x))
      .join(' + ');
  }
  const channels = Array.from(new Set((selectedPlanItems ?? []).map((item) => item.type.toUpperCase())));
  return channels.length > 0 ? channels.map((channel) => formatChannelLabel(channel)).join(' + ') : 'Not selected yet';
}

export function buildToneSummary(brief?: CampaignBrief) {
  const notes = brief?.creativeNotes?.toLowerCase() ?? '';
  if (notes.includes('premium')) return 'Premium';
  if (notes.includes('youth')) return 'Youthful';
  if (notes.includes('bold') || notes.includes('visibility')) return 'High visibility';
  return brief?.creativeNotes ? 'Campaign-led' : 'Balanced';
}

export function normalizeChannelKey(channel: string) {
  return sharedNormalizeChannelKey(channel);
}

export function buildOriginalPrompt(brief?: CampaignBrief) {
  return brief?.specialRequirements
    ?? brief?.creativeNotes
    ?? brief?.targetAudienceNotes
    ?? 'No original prompt has been captured yet.';
}

export function inferRegionFromTitle(title: string, brief?: CampaignBrief) {
  const segments = title
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean);

  if (segments.length > 1) {
    return segments[segments.length - 1];
  }

  return buildGeoSummary(brief);
}

export function buildGeneratedInventoryFallback(item: RecommendationItem, brief?: CampaignBrief) {
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

export function formatConfidenceLabel(confidenceScore?: number) {
  if (confidenceScore === undefined) {
    return null;
  }

  return `${Math.round(confidenceScore * 100)}% confidence`;
}

export function calculateConfidence(brief?: CampaignBrief) {
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

export function getConstraintChecks(
  brief: CampaignBrief | undefined,
  selectedPlanItems: SelectedPlanInventoryItem[],
  budgetConstraint: BudgetConstraintContext,
) {
  const hasOoh = selectedPlanItems.some((item) => normalizeChannelKey(item.type) === 'OOH');
  const hasRadio = selectedPlanItems.some((item) => normalizeChannelKey(item.type) === 'RADIO');
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

  const radioRequested = (brief?.preferredMediaTypes ?? []).some((channel) => normalizeChannelKey(channel) === 'RADIO');
  const nationalRadioSelected = selectedPlanItems.some((item) =>
    normalizeChannelKey(item.type) === 'RADIO' && /(metro|5fm|ukhozi|radio 2000)/i.test(item.station));

  return [
    {
      label: 'Billboards and Digital Screens first',
      ok: hasOoh,
      detail: hasOoh
        ? 'Billboards and Digital Screens are included in this recommendation.'
        : 'Add at least one Billboards and Digital Screens line before saving or sending this recommendation.',
    },
    {
      label: budgetConstraint.label,
      ok: budgetConstraint.ok,
      detail: budgetConstraint.detail,
    },
    {
      label: radioRequested ? 'Radio included' : 'Radio coverage',
      ok: radioRequested ? hasRadio : true,
      detail: hasRadio
        ? (nationalRadioSelected ? 'National-capable radio is included.' : 'Radio is included in this recommendation.')
        : (radioRequested ? 'Add at least one radio line to match the requested mix.' : 'Radio is optional for this recommendation.'),
    },
    {
      label: 'Geo aligned',
      ok: geoAligned,
      detail: geoAligned ? 'The plan matches the campaign geography.' : 'Some lines do not clearly match the campaign geography.',
    },
  ];
}

export function groupPlanItems(items: SelectedPlanInventoryItem[]) {
  return items.reduce<Record<string, SelectedPlanInventoryItem[]>>((acc, item) => {
    const key = item.type.toUpperCase();
    acc[key] = [...(acc[key] ?? []), item];
    return acc;
  }, {});
}

export function groupGeneratedRecommendationItems(items: RecommendationItem[]) {
  return items.reduce<Record<string, typeof items>>((acc, item) => {
    const key = normalizeChannelKey(item.channel);
    acc[key] = [...(acc[key] ?? []), item];
    return acc;
  }, {});
}

export function isInventoryRelevant(item: SelectedPlanInventoryItem, brief?: CampaignBrief) {
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

export function buildTargetChannelMix(
  groupedTotals: { channel: string; total: number }[],
  radioShareTarget: number,
  preferredMediaTypes?: string[] | null,
) {
  const preferredChannels = (preferredMediaTypes ?? [])
    .map((channel) => normalizeChannelKey(channel))
    .filter((channel) => channel !== 'RADIO');
  const activeNonRadioChannels = preferredChannels.length > 0
    ? preferredChannels
    : groupedTotals
      .map((entry) => normalizeChannelKey(entry.channel))
      .filter((channel) => channel !== 'RADIO');

  const uniqueNonRadioChannels = Array.from(new Set(activeNonRadioChannels));
  const remaining = Math.max(0, 100 - radioShareTarget);

  if (uniqueNonRadioChannels.length === 0) {
    return {
      radio: radioShareTarget,
      ooh: 0,
      tv: 0,
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
    tv: allocations.get('TV') ?? 0,
    digital: allocations.get('DIGITAL') ?? 0,
  };
}

import type { PackageBand } from '../../types/domain';

type SpendTier = 'entry' | 'mid' | 'premium';

export type SpendPreview = {
  tier: SpendTier;
  tierLabel: string;
  packagePurpose: string;
  quickBenefit: string;
  recommendedSpendLabel: string;
  suitableFor: string[];
  typicalIncludes: string[];
  indicativeMix: string[];
  unlocks: string[];
  upsells: string[];
  finalPlanNote: string;
};

type SpendTierPreview = {
  tierLabel: string;
  typicalIncludes: string[];
  indicativeMix: string[];
  upsells: string[];
};

type PackagePreviewDefinition = {
  packagePurpose: string;
  quickBenefit: string;
  recommendedSpendLabel: string;
  suitableFor: string[];
  unlocks: string[];
  finalPlanNote: string;
  tiers: Record<SpendTier, SpendTierPreview>;
};

const previewDefinitions: Record<string, PackagePreviewDefinition> = {
  launch: {
    packagePurpose: 'Best for first-time advertisers, local campaigns, and single-area visibility.',
    quickBenefit: 'Local visibility for smaller campaign starts.',
    recommendedSpendLabel: 'Most clients in this package spend around R35 000.',
    suitableFor: ['Awareness', 'Promotions', 'Local foot traffic', 'New product launches'],
    unlocks: ['Campaign brief', 'AI-assisted recommendation', 'Agent support'],
    finalPlanNote: 'Final media plan confirmed after purchase, brief submission, timing checks, and live availability.',
    tiers: {
      entry: {
        tierLabel: 'Entry Launch mix',
        typicalIncludes: ['1-2 local media items', 'Focused on one target area', 'Starter outdoor, digital, or radio route'],
        indicativeMix: ['Usually one area', 'Lighter frequency', 'Simple local visibility'],
        upsells: ['Add another area', 'Add a second media channel', 'Add extra frequency'],
      },
      mid: {
        tierLabel: 'Balanced Launch mix',
        typicalIncludes: ['1-2 stronger local placements', 'Starter mixed-media possibility', 'More concentrated visibility in one zone'],
        indicativeMix: ['Better local frequency', 'Stronger promotion support', 'Tighter awareness burst'],
        upsells: ['Add a second channel', 'Extend campaign duration', 'Add more repeat exposure'],
      },
      premium: {
        tierLabel: 'Expanded Launch mix',
        typicalIncludes: ['2 local media items', 'Stronger starter mix', 'Higher visibility within one main area'],
        indicativeMix: ['Higher local presence', 'More sustained exposure', 'Better support for launches and offers'],
        upsells: ['Expand into a second area', 'Add stronger radio support', 'Increase campaign burst length'],
      },
    },
  },
  boost: {
    packagePurpose: 'Best for growing brands needing more reach, repetition, and selected multi-area visibility.',
    quickBenefit: 'More reach and frequency across selected areas.',
    recommendedSpendLabel: 'Most clients in this package spend around R75 000.',
    suitableFor: ['Stronger awareness', 'Regional promotions', 'Repeat exposure', 'Broader audience reach'],
    unlocks: ['Refined media mix', 'Stronger audience targeting', 'Upsell recommendations'],
    finalPlanNote: 'Final media plan confirmed after purchase, brief submission, timing checks, and live availability.',
    tiers: {
      entry: {
        tierLabel: 'Entry Boost mix',
        typicalIncludes: ['2-3 media items', 'Stronger outdoor footprint', 'Potential support in selected markets'],
        indicativeMix: ['Local-to-regional coverage', 'More frequency than Launch', 'Better repeat exposure'],
        upsells: ['Add premium radio dayparts', 'Add another billboard or screen', 'Add a second area'],
      },
      mid: {
        tierLabel: 'Balanced Boost mix',
        typicalIncludes: ['2-4 media items', 'Outdoor plus radio support', 'Coverage across more than one area'],
        indicativeMix: ['Regional promotion support', 'Improved audience reach', 'Stronger repeat presence'],
        upsells: ['Add a second city', 'Increase premium frequency', 'Add a supporting digital layer'],
      },
      premium: {
        tierLabel: 'Expanded Boost mix',
        typicalIncludes: ['3-4 media items', 'Broader outdoor footprint', 'More confident multi-area support'],
        indicativeMix: ['Higher regional visibility', 'Stronger frequency', 'Broader market coverage'],
        upsells: ['More premium placements', 'Increase radio weight', 'Add a new region'],
      },
    },
  },
  scale: {
    packagePurpose: 'Best for established brands running stronger regional campaigns with multi-channel intent.',
    quickBenefit: 'Regional campaign weight with a stronger media mix.',
    recommendedSpendLabel: 'Most clients in this package spend around R250 000.',
    suitableFor: ['Regional reach', 'Sustained presence', 'Campaign bursts', 'Multiple target zones'],
    unlocks: ['Deeper AI planning', 'Broader inventory matching', 'Stronger optimisation options'],
    finalPlanNote: 'Final media plan confirmed after purchase, brief submission, timing checks, and live availability.',
    tiers: {
      entry: {
        tierLabel: 'Entry Scale mix',
        typicalIncludes: ['3-4 media items', 'Multi-channel direction', 'Stronger radio and outdoor balance'],
        indicativeMix: ['Wider regional coverage', 'Higher frequency', 'More refined targeting'],
        upsells: ['Add a premium placement', 'Increase regional frequency', 'Add another channel'],
      },
      mid: {
        tierLabel: 'Balanced Scale mix',
        typicalIncludes: ['4-5 media items', 'More tailored media mix', 'Broader reach across target zones'],
        indicativeMix: ['Stronger sustained presence', 'Improved market coverage', 'Better audience fit'],
        upsells: ['Add more premium placements', 'Extend burst duration', 'Increase channel depth'],
      },
      premium: {
        tierLabel: 'Expanded Scale mix',
        typicalIncludes: ['5-6 media items', 'Stronger multi-channel plan', 'Broader regional support'],
        indicativeMix: ['Wider and more frequent exposure', 'Better premium visibility', 'More tailored optimisation potential'],
        upsells: ['Add more high-value placements', 'Expand into more markets', 'Add extra support channels'],
      },
    },
  },
  dominance: {
    packagePurpose: 'Best for large campaigns needing wide reach, premium visibility, and stronger strategic handling.',
    quickBenefit: 'Premium visibility with broader market coverage.',
    recommendedSpendLabel: 'Most clients in this package spend around R900 000.',
    suitableFor: ['Major brand campaigns', 'Large promotions', 'Market-wide visibility', 'High-impact launches'],
    unlocks: ['Advanced AI and agent planning', 'Premium inventory matching', 'Managed recommendation refinement'],
    finalPlanNote: 'Final media plan confirmed after purchase, brief submission, timing checks, and live availability.',
    tiers: {
      entry: {
        tierLabel: 'Entry Dominance mix',
        typicalIncludes: ['3-4 media items', 'Premium outdoor plus radio direction', 'Strong regional presence'],
        indicativeMix: ['Broader regional coverage', 'Higher-frequency exposure', 'Stronger premium visibility'],
        upsells: ['Add R50 000 for another premium placement', 'Add R75 000 for stronger radio frequency', 'Expand into another region'],
      },
      mid: {
        tierLabel: 'Balanced Dominance mix',
        typicalIncludes: ['4-6 media items', 'Broader premium mix', 'More frequency and stronger multi-region support'],
        indicativeMix: ['Wider regional footprint', 'More premium dayparts and placements', 'Stronger sustained awareness'],
        upsells: ['Add another premium area', 'Increase radio weight', 'Add a broader channel layer'],
      },
      premium: {
        tierLabel: 'National-scale Dominance mix',
        typicalIncludes: ['5-7 media items', 'Premium multi-channel plan', 'Broad market or national-scale options'],
        indicativeMix: ['Higher visibility across more regions', 'More advanced premium mix', 'Stronger executive campaign presence'],
        upsells: ['Add extra regions', 'Add sponsorship-style opportunities', 'Expand the premium channel mix'],
      },
    },
  },
};

export function getSpendPreview(band: PackageBand, selectedSpend: number): SpendPreview {
  const definition = previewDefinitions[band.code.toLowerCase()] ?? previewDefinitions.launch;
  const tier = getSpendTier(band, selectedSpend);
  const tierPreview = definition.tiers[tier];

  return {
    tier,
    tierLabel: tierPreview.tierLabel,
    packagePurpose: definition.packagePurpose,
    quickBenefit: definition.quickBenefit,
    recommendedSpendLabel: definition.recommendedSpendLabel,
    suitableFor: definition.suitableFor,
    typicalIncludes: tierPreview.typicalIncludes,
    indicativeMix: tierPreview.indicativeMix,
    unlocks: definition.unlocks,
    upsells: tierPreview.upsells,
    finalPlanNote: definition.finalPlanNote,
  };
}

function getSpendTier(band: PackageBand, selectedSpend: number): SpendTier {
  const span = band.maxBudget - band.minBudget;
  if (span <= 0) {
    return 'entry';
  }

  const ratio = (selectedSpend - band.minBudget) / span;
  if (ratio < 0.34) {
    return 'entry';
  }

  if (ratio < 0.72) {
    return 'mid';
  }

  return 'premium';
}

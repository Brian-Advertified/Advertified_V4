export function parseDelimitedInput(value: string) {
  return value
    .split(/\r?\n|,/)
    .map((item) => item.trim())
    .filter(Boolean);
}

export function formatChannelLabel(value: string) {
  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}

export function buildDefaultCreativePrompt({
  campaignName,
  businessName,
  packageBandName,
  briefObjective,
  audience,
  creativeNotes,
  channelMood,
}: {
  campaignName: string;
  businessName?: string;
  packageBandName?: string;
  briefObjective?: string;
  audience?: string;
  creativeNotes?: string;
  channelMood: string[];
}) {
  const context = [
    `Build a production-ready campaign system for ${campaignName}.`,
    businessName ? `Brand: ${businessName}.` : undefined,
    packageBandName ? `Package frame: ${packageBandName}.` : undefined,
    briefObjective ? `Objective: ${briefObjective}.` : undefined,
    audience ? `Audience: ${audience}.` : undefined,
    channelMood.length ? `Channels to cover: ${channelMood.join(', ')}.` : undefined,
    creativeNotes ? `Creative notes: ${creativeNotes}.` : undefined,
    'Give me one strong master idea, a clear narrative spine, native channel adaptations, and production-ready outputs.',
  ];

  return context.filter(Boolean).join(' ');
}

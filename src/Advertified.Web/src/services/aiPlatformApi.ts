import { apiRequest } from './apiClient';

type AiPlatformSubmitJobResponse = {
  jobId: string;
  campaignId: string;
  status: string;
  queuedAt: string;
};

type AiPlatformJobStatusResponse = {
  jobId: string;
  campaignId: string;
  status: string;
  error?: string | null;
  updatedAt: string;
};

type AiPlatformRegenerateResponse = {
  jobId: string;
  campaignId: string;
  creativeCount: number;
  assetCount: number;
  completedAt: string;
};

type AiPlatformCampaignCreativeItemResponse = {
  id: string;
  campaignId: string;
  channel: string;
  language: string;
  score?: number | null;
  createdAt: string;
};

type AiPlatformCreativeEngineGenerateResponse = {
  campaignId: string;
  jobId: string;
  completedAt: string;
  creatives: Array<{
    creativeId: string;
    channel: string;
    language: string;
    payloadJson: string;
  }>;
};

type AiPlatformQaResultResponse = {
  creativeId: string;
  campaignId: string;
  channel: string;
  language: string;
  clarity: number;
  attention: number;
  emotionalImpact: number;
  ctaStrength: number;
  brandFit: number;
  channelFit: number;
  finalScore: number;
  status: string;
  riskLevel: string;
  issues: string[];
  suggestions: string[];
  riskFlags: string[];
  improvedPayloadJson?: string | null;
  createdAt: string;
};

type AiPlatformAssetJobResponse = {
  jobId: string;
  campaignId: string;
  creativeId: string;
  assetKind: string;
  status: string;
  assetUrl?: string | null;
  assetType?: string | null;
  error?: string | null;
  updatedAt: string;
  completedAt?: string | null;
  appliedVoicePackId?: string | null;
  appliedLanguage?: string | null;
  upsellRequired?: boolean | null;
  upsellMessage?: string | null;
  voiceQa?: {
    authenticity: number;
    clarity: number;
    conversionPotential: number;
    notes: string[];
    moderationPassed: boolean;
    moderationFlags: string[];
    moderationSuggestions: string[];
  } | null;
  variantJobIds?: string[] | null;
};

type AiPlatformCampaignCostSummaryResponse = {
  campaignId: string;
  campaignBudgetZar: number;
  maxAllowedCostZar: number;
  committedCostZar: number;
  remainingBudgetZar: number;
  utilizationPercent: number;
};

type AiAdVariantResponse = {
  id: string;
  campaignId: string;
  campaignCreativeId?: string | null;
  platform: string;
  channel: string;
  language: string;
  templateId?: number | null;
  voicePackId?: string | null;
  voicePackName?: string | null;
  script: string;
  audioAssetUrl?: string | null;
  platformAdId?: string | null;
  status: string;
  impressions?: number;
  clicks?: number;
  conversions?: number;
  costZar?: number;
  ctr?: number;
  conversionRate?: number;
  cplZar?: number | null;
  roas?: number | null;
  createdAt: string;
  updatedAt: string;
  publishedAt?: string | null;
};

type AiCampaignAdMetricsSummaryResponse = {
  campaignId: string;
  variantCount: number;
  publishedVariantCount: number;
  impressions: number;
  clicks: number;
  conversions: number;
  costZar: number;
  ctr: number;
  conversionRate: number;
  cplZar?: number | null;
  roas?: number | null;
  topVariantId?: string | null;
  topVariantConversionRate?: number | null;
  lastRecordedAt?: string | null;
};

type AiSyncCampaignMetricsResponse = {
  campaignId: string;
  syncedVariantCount: number;
  summary: AiCampaignAdMetricsSummaryResponse;
};

type AiOptimizeCampaignResponse = {
  campaignId: string;
  promotedVariantId?: string | null;
  message: string;
  optimizedAt: string;
};

type AiCampaignAdPlatformConnectionResponse = {
  linkId: string;
  connectionId: string;
  campaignId: string;
  provider: string;
  externalAccountId: string;
  accountName: string;
  externalCampaignId?: string | null;
  isPrimary: boolean;
  status: string;
  updatedAt: string;
};

type AiVoicePackResponse = {
  id: string;
  provider: string;
  name: string;
  accent?: string | null;
  language?: string | null;
  tone?: string | null;
  persona?: string | null;
  useCases: string[];
  sampleAudioUrl?: string | null;
  promptTemplate: string;
  pricingTier: 'standard' | 'premium' | 'exclusive';
  isClientSpecific: boolean;
  isClonedVoice: boolean;
  audienceTags: string[];
  objectiveTags: string[];
  sortOrder: number;
};

type AiVoicePackRecommendationResponse = {
  voicePackId: string;
  reason: string;
  matchScore: number;
};

type AiVoiceTemplateResponse = {
  id: string;
  templateNumber: number;
  category: string;
  name: string;
  promptTemplate: string;
  primaryVoicePackName: string;
  fallbackVoicePackNames: string[];
};

type AiVoiceTemplateSelectionResponse = {
  templateNumber: number;
  templateName: string;
  promptTemplate: string;
  finalPrompt: string;
  primaryVoicePackName: string;
  primaryVoicePackId?: string | null;
  fallbackVoicePackNames: string[];
  fallbackVoicePackIds: string[];
};

type AiPublishAdVariantResponse = {
  variantId: string;
  campaignId: string;
  platform: string;
  platformAdId: string;
  status: string;
  publishedAt: string;
};

export const aiPlatformApi = {
  async submitAiPlatformJob(payload: {
    campaignId: string;
    promptOverride?: string;
  }) {
    return apiRequest<AiPlatformSubmitJobResponse>('/api/v2/ai-platform/jobs', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async getAiPlatformJobStatus(jobId: string) {
    return apiRequest<AiPlatformJobStatusResponse>(`/api/v2/ai-platform/jobs/${encodeURIComponent(jobId)}`);
  },

  async regenerateAiPlatformCreative(payload: {
    creativeId: string;
    campaignId: string;
    feedback: string;
  }) {
    return apiRequest<AiPlatformRegenerateResponse>('/api/v2/ai-platform/regenerate', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async getAiPlatformCampaignCreatives(campaignId: string) {
    return apiRequest<AiPlatformCampaignCreativeItemResponse[]>(
      `/api/v2/ai-platform/campaigns/${encodeURIComponent(campaignId)}/creatives`,
    );
  },

  async generateAiPlatformCreatives(payload: {
    campaignId: string;
    promptOverride?: string;
    persistOutputs?: boolean;
    idempotencyKey?: string;
  }) {
    return apiRequest<AiPlatformCreativeEngineGenerateResponse>('/api/v2/ai-platform/creative-engine/generate', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async generateAiPlatformCreativesFromBrief(payload: {
    brief: {
      campaignId: string;
      brand: string;
      objective?: string;
      tone?: string;
      keyMessage?: string;
      callToAction?: string;
      audienceInsights?: string[];
      languages?: string[];
      channels?: string[];
      promptVersion?: number;
    };
  }) {
    return apiRequest<AiPlatformCreativeEngineGenerateResponse>('/api/v2/ai-platform/creative-engine/generate-from-brief', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async getAiPlatformQaResults(campaignId: string) {
    return apiRequest<AiPlatformQaResultResponse[]>(
      `/api/v2/ai-platform/qa/campaigns/${encodeURIComponent(campaignId)}`,
    );
  },

  async queueAiPlatformVoiceAsset(payload: {
    campaignId: string;
    creativeId: string;
    script: string;
    voiceType?: string;
    voicePackId?: string;
    language?: string;
    audience?: string;
    objective?: string;
    packageBudget?: number;
    campaignTier?: string;
    allowTierUpsell?: boolean;
    generateSaLanguageVariants?: boolean;
    requestedLanguages?: string[];
  }) {
    return apiRequest<AiPlatformAssetJobResponse>('/api/v2/ai-platform/assets/voice', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async getAiPlatformVoicePacks(provider = 'ElevenLabs', options?: {
    campaignId?: string;
    packageBudget?: number;
    campaignTier?: string;
  }) {
    const query = new URLSearchParams();
    query.set('provider', provider);
    if (options?.campaignId) {
      query.set('campaignId', options.campaignId);
    }
    if (typeof options?.packageBudget === 'number') {
      query.set('packageBudget', String(options.packageBudget));
    }
    if (options?.campaignTier) {
      query.set('campaignTier', options.campaignTier);
    }
    return apiRequest<AiVoicePackResponse[]>(`/api/v2/ai-platform/voice-packs?${query.toString()}`);
  },

  async getAiPlatformVoicePackRecommendation(params: {
    campaignId: string;
    provider?: string;
    audience?: string;
    objective?: string;
    packageBudget?: number;
    campaignTier?: string;
  }) {
    const query = new URLSearchParams();
    query.set('campaignId', params.campaignId);
    query.set('provider', params.provider ?? 'ElevenLabs');
    if (params.audience) {
      query.set('audience', params.audience);
    }
    if (params.objective) {
      query.set('objective', params.objective);
    }
    if (typeof params.packageBudget === 'number') {
      query.set('packageBudget', String(params.packageBudget));
    }
    if (params.campaignTier) {
      query.set('campaignTier', params.campaignTier);
    }
    return apiRequest<AiVoicePackRecommendationResponse>(`/api/v2/ai-platform/voice-packs/recommendation?${query.toString()}`);
  },

  async getAiPlatformVoiceTemplates() {
    return apiRequest<AiVoiceTemplateResponse[]>('/api/v2/ai-platform/voice-templates');
  },

  async selectAiPlatformVoiceTemplate(payload: {
    campaignId: string;
    product: string;
    industry?: string;
    audience?: string;
    goal?: string;
    budgetTier?: string;
    language?: string;
    platform?: string;
    objective?: string;
    brand?: string;
    business?: string;
    eventName?: string;
    offer?: string;
  }) {
    return apiRequest<AiVoiceTemplateSelectionResponse>('/api/v2/ai-platform/voice-templates/select', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async queueAiPlatformImageAsset(payload: {
    campaignId: string;
    creativeId: string;
    visualDirection: string;
    style?: string;
    variations?: number;
  }) {
    return apiRequest<AiPlatformAssetJobResponse>('/api/v2/ai-platform/assets/image', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async queueAiPlatformVideoAsset(payload: {
    campaignId: string;
    creativeId: string;
    sceneBreakdownJson: string;
    script: string;
    language?: string;
    aspectRatio?: string;
    durationSeconds?: number;
  }) {
    return apiRequest<AiPlatformAssetJobResponse>('/api/v2/ai-platform/assets/video', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async getAiPlatformAssetJobStatus(jobId: string) {
    return apiRequest<AiPlatformAssetJobResponse>(`/api/v2/ai-platform/assets/jobs/${encodeURIComponent(jobId)}`);
  },

  async getAiPlatformCampaignCostSummary(campaignId: string) {
    return apiRequest<AiPlatformCampaignCostSummaryResponse>(
      `/api/v2/ai-platform/campaigns/${encodeURIComponent(campaignId)}/cost-summary`,
    );
  },

  async createAiAdVariant(payload: {
    campaignId: string;
    campaignCreativeId?: string;
    platform?: string;
    channel?: string;
    language?: string;
    templateId?: number;
    voicePackId?: string;
    voicePackName?: string;
    script: string;
    audioAssetUrl?: string;
  }) {
    return apiRequest<AiAdVariantResponse>('/api/v2/ai-platform/ad-ops/variants', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async getAiAdVariants(campaignId: string) {
    return apiRequest<AiAdVariantResponse[]>(
      `/api/v2/ai-platform/ad-ops/campaigns/${encodeURIComponent(campaignId)}/variants`,
    );
  },

  async publishAiAdVariant(variantId: string) {
    return apiRequest<AiPublishAdVariantResponse>(
      `/api/v2/ai-platform/ad-ops/variants/${encodeURIComponent(variantId)}/publish`,
      {
        method: 'POST',
        body: JSON.stringify({}),
      },
    );
  },

  async trackAiAdConversion(variantId: string, conversions = 1) {
    return apiRequest<{ variantId: string; conversions: number }>(
      `/api/v2/ai-platform/ad-ops/variants/${encodeURIComponent(variantId)}/conversions`,
      {
        method: 'POST',
        body: JSON.stringify({ conversions }),
      },
    );
  },

  async getAiCampaignAdMetricsSummary(campaignId: string) {
    return apiRequest<AiCampaignAdMetricsSummaryResponse>(
      `/api/v2/ai-platform/ad-ops/campaigns/${encodeURIComponent(campaignId)}/metrics/summary`,
    );
  },

  async syncAiCampaignAdMetrics(campaignId: string) {
    return apiRequest<AiSyncCampaignMetricsResponse>(
      `/api/v2/ai-platform/ad-ops/campaigns/${encodeURIComponent(campaignId)}/sync-metrics`,
      {
        method: 'POST',
        body: JSON.stringify({}),
      },
    );
  },

  async optimizeAiCampaign(campaignId: string) {
    return apiRequest<AiOptimizeCampaignResponse>(
      `/api/v2/ai-platform/ad-ops/campaigns/${encodeURIComponent(campaignId)}/optimize`,
      {
        method: 'POST',
        body: JSON.stringify({}),
      },
    );
  },

  async getAiCampaignPlatformConnections(campaignId: string) {
    return apiRequest<AiCampaignAdPlatformConnectionResponse[]>(
      `/api/v2/ai-platform/ad-ops/campaigns/${encodeURIComponent(campaignId)}/platform-connections`,
    );
  },

  async upsertAiCampaignPlatformConnection(campaignId: string, payload: {
    provider: string;
    externalAccountId: string;
    accountName: string;
    externalCampaignId?: string;
    isPrimary?: boolean;
    status?: string;
    accessToken?: string;
    refreshToken?: string;
    tokenExpiresAt?: string;
  }) {
    return apiRequest<AiCampaignAdPlatformConnectionResponse>(
      `/api/v2/ai-platform/ad-ops/campaigns/${encodeURIComponent(campaignId)}/platform-connections`,
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
    );
  },
};

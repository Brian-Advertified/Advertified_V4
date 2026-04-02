import type {
  AgentInbox,
  AgentInboxItem,
  AgentSales,
  AgentSaleItem,
  CampaignAsset,
  Campaign,
  CampaignConversationListItem,
  CampaignConversationThread,
  NotificationSummary,
  CampaignBrief,
  CampaignDeliveryReport,
  CampaignCreativeSystemRecord,
  CampaignRecommendation,
  CampaignSupplierBooking,
  CreativeSystem,
  InventoryRow,
  LoginInput,
  PackageBand,
  PackageAreaOption,
  PackageCheckoutSession,
  AdminUser,
  AdminAuditEntry,
  AdminCampaignOperationsItem,
  AdminPackageOrder,
  AdminIntegrationStatus,
  AdminDashboard,
  AdminCreateOutletInput,
  AdminCreateGeographyInput,
  AdminCreateUserInput,
  AdminGeographyDetail,
  AdminOutletDetail,
  AdminOutletPricing,
  AdminUpdatePricingSettingsInput,
  AdminUpsertPackageSettingInput,
  AdminRateCardUploadInput,
  AdminRateCardUpdateInput,
  AdminUpdateEnginePolicyInput,
  AdminUpdateGeographyInput,
  AdminPreviewRuleUpdateInput,
  AdminUpsertGeographyMappingInput,
  AdminUpsertOutletPricingPackageInput,
  AdminUpsertOutletSlotRateInput,
  AdminUpdateOutletInput,
  AdminUpdateUserInput,
  PackagePreview,
  PackagePricingSummary,
  PackagePreviewMapPoint,
  PackageOrder,
  PaymentProvider,
  PlanningMode,
  ConsentPreference,
  RegistrationInput,
  RegistrationResult,
  SelectedPlanInventoryItem,
  SessionUser,
} from '../types/domain';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? 'http://localhost:5050';
const SESSION_STORAGE_KEY = 'advertified-session-user';

type ApiErrorShape = {
  message?: string;
  Message?: string;
  title?: string;
  errors?: Record<string, string[]>;
};

function humanizeApiMessage(message: string) {
  const normalized = message.trim();

  if (!normalized) {
    return 'Something went wrong. Please try again.';
  }

  if (/selected budget must be between/i.test(normalized)) {
    return normalized.replace('Selected budget must be between', 'Choose an amount between');
  }

  if (/email or password is incorrect/i.test(normalized)) {
    return 'Invalid log in, please check your email and password.';
  }

  if (/activate your account from the email we sent before signing in/i.test(normalized)) {
    return 'Your account has not been activated yet. Please check your email for the activation link.';
  }

  if (/vodapay initiate request failed/i.test(normalized) || /did not return a checkout url/i.test(normalized)) {
    return 'We could not start the VodaPay checkout just now. Please try again in a moment.';
  }

  if (/could not resolve package order id/i.test(normalized)) {
    return 'We could not match this payment update to your order.';
  }

  if (/package band not found/i.test(normalized)) {
    return 'That package is no longer available. Please choose a package again.';
  }

  if (/package order not found/i.test(normalized)) {
    return 'We could not find that order anymore. Please refresh and try again.';
  }

  if (/campaign brief not found/i.test(normalized)) {
    return 'Fill in your campaign brief first, then try again.';
  }

  if (/campaign not found/i.test(normalized)) {
    return 'We could not find that campaign. Please refresh and try again.';
  }

  if (/request failed with status 400/i.test(normalized)) {
    return 'Something in the request needs attention. Please check the form and try again.';
  }

  if (/request failed with status 401|unauthorized/i.test(normalized)) {
    return 'Your session expired due to no activity. Please sign in again.';
  }

  if (/request failed with status 403/i.test(normalized)) {
    return 'You do not have access to do that yet.';
  }

  if (/request failed with status 404/i.test(normalized)) {
    return 'We could not find what you were looking for.';
  }

  if (/request failed with status 5\d\d/i.test(normalized)) {
    return 'Our side had a problem processing that request. Please try again shortly.';
  }

  return normalized;
}

type RegisterResponse = {
  userId: string;
  email: string;
  emailVerificationRequired: boolean;
  accountStatus: string;
};

type LoginResponse = {
  userId: string;
  fullName: string;
  email: string;
  phone: string;
  role: string;
  accountStatus: string;
  emailVerified: boolean;
  identityComplete: boolean;
  sessionToken: string;
};

type SetPasswordResponse = LoginResponse;

type MeResponse = {
  userId: string;
  fullName: string;
  email: string;
  phone: string;
  role: string;
  accountStatus: string;
  emailVerified: boolean;
  identityComplete: boolean;
  phoneVerified: boolean;
  businessName?: string;
  city?: string;
  province?: string;
};

type PackageBandResponse = {
  id: string;
  code: string;
  name: string;
  minBudget: number;
  maxBudget: number;
  sortOrder: number;
  description: string;
  audienceFit: string;
  quickBenefit: string;
  packagePurpose: string;
  includeRadio: 'yes' | 'optional' | 'no';
  includeTv: 'yes' | 'optional' | 'no';
  benefits: string[];
  leadTime: string;
  recommendedSpend?: number;
  isRecommended: boolean;
  maxAdVariants: number;
  allowedAdPlatforms: string[];
  allowAdMetricsSync: boolean;
  allowAdAutoOptimize: boolean;
  allowedVoicePackTiers: string[];
  maxAdRegenerations: number;
};

type PackageAreaOptionResponse = {
  code: string;
  label: string;
  description: string;
};

type ConsentPreferenceResponse = {
  browserId: string;
  necessaryCookies: boolean;
  analyticsCookies: boolean;
  marketingCookies: boolean;
  privacyAccepted: boolean;
  hasSavedPreferences: boolean;
};

type AdminDashboardResponse = AdminDashboard;

type PackageOrderResponse = {
  id: string;
  userId: string;
  packageBandId: string;
  packageBandName: string;
  amount: number;
  currency: string;
  paymentProvider?: string | null;
  paymentStatus: string;
  refundStatus: string;
  refundedAmount: number;
  gatewayFeeRetainedAmount: number;
  refundReason?: string | null;
  refundProcessedAt?: string | null;
  createdAt: string;
  paymentReference?: string | null;
  invoiceId?: string | null;
  invoiceStatus?: string | null;
  invoicePdfUrl?: string | null;
};

type AdminPackageOrderResponse = {
  orderId: string;
  userId: string;
  clientName: string;
  clientEmail: string;
  clientPhone: string;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
  chargedAmount: number;
  currency: string;
  paymentProvider: string;
  paymentStatus: string;
  paymentReference?: string | null;
  createdAt: string;
  purchasedAt?: string | null;
  campaignId?: string | null;
  campaignName?: string | null;
  invoiceId?: string | null;
  invoiceStatus?: string | null;
  invoicePdfUrl?: string | null;
  supportingDocumentPdfUrl?: string | null;
  supportingDocumentFileName?: string | null;
  supportingDocumentUploadedAt?: string | null;
  canUpdateLulaStatus: boolean;
};

type PackagePreviewResponse = {
  budget: number;
  selectedArea: string;
  tierLabel: string;
  packagePurpose: string;
  recommendedSpend?: number;
  reachEstimate: string;
  coverage: string;
  exampleLocations: string[];
  outdoorMapPoints: PackagePreviewMapPoint[];
  radioSupportExamples: string[];
  tvSupportExamples: string[];
  typicalInclusions: string[];
  indicativeMix: string[];
  mediaMix: string[];
  note: string;
};

type PackagePricingSummaryResponse = {
  selectedBudget: number;
  chargedAmount: number;
  aiStudioReserveAmount: number;
};

type CampaignBriefResponse = {
  objective: string;
  startDate?: string;
  endDate?: string;
  durationWeeks?: number;
  geographyScope: string;
  provinces?: string[];
  cities?: string[];
  suburbs?: string[];
  areas?: string[];
  targetAgeMin?: number;
  targetAgeMax?: number;
  targetGender?: string;
  targetLanguages?: string[];
  targetLsmMin?: number;
  targetLsmMax?: number;
  targetInterests?: string[];
  targetAudienceNotes?: string;
  preferredMediaTypes?: string[];
  excludedMediaTypes?: string[];
  mustHaveAreas?: string[];
  excludedAreas?: string[];
  creativeReady?: boolean;
  creativeNotes?: string;
  maxMediaItems?: number;
  openToUpsell: boolean;
  additionalBudget?: number;
  specialRequirements?: string;
  preferredVideoAspectRatio?: string;
  preferredVideoDurationSeconds?: number;
};

type RecommendationItemResponse = {
  id: string;
  sourceInventoryId?: string | null;
  region?: string | null;
  language?: string | null;
  showDaypart?: string | null;
  timeBand?: string | null;
  slotType?: string | null;
  duration?: string | null;
  restrictions?: string | null;
  confidenceScore?: number | null;
  selectionReasons: string[];
  policyFlags: string[];
  quantity: number;
  flighting?: string | null;
  itemNotes?: string | null;
  dimensions?: string | null;
  material?: string | null;
  illuminated?: string | null;
  trafficCount?: string | null;
  siteNumber?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  title: string;
  channel: string;
  rationale: string;
  cost: number;
  type: 'base' | 'upsell';
};

type CampaignRecommendationResponse = {
  id: string;
  campaignId: string;
  proposalLabel?: string;
  proposalStrategy?: string;
  summary: string;
  rationale: string;
  clientFeedbackNotes?: string | null;
  manualReviewRequired: boolean;
  fallbackFlags: string[];
  status: 'draft' | 'sent_to_client' | 'approved';
  totalCost: number;
  items: RecommendationItemResponse[];
};

type CampaignResponse = {
  id: string;
  userId: string;
  clientName?: string | null;
  clientEmail?: string | null;
  businessName?: string | null;
  industry?: string | null;
  packageOrderId: string;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
  status: Campaign['status'];
  planningMode?: PlanningMode;
  aiUnlocked: boolean;
  agentAssistanceRequested: boolean;
  assignedAgentUserId?: string | null;
  assignedAgentName?: string | null;
  assignedAt?: string | null;
  isAssignedToCurrentUser?: boolean;
  isUnassigned?: boolean;
  campaignName?: string | null;
  nextAction: string;
  timeline?: Array<{
    key: string;
    label: string;
    description: string;
    state: 'complete' | 'current' | 'upcoming';
  }>;
  brief?: CampaignBriefResponse | null;
  recommendations?: CampaignRecommendationResponse[] | null;
  recommendation?: CampaignRecommendationResponse | null;
  recommendationPdfUrl?: string | null;
  creativeSystems?: Array<{
    id: string;
    prompt: string;
    iterationLabel?: string | null;
    createdAt: string;
    output: CreativeSystem;
  }> | null;
  latestCreativeSystem?: {
    id: string;
    prompt: string;
    iterationLabel?: string | null;
    createdAt: string;
    output: CreativeSystem;
  } | null;
  assets?: Array<{
    id: string;
    assetType: string;
    displayName: string;
    publicUrl?: string | null;
    contentType?: string | null;
    sizeBytes: number;
    createdAt: string;
  }> | null;
  supplierBookings?: Array<{
    id: string;
    supplierOrStation: string;
    channel: string;
    bookingStatus: string;
    committedAmount: number;
    bookedAt?: string | null;
    liveFrom?: string | null;
    liveTo?: string | null;
    notes?: string | null;
    proofAsset?: {
      id: string;
      assetType: string;
      displayName: string;
      publicUrl?: string | null;
      contentType?: string | null;
      sizeBytes: number;
      createdAt: string;
    } | null;
  }> | null;
  deliveryReports?: Array<{
    id: string;
    supplierBookingId?: string | null;
    reportType: string;
    headline: string;
    summary?: string | null;
    reportedAt?: string | null;
    impressions?: number | null;
    playsOrSpots?: number | null;
    spendDelivered?: number | null;
    evidenceAsset?: {
      id: string;
      assetType: string;
      displayName: string;
      publicUrl?: string | null;
      contentType?: string | null;
      sizeBytes: number;
      createdAt: string;
    } | null;
  }> | null;
  effectiveEndDate?: string | null;
  daysLeft?: number | null;
  createdAt: string;
};

type CampaignConversationListItemResponse = CampaignConversationListItem;

type CampaignConversationThreadResponse = {
  campaignId: string;
  conversationId?: string | null;
  campaignName: string;
  campaignStatus: string;
  clientName: string;
  clientEmail: string;
  packageBandName: string;
  assignedAgentName?: string | null;
  unreadCount: number;
  canSend: boolean;
  messages: Array<{
    id: string;
    senderUserId: string;
    senderRole: 'client' | 'agent';
    senderName: string;
    body: string;
    createdAt: string;
    isRead: boolean;
  }>;
};

type NotificationSummaryResponse = NotificationSummary;

type AgentInboxItemResponse = {
  id: string;
  userId: string;
  campaignName: string;
  clientName: string;
  clientEmail: string;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
  status: string;
  planningMode?: PlanningMode;
  queueStage: AgentInboxItem['queueStage'];
  queueLabel: string;
  assignedAgentUserId?: string | null;
  assignedAgentName?: string | null;
  assignedAt?: string | null;
  isAssignedToCurrentUser: boolean;
  isUnassigned: boolean;
  nextAction: string;
  manualReviewRequired: boolean;
  isOverBudget: boolean;
  isStale: boolean;
  isUrgent: boolean;
  ageInDays: number;
  hasBrief: boolean;
  hasRecommendation: boolean;
  createdAt: string;
  updatedAt: string;
};

type AgentInboxResponse = {
  totalCampaigns: number;
  assignedToMeCount: number;
  unassignedCount: number;
  urgentCount: number;
  manualReviewCount: number;
  overBudgetCount: number;
  staleCount: number;
  newlyPaidCount: number;
  briefWaitingCount: number;
  planningReadyCount: number;
  agentReviewCount: number;
  readyToSendCount: number;
  waitingOnClientCount: number;
  completedCount: number;
  items: AgentInboxItemResponse[];
};

type AgentSaleItemResponse = {
  campaignId: string;
  packageOrderId: string;
  campaignName: string;
  clientName: string;
  clientEmail: string;
  packageBandName: string;
  selectedBudget: number;
  chargedAmount: number;
  paymentProvider: string;
  paymentReference?: string | null;
  convertedFromProspect: boolean;
  purchasedAt: string;
  createdAt: string;
};

type AgentSalesResponse = {
  totalSalesCount: number;
  convertedProspectSalesCount: number;
  totalChargedAmount: number;
  totalSelectedBudget: number;
  items: AgentSaleItemResponse[];
};

type InterpretedCampaignBriefResponse = {
  objective: string;
  audience: string;
  scope: string;
  geography: string;
  tone: string;
  campaignName: string;
  channels: string[];
  summary: string;
};

type CreativeSystemResponse = CreativeSystem;
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
  createdAt: string;
  updatedAt: string;
  publishedAt?: string | null;
};

type AiPublishAdVariantResponse = {
  variantId: string;
  campaignId: string;
  platform: string;
  platformAdId: string;
  status: string;
  publishedAt: string;
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
type AdminAiVoiceProfileResponse = {
  id: string;
  provider: string;
  label: string;
  voiceId: string;
  language?: string | null;
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
};
type AdminUpsertAiVoiceProfileInput = {
  provider?: string;
  label: string;
  voiceId: string;
  language?: string;
  isActive?: boolean;
  sortOrder?: number;
};
type AdminAiVoicePackResponse = {
  id: string;
  provider: string;
  name: string;
  accent?: string | null;
  language?: string | null;
  tone?: string | null;
  persona?: string | null;
  useCases: string[];
  voiceId: string;
  sampleAudioUrl?: string | null;
  promptTemplate: string;
  pricingTier: 'standard' | 'premium' | 'exclusive';
  isClientSpecific: boolean;
  clientUserId?: string | null;
  isClonedVoice: boolean;
  audienceTags: string[];
  objectiveTags: string[];
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
};
type AdminUpsertAiVoicePackInput = {
  provider?: string;
  name: string;
  accent?: string;
  language?: string;
  tone?: string;
  persona?: string;
  useCases?: string[];
  voiceId: string;
  sampleAudioUrl?: string;
  promptTemplate: string;
  pricingTier?: 'standard' | 'premium' | 'exclusive';
  isClientSpecific?: boolean;
  clientUserId?: string;
  isClonedVoice?: boolean;
  audienceTags?: string[];
  objectiveTags?: string[];
  isActive?: boolean;
  sortOrder?: number;
};
type AdminAiVoiceTemplateResponse = {
  id: string;
  templateNumber: number;
  category: string;
  name: string;
  promptTemplate: string;
  primaryVoicePackName: string;
  fallbackVoicePackNames: string[];
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
};
type AdminUpsertAiVoiceTemplateInput = {
  templateNumber: number;
  category: string;
  name: string;
  promptTemplate: string;
  primaryVoicePackName: string;
  fallbackVoicePackNames?: string[];
  isActive?: boolean;
  sortOrder?: number;
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

function getStoredSession(): SessionUser | null {
  const raw = localStorage.getItem(SESSION_STORAGE_KEY);
  return raw ? (JSON.parse(raw) as SessionUser) : null;
}

function emptyToUndefined(value?: string) {
  if (value == null) {
    return undefined;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

function normalizeRole(role: string): SessionUser['role'] {
  if (role === 'agent') {
    return 'agent';
  }

  if (role === 'creative_director') {
    return 'creative_director';
  }

  if (role === 'admin') {
    return 'admin';
  }

  return 'client';
}

function mapSessionUser(response: LoginResponse | MeResponse, sessionToken?: string): SessionUser {
  return {
    id: response.userId,
    fullName: response.fullName,
    email: response.email,
    phone: 'phone' in response ? response.phone ?? undefined : undefined,
    role: normalizeRole(response.role),
    emailVerified: response.emailVerified,
    requiresPasswordSetup: false,
    identityComplete: response.identityComplete,
    sessionToken,
    businessName: 'businessName' in response ? response.businessName ?? undefined : undefined,
    city: 'city' in response ? response.city ?? undefined : undefined,
    province: 'province' in response ? response.province ?? undefined : undefined,
  };
}

function mapPackageBand(response: PackageBandResponse): PackageBand {
  return {
    id: response.id,
    code: response.code,
    name: response.name,
    minBudget: response.minBudget,
    maxBudget: response.maxBudget,
    description: response.description,
    audienceFit: response.audienceFit,
    quickBenefit: response.quickBenefit,
    packagePurpose: response.packagePurpose,
    includeRadio: response.includeRadio,
    includeTv: response.includeTv,
    benefits: response.benefits,
    leadTime: response.leadTime,
    recommendedSpend: response.recommendedSpend,
    isRecommended: response.isRecommended,
    maxAdVariants: response.maxAdVariants,
    allowedAdPlatforms: response.allowedAdPlatforms ?? [],
    allowAdMetricsSync: response.allowAdMetricsSync,
    allowAdAutoOptimize: response.allowAdAutoOptimize,
    allowedVoicePackTiers: response.allowedVoicePackTiers ?? [],
    maxAdRegenerations: response.maxAdRegenerations,
  };
}

function mapPackageAreaOption(response: PackageAreaOptionResponse): PackageAreaOption {
  return {
    code: response.code,
    label: response.label,
    description: response.description,
  };
}

function mapConsentPreference(response: ConsentPreferenceResponse): ConsentPreference {
  return {
    browserId: response.browserId,
    necessaryCookies: response.necessaryCookies,
    analyticsCookies: response.analyticsCookies,
    marketingCookies: response.marketingCookies,
    privacyAccepted: response.privacyAccepted,
    hasSavedPreferences: response.hasSavedPreferences,
  };
}

function mapPackageOrder(response: PackageOrderResponse): PackageOrder {
  return {
    id: response.id,
    userId: response.userId,
    packageBandId: response.packageBandId,
    packageBandName: response.packageBandName,
    amount: response.amount,
    currency: response.currency,
    paymentProvider: response.paymentProvider ?? 'vodapay',
    paymentStatus: response.paymentStatus as PackageOrder['paymentStatus'],
    refundStatus: response.refundStatus as PackageOrder['refundStatus'],
    refundedAmount: response.refundedAmount,
    gatewayFeeRetainedAmount: response.gatewayFeeRetainedAmount,
    refundReason: response.refundReason ?? undefined,
    refundProcessedAt: response.refundProcessedAt ?? undefined,
    createdAt: response.createdAt,
    paymentReference: response.paymentReference ?? undefined,
    invoiceId: response.invoiceId ?? undefined,
    invoiceStatus: response.invoiceStatus ?? undefined,
    invoicePdfUrl: response.invoicePdfUrl ?? undefined,
  };
}

function mapAdminPackageOrder(response: AdminPackageOrderResponse): AdminPackageOrder {
  return {
    orderId: response.orderId,
    userId: response.userId,
    clientName: response.clientName,
    clientEmail: response.clientEmail,
    clientPhone: response.clientPhone,
    packageBandId: response.packageBandId,
    packageBandName: response.packageBandName,
    selectedBudget: response.selectedBudget,
    chargedAmount: response.chargedAmount,
    currency: response.currency,
    paymentProvider: response.paymentProvider,
    paymentStatus: response.paymentStatus,
    paymentReference: response.paymentReference ?? undefined,
    createdAt: response.createdAt,
    purchasedAt: response.purchasedAt ?? undefined,
    campaignId: response.campaignId ?? undefined,
    campaignName: response.campaignName ?? undefined,
    invoiceId: response.invoiceId ?? undefined,
    invoiceStatus: response.invoiceStatus ?? undefined,
    invoicePdfUrl: response.invoicePdfUrl ?? undefined,
    supportingDocumentPdfUrl: response.supportingDocumentPdfUrl ?? undefined,
    supportingDocumentFileName: response.supportingDocumentFileName ?? undefined,
    supportingDocumentUploadedAt: response.supportingDocumentUploadedAt ?? undefined,
    canUpdateLulaStatus: response.canUpdateLulaStatus,
  };
}

function mapPackagePreview(response: PackagePreviewResponse): PackagePreview {
  return {
    budget: response.budget,
    selectedArea: response.selectedArea,
    tierLabel: response.tierLabel,
    packagePurpose: response.packagePurpose,
    recommendedSpend: response.recommendedSpend,
    reachEstimate: response.reachEstimate,
    coverage: response.coverage,
    exampleLocations: response.exampleLocations,
    outdoorMapPoints: (response.outdoorMapPoints ?? []).map((point) => ({
      ...point,
      isInSelectedArea: point.isInSelectedArea ?? false,
    })),
    radioSupportExamples: response.radioSupportExamples,
    tvSupportExamples: response.tvSupportExamples,
    typicalInclusions: response.typicalInclusions,
    indicativeMix: response.indicativeMix,
    mediaMix: response.mediaMix,
    note: response.note,
  };
}

function mapRecommendation(response?: CampaignRecommendationResponse | null): CampaignRecommendation | undefined {
  if (!response) {
    return undefined;
  }

    return {
      id: response.id,
      campaignId: response.campaignId,
      proposalLabel: response.proposalLabel ?? undefined,
      proposalStrategy: response.proposalStrategy ?? undefined,
      summary: response.summary,
      rationale: response.rationale,
      clientFeedbackNotes: response.clientFeedbackNotes ?? undefined,
      manualReviewRequired: response.manualReviewRequired,
      fallbackFlags: response.fallbackFlags ?? [],
      status: response.status,
      totalCost: response.totalCost,
      items: (response.items ?? []).map((item) => ({
        ...item,
        sourceInventoryId: item.sourceInventoryId ?? undefined,
        region: item.region ?? undefined,
        language: item.language ?? undefined,
        showDaypart: item.showDaypart ?? undefined,
        timeBand: item.timeBand ?? undefined,
        slotType: item.slotType ?? undefined,
        duration: item.duration ?? undefined,
        restrictions: item.restrictions ?? undefined,
        confidenceScore: item.confidenceScore ?? undefined,
        selectionReasons: item.selectionReasons ?? [],
        policyFlags: item.policyFlags ?? [],
        quantity: item.quantity,
        flighting: item.flighting ?? undefined,
        itemNotes: item.itemNotes ?? undefined,
        dimensions: item.dimensions ?? undefined,
        material: item.material ?? undefined,
        illuminated: item.illuminated ?? undefined,
        trafficCount: item.trafficCount ?? undefined,
        siteNumber: item.siteNumber ?? undefined,
        startDate: item.startDate ?? undefined,
        endDate: item.endDate ?? undefined,
      })),
  };
}

function mapCampaignAsset(response: {
  id: string;
  assetType: string;
  displayName: string;
  publicUrl?: string | null;
  contentType?: string | null;
  sizeBytes: number;
  createdAt: string;
}): CampaignAsset {
  return {
    id: response.id,
    assetType: response.assetType,
    displayName: response.displayName,
    publicUrl: response.publicUrl ?? undefined,
    contentType: response.contentType ?? undefined,
    sizeBytes: response.sizeBytes,
    createdAt: response.createdAt,
  };
}

function mapSupplierBooking(response: NonNullable<CampaignResponse['supplierBookings']>[number]): CampaignSupplierBooking {
  return {
    id: response.id,
    supplierOrStation: response.supplierOrStation,
    channel: response.channel,
    bookingStatus: response.bookingStatus,
    committedAmount: response.committedAmount,
    bookedAt: response.bookedAt ?? undefined,
    liveFrom: response.liveFrom ?? undefined,
    liveTo: response.liveTo ?? undefined,
    notes: response.notes ?? undefined,
    proofAsset: response.proofAsset ? mapCampaignAsset(response.proofAsset) : undefined,
  };
}

function mapDeliveryReport(response: NonNullable<CampaignResponse['deliveryReports']>[number]): CampaignDeliveryReport {
  return {
    id: response.id,
    supplierBookingId: response.supplierBookingId ?? undefined,
    reportType: response.reportType,
    headline: response.headline,
    summary: response.summary ?? undefined,
    reportedAt: response.reportedAt ?? undefined,
    impressions: response.impressions ?? undefined,
    playsOrSpots: response.playsOrSpots ?? undefined,
    spendDelivered: response.spendDelivered ?? undefined,
    evidenceAsset: response.evidenceAsset ? mapCampaignAsset(response.evidenceAsset) : undefined,
  };
}

function normalizeCreativeSystem(response: CreativeSystem): CreativeSystem {
  return {
    ...response,
    campaignSummary: {
      ...response.campaignSummary,
      channels: response.campaignSummary?.channels ?? [],
      constraints: response.campaignSummary?.constraints ?? [],
      assumptions: response.campaignSummary?.assumptions ?? [],
    },
    storyboard: {
      ...response.storyboard,
      scenes: response.storyboard?.scenes ?? [],
    },
    channelAdaptations: (response.channelAdaptations ?? []).map((adaptation) => ({
      ...adaptation,
      recommendedDirection: adaptation.recommendedDirection ?? '',
      adapterPrompt: adaptation.adapterPrompt ?? '',
      sections: adaptation.sections ?? [],
      versions: adaptation.versions ?? [],
      productionAssets: adaptation.productionAssets ?? [],
    })),
    visualDirection: {
      ...response.visualDirection,
      imageGenerationPrompts: response.visualDirection?.imageGenerationPrompts ?? [],
    },
    campaignLineOptions: response.campaignLineOptions ?? [],
    audioVoiceNotes: response.audioVoiceNotes ?? [],
    productionNotes: response.productionNotes ?? [],
    optionalVariations: response.optionalVariations ?? [],
  };
}

function mapCreativeSystemRecord(response: NonNullable<CampaignResponse['creativeSystems']>[number]): CampaignCreativeSystemRecord {
  return {
    id: response.id,
    prompt: response.prompt,
    iterationLabel: response.iterationLabel ?? undefined,
    createdAt: response.createdAt,
    output: normalizeCreativeSystem(response.output),
  };
}

function getBuildSourceLabel(planningMode?: PlanningMode): string {
  switch (planningMode) {
    case 'ai_assisted':
      return 'AI draft';
    case 'agent_assisted':
      return 'Agent-built';
    case 'hybrid':
      return 'Hybrid';
    default:
      return 'Not selected';
  }
}

function mapCampaign(response: CampaignResponse): Campaign {
  const recommendations = (response.recommendations ?? [])
    .map((recommendation) => ({
      ...mapRecommendation(recommendation)!,
      buildSourceLabel: getBuildSourceLabel(response.planningMode),
    }));
  const primaryRecommendation = recommendations[0]
    ?? (response.recommendation
      ? {
          ...mapRecommendation(response.recommendation)!,
          buildSourceLabel: getBuildSourceLabel(response.planningMode),
        }
      : undefined);

  return {
    id: response.id,
    userId: response.userId,
    clientName: response.clientName ?? undefined,
    clientEmail: response.clientEmail ?? undefined,
    businessName: response.businessName ?? undefined,
    industry: response.industry ?? undefined,
    packageOrderId: response.packageOrderId,
    packageBandId: response.packageBandId,
    packageBandName: response.packageBandName,
    selectedBudget: response.selectedBudget,
    status: response.status,
    planningMode: response.planningMode,
    aiUnlocked: response.aiUnlocked,
    agentAssistanceRequested: response.agentAssistanceRequested,
    assignedAgentUserId: response.assignedAgentUserId ?? undefined,
    assignedAgentName: response.assignedAgentName ?? undefined,
    assignedAt: response.assignedAt ?? undefined,
    isAssignedToCurrentUser: response.isAssignedToCurrentUser,
    isUnassigned: response.isUnassigned,
    campaignName: response.campaignName?.trim() || `${response.packageBandName} campaign`,
    nextAction: response.nextAction,
    timeline: response.timeline ?? [],
    brief: response.brief ?? undefined,
    recommendations,
    recommendation: primaryRecommendation,
    recommendationPdfUrl: response.recommendationPdfUrl ?? undefined,
    creativeSystems: (response.creativeSystems ?? []).map(mapCreativeSystemRecord),
    latestCreativeSystem: response.latestCreativeSystem ? mapCreativeSystemRecord(response.latestCreativeSystem) : undefined,
    assets: (response.assets ?? []).map(mapCampaignAsset),
    supplierBookings: (response.supplierBookings ?? []).map(mapSupplierBooking),
    deliveryReports: (response.deliveryReports ?? []).map(mapDeliveryReport),
    effectiveEndDate: response.effectiveEndDate ?? undefined,
    daysLeft: response.daysLeft ?? undefined,
    createdAt: response.createdAt,
  };
}

function mapConversationListItem(response: CampaignConversationListItemResponse): CampaignConversationListItem {
  return {
    campaignId: response.campaignId,
    conversationId: response.conversationId ?? undefined,
    campaignName: response.campaignName,
    campaignStatus: response.campaignStatus,
    clientName: response.clientName,
    clientEmail: response.clientEmail,
    packageBandName: response.packageBandName,
    assignedAgentName: response.assignedAgentName ?? undefined,
    lastMessagePreview: response.lastMessagePreview ?? undefined,
    lastMessageSenderRole: response.lastMessageSenderRole ?? undefined,
    lastMessageAt: response.lastMessageAt ?? undefined,
    unreadCount: response.unreadCount,
    hasMessages: response.hasMessages,
  };
}

function mapConversationThread(response: CampaignConversationThreadResponse): CampaignConversationThread {
  return {
    campaignId: response.campaignId,
    conversationId: response.conversationId ?? undefined,
    campaignName: response.campaignName,
    campaignStatus: response.campaignStatus,
    clientName: response.clientName,
    clientEmail: response.clientEmail,
    packageBandName: response.packageBandName,
    assignedAgentName: response.assignedAgentName ?? undefined,
    unreadCount: response.unreadCount,
    canSend: response.canSend,
    messages: response.messages.map((message) => ({
      id: message.id,
      senderUserId: message.senderUserId,
      senderRole: message.senderRole,
      senderName: message.senderName,
      body: message.body,
      createdAt: message.createdAt,
      isRead: message.isRead,
    })),
  };
}

function mapAgentInboxItem(response: AgentInboxItemResponse): AgentInboxItem {
  return {
    id: response.id,
    userId: response.userId,
    campaignName: response.campaignName,
    clientName: response.clientName,
    clientEmail: response.clientEmail,
    packageBandId: response.packageBandId,
    packageBandName: response.packageBandName,
    selectedBudget: response.selectedBudget,
    status: response.status,
    planningMode: response.planningMode,
    queueStage: response.queueStage,
    queueLabel: response.queueLabel,
    assignedAgentUserId: response.assignedAgentUserId ?? undefined,
    assignedAgentName: response.assignedAgentName ?? undefined,
    assignedAt: response.assignedAt ?? undefined,
    isAssignedToCurrentUser: response.isAssignedToCurrentUser,
    isUnassigned: response.isUnassigned,
    nextAction: response.nextAction,
    manualReviewRequired: response.manualReviewRequired,
    isOverBudget: response.isOverBudget,
    isStale: response.isStale,
    isUrgent: response.isUrgent,
    ageInDays: response.ageInDays,
    hasBrief: response.hasBrief,
    hasRecommendation: response.hasRecommendation,
    createdAt: response.createdAt,
    updatedAt: response.updatedAt,
  };
}

function mapAgentInbox(response: AgentInboxResponse): AgentInbox {
  return {
    totalCampaigns: response.totalCampaigns,
    assignedToMeCount: response.assignedToMeCount,
    unassignedCount: response.unassignedCount,
    urgentCount: response.urgentCount,
    manualReviewCount: response.manualReviewCount,
    overBudgetCount: response.overBudgetCount,
    staleCount: response.staleCount,
    newlyPaidCount: response.newlyPaidCount,
    briefWaitingCount: response.briefWaitingCount,
    planningReadyCount: response.planningReadyCount,
    agentReviewCount: response.agentReviewCount,
    readyToSendCount: response.readyToSendCount,
    waitingOnClientCount: response.waitingOnClientCount,
    completedCount: response.completedCount,
    items: response.items.map(mapAgentInboxItem),
  };
}

function mapAgentSaleItem(response: AgentSaleItemResponse): AgentSaleItem {
  return {
    campaignId: response.campaignId,
    packageOrderId: response.packageOrderId,
    campaignName: response.campaignName,
    clientName: response.clientName,
    clientEmail: response.clientEmail,
    packageBandName: response.packageBandName,
    selectedBudget: response.selectedBudget,
    chargedAmount: response.chargedAmount,
    paymentProvider: response.paymentProvider,
    paymentReference: response.paymentReference ?? undefined,
    convertedFromProspect: response.convertedFromProspect,
    purchasedAt: response.purchasedAt,
    createdAt: response.createdAt,
  };
}

function mapAgentSales(response: AgentSalesResponse): AgentSales {
  return {
    totalSalesCount: response.totalSalesCount,
    convertedProspectSalesCount: response.convertedProspectSalesCount,
    totalChargedAmount: response.totalChargedAmount,
    totalSelectedBudget: response.totalSelectedBudget,
    items: response.items.map(mapAgentSaleItem),
  };
}

async function parseApiError(response: Response) {
  let payload: ApiErrorShape | null = null;

  try {
    payload = (await response.json()) as ApiErrorShape;
  } catch {
    payload = null;
  }

  const validationErrors = payload?.errors
    ? Object.values(payload.errors)
        .flat()
        .filter(Boolean)
    : [];
  const message =
    validationErrors[0] ??
    payload?.message ??
    payload?.Message ??
    payload?.title ??
    `Request failed with status ${response.status}.`;

  throw new Error(humanizeApiMessage(message));
}

function toAbsoluteApiUrl(path?: string | null) {
  if (!path) {
    return undefined;
  }

  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  return `${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`;
}

function getAuthHeaders() {
  const headers = new Headers();
  const sessionToken = getStoredSession()?.sessionToken;
  if (sessionToken) {
    headers.set('Authorization', `Bearer ${sessionToken}`);
  }

  return headers;
}

function resolveDownloadFileName(response: Response, fallbackFileName?: string) {
  const contentDisposition = response.headers.get('content-disposition');
  const match = contentDisposition?.match(/filename\*?=(?:UTF-8''|")?([^\";]+)/i);
  const decoded = match?.[1] ? decodeURIComponent(match[1].replace(/\"/g, '')) : null;
  return decoded ?? fallbackFileName ?? 'document.pdf';
}

async function apiRequest<T>(path: string, options: RequestInit = {}, _userId?: string): Promise<T> {
  const headers = new Headers(options.headers);

  if (!headers.has('Content-Type') && options.body) {
    headers.set('Content-Type', 'application/json');
  }

  for (const [key, value] of getAuthHeaders().entries()) {
    if (!headers.has(key)) {
      headers.set(key, value);
    }
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    await parseApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const advertifiedApi = {
  toAbsoluteApiUrl,

  async downloadProtectedFile(path: string, fallbackFileName?: string) {
    const response = await fetch(toAbsoluteApiUrl(path) ?? path, {
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      await parseApiError(response);
    }

    const blob = await response.blob();
    const objectUrl = window.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = resolveDownloadFileName(response, fallbackFileName);
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    window.URL.revokeObjectURL(objectUrl);
  },

  async getConsentPreferences(browserId: string) {
    const response = await apiRequest<ConsentPreferenceResponse>(`/consent/preferences?browserId=${encodeURIComponent(browserId)}`);
    return mapConsentPreference(response);
  },

  async saveConsentPreferences(input: {
    browserId: string;
    analyticsCookies: boolean;
    marketingCookies: boolean;
    privacyAccepted: boolean;
  }) {
    const response = await apiRequest<ConsentPreferenceResponse>('/consent/preferences', {
      method: 'PUT',
      body: JSON.stringify({
        browserId: input.browserId,
        necessaryCookies: true,
        analyticsCookies: input.analyticsCookies,
        marketingCookies: input.marketingCookies,
        privacyAccepted: input.privacyAccepted,
      }),
    });
    return mapConsentPreference(response);
  },

  async register(input: RegistrationInput) {
    const response = await apiRequest<RegisterResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify({
        fullName: input.fullName,
        email: input.email,
        phone: input.phone,
        isSouthAfricanCitizen: input.isSouthAfricanCitizen,
        password: input.password,
        confirmPassword: input.confirmPassword,
        businessName: input.businessName,
        businessType: input.businessType,
        registrationNumber: input.registrationNumber,
        vatNumber: emptyToUndefined(input.vatNumber),
        industry: input.industry,
        annualRevenueBand: input.annualRevenueBand,
        tradingAsName: emptyToUndefined(input.tradingAsName),
        streetAddress: input.streetAddress,
        city: input.city,
        province: input.province,
        saIdNumber: emptyToUndefined(input.saIdNumber),
        passportNumber: emptyToUndefined(input.passportNumber),
        passportCountryIso2: emptyToUndefined(input.passportCountryIso2),
        passportIssueDate: emptyToUndefined(input.passportIssueDate),
        passportValidUntil: emptyToUndefined(input.passportValidUntil),
      }),
    });

    return {
      userId: response.userId,
      email: response.email,
      emailVerificationRequired: response.emailVerificationRequired,
      accountStatus: response.accountStatus,
    } satisfies RegistrationResult;
  },

  async login(input: LoginInput) {
    const response = await apiRequest<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(input),
    });

    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(mapSessionUser(response, response.sessionToken)));
    return this.getMe(response.userId)
      .then((user) => ({ ...user, sessionToken: response.sessionToken }))
      .catch(() => mapSessionUser(response, response.sessionToken));
  },

  async verifyEmail(token: string) {
    const response = await apiRequest<LoginResponse>('/auth/verify-email', {
      method: 'POST',
      body: JSON.stringify({ token }),
    });

    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(mapSessionUser(response, response.sessionToken)));
    return this.getMe(response.userId)
      .then((user) => ({
        ...user,
        sessionToken: response.sessionToken,
        // Registered users already have a password; invite/prospect users still need setup.
        requiresPasswordSetup: !user.identityComplete,
      }))
      .catch(() => {
        const fallbackUser = mapSessionUser(response, response.sessionToken);
        return {
          ...fallbackUser,
          requiresPasswordSetup: !fallbackUser.identityComplete,
        };
      });
  },

  async setPassword(input: { password: string; confirmPassword: string }) {
    const response = await apiRequest<SetPasswordResponse>('/auth/set-password', {
      method: 'POST',
      body: JSON.stringify(input),
    });

    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(mapSessionUser(response, response.sessionToken)));
    return this.getMe(response.userId)
      .then((user) => ({ ...user, sessionToken: response.sessionToken, requiresPasswordSetup: false }))
      .catch(() => ({ ...mapSessionUser(response, response.sessionToken), requiresPasswordSetup: false }));
  },

  async getMe(userId?: string) {
    const response = await apiRequest<MeResponse>('/me', {}, userId);
    return mapSessionUser(response);
  },

  async resendVerification(email?: string) {
    const session = getStoredSession();
    return apiRequest<{ message: string; email: string }>('/auth/resend-verification', {
      method: 'POST',
      body: JSON.stringify({ email: email ?? session?.email ?? '' }),
    });
  },

  async getPackages() {
    const response = await apiRequest<PackageBandResponse[]>('/packages');
    return response.map(mapPackageBand);
  },

  async getPackageAreas() {
    const response = await apiRequest<PackageAreaOptionResponse[]>('/packages/areas');
    return response.map(mapPackageAreaOption);
  },

  async getAdminUsers() {
    return apiRequest<AdminUser[]>('/admin/users');
  },

  async createAdminUser(input: AdminCreateUserInput) {
    return apiRequest<AdminUser>('/admin/users', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async updateAdminUser(id: string, input: AdminUpdateUserInput) {
    return apiRequest<AdminUser>(`/admin/users/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminUser(id: string) {
    return apiRequest(`/admin/users/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
  },

  async getAdminGeography(code: string) {
    return apiRequest<AdminGeographyDetail>(`/admin/geography/${encodeURIComponent(code)}`);
  },

  async createAdminGeography(input: AdminCreateGeographyInput) {
    return apiRequest<AdminGeographyDetail>('/admin/geography', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async updateAdminGeography(code: string, input: AdminUpdateGeographyInput) {
    return apiRequest<AdminGeographyDetail>(`/admin/geography/${encodeURIComponent(code)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminGeography(code: string) {
    return apiRequest(`/admin/geography/${encodeURIComponent(code)}`, {
      method: 'DELETE',
    });
  },

  async createAdminGeographyMapping(code: string, input: AdminUpsertGeographyMappingInput) {
    return apiRequest(`/admin/geography/${encodeURIComponent(code)}/mappings`, {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async updateAdminGeographyMapping(code: string, mappingId: string, input: AdminUpsertGeographyMappingInput) {
    return apiRequest(`/admin/geography/${encodeURIComponent(code)}/mappings/${encodeURIComponent(mappingId)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminGeographyMapping(code: string, mappingId: string) {
    return apiRequest(`/admin/geography/${encodeURIComponent(code)}/mappings/${encodeURIComponent(mappingId)}`, {
      method: 'DELETE',
    });
  },

  async getAdminDashboard() {
    return apiRequest<AdminDashboardResponse>('/admin/dashboard');
  },

  async updateAdminPricingSettings(input: AdminUpdatePricingSettingsInput) {
    return apiRequest('/admin/pricing-settings', {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async createAdminOutlet(input: AdminCreateOutletInput) {
    return apiRequest('/admin/outlets', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async getAdminOutlet(code: string) {
    return apiRequest<AdminOutletDetail>(`/admin/outlets/${encodeURIComponent(code)}`);
  },

  async updateAdminOutlet(existingCode: string, input: AdminUpdateOutletInput) {
    return apiRequest(`/admin/outlets/${encodeURIComponent(existingCode)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminOutlet(code: string) {
    return apiRequest(`/admin/outlets/${encodeURIComponent(code)}`, {
      method: 'DELETE',
    });
  },

  async getAdminOutletPricing(code: string) {
    return apiRequest<AdminOutletPricing>(`/admin/outlets/${encodeURIComponent(code)}/pricing`);
  },

  async createAdminOutletPricingPackage(code: string, input: AdminUpsertOutletPricingPackageInput) {
    return apiRequest(`/admin/outlets/${encodeURIComponent(code)}/pricing/packages`, {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async updateAdminOutletPricingPackage(code: string, packageId: string, input: AdminUpsertOutletPricingPackageInput) {
    return apiRequest(`/admin/outlets/${encodeURIComponent(code)}/pricing/packages/${encodeURIComponent(packageId)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminOutletPricingPackage(code: string, packageId: string) {
    return apiRequest(`/admin/outlets/${encodeURIComponent(code)}/pricing/packages/${encodeURIComponent(packageId)}`, {
      method: 'DELETE',
    });
  },

  async createAdminOutletSlotRate(code: string, input: AdminUpsertOutletSlotRateInput) {
    return apiRequest(`/admin/outlets/${encodeURIComponent(code)}/pricing/slot-rates`, {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async updateAdminOutletSlotRate(code: string, slotRateId: string, input: AdminUpsertOutletSlotRateInput) {
    return apiRequest(`/admin/outlets/${encodeURIComponent(code)}/pricing/slot-rates/${encodeURIComponent(slotRateId)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminOutletSlotRate(code: string, slotRateId: string) {
    return apiRequest(`/admin/outlets/${encodeURIComponent(code)}/pricing/slot-rates/${encodeURIComponent(slotRateId)}`, {
      method: 'DELETE',
    });
  },

  async uploadAdminRateCard(input: AdminRateCardUploadInput) {
    const formData = new FormData();
    formData.append('channel', input.channel);
    if (input.supplierOrStation) formData.append('supplierOrStation', input.supplierOrStation);
    if (input.documentTitle) formData.append('documentTitle', input.documentTitle);
    if (input.notes) formData.append('notes', input.notes);
    formData.append('file', input.file);

    const headers = new Headers();
    const sessionToken = getStoredSession()?.sessionToken;
    if (sessionToken) {
      headers.set('Authorization', `Bearer ${sessionToken}`);
    }

    const response = await fetch(`${API_BASE_URL}/admin/imports/rate-card`, {
      method: 'POST',
      body: formData,
      headers,
    });

    if (!response.ok) {
      await parseApiError(response);
    }

    return response.json();
  },

  async updateAdminRateCard(sourceFile: string, input: AdminRateCardUpdateInput) {
    return apiRequest(`/admin/imports/rate-card/${encodeURIComponent(sourceFile)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminRateCard(sourceFile: string) {
    return apiRequest(`/admin/imports/rate-card/${encodeURIComponent(sourceFile)}`, {
      method: 'DELETE',
    });
  },

  async createAdminPackageSetting(input: AdminUpsertPackageSettingInput) {
    return apiRequest('/admin/package-settings', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async updateAdminPackageSetting(id: string, input: AdminUpsertPackageSettingInput) {
    return apiRequest(`/admin/package-settings/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminPackageSetting(id: string) {
    return apiRequest(`/admin/package-settings/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
  },

  async updateAdminEnginePolicy(packageCode: string, input: AdminUpdateEnginePolicyInput) {
    return apiRequest(`/admin/engine-settings/${encodeURIComponent(packageCode)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async updateAdminPreviewRule(packageCode: string, tierCode: string, input: AdminPreviewRuleUpdateInput) {
    return apiRequest(`/admin/preview-rules/${encodeURIComponent(packageCode)}/${encodeURIComponent(tierCode)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async getAdminAuditEntries() {
    return apiRequest<AdminAuditEntry[]>('/admin/audit');
  },

  async getAdminIntegrationStatus() {
    return apiRequest<AdminIntegrationStatus>('/admin/integrations');
  },

  async getAdminAiVoiceProfiles(provider = 'ElevenLabs') {
    return apiRequest<AdminAiVoiceProfileResponse[]>(
      `/admin/ai/voices?provider=${encodeURIComponent(provider)}`,
    );
  },

  async createAdminAiVoiceProfile(input: AdminUpsertAiVoiceProfileInput) {
    return apiRequest<AdminAiVoiceProfileResponse>('/admin/ai/voices', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async updateAdminAiVoiceProfile(id: string, input: AdminUpsertAiVoiceProfileInput) {
    return apiRequest<AdminAiVoiceProfileResponse>(`/admin/ai/voices/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminAiVoiceProfile(id: string) {
    return apiRequest(`/admin/ai/voices/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
  },

  async getAdminAiVoicePacks(provider = 'ElevenLabs') {
    return apiRequest<AdminAiVoicePackResponse[]>(
      `/admin/ai/voice-packs?provider=${encodeURIComponent(provider)}`,
    );
  },

  async createAdminAiVoicePack(input: AdminUpsertAiVoicePackInput) {
    return apiRequest<AdminAiVoicePackResponse>('/admin/ai/voice-packs', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async updateAdminAiVoicePack(id: string, input: AdminUpsertAiVoicePackInput) {
    return apiRequest<AdminAiVoicePackResponse>(`/admin/ai/voice-packs/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminAiVoicePack(id: string) {
    return apiRequest(`/admin/ai/voice-packs/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
  },

  async getAdminAiVoiceTemplates() {
    return apiRequest<AdminAiVoiceTemplateResponse[]>('/admin/ai/voice-templates');
  },

  async createAdminAiVoiceTemplate(input: AdminUpsertAiVoiceTemplateInput) {
    return apiRequest<AdminAiVoiceTemplateResponse>('/admin/ai/voice-templates', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },

  async updateAdminAiVoiceTemplate(id: string, input: AdminUpsertAiVoiceTemplateInput) {
    return apiRequest<AdminAiVoiceTemplateResponse>(`/admin/ai/voice-templates/${encodeURIComponent(id)}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    });
  },

  async deleteAdminAiVoiceTemplate(id: string) {
    return apiRequest(`/admin/ai/voice-templates/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
  },

  async getAdminCampaignOperations() {
    const response = await apiRequest<{ items: AdminCampaignOperationsItem[] }>('/admin/campaign-operations');
    return response.items;
  },

  async getAdminPackageOrders() {
    const response = await apiRequest<{ items: AdminPackageOrderResponse[] }>('/admin/package-orders');
    return response.items.map(mapAdminPackageOrder);
  },

  async updateAdminPackageOrderPaymentStatus(input: {
    orderId: string;
    paymentStatus: 'paid' | 'failed';
    paymentReference?: string;
    notes?: string;
    file?: File;
  }) {
    const formData = new FormData();
    formData.append('paymentStatus', input.paymentStatus);
    if (input.paymentReference) formData.append('paymentReference', input.paymentReference);
    if (input.notes) formData.append('notes', input.notes);
    if (input.file) formData.append('file', input.file);

    const response = await fetch(`${API_BASE_URL}/admin/package-orders/${encodeURIComponent(input.orderId)}/payment-status`, {
      method: 'POST',
      body: formData,
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      await parseApiError(response);
    }

    const payload = (await response.json()) as AdminPackageOrderResponse;
    return mapAdminPackageOrder(payload);
  },

  async pauseAdminCampaign(campaignId: string, reason?: string) {
    return apiRequest<AdminCampaignOperationsItem>(`/admin/campaign-operations/${encodeURIComponent(campaignId)}/pause`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  },

  async unpauseAdminCampaign(campaignId: string, reason?: string) {
    return apiRequest<AdminCampaignOperationsItem>(`/admin/campaign-operations/${encodeURIComponent(campaignId)}/unpause`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  },

  async refundAdminCampaign(campaignId: string, payload: { amount?: number; gatewayFeeRetainedAmount?: number; reason?: string }) {
    return apiRequest<AdminCampaignOperationsItem>(`/admin/campaign-operations/${encodeURIComponent(campaignId)}/refund`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async getPackagePreview(packageBandId: string, budget: number, selectedArea: string) {
    const response = await apiRequest<PackagePreviewResponse>(
      `/packages/preview?packageBandId=${encodeURIComponent(packageBandId)}&budget=${encodeURIComponent(String(budget))}&selectedArea=${encodeURIComponent(selectedArea)}`,
    );

    return mapPackagePreview(response);
  },

  async getPackagePricingSummary(selectedBudget: number) {
    return apiRequest<PackagePricingSummaryResponse>(
      `/packages/pricing-summary?selectedBudget=${encodeURIComponent(String(selectedBudget))}`,
    ) as Promise<PackagePricingSummary>;
  },

  async createOrder(userId: string, packageBandId: string, amount: number, paymentProvider: PaymentProvider) {
    const response = await apiRequest<{
      packageOrderId: string;
      packageBandId: string;
      paymentStatus: string;
      amount: number;
      currency: string;
      paymentProvider?: string;
      checkoutUrl?: string | null;
      checkoutSessionId?: string | null;
      invoiceId?: string | null;
      invoiceStatus?: string | null;
      invoicePdfUrl?: string | null;
    }>(
      '/package-orders',
      {
        method: 'POST',
        body: JSON.stringify({
          packageBandId,
          amount,
          currency: 'ZAR',
          paymentProvider,
        }),
      },
      userId,
    );

    const packages = await this.getPackages();
    const selectedBand = packages.find((item) => item.id === response.packageBandId);

    return {
      order: {
        id: response.packageOrderId,
        userId,
        packageBandId: response.packageBandId,
        packageBandName: selectedBand?.name ?? 'Package',
        amount: response.amount,
        currency: response.currency,
        paymentProvider: response.paymentProvider ?? paymentProvider,
        paymentStatus: response.paymentStatus as PackageOrder['paymentStatus'],
        refundStatus: 'none',
        refundedAmount: 0,
        gatewayFeeRetainedAmount: 0,
        createdAt: new Date().toISOString(),
        refundReason: undefined,
        refundProcessedAt: undefined,
        invoiceId: response.invoiceId ?? undefined,
        invoiceStatus: response.invoiceStatus ?? undefined,
        invoicePdfUrl: response.invoicePdfUrl ?? undefined,
      },
      checkoutUrl: response.checkoutUrl ?? undefined,
      checkoutSessionId: response.checkoutSessionId ?? undefined,
      invoiceId: response.invoiceId ?? undefined,
      invoiceStatus: response.invoiceStatus ?? undefined,
      invoicePdfUrl: response.invoicePdfUrl ?? undefined,
    } satisfies PackageCheckoutSession;
  },

  async initiateOrderCheckout(userId: string, orderId: string, paymentProvider: PaymentProvider) {
    const response = await apiRequest<{
      packageOrderId: string;
      packageBandId: string;
      paymentStatus: string;
      amount: number;
      currency: string;
      paymentProvider?: string;
      checkoutUrl?: string | null;
      checkoutSessionId?: string | null;
      invoiceId?: string | null;
      invoiceStatus?: string | null;
      invoicePdfUrl?: string | null;
    }>(
      `/package-orders/${encodeURIComponent(orderId)}/checkout`,
      {
        method: 'POST',
        body: JSON.stringify({
          paymentProvider,
        }),
      },
      userId,
    );

    const packages = await this.getPackages();
    const selectedBand = packages.find((item) => item.id === response.packageBandId);

    return {
      order: {
        id: response.packageOrderId,
        userId,
        packageBandId: response.packageBandId,
        packageBandName: selectedBand?.name ?? 'Package',
        amount: response.amount,
        currency: response.currency,
        paymentProvider: response.paymentProvider ?? paymentProvider,
        paymentStatus: response.paymentStatus as PackageOrder['paymentStatus'],
        refundStatus: 'none',
        refundedAmount: 0,
        gatewayFeeRetainedAmount: 0,
        createdAt: new Date().toISOString(),
        refundReason: undefined,
        refundProcessedAt: undefined,
        invoiceId: response.invoiceId ?? undefined,
        invoiceStatus: response.invoiceStatus ?? undefined,
        invoicePdfUrl: response.invoicePdfUrl ?? undefined,
      },
      checkoutUrl: response.checkoutUrl ?? undefined,
      checkoutSessionId: response.checkoutSessionId ?? undefined,
      invoiceId: response.invoiceId ?? undefined,
      invoiceStatus: response.invoiceStatus ?? undefined,
      invoicePdfUrl: response.invoicePdfUrl ?? undefined,
    } satisfies PackageCheckoutSession;
  },

  async getOrders(userId: string) {
    const response = await apiRequest<PackageOrderResponse[]>('/package-orders', {}, userId);
    return response.map(mapPackageOrder);
  },

  async getOrder(orderId: string, userId: string) {
    const response = await apiRequest<PackageOrderResponse>(`/package-orders/${orderId}`, {}, userId);
    return mapPackageOrder(response);
  },

  async captureVodaPayCallback(orderId: string, queryParameters: Record<string, string>) {
    return apiRequest<{ message: string }>('/payments/callback/vodapay', {
      method: 'POST',
      body: JSON.stringify({
        packageOrderId: orderId,
        queryParameters,
      }),
    });
  },

  async getCampaigns(userId: string) {
    const response = await apiRequest<CampaignResponse[]>('/campaigns', {}, userId);
    return response.map(mapCampaign);
  },

  async getCampaign(campaignId: string) {
    const response = await apiRequest<CampaignResponse>(`/campaigns/${campaignId}`);
    return mapCampaign(response);
  },

  async getCampaignMessages(campaignId: string) {
    const response = await apiRequest<CampaignConversationThreadResponse>(`/campaigns/${campaignId}/messages`);
    return mapConversationThread(response);
  },

  async sendCampaignMessage(campaignId: string, body: string) {
    const response = await apiRequest<CampaignConversationThreadResponse>(`/campaigns/${campaignId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    });
    return mapConversationThread(response);
  },

  async getNotificationSummary() {
    return apiRequest<NotificationSummaryResponse>('/notifications/summary');
  },

  async markNotificationRead(notificationId: string) {
    await apiRequest(`/notifications/${encodeURIComponent(notificationId)}/read`, {
      method: 'POST',
      body: JSON.stringify({}),
    });
  },

  async markAllNotificationsRead() {
    await apiRequest('/notifications/read-all', {
      method: 'POST',
      body: JSON.stringify({}),
    });
  },

  async getAgentCampaign(campaignId: string) {
    const response = await apiRequest<CampaignResponse>(`/agent/campaigns/${campaignId}`);
    return mapCampaign(response);
  },

  async createAgentProspectCampaign(payload: {
    fullName: string;
    email: string;
    phone: string;
    packageBandId: string;
    campaignName?: string;
  }) {
    const response = await apiRequest<CampaignResponse>('/agent/campaigns/prospects', {
      method: 'POST',
      body: JSON.stringify(payload),
    });

    return mapCampaign(response);
  },

  async saveCampaignBrief(campaignId: string, brief: CampaignBrief) {
    await apiRequest(`/campaigns/${campaignId}/brief`, {
      method: 'PUT',
      body: JSON.stringify(brief),
    });

    return this.getCampaign(campaignId);
  },

  async submitCampaignBrief(campaignId: string) {
    await apiRequest(`/campaigns/${campaignId}/brief/submit`, {
      method: 'POST',
      body: JSON.stringify({}),
    });

    return this.getCampaign(campaignId);
  },

  async setPlanningMode(campaignId: string, mode: PlanningMode) {
    await apiRequest(`/campaigns/${campaignId}/planning-mode`, {
      method: 'POST',
      body: JSON.stringify({ planningMode: mode }),
    });

    return this.getCampaign(campaignId);
  },

  async markReviewReady(campaignId: string) {
    return this.getCampaign(campaignId);
  },

  async approveRecommendation(campaignId: string, recommendationId?: string) {
    await apiRequest(`/campaigns/${campaignId}/approve-recommendation`, {
      method: 'POST',
      body: JSON.stringify({ recommendationId }),
    });

    return this.getCampaign(campaignId);
  },

  async requestRecommendationChanges(campaignId: string, notes?: string) {
    await apiRequest(`/campaigns/${campaignId}/request-changes`, {
      method: 'POST',
      body: JSON.stringify({ notes }),
    });

    return this.getCampaign(campaignId);
  },

  async approveCreative(campaignId: string) {
    await apiRequest(`/campaigns/${campaignId}/approve-creative`, {
      method: 'POST',
      body: JSON.stringify({}),
    });

    return this.getCampaign(campaignId);
  },

  async requestCreativeChanges(campaignId: string, notes?: string) {
    await apiRequest(`/campaigns/${campaignId}/request-creative-changes`, {
      method: 'POST',
      body: JSON.stringify({ notes }),
    });

    return this.getCampaign(campaignId);
  },

  async markCampaignLaunched(campaignId: string) {
    await apiRequest(`/agent/campaigns/${campaignId}/mark-launched`, {
      method: 'POST',
      body: JSON.stringify({}),
    });

    return this.getAgentCampaign(campaignId);
  },

  async getAgentCampaigns() {
    const response = await apiRequest<CampaignResponse[]>('/agent/campaigns');
    return response.map(mapCampaign);
  },

  async getAgentInbox() {
    const response = await apiRequest<AgentInboxResponse>('/agent/campaigns/inbox');
    return mapAgentInbox(response);
  },

  async getAgentSales() {
    const response = await apiRequest<AgentSalesResponse>('/agent/campaigns/sales');
    return mapAgentSales(response);
  },

  async getCreativeInbox() {
    const response = await apiRequest<AgentInboxResponse>('/creative/campaigns/inbox');
    return mapAgentInbox(response);
  },

  async getCreativeCampaign(campaignId: string) {
    const response = await apiRequest<CampaignResponse>(`/creative/campaigns/${campaignId}`);
    return mapCampaign(response);
  },

  async getAgentMessageInbox() {
    const response = await apiRequest<CampaignConversationListItemResponse[]>('/agent/messages');
    return response.map(mapConversationListItem);
  },

  async getAgentMessageThread(campaignId: string) {
    const response = await apiRequest<CampaignConversationThreadResponse>(`/agent/messages/campaigns/${campaignId}`);
    return mapConversationThread(response);
  },

  async sendAgentMessage(campaignId: string, body: string) {
    const response = await apiRequest<CampaignConversationThreadResponse>(`/agent/messages/campaigns/${campaignId}`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    });
    return mapConversationThread(response);
  },

  async updateRecommendation(campaignId: string, recommendationId: string | undefined, notes: string, inventoryItems: SelectedPlanInventoryItem[]) {
    if (recommendationId) {
      await apiRequest(`/agent/recommendations/${recommendationId}`, {
        method: 'PUT',
        body: JSON.stringify({
          status: 'draft',
          notes,
          inventoryItems,
        }),
      });
    } else {
      await apiRequest(`/agent/campaigns/${campaignId}/recommendations`, {
        method: 'POST',
        body: JSON.stringify({ notes, inventoryItems }),
      });
    }

    const refreshedCampaign = await this.getAgentCampaign(campaignId);
    if (!refreshedCampaign.recommendations.length && !refreshedCampaign.recommendation) {
      throw new Error('Recommendation could not be loaded after saving.');
    }

    return refreshedCampaign.recommendations.find((recommendation) => recommendation.id === recommendationId)
      ?? refreshedCampaign.recommendation;
  },

  async deleteRecommendation(campaignId: string, recommendationId: string) {
    await apiRequest(`/agent/recommendations/${recommendationId}`, {
      method: 'DELETE',
    });

    return this.getAgentCampaign(campaignId);
  },

  async sendRecommendationToClient(campaignId: string) {
    await apiRequest(`/agent/campaigns/${campaignId}/send-to-client`, {
      method: 'POST',
      body: JSON.stringify({ message: 'Recommendation sent to client.' }),
    });

    return this.getAgentCampaign(campaignId);
  },

  async sendFinishedMediaToClientForApproval(campaignId: string) {
    await apiRequest(`/creative/campaigns/${campaignId}/send-finished-media-to-client`, {
      method: 'POST',
      body: JSON.stringify({}),
    });

    return this.getCreativeCampaign(campaignId);
  },

  async uploadCreativeCampaignAsset(campaignId: string, file: File, assetType: string) {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('assetType', assetType);

    const headers = new Headers();
    const sessionToken = getStoredSession()?.sessionToken;
    if (sessionToken) {
      headers.set('Authorization', `Bearer ${sessionToken}`);
    }

    const response = await fetch(`${API_BASE_URL}/creative/campaigns/${encodeURIComponent(campaignId)}/assets`, {
      method: 'POST',
      body: formData,
      headers,
    });

    if (!response.ok) {
      await parseApiError(response);
    }

    return mapCampaignAsset(await response.json());
  },

  async uploadAgentCampaignAsset(campaignId: string, file: File, assetType: string) {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('assetType', assetType);

    const headers = new Headers();
    const sessionToken = getStoredSession()?.sessionToken;
    if (sessionToken) {
      headers.set('Authorization', `Bearer ${sessionToken}`);
    }

    const response = await fetch(`${API_BASE_URL}/agent/campaigns/${encodeURIComponent(campaignId)}/assets`, {
      method: 'POST',
      body: formData,
      headers,
    });

    if (!response.ok) {
      await parseApiError(response);
    }

    return mapCampaignAsset(await response.json());
  },

  async saveSupplierBooking(campaignId: string, payload: {
    supplierOrStation: string;
    channel: string;
    bookingStatus: string;
    committedAmount: number;
    bookedAt?: string;
    liveFrom?: string;
    liveTo?: string;
    notes?: string;
    proofAssetId?: string;
  }) {
    await apiRequest(`/agent/campaigns/${campaignId}/supplier-bookings`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });

    return this.getAgentCampaign(campaignId);
  },

  async saveDeliveryReport(campaignId: string, payload: {
    supplierBookingId?: string;
    reportType: string;
    headline: string;
    summary?: string;
    reportedAt?: string;
    impressions?: number;
    playsOrSpots?: number;
    spendDelivered?: number;
    evidenceAssetId?: string;
  }) {
    await apiRequest(`/agent/campaigns/${campaignId}/delivery-reports`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });

    return this.getAgentCampaign(campaignId);
  },

  async initializeAgentRecommendation(
    campaignId: string,
    payload: {
      campaignName?: string;
      planningMode: PlanningMode;
      submitBrief: boolean;
      brief: CampaignBrief;
    },
  ) {
    const response = await apiRequest<CampaignResponse>(`/agent/campaigns/${campaignId}/initialize-recommendation`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });

    return mapCampaign(response);
  },

  async generateAgentRecommendation(
    campaignId: string,
    payload?: {
      targetRadioShare?: number;
      targetOohShare?: number;
      targetTvShare?: number;
      targetDigitalShare?: number;
    },
  ) {
    const response = await apiRequest<CampaignResponse>(`/agent/campaigns/${campaignId}/generate-recommendation`, {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    });

    return mapCampaign(response);
  },

  async interpretAgentBrief(campaignId: string, payload: { brief: string; campaignName?: string; selectedBudget: number }) {
    return apiRequest<InterpretedCampaignBriefResponse>(`/agent/campaigns/${campaignId}/interpret-brief`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async generateCreativeSystem(campaignId: string, payload: {
    prompt: string;
    iterationLabel?: string;
    brand?: string;
    product?: string;
    audience?: string;
    objective?: string;
    tone?: string;
    channels?: string[];
    cta?: string;
    constraints?: string[];
  }) {
    const response = await apiRequest<CreativeSystemResponse>(`/creative/campaigns/${campaignId}/creative-system`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });

    return normalizeCreativeSystem(response);
  },

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

  async assignCampaignToMe(campaignId: string) {
    await apiRequest(`/agent/campaigns/${campaignId}/assign`, {
      method: 'POST',
      body: JSON.stringify({}),
    });

    return this.getAgentCampaign(campaignId);
  },

  async unassignCampaign(campaignId: string) {
    await apiRequest(`/agent/campaigns/${campaignId}/unassign`, {
      method: 'POST',
      body: JSON.stringify({}),
    });

    return this.getAgentCampaign(campaignId);
  },

  async convertProspectToSale(campaignId: string, payload?: { paymentReference?: string }) {
    const response = await apiRequest<CampaignResponse>(`/agent/campaigns/${campaignId}/convert-to-sale`, {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    });

    return mapCampaign(response);
  },

  async updateProspectPricing(campaignId: string, payload: { packageBandId: string }) {
    const response = await apiRequest<CampaignResponse>(`/agent/campaigns/${campaignId}/prospect-pricing`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });

    return mapCampaign(response);
  },

  async getInventory(campaignId?: string) {
    return apiRequest<InventoryRow[]>(
      campaignId ? `/agent/inventory?campaignId=${encodeURIComponent(campaignId)}` : '/agent/inventory',
    );
  },

  async submitPartnerEnquiry(payload: {
    fullName: string;
    companyName: string;
    email: string;
    phone?: string;
    partnerType: string;
    inventorySummary?: string;
    message: string;
  }) {
    return apiRequest<{ message: string }>('/partner-enquiry', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },
};

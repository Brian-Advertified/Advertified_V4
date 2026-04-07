import type {
  AgentInbox,
  AgentInboxItem,
  AgentSales,
  AgentSaleItem,
  CampaignAsset,
  Campaign,
  CampaignConversationListItem,
  CampaignConversationThread,
  LegalDocument,
  NotificationSummary,
  CampaignDeliveryReport,
  CampaignCreativeSystemRecord,
  CampaignRecommendation,
  CampaignSupplierBooking,
  CreativeSystem,
  InventoryRow,
  PackageBand,
  PackageAreaOption,
  SelectOption,
  SharedFormOptions,
  AdminPackageOrder,
  PackagePreview,
  PackagePricingSummary,
  PackagePreviewMapPoint,
  PackageOrder,
  Lead,
  LeadAction,
  LeadActionInbox,
  LeadActionInboxItem,
  LeadInteraction,
  LeadInsightSnapshot,
  LeadIntelligence,
  PlanningMode,
  ConsentPreference,
} from '../types/domain';
import { normalizeCampaignBrief } from '../features/campaigns/briefModel';
import { authApi } from './authApi';
import { createAdminApi, type AdminPackageOrderResponse } from './adminApi';
import { createAgentApi } from './agentApi';
import { aiPlatformApi } from './aiPlatformApi';
import { createCampaignApi } from './campaignApi';
import { createCreativeApi } from './creativeApi';
import { createPublicApi } from './publicApi';
import { apiRequest, API_BASE_URL, downloadProtectedFile, downloadPublicFile, getAuthHeaders, parseApiError, toAbsoluteApiUrl } from './apiClient';

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

type LegalDocumentSectionResponse = {
  title: string;
  paragraphs: string[];
};

type LegalDocumentResponse = {
  documentKey: string;
  title: string;
  versionLabel: string;
  sections: LegalDocumentSectionResponse[];
  updatedAtUtc: string;
};

type PackageOrderResponse = {
  id: string;
  userId?: string | null;
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

type SelectOptionResponse = {
  value: string;
  label: string;
};

type SharedFormOptionsResponse = {
  businessTypes: SelectOptionResponse[];
  industries: SelectOptionResponse[];
  provinces: SelectOptionResponse[];
  revenueBands: SelectOptionResponse[];
  businessStages: SelectOptionResponse[];
  monthlyRevenueBands: SelectOptionResponse[];
  salesModels: SelectOptionResponse[];
  customerTypes: SelectOptionResponse[];
  buyingBehaviours: SelectOptionResponse[];
  decisionCycles: SelectOptionResponse[];
  growthTargets: SelectOptionResponse[];
  pricePositioning: SelectOptionResponse[];
  averageCustomerSpendBands: SelectOptionResponse[];
  urgencyLevels: SelectOptionResponse[];
  audienceClarity: SelectOptionResponse[];
  valuePropositionFocus: SelectOptionResponse[];
};

type CampaignBriefResponse = {
  objective: string;
  businessStage?: string;
  monthlyRevenueBand?: string;
  salesModel?: string;
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
  customerType?: string;
  buyingBehaviour?: string;
  decisionCycle?: string;
  pricePositioning?: string;
  averageCustomerSpendBand?: string;
  growthTarget?: string;
  urgencyLevel?: string;
  audienceClarity?: string;
  valuePropositionFocus?: string;
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
  userId?: string | null;
  clientName?: string | null;
  clientEmail?: string | null;
  businessName?: string | null;
  industry?: string | null;
  packageOrderId: string;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
  paymentProvider: string;
  paymentStatus: string;
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
  userId?: string | null;
  campaignName: string;
  clientName: string;
  clientEmail: string;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
  paymentStatus: string;
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

type PublicProspectQuestionnaireResponse = {
  campaignId: string;
  campaignName: string;
  message: string;
};

type LeadResponse = {
  id: number;
  name: string;
  website?: string | null;
  location: string;
  category: string;
  source: string;
  sourceReference?: string | null;
  lastDiscoveredAt?: string | null;
  createdAt: string;
};

type LeadSignalResponse = {
  id: number;
  leadId: number;
  hasPromo: boolean;
  hasMetaAds: boolean;
  websiteUpdatedRecently: boolean;
  createdAt: string;
};

type LeadScoreResponse = {
  leadId: number;
  score: number;
  intentLevel: string;
};

type LeadInsightResponse = {
  id: number;
  leadId: number;
  signalId?: number | null;
  trendSummary: string;
  scoreSnapshot: number;
  intentLevelSnapshot: string;
  text: string;
  createdAt: string;
};

type LeadActionResponse = {
  id: number;
  leadId: number;
  leadInsightId?: number | null;
  actionType: string;
  title: string;
  description: string;
  status: string;
  priority: string;
  assignedAgentUserId?: string | null;
  assignedAgentName?: string | null;
  assignedAt?: string | null;
  isAssignedToCurrentUser?: boolean;
  isUnassigned?: boolean;
  createdAt: string;
  completedAt?: string | null;
};

type LeadActionInboxItemResponse = {
  actionId: number;
  leadId: number;
  leadName: string;
  leadLocation: string;
  leadCategory: string;
  leadSource: string;
  action: LeadActionResponse;
};

type LeadActionInboxResponse = {
  totalOpenActions: number;
  assignedToMeCount: number;
  unassignedCount: number;
  highPriorityCount: number;
  items: LeadActionInboxItemResponse[];
};

type LeadInteractionResponse = {
  id: number;
  leadId: number;
  leadActionId?: number | null;
  interactionType: string;
  notes: string;
  createdAt: string;
};

type LeadIntelligenceResponse = {
  lead: LeadResponse;
  latestSignal?: LeadSignalResponse | null;
  score: LeadScoreResponse;
  insight: string;
  trendSummary?: string;
  signalHistory?: LeadSignalResponse[];
  insightHistory?: LeadInsightResponse[];
  recommendedActions?: LeadActionResponse[];
  interactionHistory?: LeadInteractionResponse[];
};

type CreativeSystemResponse = CreativeSystem;
function mapLead(response: LeadResponse): Lead {
  return {
    id: response.id,
    name: response.name,
    website: response.website ?? undefined,
    location: response.location,
    category: response.category,
    source: response.source,
    sourceReference: response.sourceReference ?? undefined,
    lastDiscoveredAt: response.lastDiscoveredAt ?? undefined,
    createdAt: response.createdAt,
  };
}

function mapLeadIntelligence(response: LeadIntelligenceResponse): LeadIntelligence {
  return {
    lead: mapLead(response.lead),
    latestSignal: response.latestSignal
      ? {
          id: response.latestSignal.id,
          leadId: response.latestSignal.leadId,
          hasPromo: response.latestSignal.hasPromo,
          hasMetaAds: response.latestSignal.hasMetaAds,
          websiteUpdatedRecently: response.latestSignal.websiteUpdatedRecently,
          createdAt: response.latestSignal.createdAt,
        }
      : undefined,
    score: {
      leadId: response.score.leadId,
      score: response.score.score,
      intentLevel: response.score.intentLevel,
    },
    insight: response.insight,
    trendSummary: response.trendSummary ?? '',
    signalHistory: (response.signalHistory ?? []).map((signal) => ({
      id: signal.id,
      leadId: signal.leadId,
      hasPromo: signal.hasPromo,
      hasMetaAds: signal.hasMetaAds,
      websiteUpdatedRecently: signal.websiteUpdatedRecently,
      createdAt: signal.createdAt,
    })),
    insightHistory: (response.insightHistory ?? []).map((insight): LeadInsightSnapshot => ({
      id: insight.id,
      leadId: insight.leadId,
      signalId: insight.signalId ?? undefined,
      trendSummary: insight.trendSummary,
      scoreSnapshot: insight.scoreSnapshot,
      intentLevelSnapshot: insight.intentLevelSnapshot,
      text: insight.text,
      createdAt: insight.createdAt,
    })),
    recommendedActions: (response.recommendedActions ?? []).map((action): LeadAction => ({
      id: action.id,
      leadId: action.leadId,
      leadInsightId: action.leadInsightId ?? undefined,
      actionType: action.actionType,
      title: action.title,
      description: action.description,
      status: action.status,
      priority: action.priority,
      assignedAgentUserId: action.assignedAgentUserId ?? undefined,
      assignedAgentName: action.assignedAgentName ?? undefined,
      assignedAt: action.assignedAt ?? undefined,
      isAssignedToCurrentUser: action.isAssignedToCurrentUser ?? false,
      isUnassigned: action.isUnassigned ?? !action.assignedAgentUserId,
      createdAt: action.createdAt,
      completedAt: action.completedAt ?? undefined,
    })),
    interactionHistory: (response.interactionHistory ?? []).map((interaction): LeadInteraction => ({
      id: interaction.id,
      leadId: interaction.leadId,
      leadActionId: interaction.leadActionId ?? undefined,
      interactionType: interaction.interactionType,
      notes: interaction.notes,
      createdAt: interaction.createdAt,
    })),
  };
}

function mapLeadAction(response: LeadActionResponse): LeadAction {
  return {
    id: response.id,
    leadId: response.leadId,
    leadInsightId: response.leadInsightId ?? undefined,
    actionType: response.actionType,
    title: response.title,
    description: response.description,
    status: response.status,
    priority: response.priority,
    assignedAgentUserId: response.assignedAgentUserId ?? undefined,
    assignedAgentName: response.assignedAgentName ?? undefined,
    assignedAt: response.assignedAt ?? undefined,
    isAssignedToCurrentUser: response.isAssignedToCurrentUser ?? false,
    isUnassigned: response.isUnassigned ?? !response.assignedAgentUserId,
    createdAt: response.createdAt,
    completedAt: response.completedAt ?? undefined,
  };
}

function mapLeadActionInboxItem(response: LeadActionInboxItemResponse): LeadActionInboxItem {
  return {
    actionId: response.actionId,
    leadId: response.leadId,
    leadName: response.leadName,
    leadLocation: response.leadLocation,
    leadCategory: response.leadCategory,
    leadSource: response.leadSource,
    action: mapLeadAction(response.action),
  };
}

function mapLeadActionInbox(response: LeadActionInboxResponse): LeadActionInbox {
  return {
    totalOpenActions: response.totalOpenActions,
    assignedToMeCount: response.assignedToMeCount,
    unassignedCount: response.unassignedCount,
    highPriorityCount: response.highPriorityCount,
    items: response.items.map(mapLeadActionInboxItem),
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

function mapLegalDocument(response: LegalDocumentResponse): LegalDocument {
  return {
    documentKey: response.documentKey,
    title: response.title,
    versionLabel: response.versionLabel,
    sections: response.sections ?? [],
    updatedAtUtc: response.updatedAtUtc,
  };
}

function mapPackageOrder(response: PackageOrderResponse): PackageOrder {
  return {
    id: response.id,
    userId: response.userId ?? undefined,
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
    userId: response.userId ?? undefined,
    clientName: response.clientName,
    clientEmail: response.clientEmail,
    clientPhone: response.clientPhone ?? '',
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

function mapSelectOptions(response?: SelectOptionResponse[] | null): SelectOption[] {
  return (response ?? []).map((option) => ({
    value: option.value,
    label: option.label,
  }));
}

function mapSharedFormOptions(response: SharedFormOptionsResponse): SharedFormOptions {
  return {
    businessTypes: mapSelectOptions(response.businessTypes),
    industries: mapSelectOptions(response.industries),
    provinces: mapSelectOptions(response.provinces),
    revenueBands: mapSelectOptions(response.revenueBands),
    businessStages: mapSelectOptions(response.businessStages),
    monthlyRevenueBands: mapSelectOptions(response.monthlyRevenueBands),
    salesModels: mapSelectOptions(response.salesModels),
    customerTypes: mapSelectOptions(response.customerTypes),
    buyingBehaviours: mapSelectOptions(response.buyingBehaviours),
    decisionCycles: mapSelectOptions(response.decisionCycles),
    growthTargets: mapSelectOptions(response.growthTargets),
    pricePositioning: mapSelectOptions(response.pricePositioning),
    averageCustomerSpendBands: mapSelectOptions(response.averageCustomerSpendBands),
    urgencyLevels: mapSelectOptions(response.urgencyLevels),
    audienceClarity: mapSelectOptions(response.audienceClarity),
    valuePropositionFocus: mapSelectOptions(response.valuePropositionFocus),
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
    userId: response.userId ?? undefined,
    clientName: response.clientName ?? undefined,
    clientEmail: response.clientEmail ?? undefined,
    businessName: response.businessName ?? undefined,
    industry: response.industry ?? undefined,
    packageOrderId: response.packageOrderId,
    packageBandId: response.packageBandId,
    packageBandName: response.packageBandName,
    selectedBudget: response.selectedBudget,
    paymentProvider: response.paymentProvider ?? 'vodapay',
    paymentStatus: response.paymentStatus as Campaign['paymentStatus'],
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
    brief: normalizeCampaignBrief(response.brief),
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
    userId: response.userId ?? undefined,
    campaignName: response.campaignName,
    clientName: response.clientName,
    clientEmail: response.clientEmail,
    packageBandId: response.packageBandId,
    packageBandName: response.packageBandName,
    selectedBudget: response.selectedBudget,
    paymentStatus: response.paymentStatus,
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

const adminApi = createAdminApi({
  mapAdminPackageOrder,
});

const campaignApi = createCampaignApi({
  async getCampaignById(campaignId) {
    const response = await apiRequest<CampaignResponse>(`/campaigns/${campaignId}`);
    return mapCampaign(response);
  },
  async getPublicProposalById(campaignId, token) {
    const response = await apiRequest<CampaignResponse>(
      `/public/proposals/${encodeURIComponent(campaignId)}?token=${encodeURIComponent(token)}`,
    );
    return mapCampaign(response);
  },
  async listCampaigns() {
    const response = await apiRequest<CampaignResponse[]>('/campaigns');
    return response.map(mapCampaign);
  },
  async getCampaignThread(campaignId) {
    const response = await apiRequest<CampaignConversationThreadResponse>(`/campaigns/${campaignId}/messages`);
    return mapConversationThread(response);
  },
  async sendCampaignThreadMessage(campaignId, body) {
    const response = await apiRequest<CampaignConversationThreadResponse>(`/campaigns/${campaignId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    });
    return mapConversationThread(response);
  },
  async getNotificationSummary() {
    return apiRequest<NotificationSummaryResponse>('/notifications/summary');
  },
  async mutateJson(path, method, body) {
    await apiRequest(path, {
      method,
      body: JSON.stringify(body),
    });
  },
});

const agentApi = createAgentApi({
  async getAgentCampaignById(campaignId) {
    const response = await apiRequest<CampaignResponse>(`/agent/campaigns/${campaignId}`);
    return mapCampaign(response);
  },
  async listAgentCampaigns() {
    const response = await apiRequest<CampaignResponse[]>('/agent/campaigns');
    return response.map(mapCampaign);
  },
  async getAgentInboxData() {
    const response = await apiRequest<AgentInboxResponse>('/agent/campaigns/inbox');
    return mapAgentInbox(response);
  },
  async getAgentSalesData() {
    const response = await apiRequest<AgentSalesResponse>('/agent/campaigns/sales');
    return mapAgentSales(response);
  },
  async getAgentMessageInboxData() {
    const response = await apiRequest<CampaignConversationListItemResponse[]>('/agent/messages');
    return response.map(mapConversationListItem);
  },
  async getAgentMessageThreadData(campaignId) {
    const response = await apiRequest<CampaignConversationThreadResponse>(`/agent/messages/campaigns/${campaignId}`);
    return mapConversationThread(response);
  },
  async sendAgentMessageData(campaignId, body) {
    const response = await apiRequest<CampaignConversationThreadResponse>(`/agent/messages/campaigns/${campaignId}`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    });
    return mapConversationThread(response);
  },
  async initializeRecommendationData(campaignId, payload) {
    const response = await apiRequest<{ campaignId: string }>(`/agent/campaigns/${campaignId}/initialize-recommendation`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return response;
  },
  async generateRecommendationData(campaignId, payload) {
    const response = await apiRequest<{ campaignId: string }>(`/agent/campaigns/${campaignId}/generate-recommendation`, {
      method: 'POST',
      body: JSON.stringify(payload ?? {}),
    });
    return response;
  },
  async interpretBriefData(campaignId, payload) {
    return apiRequest<InterpretedCampaignBriefResponse>(`/agent/campaigns/${campaignId}/interpret-brief`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },
  async createProspectCampaignData(payload) {
    const response = await apiRequest<CampaignResponse>('/agent/campaigns/prospects', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return mapCampaign(response);
  },
  async listInventoryData(campaignId) {
    return apiRequest<InventoryRow[]>(
      campaignId ? `/agent/inventory?campaignId=${encodeURIComponent(campaignId)}` : '/agent/inventory',
    );
  },
  async uploadAgentAssetData(campaignId, file, assetType) {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('assetType', assetType);

    const response = await fetch(`${API_BASE_URL}/agent/campaigns/${encodeURIComponent(campaignId)}/assets`, {
      method: 'POST',
      body: formData,
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      await parseApiError(response);
    }

    return mapCampaignAsset(await response.json());
  },
  async postJson(path, body) {
    await apiRequest(path, {
      method: 'POST',
      body: JSON.stringify(body),
    });
  },
  async putJson(path, body) {
    await apiRequest(path, {
      method: 'PUT',
      body: JSON.stringify(body),
    });
  },
  async deleteRequest(path) {
    await apiRequest(path, {
      method: 'DELETE',
    });
  },
});

const creativeApi = createCreativeApi({
  async getCreativeInboxData() {
    const response = await apiRequest<AgentInboxResponse>('/creative/campaigns/inbox');
    return mapAgentInbox(response);
  },
  async getCreativeCampaignById(campaignId) {
    const response = await apiRequest<CampaignResponse>(`/creative/campaigns/${campaignId}`);
    return mapCampaign(response);
  },
  async uploadCreativeAssetData(campaignId, file, assetType) {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('assetType', assetType);

    const response = await fetch(`${API_BASE_URL}/creative/campaigns/${encodeURIComponent(campaignId)}/assets`, {
      method: 'POST',
      body: formData,
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      await parseApiError(response);
    }

    return mapCampaignAsset(await response.json());
  },
  async generateCreativeSystemData(campaignId, payload) {
    const response = await apiRequest<CreativeSystemResponse>(`/creative/campaigns/${campaignId}/creative-system`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });

    return normalizeCreativeSystem(response);
  },
  async postJson(path, body) {
    await apiRequest(path, {
      method: 'POST',
      body: JSON.stringify(body),
    });
  },
});

const publicApi = createPublicApi({
  async getConsentPreferenceData(browserId) {
    const response = await apiRequest<ConsentPreferenceResponse>(`/consent/preferences?browserId=${encodeURIComponent(browserId)}`);
    return mapConsentPreference(response);
  },
  async saveConsentPreferenceData(input) {
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
  async getPackagesData() {
    const response = await apiRequest<PackageBandResponse[]>('/packages');
    return response.map(mapPackageBand);
  },
  async getPackageAreasData() {
    const response = await apiRequest<PackageAreaOptionResponse[]>('/packages/areas');
    return response.map(mapPackageAreaOption);
  },
  async getPackagePreviewData(packageBandId, budget, selectedArea) {
    const response = await apiRequest<PackagePreviewResponse>(
      `/packages/preview?packageBandId=${encodeURIComponent(packageBandId)}&budget=${encodeURIComponent(String(budget))}&selectedArea=${encodeURIComponent(selectedArea)}`,
    );
    return mapPackagePreview(response);
  },
  async getPackagePricingSummaryData(selectedBudget) {
    return apiRequest<PackagePricingSummaryResponse>(
      `/packages/pricing-summary?selectedBudget=${encodeURIComponent(String(selectedBudget))}`,
    ) as Promise<PackagePricingSummary>;
  },
  async getFormOptionsData() {
    const response = await apiRequest<SharedFormOptionsResponse>('/public/form-options');
    return mapSharedFormOptions(response);
  },
  async createOrderData(payload) {
    return apiRequest<{
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
    }>('/package-orders', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },
  async initiateOrderCheckoutData(orderId, payload) {
    return apiRequest<{
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
    }>(`/package-orders/${encodeURIComponent(orderId)}/checkout`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },
  async listOrdersData() {
    const response = await apiRequest<PackageOrderResponse[]>('/package-orders');
    return response.map(mapPackageOrder);
  },
  async getOrderData(orderId) {
    const response = await apiRequest<PackageOrderResponse>(`/package-orders/${orderId}`);
    return mapPackageOrder(response);
  },
  async captureVodaPayCallbackData(orderId, queryParameters) {
    return apiRequest<{ message: string }>('/payments/callback/vodapay', {
      method: 'POST',
      body: JSON.stringify({
        packageOrderId: orderId,
        queryParameters,
      }),
    });
  },
  async submitPartnerEnquiryData(payload) {
    return apiRequest<{ message: string }>('/partner-enquiry', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },
  async submitProspectQuestionnaireData(payload) {
    return apiRequest<PublicProspectQuestionnaireResponse>('/public/prospect-questionnaires', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },
  async getLegalDocumentData(documentKey) {
    const response = await apiRequest<LegalDocumentResponse>(`/public/legal-documents/${encodeURIComponent(documentKey)}`);
    return mapLegalDocument(response);
  },
});

export const advertifiedApi = {
  toAbsoluteApiUrl,
  downloadProtectedFile,
  downloadPublicFile,
  async getLeads() {
    const response = await apiRequest<LeadResponse[]>('/leads');
    return response.map(mapLead);
  },
  async getLeadIntelligenceList() {
    const response = await apiRequest<LeadIntelligenceResponse[]>('/leads/intelligence');
    return response.map(mapLeadIntelligence);
  },
  async getLeadIntelligence(leadId: number) {
    const response = await apiRequest<LeadIntelligenceResponse>(`/leads/${encodeURIComponent(String(leadId))}/intelligence`);
    return mapLeadIntelligence(response);
  },
  async getLeadActionInbox() {
    const response = await apiRequest<LeadActionInboxResponse>('/agent/lead-actions/inbox');
    return mapLeadActionInbox(response);
  },
  async createLead(input: { name: string; website?: string; location: string; category: string }) {
    const response = await apiRequest<LeadResponse>('/leads', {
      method: 'POST',
      body: JSON.stringify(input),
    });
    return mapLead(response);
  },
  async importLeadCsv(input: { csvText: string; defaultSource?: string }) {
    return apiRequest<{
      createdCount: number;
      updatedCount: number;
      leads: LeadResponse[];
    }>('/leads/import-csv', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  },
  async analyzeLead(leadId: number) {
    const response = await apiRequest<LeadIntelligenceResponse>(`/leads/${encodeURIComponent(String(leadId))}/analyze`, {
      method: 'POST',
    });
    return mapLeadIntelligence(response);
  },
  async updateLeadActionStatus(leadId: number, actionId: number, status: 'open' | 'completed' | 'dismissed') {
    const response = await apiRequest<LeadActionResponse>(`/leads/${encodeURIComponent(String(leadId))}/actions/${encodeURIComponent(String(actionId))}/status`, {
      method: 'POST',
      body: JSON.stringify({ status }),
    });
    return mapLeadAction(response);
  },
  async assignLeadActionToMe(leadId: number, actionId: number) {
    const response = await apiRequest<LeadActionResponse>(`/leads/${encodeURIComponent(String(leadId))}/actions/${encodeURIComponent(String(actionId))}/assign-to-me`, {
      method: 'POST',
    });
    return mapLeadAction(response);
  },
  async unassignLeadAction(leadId: number, actionId: number) {
    const response = await apiRequest<LeadActionResponse>(`/leads/${encodeURIComponent(String(leadId))}/actions/${encodeURIComponent(String(actionId))}/unassign`, {
      method: 'POST',
    });
    return mapLeadAction(response);
  },
  async createLeadInteraction(input: { leadId: number; leadActionId?: number; interactionType: string; notes: string }) {
    return apiRequest<LeadInteractionResponse>(`/leads/${encodeURIComponent(String(input.leadId))}/interactions`, {
      method: 'POST',
      body: JSON.stringify({
        leadActionId: input.leadActionId,
        interactionType: input.interactionType,
        notes: input.notes,
      }),
    });
  },
  ...publicApi,
  ...authApi,
  ...adminApi,
  ...campaignApi,
  ...agentApi,
  ...creativeApi,
  ...aiPlatformApi,

};

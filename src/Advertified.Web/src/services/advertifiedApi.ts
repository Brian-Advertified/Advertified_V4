import type {
  AgentInbox,
  AgentInboxItem,
  AgentSales,
  AgentSaleItem,
  CampaignAsset,
  Campaign,
  CampaignConversationListItem,
  CampaignConversationThread,
  CampaignPlanningTarget,
  CampaignPerformanceSnapshot,
  LegalDocument,
  NotificationSummary,
  CampaignDeliveryReport,
  CampaignPerformanceTimelinePoint,
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
  GoogleSheetsLeadIntegrationRunResult,
  GoogleSheetsLeadIntegrationStatus,
  LeadOpsCoverage,
  LeadOpsCoverageItem,
  LeadOpsCoverageSource,
  LeadOpsInbox,
  LeadOpsInboxItem,
  LeadChannelDetection,
  LeadIndustryPolicy,
  LeadIndustryContext,
  LocationSuggestion,
  LeadOpportunityProfile,
  LeadStrategy,
  LeadEnrichmentSnapshot,
  LeadSourceAutomationRunResult,
  LeadSourceAutomationStatus,
  LeadPaidMediaSyncRun,
  LeadPaidMediaSyncStatus,
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
import { createLeadApi } from './leadApi';
import { createPublicApi } from './publicApi';
import { apiRequest, API_BASE_URL, downloadProtectedFile, downloadPublicFile, parseApiError, readJsonResponse, toAbsoluteApiUrl } from './apiClient';

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
  orderIntent?: string | null;
  paymentProvider?: string | null;
  paymentStatus: string;
  refundStatus: string;
  refundedAmount: number;
  gatewayFeeRetainedAmount: number;
  refundReason?: string | null;
  refundProcessedAt?: string | null;
  createdAt: string;
  paymentReference?: string | null;
  selectedRecommendationId?: string | null;
  selectedAt?: string | null;
  selectionSource?: string | null;
  selectionStatus?: string | null;
  lostReason?: string | null;
  lostStage?: string | null;
  lostAt?: string | null;
  termsAcceptedAt?: string | null;
  termsVersion?: string | null;
  termsAcceptanceSource?: string | null;
  cancellationStatus?: string | null;
  cancellationReason?: string | null;
  cancellationRequestedAt?: string | null;
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
  industry?: string;
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
  targetLocationLabel?: string;
  targetLocationCity?: string;
  targetLocationProvince?: string;
  targetLatitude?: number;
  targetLongitude?: number;
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
  requestedStartDate?: string | null;
  requestedEndDate?: string | null;
  resolvedStartDate?: string | null;
  resolvedEndDate?: string | null;
  appliedDuration?: string | null;
  commercialExplanation?: string | null;
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
  audit?: {
    requestSummary: string;
    selectionSummary: string;
    rejectionSummary: string;
    policySummary: string;
    budgetSummary: string;
    fallbackSummary?: string | null;
  } | null;
  status: 'draft' | 'sent_to_client' | 'approved';
  totalCost: number;
  estimatedSupplierCost?: number | null;
  estimatedGrossProfit?: number | null;
  estimatedGrossMarginPercent?: number | null;
  marginStatus?: string | null;
  clientExplanation?: string | null;
  supplierAvailabilityStatus?: string | null;
  supplierAvailabilityCheckedAt?: string | null;
  supplierAvailabilityNotes?: string | null;
  emailDeliveries?: Array<{
    id: string;
    provider: string;
    purpose: string;
    templateName: string;
    status: string;
    recipientEmail: string;
    subject: string;
    latestEventType?: string | null;
    latestEventAt?: string | null;
    acceptedAt?: string | null;
    deliveredAt?: string | null;
    openedAt?: string | null;
    clickedAt?: string | null;
    bouncedAt?: string | null;
    failedAt?: string | null;
    lastError?: string | null;
  }> | null;
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
  orderIntent?: string | null;
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
  lifecycle?: {
    currentState: string;
    proposalState: string;
    paymentState: string;
    commercialState: string;
    communicationState: string;
    fulfilmentState: string;
    aiStudioAccessState: string;
  } | null;
  sendValidation?: {
    canSendRecommendation: boolean;
    reasons?: string[] | null;
  } | null;
  prospectDisposition?: {
    status: string;
    reasonCode?: string | null;
    notes?: string | null;
    closedAt?: string | null;
    closedByUserId?: string | null;
    closedByName?: string | null;
  } | null;
  businessProcess?: {
    revenueAttribution: {
      agentUserId?: string | null;
      agentName?: string | null;
      geography: string;
      packageName: string;
      channelSpend?: Record<string, number> | null;
      paidRevenue: number;
    };
    lostReason: {
      stage?: string | null;
      reason?: string | null;
      lostAt?: string | null;
    };
    recommendationCommercialCheck: {
      recommendationId?: string | null;
      totalCost: number;
      estimatedSupplierCost: number;
      estimatedGrossProfit: number;
      estimatedGrossMarginPercent?: number | null;
      marginStatus: string;
    };
    supplierReadiness: {
      status: string;
      confirmedBookings: number;
      unconfirmedBookings: number;
      summary: string;
    };
    postCampaignGrowth: {
      reportingStatus: string;
      renewalRecommended: boolean;
      nextAction: string;
    };
    termsAcceptance: {
      accepted: boolean;
      acceptedAt?: string | null;
      version?: string | null;
      source?: string | null;
    };
    refundCancellation: {
      refundStatus: string;
      refundedAmount: number;
      refundReason?: string | null;
      refundProcessedAt?: string | null;
      cancellationStatus: string;
      cancellationReason?: string | null;
      cancellationRequestedAt?: string | null;
    };
  } | null;
  effectivePlanningTarget?: {
    label: string;
    city?: string | null;
    province?: string | null;
    latitude?: number | null;
    longitude?: number | null;
    source: string;
    precision: string;
  } | null;
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
    availabilityStatus?: string | null;
    availabilityCheckedAt?: string | null;
    supplierConfirmationReference?: string | null;
    confirmedAt?: string | null;
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
  performanceTimeline?: Array<{
    date: string;
    impressions: number;
    playsOrSpots: number;
    leads: number;
    cplZar?: number | null;
    roas?: number | null;
    spendDelivered: number;
  }> | null;
  executionTasks?: Array<{
    id: string;
    taskKey: string;
    title: string;
    details?: string | null;
    status: string;
    sortOrder: number;
    dueAt?: string | null;
    completedAt?: string | null;
  }> | null;
  effectiveEndDate?: string | null;
  daysLeft?: number | null;
  createdAt: string;
};

type CampaignPerformanceSnapshotResponse = {
  campaignId: string;
  totalBookedSpend: number;
  totalDeliveredSpend: number;
  totalImpressions: number;
  totalPlaysOrSpots: number;
  totalLeads: number;
  averageCplZar?: number | null;
  averageRoas?: number | null;
  totalSyncedClicks: number;
  bookingCount: number;
  reportCount: number;
  spendDeliveryPercent: number;
  latestReportDate?: string | null;
  timeline: Array<{
    date: string;
    impressions: number;
    playsOrSpots: number;
    leads: number;
    cplZar?: number | null;
    roas?: number | null;
    spendDelivered: number;
  }>;
  channels: Array<{
    channel: string;
    label: string;
    bookedSpend: number;
    deliveredSpend: number;
    impressions: number;
    playsOrSpots: number;
    leads: number;
    cplZar?: number | null;
    roas?: number | null;
    syncedClicks: number;
    bookingCount: number;
    reportCount: number;
  }>;
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
  waitingOnClientCount: number;
  completedCount: number;
  items: AgentInboxItemResponse[];
};

type LeadOpsInboxItemResponse = {
  id: string;
  itemType: LeadOpsInboxItem['itemType'];
  itemLabel: string;
  campaignId?: string | null;
  prospectLeadId?: string | null;
  leadId?: number | null;
  leadActionId?: number | null;
  title: string;
  subtitle: string;
  description: string;
  unifiedStatus: string;
  assignedAgentUserId?: string | null;
  assignedAgentName?: string | null;
  isAssignedToCurrentUser: boolean;
  isUnassigned: boolean;
  isUrgent: boolean;
  routePath: string;
  routeLabel: string;
  createdAt: string;
  updatedAt: string;
  dueAt?: string | null;
};

type LeadOpsInboxResponse = {
  totalItems: number;
  urgentCount: number;
  assignedToMeCount: number;
  unassignedCount: number;
  newInboundProspectsCount: number;
  unassignedProspectsCount: number;
  openLeadActionsCount: number;
  noRecentActivityCount: number;
  awaitingClientResponsesCount: number;
  overdueFollowUpsCount: number;
  items: LeadOpsInboxItemResponse[];
};

type LeadOpsCoverageSourceResponse = {
  source: string;
  leadCount: number;
  prospectCount: number;
  wonCount: number;
};

type LeadOpsCoverageItemResponse = {
  recordKey: string;
  leadId: number;
  leadName: string;
  location: string;
  category: string;
  source: string;
  sourceReference?: string | null;
  unifiedStatus: string;
  ownerAgentUserId?: string | null;
  ownerAgentName?: string | null;
  ownerResolution: string;
  assignmentStatus: string;
  hasBeenContacted: boolean;
  firstContactedAt?: string | null;
  contactStatus: string;
  lastContactedAt?: string | null;
  nextAction: string;
  nextActionDueAt?: string | null;
  nextFollowUpAt?: string | null;
  slaDueAt?: string | null;
  priority: string;
  attentionReasons: string[];
  openLeadActionCount: number;
  hasProspect: boolean;
  prospectLeadId?: string | null;
  activeCampaignId?: string | null;
  wonCampaignId?: string | null;
  convertedToSale: boolean;
  lastOutcome?: string | null;
  routePath: string;
};

type LeadOpsCoverageResponse = {
  generatedAtUtc: string;
  totalLeadCount: number;
  ownedLeadCount: number;
  unownedLeadCount: number;
  ambiguousOwnerCount: number;
  uncontactedLeadCount: number;
  leadsWithNextActionCount: number;
  prospectLeadCount: number;
  activeDealCount: number;
  wonLeadCount: number;
  leadToProspectRatePercent: number;
  leadToSaleRatePercent: number;
  sources: LeadOpsCoverageSourceResponse[];
  items: LeadOpsCoverageItemResponse[];
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
  latitude?: number | null;
  longitude?: number | null;
  category: string;
  source: string;
  sourceReference?: string | null;
  ownerAgentUserId?: string | null;
  ownerAgentName?: string | null;
  autoInferredFields?: string[] | null;
  lastDiscoveredAt?: string | null;
  firstContactedAt?: string | null;
  lastContactedAt?: string | null;
  nextFollowUpAt?: string | null;
  slaDueAt?: string | null;
  lastOutcome?: string | null;
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

type LeadSourceAutomationStatusResponse = {
  dropFolderEnabled: boolean;
  inboxPath: string;
  processedPath: string;
  failedPath: string;
  pendingFileCount: number;
  processedFileCount: number;
  failedFileCount: number;
  defaultSource: string;
  defaultImportProfile: string;
  analyzeImportedLeads: boolean;
};

type LeadSourceAutomationRunResultResponse = {
  processedFileCount: number;
  failedFileCount: number;
  importedLeadCount: number;
  analyzedLeadCount: number;
};

type GoogleSheetsLeadSourceStatusResponse = {
  name: string;
  enabled: boolean;
  defaultSource: string;
  importProfile: string;
  csvExportUrl: string;
};

type GoogleSheetsLeadIntegrationStatusResponse = {
  enabled: boolean;
  importEnabled: boolean;
  exportEnabled: boolean;
  importPollIntervalMinutes: number;
  exportPollIntervalMinutes: number;
  exportWebhookConfigured: boolean;
  configuredSourceCount: number;
  activeSourceCount: number;
  sources: GoogleSheetsLeadSourceStatusResponse[];
};

type GoogleSheetsLeadIntegrationRunResultResponse = {
  operation: string;
  processedSourceCount: number;
  failedSourceCount: number;
  createdLeadCount: number;
  updatedLeadCount: number;
  exportedItemCount: number;
  message: string;
};

type LeadPaidMediaSyncRunResponse = {
  startedAtUtc: string;
  finishedAtUtc: string;
  skipped: boolean;
  skipReason?: string | null;
  totalLeadCount: number;
  processedLeadCount: number;
  failedLeadCount: number;
  evidenceRowCount: number;
  enabledProviders?: string[] | null;
  providerEvidenceCounts: Record<string, number>;
};

type LeadPaidMediaSyncStatusResponse = {
  enabled: boolean;
  batchSize: number;
  intervalMinutes: number;
  lastRun?: LeadPaidMediaSyncRunResponse | null;
};

type LeadInteractionResponse = {
  id: number;
  leadId: number;
  leadActionId?: number | null;
  interactionType: string;
  notes: string;
  createdAt: string;
};

type LeadChannelSignalResponse = {
  type: string;
  source: string;
  weight: number;
  reliabilityMultiplier: number;
  freshnessMultiplier: number;
  effectiveWeight: number;
  value: string;
};

type LeadChannelDetectionResponse = {
  leadId: number;
  channel: string;
  score: number;
  confidence: string;
  status: string;
  dominantReason: string;
  lastEvidenceAtUtc?: string | null;
  signals: LeadChannelSignalResponse[];
};

type LeadIntelligenceResponse = {
  lead: LeadResponse;
  latestSignal?: LeadSignalResponse | null;
  score: LeadScoreResponse;
  insight: string;
  trendSummary?: string;
  channelDetections?: LeadChannelDetectionResponse[];
  signalHistory?: LeadSignalResponse[];
  insightHistory?: LeadInsightResponse[];
  recommendedActions?: LeadActionResponse[];
  interactionHistory?: LeadInteractionResponse[];
  industryPolicy: LeadIndustryPolicyResponse;
  opportunityProfile: LeadOpportunityProfileResponse;
  enrichment?: LeadEnrichmentSnapshotResponse;
  strategy?: LeadStrategyResponse;
};

type LeadIndustryPolicyResponse = {
  key: string;
  name: string;
  objectiveOverride?: string | null;
  preferredTone?: string | null;
  preferredChannels: string[];
  cta: string;
  messagingAngle: string;
  guardrails: string[];
  additionalGap: string;
  additionalOutcome: string;
};

type LeadIndustryAudienceProfileResponse = {
  primaryPersona: string;
  buyingJourney: string;
  trustSensitivity: string;
  defaultLanguageBiases: string[];
  audienceHints: string[];
};

type LeadIndustryCampaignProfileResponse = {
  defaultObjective: string;
  funnelShape: string;
  primaryKpis: string[];
  salesCycle: string;
};

type LeadIndustryChannelProfileResponse = {
  preferredChannels: string[];
  baseBudgetSplit: Record<string, number>;
  geographyBias: string;
};

type LeadIndustryCreativeProfileResponse = {
  preferredTone: string;
  messagingAngle: string;
  recommendedCta: string;
  proofPoints: string[];
};

type LeadIndustryComplianceProfileResponse = {
  guardrails: string[];
  restrictedClaimTypes: string[];
};

type LeadIndustryResearchProfileResponse = {
  summary: string;
  sources: string[];
};

type LeadIndustryContextResponse = {
  code: string;
  label: string;
  policy: LeadIndustryPolicyResponse;
  audience: LeadIndustryAudienceProfileResponse;
  campaign: LeadIndustryCampaignProfileResponse;
  channels: LeadIndustryChannelProfileResponse;
  creative: LeadIndustryCreativeProfileResponse;
  compliance: LeadIndustryComplianceProfileResponse;
  research: LeadIndustryResearchProfileResponse;
};

type LeadOpportunityProfileResponse = {
  key: string;
  name: string;
  suggestedCampaignType: string;
  detectedGaps: string[];
  expectedOutcome: string;
  recommendedChannels: string[];
  whyActNow: string;
};

type LeadStrategyChannelResponse = {
  channel: string;
  budgetSharePercent: number;
  reason: string;
};

type LeadStrategyResponse = {
  archetype: string;
  objective: string;
  channels: LeadStrategyChannelResponse[];
  geoTargets: string[];
  timing: string;
  rationale: string;
};

type LeadEnrichmentFieldResponse = {
  key: string;
  label: string;
  value: string;
  confidence: string;
  source: string;
  reason: string;
  required: boolean;
};

type LeadConfidenceGateResponse = {
  isBlocked: boolean;
  requiredFields: string[];
  missingRequiredFields: string[];
  message: string;
};

type LeadEnrichmentSnapshotResponse = {
  fields: LeadEnrichmentFieldResponse[];
  confidenceGate: LeadConfidenceGateResponse;
  confidenceScore: number;
  missingFields: string[];
  generatedAtUtc: string;
};

type CreativeSystemResponse = CreativeSystem;
function mapLead(response: LeadResponse): Lead {
  return {
    id: response.id,
    name: response.name,
    website: response.website ?? undefined,
    location: response.location,
    latitude: response.latitude ?? undefined,
    longitude: response.longitude ?? undefined,
    category: response.category,
    source: response.source,
    sourceReference: response.sourceReference ?? undefined,
    ownerAgentUserId: response.ownerAgentUserId ?? undefined,
    ownerAgentName: response.ownerAgentName ?? undefined,
    autoInferredFields: response.autoInferredFields ?? [],
    lastDiscoveredAt: response.lastDiscoveredAt ?? undefined,
    firstContactedAt: response.firstContactedAt ?? undefined,
    lastContactedAt: response.lastContactedAt ?? undefined,
    nextFollowUpAt: response.nextFollowUpAt ?? undefined,
    slaDueAt: response.slaDueAt ?? undefined,
    lastOutcome: response.lastOutcome ?? undefined,
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
    channelDetections: (response.channelDetections ?? []).map((detection): LeadChannelDetection => ({
      leadId: detection.leadId,
      channel: detection.channel,
      score: detection.score,
      confidence: detection.confidence,
      status: detection.status,
      dominantReason: detection.dominantReason,
      lastEvidenceAtUtc: detection.lastEvidenceAtUtc ?? undefined,
      signals: detection.signals.map((signal) => ({
        type: signal.type,
        source: signal.source,
        weight: signal.weight,
        reliabilityMultiplier: signal.reliabilityMultiplier,
        freshnessMultiplier: signal.freshnessMultiplier,
        effectiveWeight: signal.effectiveWeight,
        value: signal.value,
      })),
    })),
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
    industryPolicy: mapLeadIndustryPolicy(response.industryPolicy),
    opportunityProfile: mapLeadOpportunityProfile(response.opportunityProfile),
    enrichment: mapLeadEnrichmentSnapshot(response.enrichment),
    strategy: mapLeadStrategy(response.strategy),
  };
}

function mapLeadEnrichmentSnapshot(response?: LeadEnrichmentSnapshotResponse): LeadEnrichmentSnapshot {
  if (!response) {
    return {
      fields: [],
      confidenceGate: {
        isBlocked: true,
        requiredFields: ['Location', 'Industry', 'Channel activity'],
        missingRequiredFields: ['Location', 'Industry', 'Channel activity'],
        message: 'Recommendation halted. Lead enrichment snapshot is missing.',
      },
      confidenceScore: 0,
      missingFields: ['Location', 'Industry', 'Channel activity'],
      generatedAtUtc: new Date().toISOString(),
    };
  }

  return {
    fields: (response.fields ?? []).map((field) => ({
      key: field.key,
      label: field.label,
      value: field.value,
      confidence: field.confidence,
      source: field.source,
      reason: field.reason,
      required: field.required,
    })),
    confidenceGate: {
      isBlocked: response.confidenceGate?.isBlocked ?? false,
      requiredFields: response.confidenceGate?.requiredFields ?? [],
      missingRequiredFields: response.confidenceGate?.missingRequiredFields ?? [],
      message: response.confidenceGate?.message ?? '',
    },
    confidenceScore: response.confidenceScore ?? 0,
    missingFields: response.missingFields ?? [],
    generatedAtUtc: response.generatedAtUtc,
  };
}

function mapLeadIndustryPolicy(response: LeadIndustryPolicyResponse): LeadIndustryPolicy {
  return {
    key: response.key,
    name: response.name,
    objectiveOverride: response.objectiveOverride ?? undefined,
    preferredTone: response.preferredTone ?? undefined,
    preferredChannels: response.preferredChannels ?? [],
    cta: response.cta,
    messagingAngle: response.messagingAngle,
    guardrails: response.guardrails ?? [],
    additionalGap: response.additionalGap,
    additionalOutcome: response.additionalOutcome,
  };
}

function mapLeadIndustryContext(response: LeadIndustryContextResponse): LeadIndustryContext {
  return {
    code: response.code,
    label: response.label,
    policy: mapLeadIndustryPolicy(response.policy),
    audience: {
      primaryPersona: response.audience?.primaryPersona ?? '',
      buyingJourney: response.audience?.buyingJourney ?? '',
      trustSensitivity: response.audience?.trustSensitivity ?? '',
      defaultLanguageBiases: response.audience?.defaultLanguageBiases ?? [],
      audienceHints: response.audience?.audienceHints ?? [],
    },
    campaign: {
      defaultObjective: response.campaign?.defaultObjective ?? '',
      funnelShape: response.campaign?.funnelShape ?? '',
      primaryKpis: response.campaign?.primaryKpis ?? [],
      salesCycle: response.campaign?.salesCycle ?? '',
    },
    channels: {
      preferredChannels: response.channels?.preferredChannels ?? [],
      baseBudgetSplit: response.channels?.baseBudgetSplit ?? {},
      geographyBias: response.channels?.geographyBias ?? '',
    },
    creative: {
      preferredTone: response.creative?.preferredTone ?? '',
      messagingAngle: response.creative?.messagingAngle ?? '',
      recommendedCta: response.creative?.recommendedCta ?? '',
      proofPoints: response.creative?.proofPoints ?? [],
    },
    compliance: {
      guardrails: response.compliance?.guardrails ?? [],
      restrictedClaimTypes: response.compliance?.restrictedClaimTypes ?? [],
    },
    research: {
      summary: response.research?.summary ?? '',
      sources: response.research?.sources ?? [],
    },
  };
}

function mapLeadOpportunityProfile(response: LeadOpportunityProfileResponse): LeadOpportunityProfile {
  return {
    key: response.key,
    name: response.name,
    suggestedCampaignType: response.suggestedCampaignType,
    detectedGaps: response.detectedGaps ?? [],
    expectedOutcome: response.expectedOutcome,
    recommendedChannels: response.recommendedChannels ?? [],
    whyActNow: response.whyActNow,
  };
}

function mapLeadStrategy(response?: LeadStrategyResponse): LeadStrategy {
  if (!response) {
    return {
      archetype: '',
      objective: '',
      channels: [],
      geoTargets: [],
      timing: '',
      rationale: '',
    };
  }

  return {
    archetype: response.archetype,
    objective: response.objective,
    channels: (response.channels ?? []).map((channel) => ({
      channel: channel.channel,
      budgetSharePercent: channel.budgetSharePercent,
      reason: channel.reason,
    })),
    geoTargets: response.geoTargets ?? [],
    timing: response.timing,
    rationale: response.rationale,
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

function mapLeadOpsInboxItem(response: LeadOpsInboxItemResponse): LeadOpsInboxItem {
  return {
    id: response.id,
    itemType: response.itemType,
    itemLabel: response.itemLabel,
    campaignId: response.campaignId ?? undefined,
    prospectLeadId: response.prospectLeadId ?? undefined,
    leadId: response.leadId ?? undefined,
    leadActionId: response.leadActionId ?? undefined,
    title: response.title,
    subtitle: response.subtitle,
    description: response.description,
    unifiedStatus: response.unifiedStatus,
    assignedAgentUserId: response.assignedAgentUserId ?? undefined,
    assignedAgentName: response.assignedAgentName ?? undefined,
    isAssignedToCurrentUser: response.isAssignedToCurrentUser,
    isUnassigned: response.isUnassigned,
    isUrgent: response.isUrgent,
    routePath: response.routePath,
    routeLabel: response.routeLabel,
    createdAt: response.createdAt,
    updatedAt: response.updatedAt,
    dueAt: response.dueAt ?? undefined,
  };
}

function mapLeadOpsInbox(response: LeadOpsInboxResponse): LeadOpsInbox {
  return {
    totalItems: response.totalItems,
    urgentCount: response.urgentCount,
    assignedToMeCount: response.assignedToMeCount,
    unassignedCount: response.unassignedCount,
    newInboundProspectsCount: response.newInboundProspectsCount,
    unassignedProspectsCount: response.unassignedProspectsCount,
    openLeadActionsCount: response.openLeadActionsCount,
    noRecentActivityCount: response.noRecentActivityCount,
    awaitingClientResponsesCount: response.awaitingClientResponsesCount,
    overdueFollowUpsCount: response.overdueFollowUpsCount,
    items: response.items.map(mapLeadOpsInboxItem),
  };
}

function mapLeadOpsCoverageSource(response: LeadOpsCoverageSourceResponse): LeadOpsCoverageSource {
  return {
    source: response.source,
    leadCount: response.leadCount,
    prospectCount: response.prospectCount,
    wonCount: response.wonCount,
  };
}

function mapLeadOpsCoverageItem(response: LeadOpsCoverageItemResponse): LeadOpsCoverageItem {
  return {
    recordKey: response.recordKey,
    leadId: response.leadId,
    leadName: response.leadName,
    location: response.location,
    category: response.category,
    source: response.source,
    sourceReference: response.sourceReference ?? undefined,
    unifiedStatus: response.unifiedStatus,
    ownerAgentUserId: response.ownerAgentUserId ?? undefined,
    ownerAgentName: response.ownerAgentName ?? undefined,
    ownerResolution: response.ownerResolution,
    assignmentStatus: response.assignmentStatus,
    hasBeenContacted: response.hasBeenContacted,
    firstContactedAt: response.firstContactedAt ?? undefined,
    contactStatus: response.contactStatus,
    lastContactedAt: response.lastContactedAt ?? undefined,
    nextAction: response.nextAction,
    nextActionDueAt: response.nextActionDueAt ?? undefined,
    nextFollowUpAt: response.nextFollowUpAt ?? undefined,
    slaDueAt: response.slaDueAt ?? undefined,
    priority: response.priority,
    attentionReasons: response.attentionReasons ?? [],
    openLeadActionCount: response.openLeadActionCount,
    hasProspect: response.hasProspect,
    prospectLeadId: response.prospectLeadId ?? undefined,
    activeCampaignId: response.activeCampaignId ?? undefined,
    wonCampaignId: response.wonCampaignId ?? undefined,
    convertedToSale: response.convertedToSale,
    lastOutcome: response.lastOutcome ?? undefined,
    routePath: response.routePath,
  };
}

function mapLeadOpsCoverage(response: LeadOpsCoverageResponse): LeadOpsCoverage {
  return {
    generatedAtUtc: response.generatedAtUtc,
    totalLeadCount: response.totalLeadCount,
    ownedLeadCount: response.ownedLeadCount,
    unownedLeadCount: response.unownedLeadCount,
    ambiguousOwnerCount: response.ambiguousOwnerCount,
    uncontactedLeadCount: response.uncontactedLeadCount,
    leadsWithNextActionCount: response.leadsWithNextActionCount,
    prospectLeadCount: response.prospectLeadCount,
    activeDealCount: response.activeDealCount,
    wonLeadCount: response.wonLeadCount,
    leadToProspectRatePercent: response.leadToProspectRatePercent,
    leadToSaleRatePercent: response.leadToSaleRatePercent,
    sources: response.sources.map(mapLeadOpsCoverageSource),
    items: response.items.map(mapLeadOpsCoverageItem),
  };
}

function mapLeadSourceAutomationStatus(response: LeadSourceAutomationStatusResponse): LeadSourceAutomationStatus {
  return {
    dropFolderEnabled: response.dropFolderEnabled,
    inboxPath: response.inboxPath,
    processedPath: response.processedPath,
    failedPath: response.failedPath,
    pendingFileCount: response.pendingFileCount,
    processedFileCount: response.processedFileCount,
    failedFileCount: response.failedFileCount,
    defaultSource: response.defaultSource,
    defaultImportProfile: response.defaultImportProfile,
    analyzeImportedLeads: response.analyzeImportedLeads,
  };
}

function mapLeadSourceAutomationRunResult(response: LeadSourceAutomationRunResultResponse): LeadSourceAutomationRunResult {
  return {
    processedFileCount: response.processedFileCount,
    failedFileCount: response.failedFileCount,
    importedLeadCount: response.importedLeadCount,
    analyzedLeadCount: response.analyzedLeadCount,
  };
}

function mapGoogleSheetsLeadIntegrationStatus(response: GoogleSheetsLeadIntegrationStatusResponse): GoogleSheetsLeadIntegrationStatus {
  return {
    enabled: response.enabled,
    importEnabled: response.importEnabled,
    exportEnabled: response.exportEnabled,
    importPollIntervalMinutes: response.importPollIntervalMinutes,
    exportPollIntervalMinutes: response.exportPollIntervalMinutes,
    exportWebhookConfigured: response.exportWebhookConfigured,
    configuredSourceCount: response.configuredSourceCount,
    activeSourceCount: response.activeSourceCount,
    sources: (response.sources ?? []).map((source) => ({
      name: source.name,
      enabled: source.enabled,
      defaultSource: source.defaultSource,
      importProfile: source.importProfile,
      csvExportUrl: source.csvExportUrl,
    })),
  };
}

function mapGoogleSheetsLeadIntegrationRunResult(response: GoogleSheetsLeadIntegrationRunResultResponse): GoogleSheetsLeadIntegrationRunResult {
  return {
    operation: response.operation,
    processedSourceCount: response.processedSourceCount,
    failedSourceCount: response.failedSourceCount,
    createdLeadCount: response.createdLeadCount,
    updatedLeadCount: response.updatedLeadCount,
    exportedItemCount: response.exportedItemCount,
    message: response.message,
  };
}

function mapLeadPaidMediaSyncRun(response: LeadPaidMediaSyncRunResponse): LeadPaidMediaSyncRun {
  return {
    startedAtUtc: response.startedAtUtc,
    finishedAtUtc: response.finishedAtUtc,
    skipped: response.skipped,
    skipReason: response.skipReason ?? undefined,
    totalLeadCount: response.totalLeadCount,
    processedLeadCount: response.processedLeadCount,
    failedLeadCount: response.failedLeadCount,
    evidenceRowCount: response.evidenceRowCount,
    enabledProviders: response.enabledProviders ?? [],
    providerEvidenceCounts: response.providerEvidenceCounts ?? {},
  };
}

function mapLeadPaidMediaSyncStatus(response: LeadPaidMediaSyncStatusResponse): LeadPaidMediaSyncStatus {
  return {
    enabled: response.enabled,
    batchSize: response.batchSize,
    intervalMinutes: response.intervalMinutes,
    lastRun: response.lastRun ? mapLeadPaidMediaSyncRun(response.lastRun) : undefined,
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
    orderIntent: response.orderIntent ?? 'sale',
    paymentProvider: response.paymentProvider ?? undefined,
    paymentStatus: response.paymentStatus as PackageOrder['paymentStatus'],
    refundStatus: response.refundStatus as PackageOrder['refundStatus'],
    refundedAmount: response.refundedAmount,
    gatewayFeeRetainedAmount: response.gatewayFeeRetainedAmount,
    refundReason: response.refundReason ?? undefined,
    refundProcessedAt: response.refundProcessedAt ?? undefined,
    createdAt: response.createdAt,
    paymentReference: response.paymentReference ?? undefined,
    selectedRecommendationId: response.selectedRecommendationId ?? undefined,
    selectedAt: response.selectedAt ?? undefined,
    selectionSource: response.selectionSource ?? undefined,
    selectionStatus: response.selectionStatus ?? undefined,
    lostReason: response.lostReason ?? undefined,
    lostStage: response.lostStage ?? undefined,
    lostAt: response.lostAt ?? undefined,
    termsAcceptedAt: response.termsAcceptedAt ?? undefined,
    termsVersion: response.termsVersion ?? undefined,
    termsAcceptanceSource: response.termsAcceptanceSource ?? undefined,
    cancellationStatus: response.cancellationStatus ?? undefined,
    cancellationReason: response.cancellationReason ?? undefined,
    cancellationRequestedAt: response.cancellationRequestedAt ?? undefined,
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
    salesCommissionPercent: response.salesCommissionPercent,
    salesCommissionPoolAmount: response.salesCommissionPoolAmount,
    salesAgentCommissionSharePercent: response.salesAgentCommissionSharePercent,
    salesAgentCommissionAmount: response.salesAgentCommissionAmount,
    advertifiedSalesCommissionAmount: response.advertifiedSalesCommissionAmount,
    salesCommissionTier: response.salesCommissionTier,
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
      audit: response.audit
        ? {
            requestSummary: response.audit.requestSummary,
            selectionSummary: response.audit.selectionSummary,
            rejectionSummary: response.audit.rejectionSummary,
            policySummary: response.audit.policySummary,
            budgetSummary: response.audit.budgetSummary,
            fallbackSummary: response.audit.fallbackSummary ?? undefined,
          }
        : undefined,
      status: response.status,
      totalCost: response.totalCost,
      estimatedSupplierCost: response.estimatedSupplierCost ?? undefined,
      estimatedGrossProfit: response.estimatedGrossProfit ?? undefined,
      estimatedGrossMarginPercent: response.estimatedGrossMarginPercent ?? undefined,
      marginStatus: response.marginStatus ?? undefined,
      clientExplanation: response.clientExplanation ?? undefined,
      supplierAvailabilityStatus: response.supplierAvailabilityStatus ?? undefined,
      supplierAvailabilityCheckedAt: response.supplierAvailabilityCheckedAt ?? undefined,
      supplierAvailabilityNotes: response.supplierAvailabilityNotes ?? undefined,
      emailDeliveries: (response.emailDeliveries ?? []).map((delivery) => ({
        id: delivery.id,
        provider: delivery.provider,
        purpose: delivery.purpose,
        templateName: delivery.templateName,
        status: delivery.status,
        recipientEmail: delivery.recipientEmail,
        subject: delivery.subject,
        latestEventType: delivery.latestEventType ?? undefined,
        latestEventAt: delivery.latestEventAt ?? undefined,
        acceptedAt: delivery.acceptedAt ?? undefined,
        deliveredAt: delivery.deliveredAt ?? undefined,
        openedAt: delivery.openedAt ?? undefined,
        clickedAt: delivery.clickedAt ?? undefined,
        bouncedAt: delivery.bouncedAt ?? undefined,
        failedAt: delivery.failedAt ?? undefined,
        lastError: delivery.lastError ?? undefined,
      })),
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
        requestedStartDate: item.requestedStartDate ?? undefined,
        requestedEndDate: item.requestedEndDate ?? undefined,
        resolvedStartDate: item.resolvedStartDate ?? undefined,
        resolvedEndDate: item.resolvedEndDate ?? undefined,
        appliedDuration: item.appliedDuration ?? undefined,
        commercialExplanation: item.commercialExplanation ?? undefined,
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
    availabilityStatus: response.availabilityStatus ?? undefined,
    availabilityCheckedAt: response.availabilityCheckedAt ?? undefined,
    supplierConfirmationReference: response.supplierConfirmationReference ?? undefined,
    confirmedAt: response.confirmedAt ?? undefined,
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

function mapPerformanceTimelinePoint(
  response: NonNullable<CampaignResponse['performanceTimeline']>[number]): CampaignPerformanceTimelinePoint
{
  return {
    date: response.date,
    impressions: response.impressions,
    playsOrSpots: response.playsOrSpots,
    leads: response.leads,
    cplZar: response.cplZar ?? undefined,
    roas: response.roas ?? undefined,
    spendDelivered: response.spendDelivered,
  };
}

function mapCampaignPlanningTarget(
  response?: NonNullable<CampaignResponse['effectivePlanningTarget']> | null,
): CampaignPlanningTarget | undefined {
  if (!response?.label?.trim()) {
    return undefined;
  }

  return {
    label: response.label.trim(),
    city: response.city ?? undefined,
    province: response.province ?? undefined,
    latitude: response.latitude ?? undefined,
    longitude: response.longitude ?? undefined,
    source: response.source,
    precision: response.precision,
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
    orderIntent: response.orderIntent ?? 'sale',
    paymentProvider: response.paymentProvider ?? undefined,
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
      lifecycle: response.lifecycle
        ? {
            currentState: response.lifecycle.currentState,
            proposalState: response.lifecycle.proposalState,
            paymentState: response.lifecycle.paymentState,
            commercialState: response.lifecycle.commercialState,
            communicationState: response.lifecycle.communicationState,
            fulfilmentState: response.lifecycle.fulfilmentState,
            aiStudioAccessState: response.lifecycle.aiStudioAccessState,
          }
      : undefined,
    sendValidation: response.sendValidation
      ? {
          canSendRecommendation: response.sendValidation.canSendRecommendation,
          reasons: response.sendValidation.reasons ?? [],
        }
      : undefined,
    prospectDisposition: response.prospectDisposition
      ? {
          status: response.prospectDisposition.status,
          reasonCode: response.prospectDisposition.reasonCode ?? undefined,
          notes: response.prospectDisposition.notes ?? undefined,
          closedAt: response.prospectDisposition.closedAt ?? undefined,
          closedByUserId: response.prospectDisposition.closedByUserId ?? undefined,
          closedByName: response.prospectDisposition.closedByName ?? undefined,
        }
      : undefined,
    businessProcess: response.businessProcess
      ? {
          revenueAttribution: {
            agentUserId: response.businessProcess.revenueAttribution.agentUserId ?? undefined,
            agentName: response.businessProcess.revenueAttribution.agentName ?? undefined,
            geography: response.businessProcess.revenueAttribution.geography,
            packageName: response.businessProcess.revenueAttribution.packageName,
            channelSpend: response.businessProcess.revenueAttribution.channelSpend ?? {},
            paidRevenue: response.businessProcess.revenueAttribution.paidRevenue,
          },
          lostReason: {
            stage: response.businessProcess.lostReason.stage ?? undefined,
            reason: response.businessProcess.lostReason.reason ?? undefined,
            lostAt: response.businessProcess.lostReason.lostAt ?? undefined,
          },
          recommendationCommercialCheck: {
            recommendationId: response.businessProcess.recommendationCommercialCheck.recommendationId ?? undefined,
            totalCost: response.businessProcess.recommendationCommercialCheck.totalCost,
            estimatedSupplierCost: response.businessProcess.recommendationCommercialCheck.estimatedSupplierCost,
            estimatedGrossProfit: response.businessProcess.recommendationCommercialCheck.estimatedGrossProfit,
            estimatedGrossMarginPercent: response.businessProcess.recommendationCommercialCheck.estimatedGrossMarginPercent ?? undefined,
            marginStatus: response.businessProcess.recommendationCommercialCheck.marginStatus,
          },
          supplierReadiness: response.businessProcess.supplierReadiness,
          postCampaignGrowth: response.businessProcess.postCampaignGrowth,
          termsAcceptance: {
            accepted: response.businessProcess.termsAcceptance.accepted,
            acceptedAt: response.businessProcess.termsAcceptance.acceptedAt ?? undefined,
            version: response.businessProcess.termsAcceptance.version ?? undefined,
            source: response.businessProcess.termsAcceptance.source ?? undefined,
          },
          refundCancellation: {
            refundStatus: response.businessProcess.refundCancellation.refundStatus,
            refundedAmount: response.businessProcess.refundCancellation.refundedAmount,
            refundReason: response.businessProcess.refundCancellation.refundReason ?? undefined,
            refundProcessedAt: response.businessProcess.refundCancellation.refundProcessedAt ?? undefined,
            cancellationStatus: response.businessProcess.refundCancellation.cancellationStatus,
            cancellationReason: response.businessProcess.refundCancellation.cancellationReason ?? undefined,
            cancellationRequestedAt: response.businessProcess.refundCancellation.cancellationRequestedAt ?? undefined,
          },
        }
      : undefined,
    effectivePlanningTarget: mapCampaignPlanningTarget(response.effectivePlanningTarget),
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
    performanceTimeline: (response.performanceTimeline ?? []).map(mapPerformanceTimelinePoint),
    executionTasks: (response.executionTasks ?? []).map((task) => ({
      id: task.id,
      taskKey: task.taskKey,
      title: task.title,
      details: task.details ?? undefined,
      status: task.status,
      sortOrder: task.sortOrder,
      dueAt: task.dueAt ?? undefined,
      completedAt: task.completedAt ?? undefined,
    })),
    effectiveEndDate: response.effectiveEndDate ?? undefined,
    daysLeft: response.daysLeft ?? undefined,
    createdAt: response.createdAt,
  };
}

function mapCampaignPerformanceSnapshot(response: CampaignPerformanceSnapshotResponse): CampaignPerformanceSnapshot {
  return {
    campaignId: response.campaignId,
    totalBookedSpend: response.totalBookedSpend,
    totalDeliveredSpend: response.totalDeliveredSpend,
    totalImpressions: response.totalImpressions,
    totalPlaysOrSpots: response.totalPlaysOrSpots,
    totalLeads: response.totalLeads,
    averageCplZar: response.averageCplZar ?? undefined,
    averageRoas: response.averageRoas ?? undefined,
    totalSyncedClicks: response.totalSyncedClicks,
    bookingCount: response.bookingCount,
    reportCount: response.reportCount,
    spendDeliveryPercent: response.spendDeliveryPercent,
    latestReportDate: response.latestReportDate ?? undefined,
    timeline: (response.timeline ?? []).map((point) => ({
      date: point.date,
      impressions: point.impressions,
      playsOrSpots: point.playsOrSpots,
      leads: point.leads,
      cplZar: point.cplZar ?? undefined,
      roas: point.roas ?? undefined,
      spendDelivered: point.spendDelivered,
    })),
    channels: (response.channels ?? []).map((channel) => ({
      channel: channel.channel,
      label: channel.label,
      bookedSpend: channel.bookedSpend,
      deliveredSpend: channel.deliveredSpend,
      impressions: channel.impressions,
      playsOrSpots: channel.playsOrSpots,
      leads: channel.leads,
      cplZar: channel.cplZar ?? undefined,
      roas: channel.roas ?? undefined,
      syncedClicks: channel.syncedClicks,
      bookingCount: channel.bookingCount,
      reportCount: channel.reportCount,
    })),
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
  async getCampaignPerformanceById(campaignId) {
    const response = await apiRequest<CampaignPerformanceSnapshotResponse>(`/campaigns/${campaignId}/performance`);
    return mapCampaignPerformanceSnapshot(response);
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
  async getAgentCampaignPerformanceById(campaignId) {
    const response = await apiRequest<CampaignPerformanceSnapshotResponse>(`/agent/campaigns/${campaignId}/performance`);
    return mapCampaignPerformanceSnapshot(response);
  },
  async listAgentCampaigns() {
    const response = await apiRequest<CampaignResponse[]>('/agent/campaigns');
    return response.map(mapCampaign);
  },
  async getAgentInboxData() {
    const response = await apiRequest<AgentInboxResponse>('/agent/campaigns/inbox');
    return mapAgentInbox(response);
  },
  async getLeadOpsInboxData() {
    const response = await apiRequest<LeadOpsInboxResponse>('/agent/lead-ops/inbox');
    return mapLeadOpsInbox(response);
  },
  async getLeadOpsCoverageData() {
    const response = await apiRequest<LeadOpsCoverageResponse>('/agent/lead-ops/coverage');
    return mapLeadOpsCoverage(response);
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
  async createRegisteredClientProspectCampaignData(payload) {
    const response = await apiRequest<CampaignResponse>('/agent/campaigns/registered-prospects', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return mapCampaign(response);
  },
  async listInventoryData(campaignId?: string, recommendationId?: string) {
    const search = new URLSearchParams();
    if (campaignId) {
      search.set('campaignId', campaignId);
    }

    if (recommendationId) {
      search.set('recommendationId', recommendationId);
    }

    return apiRequest<InventoryRow[]>(
      search.size > 0 ? `/agent/inventory?${search.toString()}` : '/agent/inventory',
    );
  },
  async getProspectDispositionReasonsData() {
    return apiRequest<SelectOption[]>('/agent/campaigns/prospect-disposition-reasons');
  },
  async uploadAgentAssetData(campaignId, file, assetType) {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('assetType', assetType);

    const response = await fetch(`${API_BASE_URL}/agent/campaigns/${encodeURIComponent(campaignId)}/assets`, {
      method: 'POST',
      body: formData,
      credentials: 'include',
    });

    if (!response.ok) {
      await parseApiError(response);
    }

    return mapCampaignAsset(await readJsonResponse(response));
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
      credentials: 'include',
    });

    if (!response.ok) {
      await parseApiError(response);
    }

    return mapCampaignAsset(await readJsonResponse(response));
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
  async getSuburbsData(city) {
    return apiRequest<string[]>(`/public/suburbs?city=${encodeURIComponent(city)}`);
  },
  async searchLocationsData(input) {
    const searchParams = new URLSearchParams({
      query: input.query,
    });

    if (input.geographyScope) {
      searchParams.set('geographyScope', input.geographyScope);
    }

    if (input.city) {
      searchParams.set('city', input.city);
    }

    if (input.limit) {
      searchParams.set('limit', String(input.limit));
    }

    return apiRequest<LocationSuggestion[]>(`/public/locations/search?${searchParams.toString()}`);
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

const leadApi = createLeadApi({
  mapLead,
  mapLeadIntelligence,
  mapLeadIndustryContext,
  mapLeadIndustryPolicy,
  mapLeadActionInbox,
  mapGoogleSheetsLeadIntegrationStatus,
  mapGoogleSheetsLeadIntegrationRunResult,
  mapLeadSourceAutomationStatus,
  mapLeadSourceAutomationRunResult,
  mapLeadPaidMediaSyncStatus,
  mapLeadPaidMediaSyncRun,
  mapLeadAction,
});

export const advertifiedApi = {
  toAbsoluteApiUrl,
  downloadProtectedFile,
  downloadPublicFile,
  ...leadApi,
  ...publicApi,
  ...authApi,
  ...adminApi,
  ...campaignApi,
  ...agentApi,
  ...creativeApi,
  ...aiPlatformApi,

};

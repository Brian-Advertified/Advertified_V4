export type UserRole = 'client' | 'agent' | 'creative_director' | 'admin';
export type PaymentStatus = 'pending' | 'paid' | 'failed';
export type RefundStatus = 'none' | 'partial' | 'refunded';
export type PaymentProvider = 'vodapay' | 'lula';
export type CampaignStatus =
  | 'awaiting_purchase'
  | 'paid'
  | 'brief_in_progress'
  | 'brief_submitted'
  | 'planning_in_progress'
  | 'review_ready'
  | 'approved'
  | 'creative_sent_to_client_for_approval'
  | 'creative_changes_requested'
  | 'creative_approved'
  | 'booking_in_progress'
  | 'launched';
export type PlanningMode = 'ai_assisted' | 'agent_assisted' | 'hybrid';

export interface SessionUser {
  id: string;
  fullName: string;
  email: string;
  phone?: string;
  role: UserRole;
  emailVerified: boolean;
  requiresPasswordSetup?: boolean;
  identityComplete?: boolean;
  sessionToken?: string;
  businessName?: string;
  registrationNumber?: string;
  city?: string;
  province?: string;
}

export interface ConsentPreference {
  browserId: string;
  necessaryCookies: boolean;
  analyticsCookies: boolean;
  marketingCookies: boolean;
  privacyAccepted: boolean;
  hasSavedPreferences: boolean;
}

export interface LegalDocumentSection {
  title: string;
  paragraphs: string[];
}

export interface LegalDocument {
  documentKey: string;
  title: string;
  versionLabel: string;
  sections: LegalDocumentSection[];
  updatedAtUtc: string;
}

export interface PackagePreviewMapPoint {
  label: string;
  siteName: string;
  city: string;
  province: string;
  latitude: number;
  longitude: number;
  isInSelectedArea: boolean;
}

export interface PackageAreaOption {
  code: string;
  label: string;
  description: string;
}

export interface SelectOption {
  value: string;
  label: string;
}

export interface SharedFormOptions {
  businessTypes: SelectOption[];
  industries: SelectOption[];
  provinces: SelectOption[];
  revenueBands: SelectOption[];
  businessStages: SelectOption[];
  monthlyRevenueBands: SelectOption[];
  salesModels: SelectOption[];
  customerTypes: SelectOption[];
  buyingBehaviours: SelectOption[];
  decisionCycles: SelectOption[];
  growthTargets: SelectOption[];
  pricePositioning: SelectOption[];
  averageCustomerSpendBands: SelectOption[];
  urgencyLevels: SelectOption[];
  audienceClarity: SelectOption[];
  valuePropositionFocus: SelectOption[];
}

export interface PackageBand {
  id: string;
  code: string;
  name: string;
  minBudget: number;
  maxBudget: number;
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
}

export interface PackagePreview {
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
}

export interface PackagePricingSummary {
  selectedBudget: number;
  chargedAmount: number;
  aiStudioReserveAmount: number;
}

export interface PackageOrder {
  id: string;
  userId?: string;
  packageBandId: string;
  packageBandName: string;
  amount: number;
  currency: string;
  paymentProvider?: string;
  paymentStatus: PaymentStatus;
  refundStatus: RefundStatus;
  refundedAmount: number;
  gatewayFeeRetainedAmount: number;
  refundReason?: string;
  refundProcessedAt?: string;
  createdAt: string;
  paymentReference?: string;
  invoiceId?: string;
  invoiceStatus?: string;
  invoicePdfUrl?: string;
}

export interface PackageCheckoutSession {
  order: PackageOrder;
  checkoutUrl?: string;
  checkoutSessionId?: string;
  invoiceId?: string;
  invoiceStatus?: string;
  invoicePdfUrl?: string;
}

export interface CampaignBrief {
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
}

export interface RecommendationItem {
  id: string;
  sourceInventoryId?: string;
  region?: string;
  language?: string;
  showDaypart?: string;
  timeBand?: string;
  slotType?: string;
  duration?: string;
  restrictions?: string;
  confidenceScore?: number;
  selectionReasons: string[];
  policyFlags: string[];
  quantity: number;
  flighting?: string;
  itemNotes?: string;
  dimensions?: string;
  material?: string;
  illuminated?: string;
  trafficCount?: string;
  siteNumber?: string;
  startDate?: string;
  endDate?: string;
  title: string;
  channel: string;
  rationale: string;
  cost: number;
  type: 'base' | 'upsell';
}

export interface CampaignRecommendation {
  id: string;
  campaignId: string;
  proposalLabel?: string;
  proposalStrategy?: string;
  summary: string;
  rationale: string;
  clientFeedbackNotes?: string;
  manualReviewRequired: boolean;
  fallbackFlags: string[];
  buildSourceLabel?: string;
  status: 'draft' | 'sent_to_client' | 'approved';
  totalCost: number;
  items: RecommendationItem[];
}

export interface CampaignAsset {
  id: string;
  assetType: string;
  displayName: string;
  publicUrl?: string;
  contentType?: string;
  sizeBytes: number;
  createdAt: string;
}

export interface CampaignSupplierBooking {
  id: string;
  supplierOrStation: string;
  channel: string;
  bookingStatus: string;
  committedAmount: number;
  bookedAt?: string;
  liveFrom?: string;
  liveTo?: string;
  notes?: string;
  proofAsset?: CampaignAsset;
}

export interface CampaignDeliveryReport {
  id: string;
  supplierBookingId?: string;
  reportType: string;
  headline: string;
  summary?: string;
  reportedAt?: string;
  impressions?: number;
  playsOrSpots?: number;
  spendDelivered?: number;
  evidenceAsset?: CampaignAsset;
}

export interface CampaignTimelineStep {
  key: string;
  label: string;
  description: string;
  state: 'complete' | 'current' | 'upcoming';
}

export interface Campaign {
  id: string;
  userId?: string;
  clientName?: string;
  clientEmail?: string;
  businessName?: string;
  industry?: string;
  packageOrderId: string;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
  paymentProvider?: string;
  paymentStatus: PaymentStatus;
  status: CampaignStatus;
  planningMode?: PlanningMode;
  aiUnlocked: boolean;
  agentAssistanceRequested: boolean;
  assignedAgentUserId?: string;
  assignedAgentName?: string;
  assignedAt?: string;
  isAssignedToCurrentUser?: boolean;
  isUnassigned?: boolean;
  campaignName: string;
  nextAction: string;
  timeline: CampaignTimelineStep[];
  brief?: CampaignBrief;
  recommendations: CampaignRecommendation[];
  recommendation?: CampaignRecommendation;
  recommendationPdfUrl?: string;
  creativeSystems: CampaignCreativeSystemRecord[];
  latestCreativeSystem?: CampaignCreativeSystemRecord;
  assets: CampaignAsset[];
  supplierBookings: CampaignSupplierBooking[];
  deliveryReports: CampaignDeliveryReport[];
  effectiveEndDate?: string;
  daysLeft?: number;
  createdAt: string;
}

export interface CampaignConversationListItem {
  campaignId: string;
  conversationId?: string;
  campaignName: string;
  campaignStatus: string;
  clientName: string;
  clientEmail: string;
  packageBandName: string;
  assignedAgentName?: string;
  lastMessagePreview?: string;
  lastMessageSenderRole?: 'client' | 'agent';
  lastMessageAt?: string;
  unreadCount: number;
  hasMessages: boolean;
}

export interface CampaignConversationMessage {
  id: string;
  senderUserId: string;
  senderRole: 'client' | 'agent';
  senderName: string;
  body: string;
  createdAt: string;
  isRead: boolean;
}

export interface CampaignConversationThread {
  campaignId: string;
  conversationId?: string;
  campaignName: string;
  campaignStatus: string;
  clientName: string;
  clientEmail: string;
  packageBandName: string;
  assignedAgentName?: string;
  unreadCount: number;
  canSend: boolean;
  messages: CampaignConversationMessage[];
}

export interface NotificationSummaryItem {
  id: string;
  title: string;
  description: string;
  href: string;
  tone: 'info' | 'success' | 'warning';
  isRead: boolean;
}

export interface NotificationSummary {
  unreadCount: number;
  items: NotificationSummaryItem[];
}

export interface CreativeCampaignSummary {
  brand: string;
  product: string;
  audience: string;
  objective: string;
  tone: string;
  channels: string[];
  cta: string;
  constraints: string[];
  assumptions: string[];
}

export interface CreativeMasterIdea {
  coreConcept: string;
  centralMessage: string;
  emotionalAngle: string;
  valueProposition: string;
  platformIdea: string;
}

export interface CreativeScene {
  order: number;
  title: string;
  purpose: string;
  visual: string;
  copyOrDialogue: string;
  onScreenText?: string;
  duration?: string;
}

export interface CreativeNarrative {
  hook: string;
  setup: string;
  tensionOrProblem: string;
  solution: string;
  payoff: string;
  cta: string;
  scenes: CreativeScene[];
}

export interface CreativeChannelAdaptation {
  channel: string;
  format: string;
  headlineOrHook: string;
  primaryCopy: string;
  cta: string;
  visualDirection: string;
  voiceoverOrAudio?: string;
  recommendedDirection: string;
  adapterPrompt: string;
  sections: CreativeChannelSection[];
  versions: CreativeChannelVersion[];
  productionAssets: string[];
}

export interface CreativeChannelSection {
  label: string;
  content: string;
}

export interface CreativeChannelVersion {
  label: string;
  intent: string;
  headlineOrHook: string;
  primaryCopy: string;
  cta: string;
}

export interface CreativeVisualDirection {
  lookAndFeel: string;
  typography: string;
  colorDirection: string;
  composition: string;
  imageGenerationPrompts: string[];
}

export interface CreativeSystem {
  campaignSummary: CreativeCampaignSummary;
  masterIdea: CreativeMasterIdea;
  campaignLineOptions: string[];
  storyboard: CreativeNarrative;
  channelAdaptations: CreativeChannelAdaptation[];
  visualDirection: CreativeVisualDirection;
  audioVoiceNotes: string[];
  productionNotes: string[];
  optionalVariations: string[];
}

export interface CampaignCreativeSystemRecord {
  id: string;
  prompt: string;
  iterationLabel?: string;
  createdAt: string;
  output: CreativeSystem;
}

export interface AgentInboxItem {
  id: string;
  userId?: string;
  campaignName: string;
  clientName: string;
  clientEmail: string;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
  paymentStatus: PaymentStatus | string;
  status: string;
  planningMode?: PlanningMode;
  queueStage: 'newly_paid' | 'brief_waiting' | 'planning_ready' | 'agent_review' | 'ready_to_send' | 'waiting_on_client' | 'completed' | 'watching';
  queueLabel: string;
  assignedAgentUserId?: string;
  assignedAgentName?: string;
  assignedAt?: string;
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
}

export interface AgentInbox {
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
  items: AgentInboxItem[];
}

export interface AgentSaleItem {
  campaignId: string;
  packageOrderId: string;
  campaignName: string;
  clientName: string;
  clientEmail: string;
  packageBandName: string;
  selectedBudget: number;
  chargedAmount: number;
  paymentProvider: string;
  paymentReference?: string;
  convertedFromProspect: boolean;
  purchasedAt: string;
  createdAt: string;
}

export interface AgentSales {
  totalSalesCount: number;
  convertedProspectSalesCount: number;
  totalChargedAmount: number;
  totalSelectedBudget: number;
  items: AgentSaleItem[];
}

export interface Lead {
  id: number;
  name: string;
  website?: string;
  location: string;
  category: string;
  source: string;
  sourceReference?: string;
  lastDiscoveredAt?: string;
  createdAt: string;
}

export interface LeadSignal {
  id: number;
  leadId: number;
  hasPromo: boolean;
  hasMetaAds: boolean;
  websiteUpdatedRecently: boolean;
  createdAt: string;
}

export interface LeadScore {
  leadId: number;
  score: number;
  intentLevel: 'Low' | 'Medium' | 'High' | string;
}

export interface LeadInsightSnapshot {
  id: number;
  leadId: number;
  signalId?: number;
  trendSummary: string;
  scoreSnapshot: number;
  intentLevelSnapshot: 'Low' | 'Medium' | 'High' | string;
  text: string;
  createdAt: string;
}

export interface LeadAction {
  id: number;
  leadId: number;
  leadInsightId?: number;
  actionType: string;
  title: string;
  description: string;
  status: string;
  priority: string;
  assignedAgentUserId?: string;
  assignedAgentName?: string;
  assignedAt?: string;
  isAssignedToCurrentUser: boolean;
  isUnassigned: boolean;
  createdAt: string;
  completedAt?: string;
}

export interface LeadActionInboxItem {
  actionId: number;
  leadId: number;
  leadName: string;
  leadLocation: string;
  leadCategory: string;
  leadSource: string;
  action: LeadAction;
}

export interface LeadActionInbox {
  totalOpenActions: number;
  assignedToMeCount: number;
  unassignedCount: number;
  highPriorityCount: number;
  items: LeadActionInboxItem[];
}

export interface LeadInteraction {
  id: number;
  leadId: number;
  leadActionId?: number;
  interactionType: string;
  notes: string;
  createdAt: string;
}

export interface LeadIntelligence {
  lead: Lead;
  latestSignal?: LeadSignal;
  score: LeadScore;
  insight: string;
  trendSummary: string;
  signalHistory: LeadSignal[];
  insightHistory: LeadInsightSnapshot[];
  recommendedActions: LeadAction[];
  interactionHistory: LeadInteraction[];
}

export interface AdminUser {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  role: UserRole;
  accountStatus: string;
  isSaCitizen: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  assignedAreaCodes: string[];
  assignedAreaLabels: string[];
  createdAt: string;
  updatedAt: string;
}

export interface AdminAuditEntry {
  id: string;
  source: string;
  actorName: string;
  actorRole: string;
  eventType: string;
  entityType?: string;
  entityLabel?: string;
  context: string;
  statusLabel?: string;
  createdAt: string;
}

export interface AdminIntegrationStatus {
  paymentRequestAuditCount: number;
  paymentWebhookAuditCount: number;
  lastPaymentRequestAt?: string;
  lastPaymentWebhookAt?: string;
}

export interface AdminCampaignOperationsItem {
  campaignId: string;
  packageOrderId: string;
  campaignName: string;
  campaignStatus: CampaignStatus | string;
  clientName: string;
  clientEmail: string;
  packageBandName: string;
  selectedBudget: number;
  chargedTotal: number;
  paymentStatus: PaymentStatus | string;
  refundStatus: RefundStatus | string;
  refundedAmount: number;
  remainingCollectedAmount: number;
  suggestedRefundAmount: number;
  maxManualRefundAmount: number;
  gatewayFeeRetainedAmount: number;
  refundPolicyStage: string;
  refundPolicyLabel: string;
  refundPolicySummary: string;
  refundReason?: string;
  refundProcessedAt?: string;
  isPaused: boolean;
  pauseReason?: string;
  pausedAt?: string;
  totalPausedDays: number;
  startDate?: string;
  endDate?: string;
  effectiveEndDate?: string;
  daysLeft?: number;
  canPause: boolean;
  canUnpause: boolean;
  canProcessRefund: boolean;
}

export interface AdminPackageOrder {
  orderId: string;
  userId?: string;
  clientName: string;
  clientEmail: string;
  clientPhone: string;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
  chargedAmount: number;
  currency: string;
  paymentProvider: string;
  paymentStatus: PaymentStatus | string;
  paymentReference?: string;
  createdAt: string;
  purchasedAt?: string;
  campaignId?: string;
  campaignName?: string;
  invoiceId?: string;
  invoiceStatus?: string;
  invoicePdfUrl?: string;
  supportingDocumentPdfUrl?: string;
  supportingDocumentFileName?: string;
  supportingDocumentUploadedAt?: string;
  canUpdateLulaStatus: boolean;
}

export interface AdminSummary {
  activeOutlets: number;
  weakOutlets: number;
  sourceDocuments: number;
  fallbackRatePercent: number;
}

export interface AdminAlert {
  title: string;
  context: string;
  severity: string;
  owner: string;
}

export interface AdminOutlet {
  code: string;
  name: string;
  mediaType: string;
  coverageType: string;
  geographyLabel: string;
  catalogHealth: string;
  hasPricing: boolean;
  packageCount: number;
  slotRateCount: number;
  minPackagePrice?: number;
  minSlotRate?: number;
  languageDisplay?: string;
  broadcastFrequency?: string;
}

export interface AdminOutletDetail {
  id: string;
  code: string;
  name: string;
  mediaType: string;
  coverageType: string;
  catalogHealth: string;
  operatorName?: string;
  isNational: boolean;
  hasPricing: boolean;
  languageNotes?: string;
  targetAudience?: string;
  broadcastFrequency?: string;
  primaryLanguages: string[];
  provinceCodes: string[];
  cityLabels: string[];
  audienceKeywords: string[];
  packageCount: number;
  slotRateCount: number;
  minPackagePrice?: number;
  minSlotRate?: number;
}

export interface AdminImportDocument {
  sourceFile: string;
  channel: string;
  supplierOrStation?: string;
  documentTitle?: string;
  notes?: string;
  pageCount?: number;
  importedAt: string;
}

export interface AdminHealth {
  strongCount: number;
  mixedCount: number;
  weakUnpricedCount: number;
  weakNoInventoryCount: number;
}

export interface AdminHealthIssue {
  outletCode: string;
  outletName: string;
  issue: string;
  impact: string;
  suggestedFix: string;
}

export interface AdminAreaMapping {
  code: string;
  label: string;
  description: string;
  mappingCount: number;
}

export interface AdminGeographyMapping {
  id: string;
  province?: string;
  city?: string;
  stationOrChannelName?: string;
}

export interface AdminGeographyDetail {
  id: string;
  code: string;
  label: string;
  description: string;
  fallbackLocations: string[];
  sortOrder: number;
  isActive: boolean;
  mappings: AdminGeographyMapping[];
}

export interface AdminPackageSetting {
  id: string;
  code: string;
  name: string;
  minBudget: number;
  maxBudget: number;
  sortOrder: number;
  isActive: boolean;
  description: string;
  recommendedSpend?: number;
  isRecommended: boolean;
  packagePurpose: string;
  audienceFit: string;
  quickBenefit: string;
  includeRadio: string;
  includeTv: string;
  leadTime: string;
  benefits: string[];
}

export interface AdminPricingSettings {
  aiStudioReservePercent: number;
  oohMarkupPercent: number;
  radioMarkupPercent: number;
  tvMarkupPercent: number;
}

export interface AdminEnginePolicy {
  packageCode: string;
  budgetFloor: number;
  minimumNationalRadioCandidates: number;
  requireNationalCapableRadio: boolean;
  requirePremiumNationalRadio: boolean;
  nationalRadioBonus: number;
  nonNationalRadioPenalty: number;
  regionalRadioPenalty: number;
}

export interface AdminCreateGeographyInput {
  code: string;
  label: string;
  description: string;
  fallbackLocations: string[];
  sortOrder: number;
  isActive: boolean;
}

export interface AdminUpdateGeographyInput {
  code: string;
  label: string;
  description: string;
  fallbackLocations: string[];
  sortOrder: number;
  isActive: boolean;
}

export interface AdminUpsertGeographyMappingInput {
  province?: string;
  city?: string;
  stationOrChannelName?: string;
}

export interface AdminUpdateEnginePolicyInput {
  budgetFloor: number;
  minimumNationalRadioCandidates: number;
  requireNationalCapableRadio: boolean;
  requirePremiumNationalRadio: boolean;
  nationalRadioBonus: number;
  nonNationalRadioPenalty: number;
  regionalRadioPenalty: number;
}

export interface AdminPreviewRule {
  packageCode: string;
  packageName: string;
  tierCode: string;
  tierLabel: string;
  typicalInclusions: string[];
  indicativeMix: string[];
}

export interface AdminMonitoring {
  totalCampaigns: number;
  planningReadyCount: number;
  waitingOnClientCount: number;
  inventoryRows: number;
  activeAreaCount: number;
  recommendationCount: number;
  retryAlertThreshold: number;
  aiJobAlertCount: number;
  aiCreativeJobAlertCount: number;
  aiAssetJobAlertCount: number;
  aiCostCapRejectionCount: number;
  creativeQueueBacklogCount: number;
  assetQueueBacklogCount: number;
  creativeDeadLetterCount: number;
  publishSuccessCount: number;
  publishFailureCount: number;
  metricsSyncLagMinutes: number;
  aiJobAlerts: AdminAiJobAlert[];
}

export interface AdminAiJobAlert {
  pipeline: string;
  jobId: string;
  campaignId: string;
  status: string;
  retryAttemptCount: number;
  lastFailure?: string;
  updatedAt: string;
  alertReason: string;
}

export interface AdminDashboard {
  summary: AdminSummary;
  alerts: AdminAlert[];
  outlets: AdminOutlet[];
  recentImports: AdminImportDocument[];
  health: AdminHealth;
  healthIssues: AdminHealthIssue[];
  areas: AdminAreaMapping[];
  packageSettings: AdminPackageSetting[];
  pricingSettings: AdminPricingSettings;
  enginePolicies: AdminEnginePolicy[];
  previewRules: AdminPreviewRule[];
  monitoring: AdminMonitoring;
  users: AdminUser[];
  auditEntries: AdminAuditEntry[];
  integrations: AdminIntegrationStatus;
}

export interface AdminUpsertPackageSettingInput {
  code: string;
  name: string;
  minBudget: number;
  maxBudget: number;
  sortOrder: number;
  isActive: boolean;
  description: string;
  audienceFit: string;
  quickBenefit: string;
  packagePurpose: string;
  includeRadio: string;
  includeTv: string;
  leadTime: string;
  recommendedSpend?: number;
  isRecommended: boolean;
  benefits: string[];
}

export interface AdminUpdatePricingSettingsInput {
  aiStudioReservePercent: number;
  oohMarkupPercent: number;
  radioMarkupPercent: number;
  tvMarkupPercent: number;
}

export interface AdminCreateOutletInput {
  code: string;
  name: string;
  mediaType: string;
  coverageType: string;
  catalogHealth: string;
  operatorName?: string;
  isNational: boolean;
  hasPricing: boolean;
  languageNotes?: string;
  targetAudience?: string;
  broadcastFrequency?: string;
  primaryLanguages: string[];
  provinceCodes: string[];
  cityLabels: string[];
  audienceKeywords: string[];
}

export interface AdminRateCardUploadInput {
  channel: string;
  supplierOrStation?: string;
  documentTitle?: string;
  notes?: string;
  file: File;
}

export interface AdminRateCardUpdateInput {
  channel: string;
  supplierOrStation?: string;
  documentTitle?: string;
  notes?: string;
}

export interface AdminPreviewRuleUpdateInput {
  tierLabel: string;
  typicalInclusions: string[];
  indicativeMix: string[];
}

export interface AdminOutletPricingPackage {
  id: string;
  packageName: string;
  packageType?: string;
  exposureCount?: number;
  monthlyExposureCount?: number;
  valueZar?: number;
  discountZar?: number;
  savingZar?: number;
  investmentZar?: number;
  costPerMonthZar?: number;
  durationMonths?: number;
  durationWeeks?: number;
  notes?: string;
  sourceName?: string;
  sourceDate?: string;
  isActive: boolean;
}

export interface AdminOutletSlotRate {
  id: string;
  dayGroup: string;
  startTime: string;
  endTime: string;
  adDurationSeconds: number;
  rateZar: number;
  rateType: string;
  sourceName?: string;
  sourceDate?: string;
  isActive: boolean;
}

export interface AdminOutletPricing {
  outletCode: string;
  outletName: string;
  mediaType: string;
  coverageType: string;
  hasPricing: boolean;
  packages: AdminOutletPricingPackage[];
  slotRates: AdminOutletSlotRate[];
}

export interface AdminUpsertOutletPricingPackageInput {
  packageName: string;
  packageType?: string;
  exposureCount?: number;
  monthlyExposureCount?: number;
  valueZar?: number;
  discountZar?: number;
  savingZar?: number;
  investmentZar?: number;
  costPerMonthZar?: number;
  durationMonths?: number;
  durationWeeks?: number;
  notes?: string;
  sourceName?: string;
  sourceDate?: string;
  isActive: boolean;
}

export interface AdminUpsertOutletSlotRateInput {
  dayGroup: string;
  startTime: string;
  endTime: string;
  adDurationSeconds: number;
  rateZar: number;
  rateType: string;
  sourceName?: string;
  sourceDate?: string;
  isActive: boolean;
}

export interface AdminUpdateOutletInput {
  code: string;
  name: string;
  mediaType: string;
  coverageType: string;
  catalogHealth: string;
  operatorName?: string;
  isNational: boolean;
  hasPricing: boolean;
  languageNotes?: string;
  targetAudience?: string;
  broadcastFrequency?: string;
  primaryLanguages: string[];
  provinceCodes: string[];
  cityLabels: string[];
  audienceKeywords: string[];
}

export interface AdminCreateUserInput {
  fullName: string;
  email: string;
  phone: string;
  password: string;
  role: UserRole;
  accountStatus: string;
  isSaCitizen: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  assignedAreaCodes: string[];
}

export interface AdminUpdateUserInput {
  fullName: string;
  email: string;
  phone: string;
  password?: string;
  role: UserRole;
  accountStatus: string;
  isSaCitizen: boolean;
  emailVerified: boolean;
  phoneVerified: boolean;
  assignedAreaCodes: string[];
}

export interface InventoryRow {
  id: string;
  type: 'radio' | 'ooh' | 'digital' | 'tv';
  station: string;
  region: string;
  language: string;
  showDaypart: string;
  timeBand: string;
  slotType: string;
  duration: string;
  rate: number;
  restrictions: string;
}

export interface SelectedPlanInventoryItem extends InventoryRow {
  quantity: number;
  flighting?: string;
  notes?: string;
  startDate?: string;
  endDate?: string;
}

export interface RegistrationInput {
  fullName: string;
  email: string;
  phone: string;
  isSouthAfricanCitizen: boolean;
  password: string;
  confirmPassword: string;
  businessName: string;
  businessType: string;
  registrationNumber: string;
  vatNumber?: string;
  industry: string;
  annualRevenueBand: string;
  tradingAsName?: string;
  streetAddress: string;
  city: string;
  province: string;
  saIdNumber?: string;
  passportNumber?: string;
  passportCountryIso2?: string;
  passportIssueDate?: string;
  passportValidUntil?: string;
  nextPath?: string;
  acceptTerms: boolean;
  acceptPopia: boolean;
}

export interface LoginInput {
  email: string;
  password: string;
}

export interface RegistrationResult {
  userId: string;
  email: string;
  emailVerificationRequired: boolean;
  accountStatus: string;
}

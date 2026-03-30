export type UserRole = 'client' | 'agent' | 'creative_director' | 'admin';
export type PaymentStatus = 'pending' | 'paid' | 'failed';
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
  | 'launched';
export type PlanningMode = 'ai_assisted' | 'agent_assisted' | 'hybrid';

export interface SessionUser {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  emailVerified: boolean;
  sessionToken?: string;
  businessName?: string;
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

export interface PackageOrder {
  id: string;
  userId: string;
  packageBandId: string;
  packageBandName: string;
  amount: number;
  currency: string;
  paymentProvider: string;
  paymentStatus: PaymentStatus;
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

export interface CampaignTimelineStep {
  key: string;
  label: string;
  description: string;
  state: 'complete' | 'current' | 'upcoming';
}

export interface Campaign {
  id: string;
  userId: string;
  clientName?: string;
  clientEmail?: string;
  businessName?: string;
  industry?: string;
  packageOrderId: string;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
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

export interface AgentInboxItem {
  id: string;
  userId: string;
  campaignName: string;
  clientName: string;
  clientEmail: string;
  packageBandName: string;
  selectedBudget: number;
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

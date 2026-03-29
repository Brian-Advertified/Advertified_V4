export type UserRole = 'client' | 'agent' | 'admin';
export type PaymentStatus = 'pending' | 'paid' | 'failed';
export type PaymentProvider = 'vodapay' | 'lula';
export type CampaignStatus =
  | 'awaiting_purchase'
  | 'paid'
  | 'brief_in_progress'
  | 'brief_submitted'
  | 'planning_in_progress'
  | 'review_ready'
  | 'approved';
export type PlanningMode = 'ai_assisted' | 'agent_assisted' | 'hybrid';

export interface SessionUser {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  emailVerified: boolean;
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
  recommendation?: CampaignRecommendation;
  createdAt: string;
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

export interface InventoryRow {
  id: string;
  type: 'radio' | 'ooh' | 'digital';
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

import type {
  AgentInbox,
  AgentInboxItem,
  Campaign,
  CampaignBrief,
  CampaignRecommendation,
  InventoryRow,
  LoginInput,
  PackageBand,
  PackageAreaOption,
  PackageCheckoutSession,
  AdminUser,
  AdminAuditEntry,
  AdminIntegrationStatus,
  AdminDashboard,
  AdminCreateOutletInput,
  AdminOutletDetail,
  AdminOutletPricing,
  AdminRateCardUploadInput,
  AdminPreviewRuleUpdateInput,
  AdminUpsertOutletPricingPackageInput,
  AdminUpsertOutletSlotRateInput,
  AdminUpdateOutletInput,
  PackagePreview,
  PackagePreviewMapPoint,
  PackageOrder,
  PaymentProvider,
  PlanningMode,
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
  role: string;
  accountStatus: string;
  emailVerified: boolean;
};

type MeResponse = {
  userId: string;
  fullName: string;
  email: string;
  phone: string;
  role: string;
  accountStatus: string;
  emailVerified: boolean;
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
};

type PackageAreaOptionResponse = {
  code: string;
  label: string;
  description: string;
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
  createdAt: string;
};

type AgentInboxItemResponse = {
  id: string;
  userId: string;
  campaignName: string;
  clientName: string;
  clientEmail: string;
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

  if (role === 'admin') {
    return 'admin';
  }

  return 'client';
}

function mapSessionUser(response: LoginResponse | MeResponse): SessionUser {
  return {
    id: response.userId,
    fullName: response.fullName,
    email: response.email,
    role: normalizeRole(response.role),
    emailVerified: response.emailVerified,
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
  };
}

function mapPackageAreaOption(response: PackageAreaOptionResponse): PackageAreaOption {
  return {
    code: response.code,
    label: response.label,
    description: response.description,
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
    createdAt: response.createdAt,
    paymentReference: response.paymentReference ?? undefined,
    invoiceId: response.invoiceId ?? undefined,
    invoiceStatus: response.invoiceStatus ?? undefined,
    invoicePdfUrl: response.invoicePdfUrl ?? undefined,
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
      items: response.items.map((item) => ({
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
    createdAt: response.createdAt,
  };
}

function mapAgentInboxItem(response: AgentInboxItemResponse): AgentInboxItem {
  return {
    id: response.id,
    userId: response.userId,
    campaignName: response.campaignName,
    clientName: response.clientName,
    clientEmail: response.clientEmail,
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

async function apiRequest<T>(path: string, options: RequestInit = {}, userId?: string): Promise<T> {
  const headers = new Headers(options.headers);

  if (!headers.has('Content-Type') && options.body) {
    headers.set('Content-Type', 'application/json');
  }

  const sessionUserId = userId ?? getStoredSession()?.id;
  if (sessionUserId) {
    headers.set('X-User-Id', sessionUserId);
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

    return this.getMe(response.userId).catch(() => mapSessionUser(response));
  },

  async verifyEmail(token: string) {
    const response = await apiRequest<LoginResponse>('/auth/verify-email', {
      method: 'POST',
      body: JSON.stringify({ token }),
    });

    return this.getMe(response.userId).catch(() => mapSessionUser(response));
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

  async getAdminDashboard() {
    return apiRequest<AdminDashboardResponse>('/admin/dashboard');
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

    const sessionUserId = getStoredSession()?.id;
    const headers = new Headers();
    if (sessionUserId) {
      headers.set('X-User-Id', sessionUserId);
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

  async getPackagePreview(packageBandId: string, budget: number, selectedArea: string) {
    const response = await apiRequest<PackagePreviewResponse>(
      `/packages/preview?packageBandId=${encodeURIComponent(packageBandId)}&budget=${encodeURIComponent(String(budget))}&selectedArea=${encodeURIComponent(selectedArea)}`,
    );

    return mapPackagePreview(response);
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
        createdAt: new Date().toISOString(),
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

  async getAgentCampaign(campaignId: string) {
    const response = await apiRequest<CampaignResponse>(`/agent/campaigns/${campaignId}`);
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

  async getAgentCampaigns() {
    const response = await apiRequest<CampaignResponse[]>('/agent/campaigns');
    return response.map(mapCampaign);
  },

  async getAgentInbox() {
    const response = await apiRequest<AgentInboxResponse>('/agent/campaigns/inbox');
    return mapAgentInbox(response);
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

  async sendRecommendationToClient(campaignId: string) {
    await apiRequest(`/agent/campaigns/${campaignId}/send-to-client`, {
      method: 'POST',
      body: JSON.stringify({ message: 'Recommendation sent to client.' }),
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

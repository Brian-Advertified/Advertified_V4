import type {
  ConsentPreference,
  LegalDocument,
  PackageAreaOption,
  PackageBand,
  PackageCheckoutSession,
  PackageOrder,
  PackagePreview,
  PackagePricingSummary,
  PaymentProvider,
} from '../types/domain';

type PublicApiDependencies = {
  getConsentPreferenceData: (browserId: string) => Promise<ConsentPreference>;
  saveConsentPreferenceData: (input: {
    browserId: string;
    analyticsCookies: boolean;
    marketingCookies: boolean;
    privacyAccepted: boolean;
  }) => Promise<ConsentPreference>;
  getPackagesData: () => Promise<PackageBand[]>;
  getPackageAreasData: () => Promise<PackageAreaOption[]>;
  getPackagePreviewData: (packageBandId: string, budget: number, selectedArea: string) => Promise<PackagePreview>;
  getPackagePricingSummaryData: (selectedBudget: number) => Promise<PackagePricingSummary>;
  createOrderData: (payload: {
    packageBandId: string;
    amount: number;
    currency: string;
    paymentProvider: PaymentProvider;
  }) => Promise<{
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
  }>;
  initiateOrderCheckoutData: (orderId: string, payload: {
    paymentProvider: PaymentProvider;
    recommendationId?: string;
  }) => Promise<{
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
  }>;
  listOrdersData: () => Promise<PackageOrder[]>;
  getOrderData: (orderId: string) => Promise<PackageOrder>;
  captureVodaPayCallbackData: (orderId: string, queryParameters: Record<string, string>) => Promise<{ message: string }>;
  submitPartnerEnquiryData: (payload: {
    fullName: string;
    companyName: string;
    email: string;
    phone?: string;
    partnerType: string;
    inventorySummary?: string;
    message: string;
  }) => Promise<{ message: string }>;
  submitProspectQuestionnaireData: (payload: {
    fullName: string;
    email: string;
    phone: string;
    businessName?: string;
    industry?: string;
    packageBandId: string;
    campaignName?: string;
    brief: Record<string, unknown>;
  }) => Promise<{
    campaignId: string;
    campaignName: string;
    message: string;
  }>;
  getLegalDocumentData: (documentKey: string) => Promise<LegalDocument>;
};

function toCheckoutSession(
  response: {
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
  },
  userId: string,
  paymentProvider: PaymentProvider,
  packageBandName?: string,
) {
  return {
    order: {
      id: response.packageOrderId,
      userId,
      packageBandId: response.packageBandId,
      packageBandName: packageBandName ?? 'Package',
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
}

export function createPublicApi({
  getConsentPreferenceData,
  saveConsentPreferenceData,
  getPackagesData,
  getPackageAreasData,
  getPackagePreviewData,
  getPackagePricingSummaryData,
  createOrderData,
  initiateOrderCheckoutData,
  listOrdersData,
  getOrderData,
  captureVodaPayCallbackData,
  submitPartnerEnquiryData,
  submitProspectQuestionnaireData,
  getLegalDocumentData,
}: PublicApiDependencies) {
  return {
    async getConsentPreferences(browserId: string) {
      return getConsentPreferenceData(browserId);
    },

    async saveConsentPreferences(input: {
      browserId: string;
      analyticsCookies: boolean;
      marketingCookies: boolean;
      privacyAccepted: boolean;
    }) {
      return saveConsentPreferenceData(input);
    },

    async getPackages() {
      return getPackagesData();
    },

    async getPackageAreas() {
      return getPackageAreasData();
    },

    async getPackagePreview(packageBandId: string, budget: number, selectedArea: string) {
      return getPackagePreviewData(packageBandId, budget, selectedArea);
    },

    async getPackagePricingSummary(selectedBudget: number) {
      return getPackagePricingSummaryData(selectedBudget);
    },

    async createOrder(userId: string, packageBandId: string, amount: number, paymentProvider: PaymentProvider) {
      const response = await createOrderData({
        packageBandId,
        amount,
        currency: 'ZAR',
        paymentProvider,
      });

      const packages = await getPackagesData();
      const selectedBand = packages.find((item) => item.id === response.packageBandId);
      return toCheckoutSession(response, userId, paymentProvider, selectedBand?.name);
    },

    async initiateOrderCheckout(userId: string, orderId: string, paymentProvider: PaymentProvider, recommendationId?: string) {
      const response = await initiateOrderCheckoutData(orderId, {
        paymentProvider,
        recommendationId,
      });

      const packages = await getPackagesData();
      const selectedBand = packages.find((item) => item.id === response.packageBandId);
      return toCheckoutSession(response, userId, paymentProvider, selectedBand?.name);
    },

    async getOrders(_userId: string) {
      return listOrdersData();
    },

    async getOrder(orderId: string, _userId: string) {
      return getOrderData(orderId);
    },

    async captureVodaPayCallback(orderId: string, queryParameters: Record<string, string>) {
      return captureVodaPayCallbackData(orderId, queryParameters);
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
      return submitPartnerEnquiryData(payload);
    },

    async getLegalDocument(documentKey: string) {
      return getLegalDocumentData(documentKey);
    },

    async submitProspectQuestionnaire(payload: {
      fullName: string;
      email: string;
      phone: string;
      businessName?: string;
      industry?: string;
      packageBandId: string;
      campaignName?: string;
      brief: Record<string, unknown>;
    }) {
      return submitProspectQuestionnaireData(payload);
    },
  };
}

import type {
  AdminAuditEntry,
  AdminCampaignOperationsItem,
  AdminCreateGeographyInput,
  AdminCreateOutletInput,
  AdminCreateUserInput,
  AdminDashboard,
  AdminGeographyDetail,
  AdminIntegrationStatus,
  AdminOutletDetail,
  AdminOutletPricing,
  AdminPackageOrder,
  AdminPreviewRuleUpdateInput,
  AdminRateCardUpdateInput,
  AdminRateCardUploadInput,
  AdminUpdateEnginePolicyInput,
  AdminUpdateGeographyInput,
  AdminUpdateOutletInput,
  AdminUpdatePricingSettingsInput,
  AdminUpdateUserInput,
  AdminUpsertGeographyMappingInput,
  AdminUpsertOutletPricingPackageInput,
  AdminUpsertOutletSlotRateInput,
  AdminUpsertPackageSettingInput,
  AdminUser,
} from '../types/domain';
import { API_BASE_URL, apiRequest, getAuthHeaders, parseApiError } from './apiClient';

type AdminDashboardResponse = AdminDashboard;

export type AdminPackageOrderResponse = {
  orderId: string;
  userId: string;
  clientName: string;
  clientEmail: string;
  clientPhone?: string | null;
  packageBandId: string;
  packageBandName: string;
  selectedBudget: number;
  chargedAmount: number;
  currency: string;
  paymentProvider: string;
  paymentStatus: 'pending' | 'paid' | 'failed';
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

type AdminApiDependencies = {
  mapAdminPackageOrder: (response: AdminPackageOrderResponse) => AdminPackageOrder;
};

export function createAdminApi({ mapAdminPackageOrder }: AdminApiDependencies) {
  return {
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

      const response = await fetch(`${API_BASE_URL}/admin/imports/rate-card`, {
        method: 'POST',
        body: formData,
        headers: getAuthHeaders(),
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
  };
}

import type {
  AgentInbox,
  AgentSales,
  Campaign,
  CampaignPerformanceSnapshot,
  CampaignBrief,
  CampaignAsset,
  CampaignConversationListItem,
  CampaignConversationThread,
  InventoryRow,
  PlanningMode,
  SelectedPlanInventoryItem,
  SelectOption,
} from '../types/domain';

type AgentApiDependencies = {
  getAgentCampaignById: (campaignId: string) => Promise<Campaign>;
  getAgentCampaignPerformanceById: (campaignId: string) => Promise<CampaignPerformanceSnapshot>;
  listAgentCampaigns: () => Promise<Campaign[]>;
  getAgentInboxData: () => Promise<AgentInbox>;
  getAgentSalesData: () => Promise<AgentSales>;
  getAgentMessageInboxData: () => Promise<CampaignConversationListItem[]>;
  getAgentMessageThreadData: (campaignId: string) => Promise<CampaignConversationThread>;
  sendAgentMessageData: (campaignId: string, body: string) => Promise<CampaignConversationThread>;
  initializeRecommendationData: (campaignId: string, payload: unknown) => Promise<{ campaignId: string }>;
  generateRecommendationData: (campaignId: string, payload?: unknown) => Promise<{ campaignId: string }>;
  interpretBriefData: (campaignId: string, payload: unknown) => Promise<{
    objective: string;
    audience: string;
    scope: string;
    geography: string;
    tone: string;
    campaignName: string;
    channels: string[];
    summary: string;
  }>;
  createProspectCampaignData: (payload: {
    fullName: string;
    email: string;
    phone: string;
    packageBandId: string;
    campaignName?: string;
  }) => Promise<Campaign>;
  createRegisteredClientProspectCampaignData: (payload: {
    email: string;
    packageBandId: string;
    campaignName?: string;
  }) => Promise<Campaign>;
  listInventoryData: (campaignId?: string) => Promise<InventoryRow[]>;
  getProspectDispositionReasonsData: () => Promise<SelectOption[]>;
  uploadAgentAssetData: (campaignId: string, file: File, assetType: string) => Promise<CampaignAsset>;
  postJson: (path: string, body: unknown) => Promise<void>;
  putJson: (path: string, body: unknown) => Promise<void>;
  deleteRequest: (path: string) => Promise<void>;
};

export function createAgentApi({
  getAgentCampaignById,
  getAgentCampaignPerformanceById,
  listAgentCampaigns,
  getAgentInboxData,
  getAgentSalesData,
  getAgentMessageInboxData,
  getAgentMessageThreadData,
  sendAgentMessageData,
  initializeRecommendationData,
  generateRecommendationData,
  interpretBriefData,
  createProspectCampaignData,
  createRegisteredClientProspectCampaignData,
  listInventoryData,
  getProspectDispositionReasonsData,
  uploadAgentAssetData,
  postJson,
  putJson,
  deleteRequest,
}: AgentApiDependencies) {
  return {
    async getAgentCampaign(campaignId: string) {
      return getAgentCampaignById(campaignId);
    },

    async getAgentCampaignPerformance(campaignId: string) {
      return getAgentCampaignPerformanceById(campaignId);
    },

    async createAgentProspectCampaign(payload: {
      fullName: string;
      email: string;
      phone: string;
      packageBandId: string;
      campaignName?: string;
    }) {
      return createProspectCampaignData(payload);
    },

    async createAgentRegisteredClientProspectCampaign(payload: {
      email: string;
      packageBandId: string;
      campaignName?: string;
    }) {
      return createRegisteredClientProspectCampaignData(payload);
    },

    async markCampaignLaunched(campaignId: string) {
      await postJson(`/agent/campaigns/${campaignId}/mark-launched`, {});
      return getAgentCampaignById(campaignId);
    },

    async getAgentCampaigns() {
      return listAgentCampaigns();
    },

    async getAgentInbox() {
      return getAgentInboxData();
    },

    async getAgentSales() {
      return getAgentSalesData();
    },

    async getAgentMessageInbox() {
      return getAgentMessageInboxData();
    },

    async getAgentMessageThread(campaignId: string) {
      return getAgentMessageThreadData(campaignId);
    },

    async sendAgentMessage(campaignId: string, body: string) {
      return sendAgentMessageData(campaignId, body);
    },

    async updateRecommendation(campaignId: string, recommendationId: string | undefined, notes: string, inventoryItems: SelectedPlanInventoryItem[]) {
      if (recommendationId) {
        await putJson(`/agent/recommendations/${recommendationId}`, {
          status: 'draft',
          notes,
          inventoryItems,
        });
      } else {
        await postJson(`/agent/campaigns/${campaignId}/recommendations`, { notes, inventoryItems });
      }

      const refreshedCampaign = await getAgentCampaignById(campaignId);
      if (!refreshedCampaign.recommendations.length && !refreshedCampaign.recommendation) {
        throw new Error('Recommendation could not be loaded after saving.');
      }

      return refreshedCampaign.recommendations.find((recommendation) => recommendation.id === recommendationId)
        ?? refreshedCampaign.recommendation;
    },

    async deleteRecommendation(campaignId: string, recommendationId: string) {
      await deleteRequest(`/agent/recommendations/${recommendationId}`);
      return getAgentCampaignById(campaignId);
    },

    async sendRecommendationToClient(campaignId: string) {
      await postJson(`/agent/campaigns/${campaignId}/send-to-client`, { message: 'Recommendation sent to client.' });
      return getAgentCampaignById(campaignId);
    },

    async resendProposalEmail(campaignId: string, payload: { toEmail: string; message?: string | null }) {
      await postJson(`/agent/campaigns/${campaignId}/resend-proposal-email`, payload);
      return getAgentCampaignById(campaignId);
    },

    async requestRecommendationChanges(campaignId: string, notes?: string) {
      await postJson(`/agent/campaigns/${campaignId}/request-recommendation-changes`, { notes });
      return getAgentCampaignById(campaignId);
    },

    async uploadAgentCampaignAsset(campaignId: string, file: File, assetType: string) {
      return uploadAgentAssetData(campaignId, file, assetType);
    },

    async getProspectDispositionReasons() {
      return getProspectDispositionReasonsData();
    },

    async closeProspect(campaignId: string, payload: { reasonCode: string; notes?: string | null }) {
      await postJson(`/agent/campaigns/${campaignId}/close-prospect`, payload);
      return getAgentCampaignById(campaignId);
    },

    async reopenProspect(campaignId: string) {
      await postJson(`/agent/campaigns/${campaignId}/reopen-prospect`, {});
      return getAgentCampaignById(campaignId);
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
      await postJson(`/agent/campaigns/${campaignId}/supplier-bookings`, payload);
      return getAgentCampaignById(campaignId);
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
      await postJson(`/agent/campaigns/${campaignId}/delivery-reports`, payload);
      return getAgentCampaignById(campaignId);
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
      return initializeRecommendationData(campaignId, payload);
    },

    async generateAgentRecommendation(
      campaignId: string,
      payload?: {
        targetRadioShare?: number;
        targetOohShare?: number;
        targetTvShare?: number;
        targetDigitalShare?: number;
      },
    ): Promise<{ campaignId: string }> {
      return generateRecommendationData(campaignId, payload);
    },

    async interpretAgentBrief(campaignId: string, payload: { brief: string; campaignName?: string; selectedBudget: number }) {
      return interpretBriefData(campaignId, payload);
    },

    async assignCampaignToMe(campaignId: string) {
      await postJson(`/agent/campaigns/${campaignId}/assign`, {});
      return getAgentCampaignById(campaignId);
    },

    async unassignCampaign(campaignId: string) {
      await postJson(`/agent/campaigns/${campaignId}/unassign`, {});
      return getAgentCampaignById(campaignId);
    },

    async convertProspectToSale(campaignId: string, payload?: { paymentReference?: string }) {
      await postJson(`/agent/campaigns/${campaignId}/convert-to-sale`, payload ?? {});
      return getAgentCampaignById(campaignId);
    },

    async updateProspectPricing(campaignId: string, payload: { packageBandId: string }) {
      await putJson(`/agent/campaigns/${campaignId}/prospect-pricing`, payload);
      return getAgentCampaignById(campaignId);
    },

    async getInventory(campaignId?: string) {
      return listInventoryData(campaignId);
    },
  };
}

import type {
  Campaign,
  CampaignBrief,
  CampaignConversationThread,
  NotificationSummary,
  PlanningMode,
} from '../types/domain';

type CampaignApiDependencies = {
  getCampaignById: (campaignId: string) => Promise<Campaign>;
  getPublicProposalById: (campaignId: string, token: string) => Promise<Campaign>;
  listCampaigns: () => Promise<Campaign[]>;
  getCampaignThread: (campaignId: string) => Promise<CampaignConversationThread>;
  sendCampaignThreadMessage: (campaignId: string, body: string) => Promise<CampaignConversationThread>;
  getNotificationSummary: () => Promise<NotificationSummary>;
  mutateJson: (path: string, method: 'POST' | 'PUT', body: unknown) => Promise<void>;
};

export function createCampaignApi({
  getCampaignById,
  getPublicProposalById,
  listCampaigns,
  getCampaignThread,
  sendCampaignThreadMessage,
  getNotificationSummary,
  mutateJson,
}: CampaignApiDependencies) {
  return {
    async getCampaigns(_userId: string) {
      return listCampaigns();
    },

    async getCampaign(campaignId: string) {
      return getCampaignById(campaignId);
    },

    async getPublicProposal(campaignId: string, token: string) {
      return getPublicProposalById(campaignId, token);
    },

    async approvePublicProposal(campaignId: string, token: string, recommendationId?: string) {
      await mutateJson(`/public/proposals/${encodeURIComponent(campaignId)}/approve`, 'POST', { token, recommendationId });
      return getPublicProposalById(campaignId, token);
    },

    async preparePublicProposalCheckout(campaignId: string, token: string, recommendationId: string) {
      await mutateJson(`/public/proposals/${encodeURIComponent(campaignId)}/prepare-checkout`, 'POST', { token, recommendationId });
    },

    async requestPublicProposalChanges(campaignId: string, token: string, notes?: string) {
      await mutateJson(`/public/proposals/${encodeURIComponent(campaignId)}/request-changes`, 'POST', { token, notes });
      return getPublicProposalById(campaignId, token);
    },

    async getCampaignMessages(campaignId: string) {
      return getCampaignThread(campaignId);
    },

    async sendCampaignMessage(campaignId: string, body: string) {
      return sendCampaignThreadMessage(campaignId, body);
    },

    async getNotificationSummary() {
      return getNotificationSummary();
    },

    async markNotificationRead(notificationId: string) {
      await mutateJson(`/notifications/${encodeURIComponent(notificationId)}/read`, 'POST', {});
    },

    async markAllNotificationsRead() {
      await mutateJson('/notifications/read-all', 'POST', {});
    },

    async saveCampaignBrief(campaignId: string, brief: CampaignBrief) {
      await mutateJson(`/campaigns/${campaignId}/brief`, 'PUT', brief);
      return getCampaignById(campaignId);
    },

    async submitCampaignBrief(campaignId: string) {
      await mutateJson(`/campaigns/${campaignId}/brief/submit`, 'POST', {});
      return getCampaignById(campaignId);
    },

    async setPlanningMode(campaignId: string, mode: PlanningMode) {
      await mutateJson(`/campaigns/${campaignId}/planning-mode`, 'POST', { planningMode: mode });
      return getCampaignById(campaignId);
    },

    async approveRecommendation(campaignId: string, recommendationId?: string) {
      await mutateJson(`/campaigns/${campaignId}/approve-recommendation`, 'POST', { recommendationId });
      return getCampaignById(campaignId);
    },

    async prepareRecommendationCheckout(campaignId: string, recommendationId: string) {
      await mutateJson(`/campaigns/${encodeURIComponent(campaignId)}/prepare-checkout`, 'POST', { recommendationId });
      return getCampaignById(campaignId);
    },

    async requestRecommendationChanges(campaignId: string, notes?: string) {
      await mutateJson(`/campaigns/${campaignId}/request-changes`, 'POST', { notes });
      return getCampaignById(campaignId);
    },

    async approveCreative(campaignId: string) {
      await mutateJson(`/campaigns/${campaignId}/approve-creative`, 'POST', {});
      return getCampaignById(campaignId);
    },

    async requestCreativeChanges(campaignId: string, notes?: string) {
      await mutateJson(`/campaigns/${campaignId}/request-creative-changes`, 'POST', { notes });
      return getCampaignById(campaignId);
    },
  };
}

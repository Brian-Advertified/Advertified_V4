import type {
  AgentInbox,
  Campaign,
  CampaignAsset,
  CreativeSystem,
} from '../types/domain';

type CreativeApiDependencies = {
  getCreativeInboxData: () => Promise<AgentInbox>;
  getCreativeCampaignById: (campaignId: string) => Promise<Campaign>;
  uploadCreativeAssetData: (campaignId: string, file: File, assetType: string) => Promise<CampaignAsset>;
  generateCreativeSystemData: (campaignId: string, payload: {
    prompt: string;
    iterationLabel?: string;
    brand?: string;
    product?: string;
    audience?: string;
    objective?: string;
    tone?: string;
    channels?: string[];
    cta?: string;
    constraints?: string[];
  }) => Promise<CreativeSystem>;
  postJson: (path: string, body: unknown) => Promise<void>;
};

export function createCreativeApi({
  getCreativeInboxData,
  getCreativeCampaignById,
  uploadCreativeAssetData,
  generateCreativeSystemData,
  postJson,
}: CreativeApiDependencies) {
  return {
    async getCreativeInbox() {
      return getCreativeInboxData();
    },

    async getCreativeCampaign(campaignId: string) {
      return getCreativeCampaignById(campaignId);
    },

    async sendFinishedMediaToClientForApproval(campaignId: string) {
      await postJson(`/creative/campaigns/${campaignId}/send-finished-media-to-client`, {});
      return getCreativeCampaignById(campaignId);
    },

    async uploadCreativeCampaignAsset(campaignId: string, file: File, assetType: string) {
      return uploadCreativeAssetData(campaignId, file, assetType);
    },

    async generateCreativeSystem(campaignId: string, payload: {
      prompt: string;
      iterationLabel?: string;
      brand?: string;
      product?: string;
      audience?: string;
      objective?: string;
      tone?: string;
      channels?: string[];
      cta?: string;
      constraints?: string[];
    }) {
      return generateCreativeSystemData(campaignId, payload);
    },
  };
}

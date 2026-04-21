import type {
  Lead,
  LeadAction,
  LeadActionInbox,
  LeadIndustryContext,
  LeadIndustryPolicy,
  LeadInteraction,
  LeadIntelligence,
  LeadPaidMediaSyncRun,
  LeadPaidMediaSyncStatus,
  LeadSourceAutomationRunResult,
  LeadSourceAutomationStatus,
} from '../types/domain';
import { apiRequest } from './apiClient';

type LeadMapper<TOutput> = {
  bivarianceHack(response: unknown): TOutput;
}['bivarianceHack'];

type LeadApiMappers = {
  mapLead: LeadMapper<Lead>;
  mapLeadIntelligence: LeadMapper<LeadIntelligence>;
  mapLeadIndustryContext: LeadMapper<LeadIndustryContext>;
  mapLeadIndustryPolicy: LeadMapper<LeadIndustryPolicy>;
  mapLeadActionInbox: LeadMapper<LeadActionInbox>;
  mapLeadSourceAutomationStatus: LeadMapper<LeadSourceAutomationStatus>;
  mapLeadSourceAutomationRunResult: LeadMapper<LeadSourceAutomationRunResult>;
  mapLeadPaidMediaSyncStatus: LeadMapper<LeadPaidMediaSyncStatus>;
  mapLeadPaidMediaSyncRun: LeadMapper<LeadPaidMediaSyncRun>;
  mapLeadAction: LeadMapper<LeadAction>;
};

export function createLeadApi(mappers: LeadApiMappers) {
  return {
    async getLeads() {
      const response = await apiRequest<unknown[]>('/leads');
      return response.map(mappers.mapLead);
    },
    async getLeadIntelligenceList() {
      const response = await apiRequest<unknown[]>('/leads/intelligence');
      return response.map(mappers.mapLeadIntelligence);
    },
    async getLeadIntelligence(leadId: number) {
      const response = await apiRequest<unknown>(`/leads/${encodeURIComponent(String(leadId))}/intelligence`);
      return mappers.mapLeadIntelligence(response);
    },
    async trackPublicLeadProposalEngagement(input: {
      campaignId: string;
      token: string;
      eventType: 'page_view' | 'reply_click' | 'download_pdf_click' | 'callback_click';
      context?: string;
      pageUrl?: string;
      userAgent?: string;
    }) {
      return apiRequest<{ campaignId: string; eventType: string; message: string }>(
        `/public/proposals/${encodeURIComponent(input.campaignId)}/lead-engagement`,
        {
          method: 'POST',
          body: JSON.stringify({
            token: input.token,
            eventType: input.eventType,
            context: input.context,
            pageUrl: input.pageUrl,
            userAgent: input.userAgent,
          }),
        });
    },
    async resolveLeadIndustryPolicy(category?: string) {
      const query = category?.trim()
        ? `?category=${encodeURIComponent(category.trim())}`
        : '';
      const response = await apiRequest<unknown>(`/leads/industry-policy/resolve${query}`);
      return mappers.mapLeadIndustryPolicy(response);
    },
    async resolveLeadIndustryContext(category?: string) {
      const query = category?.trim()
        ? `?category=${encodeURIComponent(category.trim())}`
        : '';
      const response = await apiRequest<unknown>(`/leads/industry-context/resolve${query}`);
      return mappers.mapLeadIndustryContext(response);
    },
    async getLeadActionInbox() {
      const response = await apiRequest<unknown>('/agent/lead-intelligence/inbox');
      return mappers.mapLeadActionInbox(response);
    },
    async getLeadSourceAutomationStatus() {
      const response = await apiRequest<unknown>('/leads/source-automation/status');
      return mappers.mapLeadSourceAutomationStatus(response);
    },
    async processLeadSourceAutomationNow() {
      const response = await apiRequest<unknown>('/leads/source-automation/process-now', {
        method: 'POST',
      });
      return mappers.mapLeadSourceAutomationRunResult(response);
    },
    async getLeadPaidMediaSyncStatus() {
      const response = await apiRequest<unknown>('/leads/paid-media-sync/status');
      return mappers.mapLeadPaidMediaSyncStatus(response);
    },
    async runLeadPaidMediaSyncNow() {
      const response = await apiRequest<unknown>('/leads/paid-media-sync/run-now', {
        method: 'POST',
      });
      return mappers.mapLeadPaidMediaSyncRun(response);
    },
    async createLead(input: { name: string; website?: string; location?: string; category?: string }) {
      const response = await apiRequest<unknown>('/leads', {
        method: 'POST',
        body: JSON.stringify(input),
      });
      return mappers.mapLead(response);
    },
    async importLeadCsv(input: { csvText: string; defaultSource?: string; importProfile?: string }) {
      return apiRequest<{
        createdCount: number;
        updatedCount: number;
        leads: Lead[];
      }>('/leads/import-csv', {
        method: 'POST',
        body: JSON.stringify(input),
      });
    },
    async analyzeLead(leadId: number) {
      const response = await apiRequest<unknown>(`/leads/${encodeURIComponent(String(leadId))}/analyze`, {
        method: 'POST',
      });
      return mappers.mapLeadIntelligence(response);
    },
    async updateLeadActionStatus(leadId: number, actionId: number, status: 'open' | 'completed' | 'dismissed') {
      const response = await apiRequest<unknown>(`/leads/${encodeURIComponent(String(leadId))}/actions/${encodeURIComponent(String(actionId))}/status`, {
        method: 'POST',
        body: JSON.stringify({ status }),
      });
      return mappers.mapLeadAction(response);
    },
    async assignLeadActionToMe(leadId: number, actionId: number) {
      const response = await apiRequest<unknown>(`/leads/${encodeURIComponent(String(leadId))}/actions/${encodeURIComponent(String(actionId))}/assign-to-me`, {
        method: 'POST',
      });
      return mappers.mapLeadAction(response);
    },
    async unassignLeadAction(leadId: number, actionId: number) {
      const response = await apiRequest<unknown>(`/leads/${encodeURIComponent(String(leadId))}/actions/${encodeURIComponent(String(actionId))}/unassign`, {
        method: 'POST',
      });
      return mappers.mapLeadAction(response);
    },
    async createLeadInteraction(input: { leadId: number; leadActionId?: number; interactionType: string; notes: string }) {
      return apiRequest<LeadInteraction>(`/leads/${encodeURIComponent(String(input.leadId))}/interactions`, {
        method: 'POST',
        body: JSON.stringify({
          leadActionId: input.leadActionId,
          interactionType: input.interactionType,
          notes: input.notes,
        }),
      });
    },
  };
}

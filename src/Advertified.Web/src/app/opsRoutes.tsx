import { Navigate } from 'react-router-dom';
import { ProtectedRoute } from '../components/ui/ProtectedRoute';
import type { AppRoute } from './routeUtils';
import {
  CreativeDirectorDashboardPage,
  CreativeStudioDemoPage,
  CreativeDirectorStudioPage,
} from '../pages/creative/CreativeDirectorPages';
import { AgentCampaignDetailPage } from '../pages/agent/AgentCampaignDetailPage';
import { AgentCreateRecommendationPage } from '../pages/agent/AgentCreateRecommendationPage';
import { AgentApprovalsPage } from '../pages/agent/AgentApprovalsPage';
import { AgentBriefsPage } from '../pages/agent/AgentBriefsPage';
import { AgentCampaignsPage } from '../pages/agent/AgentCampaignsPage';
import { AgentDashboardPage } from '../pages/agent/AgentDashboardPage';
import { AgentLeadIntelligencePage } from '../pages/agent/AgentLeadIntelligencePage';
import { AgentLeadsClientsPage } from '../pages/agent/AgentLeadsClientsPage';
import { AgentMessagesNotesPage } from '../pages/agent/AgentMessagesNotesPage';
import { AgentRecommendationBuilderPage } from '../pages/agent/AgentRecommendationBuilderPage';
import { AgentReviewSendPage } from '../pages/agent/AgentReviewSendPage';
import { AgentSalesPage } from '../pages/agent/AgentSalesPage';
import { AdminDashboardPage } from '../pages/admin/AdminDashboardPage';
import { AdminCampaignOperationsPage } from '../pages/admin/AdminCampaignOperationsPage';
import { AdminPackageOrdersPage } from '../pages/admin/AdminPackageOrdersPage';
import { AdminAuditPage } from '../pages/admin/AdminAuditPage';
import { AdminEnginePage } from '../pages/admin/AdminEnginePage';
import { AdminGeographyPage } from '../pages/admin/AdminGeographyPage';
import { AdminHealthPage, AdminImportsPage } from '../pages/admin/AdminSectionPages';
import { AdminIntegrationsPage } from '../pages/admin/AdminIntegrationsPage';
import { AdminMonitoringPage } from '../pages/admin/AdminMonitoringPage';
import { AdminPreviewRulesPage } from '../pages/admin/AdminPreviewRulesPage';
import { AdminAiVoicesPage } from '../pages/admin/AdminAiVoicesPage';
import { AdminAiVoicePacksPage } from '../pages/admin/AdminAiVoicePacksPage';
import { AdminAiVoiceTemplatesPage } from '../pages/admin/AdminAiVoiceTemplatesPage';
import { AdminAiAdOpsPage } from '../pages/admin/AdminAiAdOpsPage';
import { AdminPricingPage } from '../pages/admin/AdminPricingPage';
import { AdminStationsPage } from '../pages/admin/AdminStationsPage';
import { AdminUsersPage } from '../pages/admin/AdminUsersPage';

export const opsRoutes: AppRoute[] = [
  { path: '/creative', element: <ProtectedRoute requireCreativeDirector><CreativeDirectorDashboardPage /></ProtectedRoute> },
  { path: '/creative/studio-demo', element: <ProtectedRoute requireCreativeDirector><CreativeStudioDemoPage /></ProtectedRoute> },
  { path: '/creative/campaigns/:id/studio', element: <ProtectedRoute requireCreativeDirector><CreativeDirectorStudioPage /></ProtectedRoute> },

  { path: '/admin', element: <ProtectedRoute requireAdmin><AdminDashboardPage /></ProtectedRoute> },
  { path: '/admin/package-orders', element: <ProtectedRoute requireAdmin><AdminPackageOrdersPage /></ProtectedRoute> },
  { path: '/admin/campaign-operations', element: <ProtectedRoute requireAdmin><AdminCampaignOperationsPage /></ProtectedRoute> },
  { path: '/admin/stations', element: <ProtectedRoute requireAdmin><AdminStationsPage /></ProtectedRoute> },
  { path: '/admin/pricing', element: <ProtectedRoute requireAdmin><AdminPricingPage /></ProtectedRoute> },
  { path: '/admin/imports', element: <ProtectedRoute requireAdmin><AdminImportsPage /></ProtectedRoute> },
  { path: '/admin/health', element: <ProtectedRoute requireAdmin><AdminHealthPage /></ProtectedRoute> },
  { path: '/admin/geography', element: <ProtectedRoute requireAdmin><AdminGeographyPage /></ProtectedRoute> },
  { path: '/admin/engine', element: <ProtectedRoute requireAdmin><AdminEnginePage /></ProtectedRoute> },
  { path: '/admin/preview-rules', element: <ProtectedRoute requireAdmin><AdminPreviewRulesPage /></ProtectedRoute> },
  { path: '/admin/ai-voices', element: <ProtectedRoute requireAdmin><AdminAiVoicesPage /></ProtectedRoute> },
  { path: '/admin/ai-voice-packs', element: <ProtectedRoute requireAdmin><AdminAiVoicePacksPage /></ProtectedRoute> },
  { path: '/admin/ai-voice-templates', element: <ProtectedRoute requireAdmin><AdminAiVoiceTemplatesPage /></ProtectedRoute> },
  { path: '/admin/ai-ad-ops', element: <ProtectedRoute requireAdmin><AdminAiAdOpsPage /></ProtectedRoute> },
  { path: '/admin/monitoring', element: <ProtectedRoute requireAdmin><AdminMonitoringPage /></ProtectedRoute> },
  { path: '/admin/users', element: <ProtectedRoute requireAdmin><AdminUsersPage /></ProtectedRoute> },
  { path: '/admin/audit', element: <ProtectedRoute requireAdmin><AdminAuditPage /></ProtectedRoute> },
  { path: '/admin/integrations', element: <ProtectedRoute requireAdmin><AdminIntegrationsPage /></ProtectedRoute> },

  { path: '/agent', element: <ProtectedRoute requireAgent><AgentDashboardPage /></ProtectedRoute> },
  { path: '/agent/lead-intelligence', element: <ProtectedRoute requireAgent><AgentLeadIntelligencePage /></ProtectedRoute> },
  { path: '/agent/lead-actions', element: <ProtectedRoute requireAgent><AgentLeadIntelligencePage /></ProtectedRoute> },
  { path: '/agent/leads', element: <ProtectedRoute requireAgent><AgentLeadsClientsPage /></ProtectedRoute> },
  { path: '/agent/briefs', element: <ProtectedRoute requireAgent><AgentBriefsPage /></ProtectedRoute> },
  { path: '/agent/recommendation-builder', element: <ProtectedRoute requireAgent><AgentRecommendationBuilderPage /></ProtectedRoute> },
  { path: '/agent/review-send', element: <ProtectedRoute requireAgent><AgentReviewSendPage /></ProtectedRoute> },
  { path: '/agent/approvals', element: <ProtectedRoute requireAgent><AgentApprovalsPage /></ProtectedRoute> },
  { path: '/agent/messages', element: <ProtectedRoute requireAgent><AgentMessagesNotesPage /></ProtectedRoute> },
  { path: '/agent/sales', element: <ProtectedRoute requireAgent><AgentSalesPage /></ProtectedRoute> },
  { path: '/agent/recommendations/new', element: <ProtectedRoute requireAgent><AgentCreateRecommendationPage /></ProtectedRoute> },
  { path: '/agent/campaigns', element: <ProtectedRoute requireAgent><AgentCampaignsPage /></ProtectedRoute> },
  { path: '/agent/campaigns/:id', element: <ProtectedRoute requireAgent><AgentCampaignDetailPage /></ProtectedRoute> },
  { path: '/agent/packages', element: <Navigate to="/agent/campaigns" replace /> },
  { path: '/agent/checkout', element: <Navigate to="/agent/campaigns" replace /> },
  { path: '/agent/tasks', element: <Navigate to="/agent/campaigns" replace /> },
  { path: '/agent/performance', element: <Navigate to="/agent/campaigns" replace /> },
  { path: '/agent/inventory', element: <Navigate to="/agent/campaigns" replace /> },
];

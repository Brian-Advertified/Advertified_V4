import { Navigate } from 'react-router-dom';
import { ProtectedRoute } from '../components/ui/ProtectedRoute';
import { lazyPage, type AppRoute } from './routeUtils';

const AiStudioConsolePage = lazyPage(() => import('../pages/public/AiStudioPage'), 'AiStudioConsolePage');
const AgentCampaignDetailPage = lazyPage(() => import('../pages/agent/AgentCampaignDetailPage'), 'AgentCampaignDetailPage');
const AgentCreateRecommendationPage = lazyPage(() => import('../pages/agent/AgentCreateRecommendationPage'), 'AgentCreateRecommendationPage');
const AgentApprovalsPage = lazyPage(() => import('../pages/agent/AgentApprovalsPage'), 'AgentApprovalsPage');
const AgentBriefsPage = lazyPage(() => import('../pages/agent/AgentBriefsPage'), 'AgentBriefsPage');
const AgentCampaignsPage = lazyPage(() => import('../pages/agent/AgentCampaignsPage'), 'AgentCampaignsPage');
const AgentDashboardPage = lazyPage(() => import('../pages/agent/AgentDashboardPage'), 'AgentDashboardPage');
const AgentLeadsClientsPage = lazyPage(() => import('../pages/agent/AgentLeadsClientsPage'), 'AgentLeadsClientsPage');
const AgentMessagesNotesPage = lazyPage(() => import('../pages/agent/AgentMessagesNotesPage'), 'AgentMessagesNotesPage');
const AgentRecommendationBuilderPage = lazyPage(() => import('../pages/agent/AgentRecommendationBuilderPage'), 'AgentRecommendationBuilderPage');
const AgentReviewSendPage = lazyPage(() => import('../pages/agent/AgentReviewSendPage'), 'AgentReviewSendPage');
const AgentSalesPage = lazyPage(() => import('../pages/agent/AgentSalesPage'), 'AgentSalesPage');

const CreativeDirectorStudioPage = lazyPage(() => import('../pages/creative/CreativeDirectorPages'), 'CreativeDirectorStudioPage');

const AdminDashboardPage = lazyPage(() => import('../pages/admin/AdminDashboardPage'), 'AdminDashboardPage');
const AdminCampaignOperationsPage = lazyPage(() => import('../pages/admin/AdminCampaignOperationsPage'), 'AdminCampaignOperationsPage');
const AdminPackageOrdersPage = lazyPage(() => import('../pages/admin/AdminPackageOrdersPage'), 'AdminPackageOrdersPage');
const AdminAuditPage = lazyPage(() => import('../pages/admin/AdminAuditPage'), 'AdminAuditPage');
const AdminEnginePage = lazyPage(() => import('../pages/admin/AdminEnginePage'), 'AdminEnginePage');
const AdminGeographyPage = lazyPage(() => import('../pages/admin/AdminGeographyPage'), 'AdminGeographyPage');
const AdminHealthPage = lazyPage(() => import('../pages/admin/AdminSectionPages'), 'AdminHealthPage');
const AdminImportsPage = lazyPage(() => import('../pages/admin/AdminSectionPages'), 'AdminImportsPage');
const AdminIntegrationsPage = lazyPage(() => import('../pages/admin/AdminIntegrationsPage'), 'AdminIntegrationsPage');
const AdminMonitoringPage = lazyPage(() => import('../pages/admin/AdminMonitoringPage'), 'AdminMonitoringPage');
const AdminPreviewRulesPage = lazyPage(() => import('../pages/admin/AdminPreviewRulesPage'), 'AdminPreviewRulesPage');
const AdminAiVoicesPage = lazyPage(() => import('../pages/admin/AdminAiVoicesPage'), 'AdminAiVoicesPage');
const AdminAiVoicePacksPage = lazyPage(() => import('../pages/admin/AdminAiVoicePacksPage'), 'AdminAiVoicePacksPage');
const AdminAiVoiceTemplatesPage = lazyPage(() => import('../pages/admin/AdminAiVoiceTemplatesPage'), 'AdminAiVoiceTemplatesPage');
const AdminAiAdOpsPage = lazyPage(() => import('../pages/admin/AdminAiAdOpsPage'), 'AdminAiAdOpsPage');
const AdminPricingPage = lazyPage(() => import('../pages/admin/AdminPricingPage'), 'AdminPricingPage');
const AdminStationsPage = lazyPage(() => import('../pages/admin/AdminStationsPage'), 'AdminStationsPage');
const AdminUsersPage = lazyPage(() => import('../pages/admin/AdminUsersPage'), 'AdminUsersPage');

export const opsRoutes: AppRoute[] = [
  { path: '/creative', element: <ProtectedRoute requireCreativeDirector><AiStudioConsolePage /></ProtectedRoute> },
  { path: '/creative/studio-demo', element: <ProtectedRoute requireCreativeDirector><AiStudioConsolePage /></ProtectedRoute> },
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

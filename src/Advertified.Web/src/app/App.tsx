import { Suspense, lazy, type ComponentType } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { Footer } from '../components/layout/Footer';
import { Navbar } from '../components/layout/Navbar';
import { LoadingState } from '../components/ui/LoadingState';
import { ProtectedRoute } from '../components/ui/ProtectedRoute';
import { publicAiStudioEnabled } from '../lib/featureFlags';

function lazyPage<TModule extends Record<string, unknown>, TExport extends keyof TModule>(
  load: () => Promise<TModule>,
  exportName: TExport,
) {
  return lazy(async () => {
    const module = await load();

    return {
      default: module[exportName] as ComponentType,
    };
  });
}

const HomePage = lazyPage(() => import('../pages/public/HomePage'), 'HomePage');
const RegisterPage = lazyPage(() => import('../pages/public/RegisterPage'), 'RegisterPage');
const LoginPage = lazyPage(() => import('../pages/public/LoginPage'), 'LoginPage');
const VerifyEmailPage = lazyPage(() => import('../pages/public/VerifyEmailPage'), 'VerifyEmailPage');
const PackagesPage = lazyPage(() => import('../pages/public/PackagesPage'), 'PackagesPage');
const HowItWorksPage = lazyPage(() => import('../pages/public/HowItWorksPage'), 'HowItWorksPage');
const AboutUsPage = lazyPage(() => import('../pages/public/AboutUsPage'), 'AboutUsPage');
const AiStudioPage = lazyPage(() => import('../pages/public/AiStudioPage'), 'AiStudioPage');
const FaqPage = lazyPage(() => import('../pages/public/FaqPage'), 'FaqPage');
const MediaPartnersPage = lazyPage(() => import('../pages/public/MediaPartnersPage'), 'MediaPartnersPage');
const PartnerEnquiryPage = lazyPage(() => import('../pages/public/PartnerEnquiryPage'), 'PartnerEnquiryPage');
const PaymentSelectionPage = lazyPage(() => import('../pages/public/PaymentSelectionPage'), 'PaymentSelectionPage');
const CheckoutConfirmationPage = lazyPage(() => import('../pages/public/CheckoutConfirmationPage'), 'CheckoutConfirmationPage');
const PrivacyPolicyPage = lazyPage(() => import('../pages/public/LegalPages'), 'PrivacyPolicyPage');
const CookiePolicyPage = lazyPage(() => import('../pages/public/LegalPages'), 'CookiePolicyPage');
const TermsPage = lazyPage(() => import('../pages/public/LegalPages'), 'TermsPage');

const DashboardPage = lazyPage(() => import('../pages/client/DashboardPage'), 'DashboardPage');
const OrdersPage = lazyPage(() => import('../pages/client/OrdersPage'), 'OrdersPage');
const CampaignDetailPage = lazyPage(() => import('../pages/client/CampaignDetailPage'), 'CampaignDetailPage');

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
const CreativeStudioPreviewPage = lazyPage(() => import('../pages/creative/CreativeDirectorPages'), 'CreativeStudioPreviewPage');

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
const AdminPricingPage = lazyPage(() => import('../pages/admin/AdminPricingPage'), 'AdminPricingPage');
const AdminStationsPage = lazyPage(() => import('../pages/admin/AdminStationsPage'), 'AdminStationsPage');
const AdminUsersPage = lazyPage(() => import('../pages/admin/AdminUsersPage'), 'AdminUsersPage');

export function App() {
  const location = useLocation();
  const isAgentRoute = location.pathname.startsWith('/agent') || location.pathname.startsWith('/admin');

  return (
    <div className="min-h-screen">
      <Navbar />
      <main className={isAgentRoute ? 'pb-20 pt-8' : 'pb-20 pt-6 sm:pt-8'}>
        <Suspense fallback={<LoadingState label="Loading your workspace..." />}>
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/register" element={<ProtectedRoute guestOnly><RegisterPage /></ProtectedRoute>} />
            <Route path="/login" element={<ProtectedRoute guestOnly><LoginPage /></ProtectedRoute>} />
            <Route path="/verify-email" element={<ProtectedRoute guestOnly><VerifyEmailPage /></ProtectedRoute>} />
            <Route path="/packages" element={<PackagesPage />} />
            <Route path="/how-it-works" element={<HowItWorksPage />} />
            <Route path="/about" element={<AboutUsPage />} />
            <Route path="/ai-studio" element={publicAiStudioEnabled ? <AiStudioPage /> : <Navigate to="/" replace />} />
            <Route path="/faq" element={<FaqPage />} />
            <Route path="/media-partners" element={<MediaPartnersPage />} />
            <Route path="/partner-enquiry" element={<PartnerEnquiryPage />} />
            <Route path="/privacy" element={<PrivacyPolicyPage />} />
            <Route path="/cookie-policy" element={<CookiePolicyPage />} />
            <Route path="/terms-of-service" element={<TermsPage />} />
            <Route path="/terms" element={<Navigate to="/terms-of-service" replace />} />
            <Route path="/checkout/payment" element={<ProtectedRoute><PaymentSelectionPage /></ProtectedRoute>} />
            <Route path="/checkout/confirmation" element={<CheckoutConfirmationPage />} />
            <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
            <Route path="/orders" element={<ProtectedRoute><OrdersPage /></ProtectedRoute>} />
            <Route path="/campaigns/:id" element={<ProtectedRoute><CampaignDetailPage /></ProtectedRoute>} />
            <Route path="/campaigns/:id/overview" element={<ProtectedRoute><CampaignDetailPage /></ProtectedRoute>} />
            <Route path="/campaigns/:id/approvals" element={<ProtectedRoute><CampaignDetailPage /></ProtectedRoute>} />
            <Route path="/campaigns/:id/messages" element={<ProtectedRoute><CampaignDetailPage /></ProtectedRoute>} />
            <Route path="/campaigns/:id/studio-preview" element={<ProtectedRoute><CreativeStudioPreviewPage /></ProtectedRoute>} />
            <Route path="/creative" element={<ProtectedRoute requireCreativeDirector><AiStudioPage /></ProtectedRoute>} />
            <Route path="/creative/studio-demo" element={<ProtectedRoute requireCreativeDirector><AiStudioPage /></ProtectedRoute>} />
            <Route path="/creative/campaigns/:id/studio" element={<ProtectedRoute requireCreativeDirector><CreativeDirectorStudioPage /></ProtectedRoute>} />
            <Route path="/admin" element={<ProtectedRoute requireAdmin><AdminDashboardPage /></ProtectedRoute>} />
            <Route path="/admin/package-orders" element={<ProtectedRoute requireAdmin><AdminPackageOrdersPage /></ProtectedRoute>} />
            <Route path="/admin/campaign-operations" element={<ProtectedRoute requireAdmin><AdminCampaignOperationsPage /></ProtectedRoute>} />
            <Route path="/admin/stations" element={<ProtectedRoute requireAdmin><AdminStationsPage /></ProtectedRoute>} />
            <Route path="/admin/pricing" element={<ProtectedRoute requireAdmin><AdminPricingPage /></ProtectedRoute>} />
            <Route path="/admin/imports" element={<ProtectedRoute requireAdmin><AdminImportsPage /></ProtectedRoute>} />
            <Route path="/admin/health" element={<ProtectedRoute requireAdmin><AdminHealthPage /></ProtectedRoute>} />
            <Route path="/admin/geography" element={<ProtectedRoute requireAdmin><AdminGeographyPage /></ProtectedRoute>} />
            <Route path="/admin/engine" element={<ProtectedRoute requireAdmin><AdminEnginePage /></ProtectedRoute>} />
            <Route path="/admin/preview-rules" element={<ProtectedRoute requireAdmin><AdminPreviewRulesPage /></ProtectedRoute>} />
            <Route path="/admin/monitoring" element={<ProtectedRoute requireAdmin><AdminMonitoringPage /></ProtectedRoute>} />
            <Route path="/admin/users" element={<ProtectedRoute requireAdmin><AdminUsersPage /></ProtectedRoute>} />
            <Route path="/admin/audit" element={<ProtectedRoute requireAdmin><AdminAuditPage /></ProtectedRoute>} />
            <Route path="/admin/integrations" element={<ProtectedRoute requireAdmin><AdminIntegrationsPage /></ProtectedRoute>} />
            <Route path="/agent" element={<ProtectedRoute requireAgent><AgentDashboardPage /></ProtectedRoute>} />
            <Route path="/agent/leads" element={<ProtectedRoute requireAgent><AgentLeadsClientsPage /></ProtectedRoute>} />
            <Route path="/agent/briefs" element={<ProtectedRoute requireAgent><AgentBriefsPage /></ProtectedRoute>} />
            <Route path="/agent/recommendation-builder" element={<ProtectedRoute requireAgent><AgentRecommendationBuilderPage /></ProtectedRoute>} />
            <Route path="/agent/review-send" element={<ProtectedRoute requireAgent><AgentReviewSendPage /></ProtectedRoute>} />
            <Route path="/agent/approvals" element={<ProtectedRoute requireAgent><AgentApprovalsPage /></ProtectedRoute>} />
            <Route path="/agent/messages" element={<ProtectedRoute requireAgent><AgentMessagesNotesPage /></ProtectedRoute>} />
            <Route path="/agent/sales" element={<ProtectedRoute requireAgent><AgentSalesPage /></ProtectedRoute>} />
            <Route path="/agent/recommendations/new" element={<ProtectedRoute requireAgent><AgentCreateRecommendationPage /></ProtectedRoute>} />
            <Route path="/agent/campaigns" element={<ProtectedRoute requireAgent><AgentCampaignsPage /></ProtectedRoute>} />
            <Route path="/agent/campaigns/:id" element={<ProtectedRoute requireAgent><AgentCampaignDetailPage /></ProtectedRoute>} />
            <Route path="/agent/packages" element={<Navigate to="/agent/campaigns" replace />} />
            <Route path="/agent/checkout" element={<Navigate to="/agent/campaigns" replace />} />
            <Route path="/agent/tasks" element={<Navigate to="/agent/campaigns" replace />} />
            <Route path="/agent/performance" element={<Navigate to="/agent/campaigns" replace />} />
            <Route path="/agent/inventory" element={<Navigate to="/agent/campaigns" replace />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Suspense>
      </main>
      <Footer />
    </div>
  );
}

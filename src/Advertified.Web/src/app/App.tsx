import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { Navbar } from '../components/layout/Navbar';
import { Footer } from '../components/layout/Footer';
import { ProtectedRoute } from '../components/ui/ProtectedRoute';
import { HomePage } from '../pages/public/HomePage';
import { RegisterPage } from '../pages/public/RegisterPage';
import { LoginPage } from '../pages/public/LoginPage';
import { VerifyEmailPage } from '../pages/public/VerifyEmailPage';
import { PackagesPage } from '../pages/public/PackagesPage';
import { HowItWorksPage } from '../pages/public/HowItWorksPage';
import { MediaPartnersPage } from '../pages/public/MediaPartnersPage';
import { PartnerEnquiryPage } from '../pages/public/PartnerEnquiryPage';
import { PaymentSelectionPage } from '../pages/public/PaymentSelectionPage';
import { CheckoutConfirmationPage } from '../pages/public/CheckoutConfirmationPage';
import { DashboardPage } from '../pages/client/DashboardPage';
import { OrdersPage } from '../pages/client/OrdersPage';
import { CampaignDetailPage } from '../pages/client/CampaignDetailPage';
import { CampaignBriefPage } from '../pages/client/CampaignBriefPage';
import { CampaignPlanningPage } from '../pages/client/CampaignPlanningPage';
import { CampaignReviewPage } from '../pages/client/CampaignReviewPage';
import { AgentCampaignDetailPage } from '../pages/agent/AgentCampaignDetailPage';
import { AgentCreateRecommendationPage } from '../pages/agent/AgentCreateRecommendationPage';
import { AgentInventoryPage } from '../pages/agent/AgentInventoryPage';
import { AgentApprovalsPage, AgentBriefsPage, AgentCampaignsPage, AgentCheckoutStatusPage, AgentDashboardPage, AgentLeadsClientsPage, AgentMessagesNotesPage, AgentPackageSelectionPage, AgentPerformancePage, AgentRecommendationBuilderPage, AgentReviewSendPage, AgentTasksPage } from '../pages/agent/AgentSectionPages';
import { AdminDashboardPage } from '../pages/admin/AdminDashboardPage';
import { AdminAuditPage, AdminEnginePage, AdminGeographyPage, AdminHealthPage, AdminImportsPage, AdminIntegrationsPage, AdminMonitoringPage, AdminPreviewRulesPage, AdminPricingPage, AdminStationsPage, AdminUsersPage } from '../pages/admin/AdminSectionPages';

export function App() {
  const location = useLocation();
  const isAgentRoute = location.pathname.startsWith('/agent') || location.pathname.startsWith('/admin');

  return (
    <div className="min-h-screen">
      <Navbar />
      <main className={isAgentRoute ? 'pb-20 pt-8' : 'pb-20 pt-6 sm:pt-8'}>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/register" element={<ProtectedRoute guestOnly><RegisterPage /></ProtectedRoute>} />
          <Route path="/login" element={<ProtectedRoute guestOnly><LoginPage /></ProtectedRoute>} />
          <Route path="/verify-email" element={<ProtectedRoute guestOnly><VerifyEmailPage /></ProtectedRoute>} />
          <Route path="/packages" element={<PackagesPage />} />
          <Route path="/how-it-works" element={<HowItWorksPage />} />
          <Route path="/media-partners" element={<MediaPartnersPage />} />
          <Route path="/partner-enquiry" element={<PartnerEnquiryPage />} />
          <Route path="/checkout/payment" element={<ProtectedRoute><PaymentSelectionPage /></ProtectedRoute>} />
          <Route path="/checkout/confirmation" element={<CheckoutConfirmationPage />} />
          <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
          <Route path="/orders" element={<ProtectedRoute><OrdersPage /></ProtectedRoute>} />
          <Route path="/campaigns/:id" element={<ProtectedRoute><CampaignDetailPage /></ProtectedRoute>} />
          <Route path="/campaigns/:id/brief" element={<ProtectedRoute requirePurchase><CampaignBriefPage /></ProtectedRoute>} />
          <Route path="/campaigns/:id/planning" element={<ProtectedRoute requirePlanningAccess><CampaignPlanningPage /></ProtectedRoute>} />
          <Route path="/campaigns/:id/review" element={<ProtectedRoute requirePlanningAccess><CampaignReviewPage /></ProtectedRoute>} />
          <Route path="/admin" element={<ProtectedRoute requireAdmin><AdminDashboardPage /></ProtectedRoute>} />
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
          <Route path="/agent/packages" element={<ProtectedRoute requireAgent><AgentPackageSelectionPage /></ProtectedRoute>} />
          <Route path="/agent/checkout" element={<ProtectedRoute requireAgent><AgentCheckoutStatusPage /></ProtectedRoute>} />
          <Route path="/agent/briefs" element={<ProtectedRoute requireAgent><AgentBriefsPage /></ProtectedRoute>} />
          <Route path="/agent/recommendation-builder" element={<ProtectedRoute requireAgent><AgentRecommendationBuilderPage /></ProtectedRoute>} />
          <Route path="/agent/review-send" element={<ProtectedRoute requireAgent><AgentReviewSendPage /></ProtectedRoute>} />
          <Route path="/agent/approvals" element={<ProtectedRoute requireAgent><AgentApprovalsPage /></ProtectedRoute>} />
          <Route path="/agent/messages" element={<ProtectedRoute requireAgent><AgentMessagesNotesPage /></ProtectedRoute>} />
          <Route path="/agent/tasks" element={<ProtectedRoute requireAgent><AgentTasksPage /></ProtectedRoute>} />
          <Route path="/agent/performance" element={<ProtectedRoute requireAgent><AgentPerformancePage /></ProtectedRoute>} />
          <Route path="/agent/recommendations/new" element={<ProtectedRoute requireAgent><AgentCreateRecommendationPage /></ProtectedRoute>} />
          <Route path="/agent/campaigns" element={<ProtectedRoute requireAgent><AgentCampaignsPage /></ProtectedRoute>} />
          <Route path="/agent/campaigns/:id" element={<ProtectedRoute requireAgent><AgentCampaignDetailPage /></ProtectedRoute>} />
          <Route path="/agent/inventory" element={<ProtectedRoute requireAgent><AgentInventoryPage /></ProtectedRoute>} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
      <Footer />
    </div>
  );
}

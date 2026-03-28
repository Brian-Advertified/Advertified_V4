import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { Navbar } from '../components/layout/Navbar';
import { Footer } from '../components/layout/Footer';
import { ProtectedRoute } from '../components/ui/ProtectedRoute';
import { HomePage } from '../pages/public/HomePage';
import { RegisterPage } from '../pages/public/RegisterPage';
import { LoginPage } from '../pages/public/LoginPage';
import { VerifyEmailPage } from '../pages/public/VerifyEmailPage';
import { PackagesPage } from '../pages/public/PackagesPage';
import { PaymentSelectionPage } from '../pages/public/PaymentSelectionPage';
import { CheckoutConfirmationPage } from '../pages/public/CheckoutConfirmationPage';
import { DashboardPage } from '../pages/client/DashboardPage';
import { OrdersPage } from '../pages/client/OrdersPage';
import { CampaignDetailPage } from '../pages/client/CampaignDetailPage';
import { CampaignBriefPage } from '../pages/client/CampaignBriefPage';
import { CampaignPlanningPage } from '../pages/client/CampaignPlanningPage';
import { CampaignReviewPage } from '../pages/client/CampaignReviewPage';
import { AgentDashboardPage } from '../pages/agent/AgentDashboardPage';
import { AgentCampaignsPage } from '../pages/agent/AgentCampaignsPage';
import { AgentCampaignDetailPage } from '../pages/agent/AgentCampaignDetailPage';
import { AgentCreateRecommendationPage } from '../pages/agent/AgentCreateRecommendationPage';
import { AgentInventoryPage } from '../pages/agent/AgentInventoryPage';

export function App() {
  const location = useLocation();
  const isAgentRoute = location.pathname.startsWith('/agent');

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
          <Route path="/checkout/payment" element={<ProtectedRoute><PaymentSelectionPage /></ProtectedRoute>} />
          <Route path="/checkout/confirmation" element={<CheckoutConfirmationPage />} />
          <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
          <Route path="/orders" element={<ProtectedRoute><OrdersPage /></ProtectedRoute>} />
          <Route path="/campaigns/:id" element={<ProtectedRoute><CampaignDetailPage /></ProtectedRoute>} />
          <Route path="/campaigns/:id/brief" element={<ProtectedRoute requirePurchase><CampaignBriefPage /></ProtectedRoute>} />
          <Route path="/campaigns/:id/planning" element={<ProtectedRoute requirePlanningAccess><CampaignPlanningPage /></ProtectedRoute>} />
          <Route path="/campaigns/:id/review" element={<ProtectedRoute requirePlanningAccess><CampaignReviewPage /></ProtectedRoute>} />
          <Route path="/agent" element={<ProtectedRoute requireAgent><AgentDashboardPage /></ProtectedRoute>} />
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

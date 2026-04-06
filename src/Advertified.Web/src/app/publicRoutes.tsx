import { Navigate } from 'react-router-dom';
import { ProtectedRoute } from '../components/ui/ProtectedRoute';
import { publicAiStudioEnabled } from '../lib/featureFlags';
import type { AppRoute } from './routeUtils';
import { HomePage } from '../pages/public/HomePage';
import { RegisterPage } from '../pages/public/RegisterPage';
import { LoginPage } from '../pages/public/LoginPage';
import { VerifyEmailPage } from '../pages/public/VerifyEmailPage';
import { SetPasswordPage } from '../pages/public/SetPasswordPage';
import { PackagesPage } from '../pages/public/PackagesPage';
import { HowItWorksPage } from '../pages/public/HowItWorksPage';
import { AboutUsPage } from '../pages/public/AboutUsPage';
import { AiStudioPage } from '../pages/public/AiStudioPage';
import { FaqPage } from '../pages/public/FaqPage';
import { MediaPartnersPage } from '../pages/public/MediaPartnersPage';
import { PartnerEnquiryPage } from '../pages/public/PartnerEnquiryPage';
import { PaymentSelectionPage } from '../pages/public/PaymentSelectionPage';
import { CheckoutConfirmationPage } from '../pages/public/CheckoutConfirmationPage';
import { ProposalEntryPage } from '../pages/public/ProposalEntryPage';
import { ProspectQuestionnairePage } from '../pages/public/ProspectQuestionnairePage';
import { PrivacyPolicyPage, CookiePolicyPage, TermsPage } from '../pages/public/LegalPages';

export const publicRoutes: AppRoute[] = [
  { path: '/', element: <HomePage /> },
  { path: '/register', element: <ProtectedRoute guestOnly><RegisterPage /></ProtectedRoute> },
  { path: '/login', element: <ProtectedRoute guestOnly><LoginPage /></ProtectedRoute> },
  { path: '/verify-email', element: <ProtectedRoute guestOnly><VerifyEmailPage /></ProtectedRoute> },
  { path: '/set-password', element: <ProtectedRoute><SetPasswordPage /></ProtectedRoute> },
  { path: '/packages', element: <PackagesPage /> },
  { path: '/how-it-works', element: <HowItWorksPage /> },
  { path: '/about', element: <AboutUsPage /> },
  { path: '/ai-studio', element: publicAiStudioEnabled ? <AiStudioPage /> : <Navigate to="/" replace /> },
  { path: '/faq', element: <FaqPage /> },
  { path: '/media-partners', element: <MediaPartnersPage /> },
  { path: '/partner-enquiry', element: <PartnerEnquiryPage /> },
  { path: '/start-campaign', element: <ProspectQuestionnairePage /> },
  { path: '/privacy', element: <PrivacyPolicyPage /> },
  { path: '/cookie-policy', element: <CookiePolicyPage /> },
  { path: '/terms-of-service', element: <TermsPage /> },
  { path: '/terms', element: <Navigate to="/terms-of-service" replace /> },
  { path: '/checkout/payment', element: <ProtectedRoute><PaymentSelectionPage /></ProtectedRoute> },
  { path: '/checkout/confirmation', element: <CheckoutConfirmationPage /> },
  { path: '/proposal/:id', element: <ProposalEntryPage /> },
];

import { Navigate } from 'react-router-dom';
import { ProtectedRoute } from '../components/ui/ProtectedRoute';
import { publicAiStudioEnabled } from '../lib/featureFlags';
import { lazyPage, type AppRoute } from './routeUtils';

const HomePage = lazyPage(() => import('../pages/public/HomePage'), 'HomePage');
const RegisterPage = lazyPage(() => import('../pages/public/RegisterPage'), 'RegisterPage');
const LoginPage = lazyPage(() => import('../pages/public/LoginPage'), 'LoginPage');
const VerifyEmailPage = lazyPage(() => import('../pages/public/VerifyEmailPage'), 'VerifyEmailPage');
const SetPasswordPage = lazyPage(() => import('../pages/public/SetPasswordPage'), 'SetPasswordPage');
const PackagesPage = lazyPage(() => import('../pages/public/PackagesPage'), 'PackagesPage');
const HowItWorksPage = lazyPage(() => import('../pages/public/HowItWorksPage'), 'HowItWorksPage');
const AboutUsPage = lazyPage(() => import('../pages/public/AboutUsPage'), 'AboutUsPage');
const AiStudioPage = lazyPage(() => import('../pages/public/AiStudioPage'), 'AiStudioPage');
const FaqPage = lazyPage(() => import('../pages/public/FaqPage'), 'FaqPage');
const MediaPartnersPage = lazyPage(() => import('../pages/public/MediaPartnersPage'), 'MediaPartnersPage');
const PartnerEnquiryPage = lazyPage(() => import('../pages/public/PartnerEnquiryPage'), 'PartnerEnquiryPage');
const PaymentSelectionPage = lazyPage(() => import('../pages/public/PaymentSelectionPage'), 'PaymentSelectionPage');
const CheckoutConfirmationPage = lazyPage(() => import('../pages/public/CheckoutConfirmationPage'), 'CheckoutConfirmationPage');
const ProposalEntryPage = lazyPage(() => import('../pages/public/ProposalEntryPage'), 'ProposalEntryPage');
const LeadProposalEntryPage = lazyPage(() => import('../pages/public/LeadProposalEntryPage'), 'LeadProposalEntryPage');
const ProspectQuestionnairePage = lazyPage(() => import('../pages/public/ProspectQuestionnairePage'), 'ProspectQuestionnairePage');
const PrivacyPolicyPage = lazyPage(() => import('../pages/public/LegalPages'), 'PrivacyPolicyPage');
const CookiePolicyPage = lazyPage(() => import('../pages/public/LegalPages'), 'CookiePolicyPage');
const TermsPage = lazyPage(() => import('../pages/public/LegalPages'), 'TermsPage');

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
  { path: '/lead-proposal/:id', element: <LeadProposalEntryPage /> },
];

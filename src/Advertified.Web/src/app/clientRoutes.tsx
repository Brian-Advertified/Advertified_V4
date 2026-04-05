import { ProtectedRoute } from '../components/ui/ProtectedRoute';
import { lazyPage, type AppRoute } from './routeUtils';

const DashboardPage = lazyPage(() => import('../pages/client/DashboardPage'), 'DashboardPage');
const OrdersPage = lazyPage(() => import('../pages/client/OrdersPage'), 'OrdersPage');
const CampaignDetailPage = lazyPage(() => import('../pages/client/CampaignDetailPage'), 'CampaignDetailPage');
const CreativeStudioPreviewPage = lazyPage(() => import('../pages/creative/CreativeDirectorPages'), 'CreativeStudioPreviewPage');

export const clientRoutes: AppRoute[] = [
  { path: '/dashboard', element: <ProtectedRoute><DashboardPage /></ProtectedRoute> },
  { path: '/orders', element: <ProtectedRoute><OrdersPage /></ProtectedRoute> },
  { path: '/campaigns/:id', element: <ProtectedRoute><CampaignDetailPage /></ProtectedRoute> },
  { path: '/campaigns/:id/overview', element: <ProtectedRoute><CampaignDetailPage /></ProtectedRoute> },
  { path: '/campaigns/:id/approvals', element: <ProtectedRoute><CampaignDetailPage /></ProtectedRoute> },
  { path: '/campaigns/:id/messages', element: <ProtectedRoute><CampaignDetailPage /></ProtectedRoute> },
  { path: '/campaigns/:id/studio-preview', element: <ProtectedRoute><CreativeStudioPreviewPage /></ProtectedRoute> },
];

import { ProtectedRoute } from '../components/ui/ProtectedRoute';
import type { AppRoute } from './routeUtils';
import { DashboardPage } from '../pages/client/DashboardPage';
import { OrdersPage } from '../pages/client/OrdersPage';
import { CampaignDetailPage } from '../pages/client/CampaignDetailPage';
import { CreativeStudioPreviewPage } from '../pages/creative/CreativeDirectorPages';

export const clientRoutes: AppRoute[] = [
  { path: '/dashboard', element: <ProtectedRoute><DashboardPage /></ProtectedRoute> },
  { path: '/orders', element: <ProtectedRoute><OrdersPage /></ProtectedRoute> },
  { path: '/campaigns/:id', element: <ProtectedRoute><CampaignDetailPage /></ProtectedRoute> },
  { path: '/campaigns/:id/overview', element: <ProtectedRoute><CampaignDetailPage /></ProtectedRoute> },
  { path: '/campaigns/:id/performance', element: <ProtectedRoute><CampaignDetailPage /></ProtectedRoute> },
  { path: '/campaigns/:id/approvals', element: <ProtectedRoute><CampaignDetailPage /></ProtectedRoute> },
  { path: '/campaigns/:id/messages', element: <ProtectedRoute><CampaignDetailPage /></ProtectedRoute> },
  { path: '/campaigns/:id/studio-preview', element: <ProtectedRoute><CreativeStudioPreviewPage /></ProtectedRoute> },
];

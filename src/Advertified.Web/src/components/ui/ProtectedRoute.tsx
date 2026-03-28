import type { ReactElement } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Navigate, useLocation, useParams } from 'react-router-dom';
import { canOpenBrief, canOpenPlanning, isAgent } from '../../lib/access';
import { advertifiedApi } from '../../services/advertifiedApi';
import { useAuth } from '../../features/auth/auth-context';
import { LoadingState } from './LoadingState';

export function ProtectedRoute({
  children,
  guestOnly,
  requireAgent,
  requirePurchase,
  requirePlanningAccess,
}: {
  children: ReactElement;
  guestOnly?: boolean;
  requireAgent?: boolean;
  requirePurchase?: boolean;
  requirePlanningAccess?: boolean;
}) {
  const { user, isAuthenticated } = useAuth();
  const location = useLocation();
  const { id } = useParams();
  const isAgentRoute = location.pathname.startsWith('/agent');
  const needsCampaign = Boolean(id && (requirePurchase || requirePlanningAccess));

  const campaignQuery = useQuery({
    queryKey: [isAgentRoute ? 'agent-campaign' : 'campaign', id],
    queryFn: () => (isAgentRoute ? advertifiedApi.getAgentCampaign(id!) : advertifiedApi.getCampaign(id!)),
    enabled: needsCampaign,
  });

  if (guestOnly && isAuthenticated) {
    return <Navigate to={isAgent(user) ? '/agent' : '/dashboard'} replace />;
  }

  if (!guestOnly && !isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  if (requireAgent && !isAgent(user)) {
    return <Navigate to="/dashboard" replace />;
  }

  if (needsCampaign && campaignQuery.isLoading) {
    return <LoadingState label="Checking campaign access..." />;
  }

  if (requirePurchase && !canOpenBrief(campaignQuery.data)) {
    return <Navigate to={`/campaigns/${id}`} replace />;
  }

  if (requirePlanningAccess && !canOpenPlanning(campaignQuery.data)) {
    return <Navigate to={`/campaigns/${id}`} replace />;
  }

  return children;
}

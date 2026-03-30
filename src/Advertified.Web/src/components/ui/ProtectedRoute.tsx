import type { ReactElement } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Navigate, useLocation, useParams } from 'react-router-dom';
import { canAccessCreativeStudio, canAccessOperations, canOpenBrief, canOpenPlanning, isAdmin, isAgent, isCreativeDirector } from '../../lib/access';
import { advertifiedApi } from '../../services/advertifiedApi';
import { useAuth } from '../../features/auth/auth-context';
import { LoadingState } from './LoadingState';

export function ProtectedRoute({
  children,
  guestOnly,
  requireAgent,
  requireCreativeDirector,
  requireAdmin,
  requirePurchase,
  requirePlanningAccess,
}: {
  children: ReactElement;
  guestOnly?: boolean;
  requireAgent?: boolean;
  requireCreativeDirector?: boolean;
  requireAdmin?: boolean;
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
    return <Navigate to={isAdmin(user) ? '/admin' : isCreativeDirector(user) ? '/creative' : isAgent(user) ? '/agent' : '/dashboard'} replace />;
  }

  if (!guestOnly && !isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  if (requireAgent && !canAccessOperations(user)) {
    return <Navigate to="/dashboard" replace />;
  }

  if (requireCreativeDirector && !canAccessCreativeStudio(user)) {
    return <Navigate to="/dashboard" replace />;
  }

  if (requireAdmin && !isAdmin(user)) {
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

import type { ReactElement } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { canAccessCreativeStudio, canAccessOperations, isAdmin, isAgent, isCreativeDirector } from '../../lib/access';
import { useAuth } from '../../features/auth/auth-context';

export function ProtectedRoute({
  children,
  guestOnly,
  requireAgent,
  requireCreativeDirector,
  requireAdmin,
}: {
  children: ReactElement;
  guestOnly?: boolean;
  requireAgent?: boolean;
  requireCreativeDirector?: boolean;
  requireAdmin?: boolean;
}) {
  const { user, isAuthenticated } = useAuth();
  const location = useLocation();

  if (guestOnly && isAuthenticated) {
    return <Navigate to={user?.requiresPasswordSetup ? '/set-password' : (isAdmin(user) ? '/admin' : isCreativeDirector(user) ? '/creative/studio-demo' : isAgent(user) ? '/agent' : '/dashboard')} replace />;
  }

  if (!guestOnly && !isAuthenticated) {
    return <Navigate to="/login" state={{ from: `${location.pathname}${location.search}` }} replace />;
  }

  if (!guestOnly && user?.requiresPasswordSetup && location.pathname !== '/set-password') {
    return <Navigate to={`/set-password?next=${encodeURIComponent(`${location.pathname}${location.search}`)}`} replace />;
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

  return children;
}

import { useQuery } from '@tanstack/react-query';
import { Navigate } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { StatusBadge } from '../../components/ui/StatusBadge';
import { formatCurrency, formatDate } from '../../lib/utils';
import { useAuth } from '../../features/auth/auth-context';
import { advertifiedApi } from '../../services/advertifiedApi';
import { ClientPortalShell } from './clientWorkspace';

export function OrdersPage() {
  const { user } = useAuth();

  if (user?.role === 'creative_director') {
    return <Navigate to="/creative/studio-demo" replace />;
  }

  if (user?.role === 'agent' || user?.role === 'admin') {
    return <Navigate to={user.role === 'admin' ? '/admin' : '/agent'} replace />;
  }

  const campaignsQuery = useQuery({
    queryKey: ['campaigns', user?.id],
    queryFn: () => advertifiedApi.getCampaigns(user!.id),
    enabled: Boolean(user),
  });
  const ordersQuery = useQuery({
    queryKey: ['orders', user?.id],
    queryFn: () => advertifiedApi.getOrders(user!.id),
    enabled: Boolean(user),
  });

  if (ordersQuery.isLoading || campaignsQuery.isLoading) {
    return <LoadingState label="Loading your package orders..." />;
  }

  const orders = ordersQuery.data ?? [];
  const campaigns = campaignsQuery.data ?? [];

  return (
    <ClientPortalShell
      campaigns={campaigns}
      activeNav="orders"
      title="Package Orders"
      description="Track each package purchase, payment state, and reference from one clear order history inside the client portal."
    >
      {orders.length ? (
        <div className="grid gap-4">
          {orders.map((order) => (
            <div key={order.id} className="user-card flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-lg font-semibold text-ink">{order.packageBandName}</p>
                <p className="mt-2 text-sm text-ink-soft">{formatDate(order.createdAt)} | {formatCurrency(order.amount)}</p>
              </div>
              <div className="flex items-center gap-4">
                <StatusBadge status={order.paymentStatus} />
                <p className="text-sm font-medium text-ink-soft">{order.paymentReference ?? 'Awaiting payment reference'}</p>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <EmptyState title="No package orders yet" description="Once you purchase a package, every order and payment state will appear here." ctaHref="/packages" ctaLabel="Choose a package" />
      )}
    </ClientPortalShell>
  );
}

import { useQuery } from '@tanstack/react-query';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { StatusBadge } from '../../components/ui/StatusBadge';
import { formatCurrency, formatDate } from '../../lib/utils';
import { useAuth } from '../../features/auth/auth-context';
import { advertifiedApi } from '../../services/advertifiedApi';

export function OrdersPage() {
  const { user } = useAuth();
  const ordersQuery = useQuery({
    queryKey: ['orders', user?.id],
    queryFn: () => advertifiedApi.getOrders(user!.id),
    enabled: Boolean(user),
  });

  if (ordersQuery.isLoading) {
    return <LoadingState label="Loading your package orders..." />;
  }

  const orders = ordersQuery.data ?? [];

  return (
    <section className="page-shell space-y-8">
      <div>
        <div className="pill bg-highlight-soft text-highlight">Orders</div>
        <h1 className="mt-4 text-4xl font-semibold tracking-tight text-ink">Package orders</h1>
      </div>
      {orders.length ? (
        <div className="grid gap-4">
          {orders.map((order) => (
            <div key={order.id} className="panel flex flex-col gap-4 px-6 py-6 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-lg font-semibold text-ink">{order.packageBandName}</p>
                <p className="mt-2 text-sm text-ink-soft">{formatDate(order.createdAt)} • {formatCurrency(order.amount)}</p>
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
    </section>
  );
}

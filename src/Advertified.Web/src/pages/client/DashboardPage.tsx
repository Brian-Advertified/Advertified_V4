import { useQuery } from '@tanstack/react-query';
import { Navigate } from 'react-router-dom';
import { Link } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../features/auth/auth-context';
import { canAccessAiStudioForStatus } from '../../features/campaigns/aiStudioAccess';
import { getCampaignPrimaryAction } from '../../lib/access';
import { formatCurrency, formatDate, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { ClientPortalShell, getCampaignProgressPercent } from './clientWorkspace';

export function DashboardPage() {
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

  if (campaignsQuery.isLoading || ordersQuery.isLoading) {
    return <LoadingState label="Loading your dashboard..." />;
  }

  const campaigns = campaignsQuery.data ?? [];
  const orders = ordersQuery.data ?? [];
  const unpaidOrders = orders.filter((order) => order.paymentStatus !== 'paid');
  const nextPendingOrder = unpaidOrders[0];
  const pendingCampaign = campaigns.find((campaign) => campaign.packageOrderId === nextPendingOrder?.id)
    ?? campaigns.find((campaign) => campaign.paymentStatus !== 'paid' && campaign.status === 'review_ready')
    ?? campaigns.find((campaign) => campaign.paymentStatus !== 'paid');
  const pendingRecommendation = pendingCampaign?.recommendations[0] ?? pendingCampaign?.recommendation;
  const paymentHref = nextPendingOrder
    ? `/checkout/payment?orderId=${encodeURIComponent(nextPendingOrder.id)}${pendingCampaign ? `&campaignId=${encodeURIComponent(pendingCampaign.id)}` : ''}${pendingRecommendation?.id ? `&recommendationId=${encodeURIComponent(pendingRecommendation.id)}` : ''}`
    : null;
  const paymentBlockedCampaignCount = campaigns.filter((campaign) => campaign.paymentStatus !== 'paid').length;
  const reviewReadyAwaitingPaymentCount = campaigns.filter((campaign) => campaign.status === 'review_ready' && campaign.paymentStatus !== 'paid').length;

  return (
    <ClientPortalShell
      campaigns={campaigns}
      activeNav="dashboard"
      title="Client Portal"
      description="Your new overview for active campaigns, purchased packages, and the next workspace each campaign should open."
    >
      <div className="user-banner">
        <strong>How it works:</strong> this is now the top-level client portal. Choose a campaign below to enter its simplified workspace with the current approval and direct team messaging.
      </div>

      {paymentHref ? (
        <div className="mt-6 rounded-[28px] border border-amber-200 bg-[linear-gradient(180deg,#fff8eb_0%,#fffdf7_100%)] p-6 shadow-[0_18px_44px_rgba(217,119,6,0.08)]">
          <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
            <div className="max-w-3xl">
              <div className="inline-flex items-center rounded-full border border-amber-300 bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-amber-900">
                Payment required
              </div>
              <h3 className="mt-4 !mb-2">One more step: complete payment to unlock your campaign</h3>
              <p className="user-muted">
                {pendingCampaign
                  ? `${pendingCampaign.campaignName} is in your portal already, but approval and planning stay locked until this payment is completed.`
                  : 'Your order is in your portal already, but approval and planning stay locked until this payment is completed.'}
              </p>
              <div className="mt-4 flex flex-wrap gap-2">
                {nextPendingOrder ? <span className="user-pill">{nextPendingOrder.packageBandName}</span> : null}
                {nextPendingOrder ? <span className="user-pill">{formatCurrency(nextPendingOrder.amount)}</span> : null}
                <span className="user-pill">{unpaidOrders.length} pending order{unpaidOrders.length === 1 ? '' : 's'}</span>
                {reviewReadyAwaitingPaymentCount > 0 ? <span className="user-pill">{reviewReadyAwaitingPaymentCount} ready to approve after payment</span> : null}
              </div>
            </div>
            <div className="flex w-full flex-col gap-3 lg:w-auto lg:min-w-[250px]">
              <Link to={paymentHref} className="user-btn-primary w-full justify-center text-center">Complete payment now</Link>
              {pendingCampaign ? (
                <Link to={`/campaigns/${pendingCampaign.id}`} className="user-btn-secondary w-full justify-center text-center">Open campaign workspace</Link>
              ) : (
                <Link to="/orders" className="user-btn-secondary w-full justify-center text-center">View order history</Link>
              )}
            </div>
          </div>
        </div>
      ) : null}

      <div className="user-grid-4">
        <div className="user-card">
          <h3>Active Campaigns</h3>
          <div className="user-metric">{campaigns.length}</div>
          <div className="user-muted">Campaign workspaces available in the new client flow.</div>
        </div>
        <div className="user-card">
          <h3>{paymentBlockedCampaignCount > 0 ? 'Payment Required' : 'Orders'}</h3>
          <div className="user-metric">{paymentBlockedCampaignCount > 0 ? paymentBlockedCampaignCount : orders.length}</div>
          <div className="user-muted">
            {paymentBlockedCampaignCount > 0
              ? 'Campaigns waiting for payment before approval can continue.'
              : 'Purchased packages linked to your account.'}
          </div>
        </div>
        <div className="user-card">
          <h3>Planning Unlocked</h3>
          <div className="user-metric">{campaigns.filter((campaign) => campaign.aiUnlocked).length}</div>
          <div className="user-muted">Campaigns ready for planning and recommendation flow.</div>
        </div>
        <div className="user-card">
          <h3>Approved</h3>
          <div className="user-metric">{campaigns.filter((campaign) => campaign.status === 'approved' || campaign.status === 'creative_approved' || campaign.status === 'launched').length}</div>
          <div className="user-muted">Campaigns that have reached approval in the live workflow.</div>
        </div>
      </div>

      {orders.length ? (
        <div className="mt-6">
          <div className="user-card">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
              <div>
                <h3 className="!mb-2">Recent Package Orders</h3>
                <p className="user-muted">Billing and purchase history stays available here, without pulling you out of the main campaign flow.</p>
              </div>
              <Link to="/orders" className="user-btn-secondary">Open full order history</Link>
            </div>
            <div className="mt-4 space-y-3">
              {orders.slice(0, 3).map((order) => (
                <div key={order.id} className="user-wire">
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                    <div>
                      <strong>{order.packageBandName}</strong>
                      <div>{formatDate(order.createdAt)} | {formatCurrency(order.amount)}</div>
                    </div>
                    <div className="flex flex-col gap-2 text-left lg:items-end lg:text-right">
                      <div>{titleCase(order.paymentStatus)}{order.paymentReference ? ` | ${order.paymentReference}` : ''}</div>
                      {order.paymentStatus !== 'paid' ? (
                        <Link
                          to={`/checkout/payment?orderId=${encodeURIComponent(order.id)}${campaigns.find((campaign) => campaign.packageOrderId === order.id) ? `&campaignId=${encodeURIComponent(campaigns.find((campaign) => campaign.packageOrderId === order.id)!.id)}` : ''}`}
                          className="user-btn-primary"
                        >
                          Complete payment
                        </Link>
                      ) : null}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      ) : null}

      {campaigns.length ? (
        <div className="mt-6 space-y-4">
          {campaigns.map((campaign) => {
            const action = getCampaignPrimaryAction(campaign);
            const paymentRequired = campaign.paymentStatus !== 'paid';
            return (
              <div key={campaign.id} className="user-card">
                <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                  <div className="space-y-3">
                    <div className="flex flex-wrap gap-2">
                      <span className="user-pill">{campaign.packageBandName}</span>
                      <span className="user-pill">{formatCurrency(campaign.selectedBudget)}</span>
                      <span className="user-pill">{titleCase(campaign.status)}</span>
                    </div>
                    <div>
                      <h3 className="!mb-2">{campaign.campaignName}</h3>
                      <p className="user-muted">
                        {paymentRequired && campaign.status === 'review_ready'
                          ? 'Payment is still required before you can approve this recommendation.'
                          : campaign.nextAction}
                      </p>
                    </div>
                  </div>
                  <div className="text-left lg:text-right">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Progress</div>
                    <div className="mt-2 text-2xl font-semibold text-ink">{getCampaignProgressPercent(campaign)}%</div>
                    <div className="mt-2 user-muted">Created {formatDate(campaign.createdAt)}</div>
                  </div>
                </div>

                <div className="user-grid-3 mt-4">
                  <div className="user-wire">
                    <strong>Primary action</strong>
                    <div>{action.label}</div>
                    <div className="mt-2">{action.description}</div>
                  </div>
                  <div className="user-wire">
                    <strong>Planning mode</strong>
                    <div>{campaign.planningMode ? titleCase(campaign.planningMode) : 'Not selected yet'}</div>
                  </div>
                  <div className="user-wire">
                    <strong>{paymentRequired ? 'Payment status' : 'Workspace'}</strong>
                    <div>
                      {paymentRequired
                        ? 'Your campaign is visible now, but payment must be completed before approval can move forward.'
                        : 'Open the campaign workspace to review the current approval and message your Advertified team from one screen.'}
                    </div>
                  </div>
                </div>

                <div className="user-toolbar mt-4">
                  <Link to={`/campaigns/${campaign.id}`} className="user-btn-primary">Open Campaign Workspace</Link>
                  <Link to={action.href} className={paymentRequired ? 'user-btn-primary' : 'user-btn-secondary'}>
                    {paymentRequired ? 'Complete Payment' : 'Continue Next Step'}
                  </Link>
                  {canAccessAiStudioForStatus(campaign.status) ? (
                    <Link to={`/ai-studio?campaignId=${campaign.id}`} className="user-btn-secondary">Prefill from approved recommendation</Link>
                  ) : null}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="mt-6">
          <EmptyState
            title="No campaigns yet"
            description="Secure your first package to unlock the new client portal workflow."
            ctaHref="/packages"
            ctaLabel="Browse packages"
          />
        </div>
      )}
    </ClientPortalShell>
  );
}

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
import { ClientPortalShell, getCampaignProgressPercent, getClientFacingBudget } from './clientWorkspace';

function getSimpleCampaignMessage(
  campaign: { status: string; paymentStatus: string },
) {
  if (campaign.paymentStatus !== 'paid') {
    return 'Please complete payment to continue with this campaign.';
  }

  switch (campaign.status) {
    case 'paid':
    case 'brief_in_progress':
      return 'Your campaign is ready for the next details from you.';
    case 'brief_submitted':
      return 'We are reviewing your campaign details.';
    case 'planning_in_progress':
      return 'We are preparing your recommendation.';
    case 'review_ready':
      return 'Your recommendation is ready for you to review.';
    case 'approved':
      return 'Your campaign has been approved and work is underway.';
    case 'creative_sent_to_client_for_approval':
      return 'Your final advert is ready for your review.';
    case 'creative_changes_requested':
      return 'We are working on the changes you requested.';
    case 'creative_approved':
      return 'Everything is approved and almost ready to go live.';
    case 'launched':
      return 'Your campaign is now live.';
    default:
      return 'Open your campaign to see the latest update.';
  }
}

function getSimplePrimaryActionDescription(
  campaign: { paymentStatus: string; status: string },
  actionLabel: string,
) {
  if (campaign.paymentStatus !== 'paid') {
    return 'Pay now to continue with this campaign.';
  }

  if (campaign.status === 'review_ready') {
    return 'Open your recommendation and choose what you want to do next.';
  }

  if (campaign.status === 'approved' || campaign.status === 'creative_sent_to_client_for_approval' || campaign.status === 'creative_changes_requested' || campaign.status === 'creative_approved' || campaign.status === 'launched') {
    return 'Open your campaign to see the latest progress.';
  }

  return `${actionLabel} to keep things moving.`;
}

export function DashboardPage() {
  const { user } = useAuth();
  const isOpsUser = user?.role === 'agent' || user?.role === 'admin';
  const isCreativeDirector = user?.role === 'creative_director';

  const campaignsQuery = useQuery({
    queryKey: ['campaigns', user?.id],
    queryFn: () => advertifiedApi.getCampaigns(user!.id),
    enabled: Boolean(user && !isOpsUser && !isCreativeDirector),
    refetchInterval: (query) => (query.state.data?.some((campaign) => campaign.paymentStatus !== 'paid') ? 15_000 : false),
    refetchOnWindowFocus: true,
  });
  const ordersQuery = useQuery({
    queryKey: ['orders', user?.id],
    queryFn: () => advertifiedApi.getOrders(user!.id),
    enabled: Boolean(user && !isOpsUser && !isCreativeDirector),
    refetchInterval: (query) => (query.state.data?.some((order) => order.paymentStatus !== 'paid') ? 15_000 : false),
    refetchOnWindowFocus: true,
  });

  if (isCreativeDirector) {
    return <Navigate to="/creative/studio-demo" replace />;
  }

  if (isOpsUser) {
    return <Navigate to={user.role === 'admin' ? '/admin' : '/agent'} replace />;
  }

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
      title="Dashboard"
      description="See your campaigns, payments, and next steps in one place."
    >
      <div className="user-banner">
        <strong>Next step:</strong> choose a campaign below to see what to do next.
      </div>

      {paymentHref ? (
        <div className="mt-6 rounded-[28px] border border-amber-200 bg-[linear-gradient(180deg,#fff8eb_0%,#fffdf7_100%)] p-6 shadow-[0_18px_44px_rgba(217,119,6,0.08)]">
          <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
            <div className="max-w-3xl">
              <div className="inline-flex items-center rounded-full border border-amber-300 bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-amber-900">
                Payment required
              </div>
              <h3 className="mt-4 !mb-2">One more step: complete payment</h3>
              <p className="user-muted">
                {pendingCampaign
                  ? `${pendingCampaign.campaignName} is ready, but we still need your payment before work can continue.`
                  : 'Your order is ready, but we still need your payment before work can continue.'}
              </p>
              <div className="mt-4 flex flex-wrap gap-2">
                {nextPendingOrder ? <span className="user-pill">{nextPendingOrder.packageBandName}</span> : null}
                <span className="user-pill">{formatCurrency(pendingRecommendation?.totalCost ?? nextPendingOrder?.amount ?? 0)}</span>
                <span className="user-pill">{unpaidOrders.length} pending order{unpaidOrders.length === 1 ? '' : 's'}</span>
                {reviewReadyAwaitingPaymentCount > 0 ? <span className="user-pill">{reviewReadyAwaitingPaymentCount} ready to approve after payment</span> : null}
              </div>
            </div>
            <div className="flex w-full flex-col gap-3 lg:w-auto lg:min-w-[250px]">
              <Link to={paymentHref} className="user-btn-primary w-full justify-center text-center">Complete payment now</Link>
              {pendingCampaign ? (
                <Link to={`/campaigns/${pendingCampaign.id}`} className="user-btn-secondary w-full justify-center text-center">View campaign</Link>
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
          <div className="user-muted">Campaigns in your account.</div>
        </div>
        <div className="user-card">
          <h3>{paymentBlockedCampaignCount > 0 ? 'Payment Required' : 'Orders'}</h3>
          <div className="user-metric">{paymentBlockedCampaignCount > 0 ? paymentBlockedCampaignCount : orders.length}</div>
          <div className="user-muted">
            {paymentBlockedCampaignCount > 0
              ? 'Campaigns waiting for payment.'
              : 'Your recent orders.'}
          </div>
        </div>
        <div className="user-card">
          <h3>Ready To Review</h3>
          <div className="user-metric">{campaigns.filter((campaign) => campaign.aiUnlocked).length}</div>
          <div className="user-muted">Campaigns with updates ready for you.</div>
        </div>
        <div className="user-card">
          <h3>Approved</h3>
          <div className="user-metric">{campaigns.filter((campaign) => campaign.status === 'approved' || campaign.status === 'creative_approved' || campaign.status === 'launched').length}</div>
          <div className="user-muted">Campaigns you have already approved.</div>
        </div>
      </div>

      {orders.length ? (
        <div className="mt-6">
          <div className="user-card">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
              <div>
                <h3 className="!mb-2">Recent Orders</h3>
                <p className="user-muted">See your latest payments and invoices here.</p>
              </div>
              <Link to="/orders" className="user-btn-secondary">View all orders</Link>
            </div>
            <div className="mt-4 space-y-3">
              {orders.slice(0, 3).map((order) => (
                <div key={order.id} className="user-wire">
                  {(() => {
                    const linkedCampaign = campaigns.find((campaign) => campaign.packageOrderId === order.id);
                    const linkedRecommendationId = linkedCampaign?.recommendations[0]?.id ?? linkedCampaign?.recommendation?.id;

                    return (
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                    <div>
                      <strong>{order.packageBandName}</strong>
                      <div>{formatDate(order.createdAt)} | {formatCurrency(order.amount)}</div>
                    </div>
                    <div className="flex flex-col gap-2 text-left lg:items-end lg:text-right">
                      <div>{titleCase(order.paymentStatus)}{order.paymentReference ? ` | ${order.paymentReference}` : ''}</div>
                      {order.paymentStatus !== 'paid' ? (
                        <Link
                          to={`/checkout/payment?orderId=${encodeURIComponent(order.id)}${linkedCampaign ? `&campaignId=${encodeURIComponent(linkedCampaign.id)}` : ''}${linkedRecommendationId ? `&recommendationId=${encodeURIComponent(linkedRecommendationId)}` : ''}`}
                          className="user-btn-primary"
                        >
                          Complete payment
                        </Link>
                      ) : null}
                    </div>
                  </div>
                    );
                  })()}
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
                      <span className="user-pill">{formatCurrency(getClientFacingBudget(campaign))}</span>
                      <span className="user-pill">{titleCase(campaign.status)}</span>
                    </div>
                    <div>
                      <h3 className="!mb-2">{campaign.campaignName}</h3>
                      <p className="user-muted">{getSimpleCampaignMessage(campaign)}</p>
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
                    <strong>What to do next</strong>
                    <div>{action.label}</div>
                    <div className="mt-2">{getSimplePrimaryActionDescription(campaign, action.label)}</div>
                  </div>
                  <div className="user-wire">
                    <strong>Campaign type</strong>
                    <div>{campaign.planningMode ? titleCase(campaign.planningMode) : 'We are still setting this up'}</div>
                  </div>
                  <div className="user-wire">
                    <strong>{paymentRequired ? 'Payment' : 'Your campaign'}</strong>
                    <div>
                      {paymentRequired
                        ? 'Pay first, then you can review your recommendation.'
                        : 'Open your campaign to see updates and next steps.'}
                    </div>
                  </div>
                </div>

                <div className="user-toolbar mt-4">
                  <Link to={`/campaigns/${campaign.id}`} className="user-btn-primary">View campaign</Link>
                  <Link to={action.href} className={paymentRequired ? 'user-btn-primary' : 'user-btn-secondary'}>
                    {paymentRequired ? 'Complete payment' : 'Continue'}
                  </Link>
                  {canAccessAiStudioForStatus(campaign.status) ? (
                    <Link to={`/ai-studio?campaignId=${campaign.id}`} className="user-btn-secondary">Use approved recommendation</Link>
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
            description="Buy your first package to get started."
            ctaHref="/packages"
            ctaLabel="Browse packages"
          />
        </div>
      )}
    </ClientPortalShell>
  );
}

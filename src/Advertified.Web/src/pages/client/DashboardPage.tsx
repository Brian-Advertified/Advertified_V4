import { useQuery } from '@tanstack/react-query';
import { Navigate } from 'react-router-dom';
import { Link } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../features/auth/auth-context';
import { canAccessAiStudio } from '../../features/campaigns/aiStudioAccess';
import { campaignNeedsCheckout, getCampaignPrimaryAction, getClientCampaignState, isPaymentAwaitingManualReview } from '../../lib/access';
import { getPrimaryRecommendation } from '../../lib/campaignStatus';
import { getPendingPaymentPollInterval } from '../../lib/queryPolling';
import { formatCurrency, formatDate, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { Campaign } from '../../types/domain';
import { ClientPortalShell, getCampaignProgressPercent, getClientFacingBudget } from './clientWorkspace';

function getSimpleCampaignMessage(
  campaign: Pick<Campaign, 'status' | 'paymentStatus' | 'paymentProvider'>,
) {
  return getClientCampaignState(campaign as Campaign).description;
}

function getSimplePrimaryActionDescription(
  campaign: Pick<Campaign, 'paymentStatus' | 'paymentProvider' | 'status'>,
  actionLabel: string,
) {
  const state = getClientCampaignState(campaign as Campaign);
  return state.requiresClientAction ? `${actionLabel} to keep things moving.` : state.nextStep;
}

function getOrderStatusLabel(order: Pick<Campaign, 'paymentStatus' | 'paymentProvider'>) {
  if (isPaymentAwaitingManualReview(order.paymentProvider, order.paymentStatus)) {
    return 'Pay Later under review';
  }

  if (order.paymentStatus === 'paid') {
    return 'Payment confirmed';
  }

  if (order.paymentStatus === 'failed') {
    return 'Payment not confirmed';
  }

  return 'Payment required';
}

function getOrderStatusDescription(order: Pick<Campaign, 'paymentStatus' | 'paymentProvider'>) {
  if (isPaymentAwaitingManualReview(order.paymentProvider, order.paymentStatus)) {
    return 'Your Finance Partner application is being reviewed. There is nothing else you need to do right now.';
  }

  if (order.paymentStatus === 'paid') {
    return 'Payment is complete and Advertified can continue with the next campaign stage.';
  }

  if (order.paymentStatus === 'failed') {
    return 'This payment did not go through. You can retry or choose a different payment route.';
  }

  return 'Payment still needs to be completed before this campaign can move forward.';
}

export function DashboardPage() {
  const { user } = useAuth();
  const isOpsUser = user?.role === 'agent' || user?.role === 'admin';
  const isCreativeDirector = user?.role === 'creative_director';

  const campaignsQuery = useQuery({
    queryKey: ['campaigns', user?.id],
    queryFn: () => advertifiedApi.getCampaigns(user!.id),
    enabled: Boolean(user && !isOpsUser && !isCreativeDirector),
    refetchInterval: (query) => getPendingPaymentPollInterval(query.state.data),
  });
  const ordersQuery = useQuery({
    queryKey: ['orders', user?.id],
    queryFn: () => advertifiedApi.getOrders(user!.id),
    enabled: Boolean(user && !isOpsUser && !isCreativeDirector),
    refetchInterval: (query) => getPendingPaymentPollInterval(query.state.data),
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
  const unresolvedOrderIds = new Set(
    campaigns
      .filter((campaign) => campaignNeedsCheckout(campaign))
      .map((campaign) => campaign.packageOrderId),
  );
  const unpaidOrders = orders.filter((order) => order.paymentStatus !== 'paid' && (!unresolvedOrderIds.size || unresolvedOrderIds.has(order.id)));
  const actionableOrders = unpaidOrders.filter((order) => !isPaymentAwaitingManualReview(order.paymentProvider, order.paymentStatus));
  const reviewPendingOrders = unpaidOrders.filter((order) => isPaymentAwaitingManualReview(order.paymentProvider, order.paymentStatus));
  const nextPendingOrder = actionableOrders[0];
  const nextReviewPendingOrder = reviewPendingOrders[0];
  const reviewPendingCampaign = campaigns.find((campaign) => campaign.packageOrderId === nextReviewPendingOrder?.id)
    ?? campaigns.find((campaign) => isPaymentAwaitingManualReview(campaign.paymentProvider, campaign.paymentStatus));
  const pendingCampaign = campaigns.find((campaign) => campaign.packageOrderId === nextPendingOrder?.id)
    ?? campaigns.find((campaign) => campaignNeedsCheckout(campaign) && campaign.status === 'review_ready')
    ?? campaigns.find((campaign) => campaignNeedsCheckout(campaign));
  const pendingRecommendation = pendingCampaign ? getPrimaryRecommendation(pendingCampaign) : undefined;
  const paymentHref = nextPendingOrder
    ? `/checkout/payment?orderId=${encodeURIComponent(nextPendingOrder.id)}${pendingCampaign ? `&campaignId=${encodeURIComponent(pendingCampaign.id)}` : ''}${pendingRecommendation?.id ? `&recommendationId=${encodeURIComponent(pendingRecommendation.id)}` : ''}`
    : null;
  const paymentBlockedCampaignCount = campaigns.filter((campaign) => campaignNeedsCheckout(campaign)).length;
  const reviewReadyAwaitingPaymentCount = campaigns.filter((campaign) => campaign.status === 'review_ready' && campaignNeedsCheckout(campaign)).length;

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

      {!paymentHref && nextReviewPendingOrder ? (
        <div className="mt-6 rounded-[28px] border border-sky-200 bg-[linear-gradient(180deg,#eff8ff_0%,#f9fcff_100%)] p-6 shadow-[0_18px_44px_rgba(14,116,144,0.08)]">
          <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
            <div className="max-w-3xl">
              <div className="inline-flex items-center rounded-full border border-sky-300 bg-white px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-sky-900">
                Pay Later pending review
              </div>
              <h3 className="mt-4 !mb-2">Your application is already in review</h3>
              <p className="user-muted">
                {reviewPendingCampaign
                  ? `${reviewPendingCampaign.campaignName} is waiting for Finance Partner approval. You do not need to make another payment while this is pending.`
                  : 'Your order is waiting for Finance Partner approval. You do not need to make another payment while this is pending.'}
              </p>
              <div className="mt-4 flex flex-wrap gap-2">
                <span className="user-pill">{nextReviewPendingOrder.packageBandName}</span>
                <span className="user-pill">{formatCurrency(nextReviewPendingOrder.amount)}</span>
                <span className="user-pill">{reviewPendingOrders.length} pending review order{reviewPendingOrders.length === 1 ? '' : 's'}</span>
              </div>
            </div>
            <div className="flex w-full flex-col gap-3 lg:w-auto lg:min-w-[250px]">
              {reviewPendingCampaign ? (
                <Link to={`/campaigns/${reviewPendingCampaign.id}`} className="user-btn-primary w-full justify-center text-center">View campaign</Link>
              ) : (
                <Link to="/orders" className="user-btn-primary w-full justify-center text-center">View my orders</Link>
              )}
              <Link to="/orders" className="user-btn-secondary w-full justify-center text-center">Open order history</Link>
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
          <div className="user-metric">{campaigns.filter((campaign) => getClientCampaignState(campaign).requiresClientAction).length}</div>
          <div className="user-muted">Campaigns that currently need something from you.</div>
        </div>
        <div className="user-card">
          <h3>Approved</h3>
          <div className="user-metric">{campaigns.filter((campaign) => campaign.status === 'approved' || campaign.status === 'creative_approved' || campaign.status === 'booking_in_progress' || campaign.status === 'launched').length}</div>
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
                     const linkedRecommendationId = linkedCampaign ? getPrimaryRecommendation(linkedCampaign)?.id : undefined;
                     const orderNeedsPayment = !isPaymentAwaitingManualReview(order.paymentProvider, order.paymentStatus)
                       && order.paymentStatus !== 'paid'
                       && (!linkedCampaign || campaignNeedsCheckout(linkedCampaign));

                    return (
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                    <div>
                      <strong>{order.packageBandName}</strong>
                      <div>{formatDate(order.createdAt)} | {formatCurrency(order.amount)}</div>
                    </div>
                    <div className="flex flex-col gap-2 text-left lg:items-end lg:text-right">
                      <div>{getOrderStatusLabel(order)}{order.paymentReference ? ` | ${order.paymentReference}` : ''}</div>
                      <div className="text-sm text-ink-soft">{getOrderStatusDescription(order)}</div>
                      {orderNeedsPayment ? (
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
            const state = getClientCampaignState(campaign);
            const paymentRequired = campaignNeedsCheckout(campaign);
            const paymentAwaitingReview = isPaymentAwaitingManualReview(campaign.paymentProvider, campaign.paymentStatus);
            return (
              <div key={campaign.id} className="user-card">
                <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                  <div className="space-y-3">
                    <div className="flex flex-wrap gap-2">
                      <span className="user-pill">{campaign.packageBandName}</span>
                      <span className="user-pill">{formatCurrency(getClientFacingBudget(campaign))}</span>
                      <span className="user-pill">{state.statusLabel}</span>
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
                    <div>{state.actionLabel}</div>
                    <div className="mt-2">{getSimplePrimaryActionDescription(campaign, action.label)}</div>
                  </div>
                  <div className="user-wire">
                    <strong>Campaign type</strong>
                    <div>{campaign.planningMode ? titleCase(campaign.planningMode) : 'We are still setting this up'}</div>
                  </div>
                  <div className="user-wire">
                    <strong>{paymentRequired ? 'Payment' : 'Current status'}</strong>
                    <div>
                      {paymentAwaitingReview
                        ? 'Your Pay Later application is currently under review.'
                        : paymentRequired
                          ? 'Pay first, then recommendation review can continue.'
                          : state.statusLabel}
                    </div>
                  </div>
                </div>

                <div className="user-toolbar mt-4">
                  <Link to={`/campaigns/${campaign.id}`} className="user-btn-primary">View campaign</Link>
                  <Link to={action.href} className={paymentRequired ? 'user-btn-primary' : 'user-btn-secondary'}>
                    {paymentRequired ? 'Complete payment' : 'Continue'}
                  </Link>
                  {canAccessAiStudio(campaign) ? (
                    <Link to={`/ai-studio?campaignId=${campaign.id}`} className="user-btn-secondary">Open campaign content</Link>
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

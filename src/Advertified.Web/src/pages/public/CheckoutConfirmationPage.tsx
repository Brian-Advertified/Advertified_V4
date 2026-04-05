import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useRef } from 'react';
import { BadgeCheck, CircleAlert, Clock3, FileText } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import advertifiedLogo from '../../assets/advertified-logo-v3.png';
import { LoadingState } from '../../components/ui/LoadingState';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { getCampaignPrimaryAction } from '../../lib/access';
import { getPendingPaymentPollInterval } from '../../lib/queryPolling';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { clearCheckoutAutoApproval, readCheckoutAutoApproval } from '../../services/checkoutAutoApprovalStore';

type VodaPayReturnData = {
  responseCode?: string;
  responseMessage?: string;
  transactionId?: string;
};

export function CheckoutConfirmationPage() {
  const [searchParams] = useSearchParams();
  const currentSearch = searchParams.toString();
  const { user } = useAuth();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const orderId = searchParams.get('orderId') ?? '';
  const provider = (searchParams.get('provider') ?? '').toLowerCase();
  const requestedCampaignId = searchParams.get('campaignId')?.trim() ?? '';
  const requestedRecommendationId = searchParams.get('recommendationId')?.trim() ?? '';
  const requestedProposalPath = searchParams.get('proposalPath')?.trim() ?? '';
  const callbackCapturedRef = useRef(false);
  const statusToastKeyRef = useRef<string | null>(null);
  const autoApprovalAttemptedKeyRef = useRef<string | null>(null);

  const orderQuery = useQuery({
    queryKey: ['package-order', orderId, user?.id],
    queryFn: () => advertifiedApi.getOrder(orderId, user!.id),
    enabled: Boolean(orderId && user?.id),
    refetchInterval: (query) => {
      const order = query.state.data;
      if (order?.paymentStatus !== 'pending') {
        return false;
      }

      return getPendingPaymentPollInterval(order ? [order] : [], provider === 'lula' ? 15_000 : 4_000);
    },
  });
  const order = orderQuery.data;
  const campaignsQuery = useQuery({
    queryKey: ['campaigns', user?.id],
    queryFn: () => advertifiedApi.getCampaigns(user!.id),
    enabled: Boolean(user?.id && order?.paymentStatus === 'paid'),
  });
  const vodaPayReturnData = useMemo(() => parseVodaPayReturnData(searchParams.get('data')), [searchParams]);
  const storedAutoApproval = useMemo(() => {
    return readCheckoutAutoApproval(orderId);
  }, [orderId]);
  const currentReturnPath = useMemo(
    () => `/checkout/confirmation${currentSearch ? `?${currentSearch}` : ''}`,
    [currentSearch],
  );
  const linkedCampaign = (campaignsQuery.data ?? []).find((campaign) => campaign.packageOrderId === order?.id);
  const linkedCampaignAction = linkedCampaign ? getCampaignPrimaryAction(linkedCampaign) : null;
  const effectiveCampaignId = requestedCampaignId || storedAutoApproval?.campaignId || linkedCampaign?.id || '';
  const effectiveRecommendationId = requestedRecommendationId || storedAutoApproval?.recommendationId || '';
  const effectiveProposalPath = requestedProposalPath || storedAutoApproval?.proposalPath || '';

  const autoApproveMutation = useMutation({
    mutationFn: async () => {
      if (!effectiveCampaignId || !effectiveRecommendationId) {
        return null;
      }

      return advertifiedApi.approveRecommendation(effectiveCampaignId, effectiveRecommendationId);
    },
    onSuccess: async () => {
      clearCheckoutAutoApproval(orderId);

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['campaigns', user?.id] }),
        effectiveCampaignId ? queryClient.invalidateQueries({ queryKey: ['campaign', effectiveCampaignId] }) : Promise.resolve(),
      ]);

      pushToast({
        title: 'Proposal accepted.',
        description: 'Your selected proposal was accepted automatically after successful payment.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Payment succeeded, but proposal acceptance needs attention.',
        description: error instanceof Error ? error.message : 'Please open campaign approvals and accept manually.',
      }, 'error');
    },
  });

  useEffect(() => {
    if (provider !== 'vodapay' || !orderId || callbackCapturedRef.current) {
      return;
    }

    const queryParameters = Object.fromEntries(searchParams.entries());
    callbackCapturedRef.current = true;
    void advertifiedApi.captureVodaPayCallback(orderId, queryParameters);
  }, [orderId, provider, searchParams]);

  const attemptKey = `${orderId}:${effectiveCampaignId}:${effectiveRecommendationId}`;
  useEffect(() => {
    if (
      !orderId
      || !effectiveRecommendationId
      || !effectiveCampaignId
      || order?.paymentStatus !== 'paid'
      || autoApprovalAttemptedKeyRef.current === attemptKey
      || autoApproveMutation.isPending
    ) {
      return;
    }

    autoApprovalAttemptedKeyRef.current = attemptKey;
    autoApproveMutation.mutate();
  }, [attemptKey, autoApproveMutation, effectiveCampaignId, effectiveRecommendationId, order?.paymentStatus, orderId]);

  useEffect(() => {
    if (!orderId || order?.paymentStatus === 'paid') {
      return;
    }

    clearCheckoutAutoApproval(orderId);
  }, [order?.paymentStatus, orderId]);

  const statusKey = `${order?.id ?? ''}:${order?.paymentStatus ?? ''}:${vodaPayReturnData?.responseCode ?? ''}:${vodaPayReturnData?.responseMessage ?? ''}`;
  useEffect(() => {
    if (!order || statusToastKeyRef.current === statusKey) {
      return;
    }

    if (order.paymentStatus === 'paid') {
      pushToast({
        title: 'Payment successful.',
        description: `Your ${order.packageBandName} package has been paid successfully.`,
      });
      statusToastKeyRef.current = statusKey;
      return;
    }

    if (order.paymentStatus === 'failed') {
      const reason = vodaPayReturnData?.responseMessage?.trim();
      pushToast({
        title: reason ? `Payment failed due to ${reason.toLowerCase()}.` : 'Payment failed.',
        description: 'No money was confirmed for this order. You can try again when you are ready.',
      }, 'error');
      statusToastKeyRef.current = statusKey;
      return;
    }

    if (provider === 'vodapay' && vodaPayReturnData?.responseCode === '00') {
      pushToast({
        title: "We're finalising your payment confirmation.",
        description: 'Your payment provider has returned you to Advertified while we wait for final validation.',
      }, 'info');
      statusToastKeyRef.current = statusKey;
    }
  }, [order, provider, pushToast, statusKey, vodaPayReturnData?.responseCode, vodaPayReturnData?.responseMessage]);

  if (!user) {
    return (
      <section className="page-shell max-w-2xl">
        <div className="rounded-[28px] border border-line bg-white p-8 shadow-soft">
          <h1 className="text-3xl font-semibold tracking-tight text-ink">Sign in to check your payment</h1>
          <p className="mt-3 text-base leading-7 text-ink-soft">
            {provider === 'lula'
              ? 'Sign in with the same account to view the invoice prepared for your Lula payment route.'
              : 'Your VodaPay session has returned to Advertified. Sign in with the same account to view the latest payment state.'}
          </p>
          <div className="mt-6 flex flex-wrap gap-3">
            <Link to={`/login?next=${encodeURIComponent(currentReturnPath)}`} className="button-primary px-5 py-3">Log in</Link>
            <Link to="/packages" className="button-secondary px-5 py-3">Back to packages</Link>
          </div>
        </div>
      </section>
    );
  }

  if (orderQuery.isLoading) {
    return <LoadingState label={provider === 'lula' ? 'Loading invoice details...' : 'Checking payment status...'} />;
  }

  if (!order) {
    return (
      <section className="page-shell max-w-2xl">
        <div className="rounded-[28px] border border-line bg-white p-8 shadow-soft">
          <h1 className="text-3xl font-semibold tracking-tight text-ink">Order not found</h1>
          <p className="mt-3 text-base leading-7 text-ink-soft">We could not locate that package order for your account.</p>
          <div className="mt-6">
            <Link to="/orders" className="button-primary px-5 py-3">View my orders</Link>
          </div>
        </div>
      </section>
      );
  }

  const statusContent = (() => {
    if (provider === 'lula' && order.paymentStatus === 'pending') {
      return {
        status: 'pending',
        tone: 'review' as const,
        label: 'Pending Lula approval',
        title: 'Your Lula application has been submitted',
        description: 'Your order is safely queued for manual review. Approval can take up to 24 hours, so there is nothing else you need to keep open on this page.',
        icon: <FileText className="size-5" />,
        primaryHref: '/orders',
        primaryLabel: 'View my orders',
        secondaryHref: '/dashboard',
        secondaryLabel: 'Go to dashboard',
        detailTitle: 'What happens next',
        detailLines: [
          'Lula reviews your application and invoice details.',
          'They may contact you by email or phone if they need anything else.',
          'Your portal will update once the payment decision is ready.',
        ],
      };
    }

    if (order.paymentStatus === 'paid') {
      return {
        status: 'success',
        tone: 'success' as const,
        label: 'Payment confirmed',
        title: "You're all set!",
        description: linkedCampaignAction
          ? `Your payment was successfully processed. Next, ${linkedCampaignAction.label.toLowerCase()}.`
          : 'Your payment was successfully processed. Your package is now active and ready to use.',
        icon: <BadgeCheck className="size-5" />,
        primaryHref: linkedCampaignAction?.href ?? '/dashboard',
        primaryLabel: linkedCampaignAction?.label ?? 'Go to dashboard',
        secondaryHref: order.invoicePdfUrl ? advertifiedApi.toAbsoluteApiUrl(order.invoicePdfUrl) ?? '/orders' : '/orders',
        secondaryLabel: order.invoicePdfUrl ? 'View receipt' : 'Open my orders',
      };
    }

    if (order.paymentStatus === 'failed') {
      return {
        status: 'failed',
        tone: 'failed' as const,
        label: 'Payment not confirmed',
        title: 'Something went wrong',
        description: vodaPayReturnData?.responseMessage
          ? `VodaPay returned: ${vodaPayReturnData.responseMessage}. You can retry from the packages page or review the order in your dashboard.`
          : 'VodaPay did not confirm this payment. You can retry from the packages page or review the order in your dashboard.',
        icon: <CircleAlert className="size-5" />,
        primaryHref: '/packages',
        primaryLabel: 'Choose a package again',
        secondaryHref: '/dashboard',
        secondaryLabel: 'Go to dashboard',
      };
    }

    if (provider === 'vodapay' && vodaPayReturnData?.responseCode === '00') {
      return {
        status: 'pending',
        tone: 'loading' as const,
        label: 'Awaiting final confirmation',
        title: 'Payment received',
        description: "We're finalising confirmation with your payment provider.",
        icon: <Clock3 className="size-5" />,
        primaryHref: '/orders',
        primaryLabel: 'Check status',
        secondaryHref: '/dashboard',
        secondaryLabel: 'Go to dashboard',
      };
    }

    return {
      status: 'pending',
      tone: 'loading' as const,
      label: 'Awaiting confirmation',
      title: 'Processing your payment',
      description: "Your payment is being verified with VodaPay. This usually takes a few seconds. Please don't close this page.",
      icon: <Clock3 className="size-5" />,
      primaryHref: '/orders',
      primaryLabel: 'Check status',
      secondaryHref: '/dashboard',
      secondaryLabel: 'Go to dashboard',
    };
  })();

  const paymentMethodLabel = provider === 'lula' ? 'Pay Later' : 'Pay Now';
  const paymentMethodCaption = provider === 'lula' ? 'Powered by Lula' : 'Powered by VodaPay';
  const lulaNextStepMessage = provider === 'lula' && order.paymentStatus === 'pending'
    ? `Review can take up to 24 hours. Lula will contact you on ${user.email}, ${user.phone ?? 'your registered cellphone'} if they need anything else to complete the approval.`
    : null;
  const showPendingSpinner = statusContent.tone === 'loading';
  const showStatusRings = statusContent.tone !== 'loading';

  return (
    <section className={`checkout-status-page-shell checkout-status-page-${statusContent.status}`}>
      <div className="checkout-status-card">
        <div className="checkout-status-brand-strip checkout-status-animate checkout-status-delay-0">
          <div className="checkout-status-brand-card">
            <span>{paymentMethodLabel}</span>
            <span aria-hidden="true">•</span>
            <span>{paymentMethodCaption}</span>
          </div>
          <div className="checkout-status-brand-separator" />
          <img src={advertifiedLogo} alt="Advertified logo" className="checkout-status-brand-logo checkout-status-brand-logo-advertified" />
        </div>

        <div className="checkout-status-icon-wrap checkout-status-animate checkout-status-delay-0">
          {showStatusRings ? (
            <>
              <div className="checkout-status-ring" />
              <div className="checkout-status-ring" />
              <div className="checkout-status-ring" />
            </>
          ) : null}
          <div className="checkout-status-icon-circle">
            {showPendingSpinner ? (
              <div className="checkout-status-spinner" aria-hidden="true" />
            ) : (
              <div className="checkout-status-icon">{statusContent.icon}</div>
            )}
          </div>
        </div>

        <div className="checkout-status-badge checkout-status-animate checkout-status-delay-60">
          <span className={`checkout-status-badge-dot ${showPendingSpinner ? 'checkout-status-badge-dot-pending' : ''}`} />
          {statusContent.label}
        </div>

        {lulaNextStepMessage ? (
          <div className="checkout-status-next-step checkout-status-animate checkout-status-delay-110">
            <div className="checkout-status-next-step-head">Next Step</div>
            <p className="checkout-status-next-step-copy">{lulaNextStepMessage}</p>
          </div>
        ) : null}

        <h1 className="checkout-status-title checkout-status-animate checkout-status-delay-110">{statusContent.title}</h1>
        <p className="checkout-status-description checkout-status-animate checkout-status-delay-150">{statusContent.description}</p>

        {'detailLines' in statusContent && statusContent.detailLines?.length ? (
          <div className="checkout-status-next-step checkout-status-animate checkout-status-delay-150">
            <div className="checkout-status-next-step-head">{statusContent.detailTitle}</div>
            <div className="space-y-2">
              {statusContent.detailLines.map((line) => (
                <p key={line} className="checkout-status-next-step-copy">{line}</p>
              ))}
            </div>
          </div>
        ) : null}

        <div className="checkout-status-summary-panel checkout-status-animate checkout-status-delay-200">
          <div className="checkout-status-summary-head">Order Summary</div>
          <div className="checkout-status-summary-entry">
            <span className="checkout-status-summary-entry-label">Package</span>
            <span className="checkout-status-summary-pill">{order.packageBandName}</span>
          </div>
          <div className="checkout-status-summary-entry">
            <span className="checkout-status-summary-entry-label">Total</span>
            <span className="checkout-status-summary-entry-value">
              {formatCurrency(order.amount, order.currency)}
            </span>
          </div>
          <div className="checkout-status-summary-entry">
            <span className="checkout-status-summary-entry-label">Payment method</span>
            <span className="checkout-status-summary-entry-value">{paymentMethodLabel}</span>
          </div>
          <div className="checkout-status-summary-entry">
            <span className="checkout-status-summary-entry-label">Date / Time</span>
            <span className="checkout-status-summary-entry-value">
              {formatDateTime(order.createdAt)}
            </span>
          </div>
          <div className="checkout-status-summary-entry">
            <span className="checkout-status-summary-entry-label">Reference</span>
            <span className="checkout-status-summary-entry-value checkout-status-summary-entry-mono">
              {order.paymentReference ?? 'Awaiting provider reference'}
            </span>
          </div>
        </div>

        <div className="checkout-status-button-row checkout-status-animate checkout-status-delay-250">
          {effectiveProposalPath ? (
            <Link to={effectiveProposalPath} className="checkout-status-button checkout-status-button-ghost">
              Back to proposal
            </Link>
          ) : null}
          {statusContent.secondaryHref.startsWith('http') ? (
            <a href={statusContent.secondaryHref} target="_blank" rel="noreferrer" className="checkout-status-button checkout-status-button-ghost">
              {statusContent.secondaryLabel}
            </a>
          ) : (
            <Link to={statusContent.secondaryHref} className="checkout-status-button checkout-status-button-ghost">
              {statusContent.secondaryLabel}
            </Link>
          )}
          <Link to={statusContent.primaryHref} className="checkout-status-button checkout-status-button-primary">
            {statusContent.primaryLabel}
          </Link>
        </div>

        <div className="checkout-status-divider checkout-status-animate checkout-status-delay-290" />

        <p className="checkout-status-help checkout-status-animate checkout-status-delay-310">
          Need help? <a href="mailto:support@advertified.com">Contact support</a>
        </p>
      </div>
    </section>
  );
}

function parseVodaPayReturnData(encodedValue: string | null): VodaPayReturnData | null {
  if (!encodedValue) {
    return null;
  }

  try {
    const normalized = encodedValue.replace(/-/g, '+').replace(/_/g, '/');
    const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), '=');
    const decoded = window.atob(padded);
    return JSON.parse(decoded) as VodaPayReturnData;
  } catch {
    return null;
  }
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('en-ZA', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value));
}

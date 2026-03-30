import { useQuery } from '@tanstack/react-query';
import { useMemo, useRef } from 'react';
import { BadgeCheck, CircleAlert, Clock3, FileText } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import advertifiedLogo from '../../assets/advertified-logo-v3.png';
import lulaLogo from '../../assets/lula.png';
import vodaLogo from '../../assets/voda.jpeg';
import { LoadingState } from '../../components/ui/LoadingState';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { getCampaignPrimaryAction } from '../../lib/access';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';

type VodaPayReturnData = {
  responseCode?: string;
  responseMessage?: string;
  transactionId?: string;
};

export function CheckoutConfirmationPage() {
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  const { pushToast } = useToast();
  const orderId = searchParams.get('orderId') ?? '';
  const provider = (searchParams.get('provider') ?? '').toLowerCase();
  const callbackCapturedRef = useRef(false);
  const statusToastKeyRef = useRef<string | null>(null);
  if (provider === 'vodapay' && orderId && !callbackCapturedRef.current) {
    const queryParameters = Object.fromEntries(searchParams.entries());
    callbackCapturedRef.current = true;
    void advertifiedApi.captureVodaPayCallback(orderId, queryParameters);
  }

  const orderQuery = useQuery({
    queryKey: ['package-order', orderId, user?.id],
    queryFn: () => advertifiedApi.getOrder(orderId, user!.id),
    enabled: Boolean(orderId && user?.id),
    refetchInterval: (query) => (provider === 'lula' ? false : query.state.data?.paymentStatus === 'pending' ? 4000 : false),
  });
  const order = orderQuery.data;
  const campaignsQuery = useQuery({
    queryKey: ['campaigns', user?.id],
    queryFn: () => advertifiedApi.getCampaigns(user!.id),
    enabled: Boolean(user?.id && order?.paymentStatus === 'paid'),
  });
  const vodaPayReturnData = useMemo(() => parseVodaPayReturnData(searchParams.get('data')), [searchParams]);

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
            <Link to="/login" className="button-primary px-5 py-3">Log in</Link>
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

  const linkedCampaign = (campaignsQuery.data ?? []).find((campaign) => campaign.packageOrderId === order.id);
  const linkedCampaignAction = linkedCampaign ? getCampaignPrimaryAction(linkedCampaign) : null;

  const statusKey = `${order.id}:${order.paymentStatus}:${vodaPayReturnData?.responseCode ?? ''}:${vodaPayReturnData?.responseMessage ?? ''}`;
  if (statusToastKeyRef.current !== statusKey) {
    if (order.paymentStatus === 'paid') {
      pushToast({
        title: 'Payment successful.',
        description: `Your ${order.packageBandName} package has been paid successfully.`,
      });
      statusToastKeyRef.current = statusKey;
    } else if (order.paymentStatus === 'failed') {
      const reason = vodaPayReturnData?.responseMessage?.trim();
      pushToast({
        title: reason ? `Payment failed due to ${reason.toLowerCase()}.` : 'Payment failed.',
        description: 'No money was confirmed for this order. You can try again when you are ready.',
      }, 'error');
      statusToastKeyRef.current = statusKey;
    } else if (provider === 'vodapay' && vodaPayReturnData?.responseCode === '00') {
      pushToast({
        title: "We're finalising your payment confirmation.",
        description: 'Your payment provider has returned you to Advertified while we wait for final validation.',
      }, 'info');
      statusToastKeyRef.current = statusKey;
    }
  }

  const statusContent = (() => {
    if (provider === 'lula' && order.paymentStatus === 'pending') {
      return {
        status: 'pending',
        label: 'Pending Lula approval',
        title: 'Your order is pending Lula approval',
        description: 'Your invoice has been created and your Lula application is now waiting for manual review.',
        icon: <FileText className="size-5" />,
        colorVar: 'var(--color-highlight)',
        primaryHref: '/orders',
        primaryLabel: 'View my orders',
        secondaryHref: '/dashboard',
        secondaryLabel: 'Go to dashboard',
      };
    }

    if (order.paymentStatus === 'paid') {
      return {
        status: 'success',
        label: 'Payment confirmed',
        title: "You're all set!",
        description: linkedCampaignAction
          ? `Your payment was successfully processed. Next, ${linkedCampaignAction.label.toLowerCase()}.`
          : 'Your payment was successfully processed. Your package is now active and ready to use.',
        icon: <BadgeCheck className="size-5" />,
        colorVar: 'var(--color-highlight)',
        primaryHref: linkedCampaignAction?.href ?? '/dashboard',
        primaryLabel: linkedCampaignAction?.label ?? 'Go to dashboard',
        secondaryHref: order.invoicePdfUrl ? advertifiedApi.toAbsoluteApiUrl(order.invoicePdfUrl) ?? '/orders' : '/orders',
        secondaryLabel: order.invoicePdfUrl ? 'View receipt' : 'Open my orders',
      };
    }

    if (order.paymentStatus === 'failed') {
      return {
        status: 'failed',
        label: 'Payment not confirmed',
        title: 'Something went wrong',
        description: vodaPayReturnData?.responseMessage
          ? `VodaPay returned: ${vodaPayReturnData.responseMessage}. You can retry from the packages page or review the order in your dashboard.`
          : 'VodaPay did not confirm this payment. You can retry from the packages page or review the order in your dashboard.',
        icon: <CircleAlert className="size-5" />,
        colorVar: '#dc2626',
        primaryHref: '/packages',
        primaryLabel: 'Choose a package again',
        secondaryHref: '/dashboard',
        secondaryLabel: 'Go to dashboard',
      };
    }

    if (provider === 'vodapay' && vodaPayReturnData?.responseCode === '00') {
      return {
        status: 'pending',
        label: 'Awaiting final confirmation',
        title: 'Payment received',
        description: "We're finalising confirmation with your payment provider.",
        icon: <Clock3 className="size-5" />,
        colorVar: '#d97706',
        primaryHref: '/orders',
        primaryLabel: 'Check status',
        secondaryHref: '/dashboard',
        secondaryLabel: 'Go to dashboard',
      };
    }

    return {
      status: 'pending',
      label: 'Awaiting confirmation',
      title: 'Processing your payment',
      description: "Your payment is being verified with VodaPay. This usually takes a few seconds. Please don't close this page.",
      icon: <Clock3 className="size-5" />,
      colorVar: '#d97706',
      primaryHref: '/orders',
      primaryLabel: 'Check status',
      secondaryHref: '/dashboard',
      secondaryLabel: 'Go to dashboard',
    };
  })();

  const providerArtwork = provider === 'lula'
    ? { src: lulaLogo, alt: 'Lula logo', label: 'Paid through Lula' }
    : { src: vodaLogo, alt: 'VodaPay logo', label: 'Paid through VodaPay' };
  const lulaNextStepMessage = provider === 'lula' && order.paymentStatus === 'pending'
    ? `Your order is pending Lula Approval. Lula will contact you on ${user.email}, ${user.phone ?? 'your registered cellphone'} to process your application further.`
    : null;

  return (
    <section className={`checkout-status-page-shell checkout-status-page-${statusContent.status}`}>
      <div className="checkout-status-card">
        <div className="checkout-status-brand-strip checkout-status-animate checkout-status-delay-0">
          <div className="checkout-status-brand-card">
            <img src={providerArtwork.src} alt={providerArtwork.alt} className="checkout-status-brand-logo" />
            <span>{providerArtwork.label}</span>
          </div>
          <div className="checkout-status-brand-separator" />
          <img src={advertifiedLogo} alt="Advertified logo" className="checkout-status-brand-logo checkout-status-brand-logo-advertified" />
        </div>

        <div className="checkout-status-icon-wrap checkout-status-animate checkout-status-delay-0">
          {statusContent.status !== 'pending' ? (
            <>
              <div className="checkout-status-ring" />
              <div className="checkout-status-ring" />
              <div className="checkout-status-ring" />
            </>
          ) : null}
          <div className="checkout-status-icon-circle">
            {statusContent.status === 'pending' ? (
              <div className="checkout-status-spinner" aria-hidden="true" />
            ) : (
              <div className="checkout-status-icon">{statusContent.icon}</div>
            )}
          </div>
        </div>

        <div className="checkout-status-badge checkout-status-animate checkout-status-delay-60">
          <span className={`checkout-status-badge-dot ${statusContent.status === 'pending' ? 'checkout-status-badge-dot-pending' : ''}`} />
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

        <div className="checkout-status-summary-panel checkout-status-animate checkout-status-delay-200">
          <div className="checkout-status-summary-head">Order Summary</div>
          <div className="checkout-status-summary-entry">
            <span className="checkout-status-summary-entry-label">Package</span>
            <span className="checkout-status-summary-pill">{order.packageBandName}</span>
          </div>
          <div className="checkout-status-summary-entry">
            <span className="checkout-status-summary-entry-label">Total</span>
            <span className="checkout-status-summary-entry-value">
              {order.paymentStatus === 'paid' ? formatCurrency(order.amount, order.currency) : 'Not paid'}
            </span>
          </div>
          <div className="checkout-status-summary-entry">
            <span className="checkout-status-summary-entry-label">Payment Provider</span>
            <span className="checkout-status-summary-entry-value">{order.paymentProvider}</span>
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

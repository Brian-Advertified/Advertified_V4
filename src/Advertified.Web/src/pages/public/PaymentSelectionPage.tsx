import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Lock } from 'lucide-react';
import * as React from 'react';
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import lulaLogo from '../../assets/lula.png';
import vodaLogo from '../../assets/voda.jpeg';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { PageHero } from '../../components/marketing/PageHero';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { canBuyPackage } from '../../lib/access';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { PaymentProvider } from '../../types/domain';

type ProviderOption = {
  id: PaymentProvider;
  name: string;
  caption: string;
  description: string;
  assetSrc: string;
  assetAlt: string;
};

const providerOptions: ProviderOption[] = [
  {
    id: 'vodapay',
    name: 'VodaPay',
    caption: 'Wallet and mobile checkout',
    description: 'Redirect customers to a live hosted checkout with branded payment instructions.',
    assetSrc: vodaLogo,
    assetAlt: 'VodaPay logo',
  },
  {
    id: 'lula',
    name: 'Lula',
    caption: 'Business account payment',
    description: 'Create a downloadable invoice that the admin team can send to Lula manually.',
    assetSrc: lulaLogo,
    assetAlt: 'Lula logo',
  },
];

export function PaymentSelectionPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { pushToast } = useToast();
  const packageBandId = searchParams.get('packageBandId') ?? '';
  const amount = Number(searchParams.get('amount') ?? '0');
  const selectedArea = searchParams.get('area') ?? 'gauteng';
  const [selectedProvider, setSelectedProvider] = React.useState<PaymentProvider>('vodapay');
  const [isResendingActivation, setIsResendingActivation] = React.useState(false);

  const packagesQuery = useQuery({ queryKey: ['packages'], queryFn: advertifiedApi.getPackages });
  const selectedBand = packagesQuery.data?.find((item) => item.id === packageBandId);
  const chargedAmount = amount;

  const checkoutMutation = useMutation({
    mutationFn: async (paymentProvider: PaymentProvider) => {
      if (!user) {
        throw new Error('Create an account or log in before choosing a payment method.');
      }

      if (!selectedBand) {
        throw new Error('Choose a package before selecting payment.');
      }

      const checkout = await advertifiedApi.createOrder(user.id, selectedBand.id, amount, paymentProvider);
      if (!checkout.checkoutUrl) {
        if (paymentProvider === 'lula') {
          navigate(`/checkout/confirmation?provider=lula&orderId=${encodeURIComponent(checkout.order.id)}`);
          return checkout;
        }

        throw new Error('VodaPay did not return a checkout URL.');
      }

      window.location.assign(checkout.checkoutUrl);
      return checkout;
    },
    onError: (error) => {
      pushToast({
        title: selectedProvider === 'lula' ? 'We could not create the Lula invoice.' : 'We could not start the payment.',
        description: error instanceof Error ? error.message : 'Please try again in a moment.',
      }, 'error');
    },
  });

  if (!packageBandId || !Number.isFinite(amount) || amount <= 0) {
    return <Navigate to="/packages" replace />;
  }

  if (packagesQuery.isLoading) {
    return <LoadingState label="Loading payment options..." />;
  }

  if (!selectedBand) {
    return <Navigate to="/packages" replace />;
  }

  return (
    <section className="page-shell space-y-8 pb-20">
      {checkoutMutation.isPending || isResendingActivation ? (
        <ProcessingOverlay
          label={
            isResendingActivation
              ? 'Sending a fresh activation email...'
              : selectedProvider === 'lula'
                ? 'Creating your Lula invoice...'
                : 'Starting your VodaPay checkout...'
          }
        />
      ) : null}
      <PageHero
        kicker="Payment"
        title="Choose your payment method."
        description={`Select the provider you want to use for ${selectedBand.name} at ${formatCurrency(chargedAmount)}.`}
        actions={(
          <Link
            to={`/packages?band=${encodeURIComponent(selectedBand.code)}`}
            className="hero-secondary-button rounded-full font-semibold"
          >
            <ArrowLeft className="size-4" />
            Back to package selection
          </Link>
        )}
      />

      <div className="grid gap-6 lg:grid-cols-[1.05fr_0.95fr]">
        <div className="space-y-4">
          {providerOptions.map((provider) => {
            const selected = provider.id === selectedProvider;

            return (
              <button
                key={provider.id}
                type="button"
                className={`payment-option-card w-full text-left ${selected ? 'payment-option-card-selected' : ''}`}
                onClick={() => setSelectedProvider(provider.id)}
              >
                <div className="flex items-center gap-4">
                  <div className="payment-option-logo-shell">
                    <img src={provider.assetSrc} alt={provider.assetAlt} className="payment-option-logo" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <p className="text-lg font-semibold tracking-tight text-ink">{provider.name}</p>
                        <p className="text-sm text-ink-soft">{provider.caption}</p>
                      </div>
                      <div className={`payment-option-indicator ${selected ? 'payment-option-indicator-selected' : ''}`} />
                    </div>
                    <p className="mt-3 text-sm leading-7 text-ink-soft">{provider.description}</p>
                  </div>
                </div>
              </button>
            );
          })}
        </div>

        <aside className="package-preview-tab space-y-5">
          <div>
            <p className="package-preview-tab-label">Order summary</p>
            <h2 className="mt-2 text-2xl font-semibold tracking-tight text-ink">{selectedBand.name}</h2>
            <p className="mt-1 text-sm text-ink-soft">{formatCurrency(chargedAmount)} in the {selectedArea.replace('-', ' ')} planning area.</p>
          </div>

          <div className="space-y-3 rounded-[20px] border border-line bg-white px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand">What happens next</p>
            <p className="text-sm leading-7 text-ink-soft">
              {selectedProvider === 'lula'
                ? 'We will create and save a downloadable invoice for this order so the admin team can route it to Lula manually.'
                : 'We will open your selected provider, confirm the transaction, then bring you back to Advertified to continue your campaign setup.'}
            </p>
          </div>

          {!user ? (
            <div className="space-y-3">
              <Link to="/register" className="button-primary flex items-center justify-center gap-2 px-5 py-3">
                Register to continue
              </Link>
              <Link to="/login" className="button-secondary flex items-center justify-center gap-2 px-5 py-3">
                Log in
              </Link>
            </div>
          ) : !canBuyPackage(user) ? (
            <div className="space-y-4 rounded-[20px] border border-amber-200 bg-amber-50 px-4 py-4">
              <div className="flex items-start gap-3 text-sm text-amber-800">
                <Lock className="mt-0.5 size-4 shrink-0" />
                <span>Verify your email before choosing a payment method.</span>
              </div>
              <button
                type="button"
                className="button-primary w-full px-5 py-3"
                onClick={async () => {
                  try {
                    setIsResendingActivation(true);
                    await advertifiedApi.resendVerification(user.email);
                    pushToast({
                      title: 'A fresh activation email is on its way.',
                      description: 'Check your inbox for the new activation link.',
                    });
                    navigate(`/verify-email?email=${encodeURIComponent(user.email)}`);
                  } finally {
                    setIsResendingActivation(false);
                  }
                }}
                disabled={isResendingActivation}
              >
                {isResendingActivation ? 'Resending activation...' : 'Resend activation'}
              </button>
            </div>
          ) : (
            <button
              type="button"
              disabled={checkoutMutation.isPending}
              onClick={() => checkoutMutation.mutate(selectedProvider)}
              className="button-primary flex w-full items-center justify-center gap-2 px-5 py-3 disabled:opacity-60"
            >
              {checkoutMutation.isPending
                ? `${selectedProvider === 'lula' ? 'Creating Lula invoice' : 'Starting VodaPay'}...`
                : `${selectedProvider === 'lula' ? 'Create Lula invoice' : 'Continue with VodaPay'}`}
              <ArrowRight className="size-4" />
            </button>
          )}
        </aside>
      </div>
    </section>
  );
}

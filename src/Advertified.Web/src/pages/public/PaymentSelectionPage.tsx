import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Lock } from 'lucide-react';
import * as React from 'react';
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { PageHero } from '../../components/marketing/PageHero';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { canBuyPackage, getPackagePurchaseRestriction } from '../../lib/access';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { PackageBand, PaymentProvider } from '../../types/domain';

type ProviderOption = {
  id: PaymentProvider;
  name: string;
  caption: string;
  description: string;
};

const providerOptions: ProviderOption[] = [
  {
    id: 'lula',
    name: 'Pay Later',
    caption: 'This transaction is powered by Lula',
    description: 'Create a downloadable invoice that the admin team can send to Lula manually.',
  },
  {
    id: 'vodapay',
    name: 'Pay Now',
    caption: 'This transaction is powered by VodaPay',
    description: 'Redirect customers to a live hosted checkout to complete payment immediately.',
  },
];

export function PaymentSelectionPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const purchaseRestriction = getPackagePurchaseRestriction(user);
  const { pushToast } = useToast();
  const orderId = searchParams.get('orderId')?.trim() ?? '';
  const campaignId = searchParams.get('campaignId')?.trim() ?? '';
  const recommendationId = searchParams.get('recommendationId')?.trim() ?? '';
  const packageBandId = searchParams.get('packageBandId') ?? '';
  const amount = Number(searchParams.get('amount') ?? '0');
  const selectedArea = searchParams.get('area') ?? 'gauteng';
  const isExistingOrderCheckout = orderId.length > 0;
  const [selectedProvider, setSelectedProvider] = React.useState<PaymentProvider>('lula');
  const [isResendingActivation, setIsResendingActivation] = React.useState(false);

  const packagesQuery = useQuery({ queryKey: ['packages'], queryFn: advertifiedApi.getPackages });
  const existingOrderQuery = useQuery({
    queryKey: ['package-order', user?.id, orderId],
    queryFn: () => advertifiedApi.getOrder(orderId, user!.id),
    enabled: Boolean(user && isExistingOrderCheckout),
  });
  const selectedBand = isExistingOrderCheckout
    ? packagesQuery.data?.find((item) => item.id === existingOrderQuery.data?.packageBandId)
    : packagesQuery.data?.find((item) => item.id === packageBandId);
  const chargedAmount = isExistingOrderCheckout ? (existingOrderQuery.data?.amount ?? 0) : amount;

  const checkoutMutation = useMutation({
    mutationFn: async (paymentProvider: PaymentProvider) => {
      if (!user) {
        throw new Error('Create an account or log in before choosing a payment method.');
      }

      if (!selectedBand) {
        throw new Error('Choose a package before selecting payment.');
      }

      const checkout = isExistingOrderCheckout
        ? await advertifiedApi.initiateOrderCheckout(user.id, orderId, paymentProvider)
        : await advertifiedApi.createOrder(user.id, selectedBand.id, amount, paymentProvider);
      if (!checkout.checkoutUrl) {
        if (paymentProvider === 'lula') {
          const lulaConfirmationUrl = campaignId
            ? `/checkout/confirmation?provider=lula&orderId=${encodeURIComponent(checkout.order.id)}&campaignId=${encodeURIComponent(campaignId)}${recommendationId ? `&recommendationId=${encodeURIComponent(recommendationId)}` : ''}`
            : `/checkout/confirmation?provider=lula&orderId=${encodeURIComponent(checkout.order.id)}`;
          navigate(lulaConfirmationUrl);
          return checkout;
        }

        throw new Error('VodaPay did not return a checkout URL.');
      }

      if (recommendationId && typeof window !== 'undefined') {
        const payload = JSON.stringify({
          campaignId,
          recommendationId,
        });
        window.sessionStorage.setItem(`advertified:auto-approve:${checkout.order.id}`, payload);
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

  if (!isExistingOrderCheckout && (!packageBandId || !Number.isFinite(amount) || amount <= 0)) {
    return <Navigate to="/packages" replace />;
  }

  if (packagesQuery.isLoading || (isExistingOrderCheckout && existingOrderQuery.isLoading)) {
    return <LoadingState label="Loading payment options..." />;
  }

  if (isExistingOrderCheckout && (existingOrderQuery.isError || !existingOrderQuery.data)) {
    return <Navigate to={campaignId ? `/campaigns/${campaignId}/approvals` : '/dashboard'} replace />;
  }

  if (!selectedBand || chargedAmount <= 0) {
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
            to={campaignId ? `/campaigns/${campaignId}/approvals` : `/packages?band=${encodeURIComponent(selectedBand.code)}`}
            className="hero-secondary-button rounded-full font-semibold"
          >
            <ArrowLeft className="size-4" />
            {campaignId ? 'Back to campaign approvals' : 'Back to package selection'}
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
                <div className="min-w-0">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <p className="text-lg font-semibold tracking-tight text-ink">{provider.name}</p>
                      <p className="text-sm text-ink-soft">{provider.caption}</p>
                    </div>
                    <div className={`payment-option-indicator ${selected ? 'payment-option-indicator-selected' : ''}`} />
                  </div>
                  <p className="mt-3 text-sm leading-7 text-ink-soft">{provider.description}</p>
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

          <div className="space-y-3 rounded-[20px] border border-brand/20 bg-brand/[0.06] px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand">AI Studio included</p>
            <div className="flex flex-wrap gap-2">
              {getAiStudioCapabilities(selectedBand).map((item) => (
                <span
                  key={item}
                  className="rounded-full border border-brand/25 bg-white px-3 py-1.5 text-xs font-semibold text-brand"
                >
                  {item}
                </span>
              ))}
            </div>
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
                <span>
                  {purchaseRestriction === 'identity_incomplete'
                    ? 'Complete your identity details before choosing a payment method.'
                    : 'Verify your email before choosing a payment method.'}
                </span>
              </div>
              {purchaseRestriction === 'identity_incomplete' ? (
                <a
                  href="mailto:support@advertified.com?subject=Identity%20details%20required%20for%20checkout"
                  className="button-primary block w-full px-5 py-3 text-center"
                >
                  Contact support
                </a>
              ) : (
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
              )}
            </div>
          ) : (
            <button
              type="button"
              disabled={checkoutMutation.isPending}
              onClick={() => checkoutMutation.mutate(selectedProvider)}
              className="button-primary flex w-full items-center justify-center gap-2 px-5 py-3 disabled:opacity-60"
            >
              {checkoutMutation.isPending
                ? `${selectedProvider === 'lula' ? 'Preparing Pay Later' : 'Starting Pay Now'}...`
                : `${selectedProvider === 'lula' ? 'Continue with Pay Later' : 'Continue with Pay Now'}`}
              <ArrowRight className="size-4" />
            </button>
          )}
        </aside>
      </div>
    </section>
  );
}

function getAiStudioCapabilities(band: PackageBand) {
  const capabilities: string[] = [];
  const variantsLabel = band.maxAdVariants === 1 ? '1 ad variant' : `${band.maxAdVariants} ad variants`;
  capabilities.push(variantsLabel);

  if (band.allowedAdPlatforms.length > 0) {
    capabilities.push(`Platforms: ${band.allowedAdPlatforms.join(', ')}`);
  }

  if (band.allowedVoicePackTiers.length > 0) {
    capabilities.push(`Voice tiers: ${band.allowedVoicePackTiers.join(', ')}`);
  }

  capabilities.push(band.allowAdMetricsSync ? 'Metrics sync' : 'No metrics sync');
  capabilities.push(band.allowAdAutoOptimize ? 'Auto optimize' : 'Manual optimize');

  const regenerationsLabel = band.maxAdRegenerations === 1
    ? '1 regeneration'
    : `${band.maxAdRegenerations} regenerations`;
  capabilities.push(regenerationsLabel);

  return capabilities;
}

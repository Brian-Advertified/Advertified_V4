import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Lock } from 'lucide-react';
import * as React from 'react';
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { PageHero } from '../../components/marketing/PageHero';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { catalogQueryOptions } from '../../lib/catalogQueryOptions';
import { canBuyPackage, getPackagePurchaseRestriction } from '../../lib/access';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { writeCheckoutAutoApproval } from '../../services/checkoutAutoApprovalStore';
import type { PackageBand, PaymentProvider } from '../../types/domain';

type ProviderOption = {
  id: PaymentProvider;
  name: string;
  description: string;
};

const providerOptions: ProviderOption[] = [
  {
    id: 'lula',
    name: 'Pay Later',
    description: 'Apply for pay-later approval if your business meets eligibility requirements.',
  },
  {
    id: 'vodapay',
    name: 'Pay Now',
    description: 'Pay securely now and come straight back to Advertified when payment is complete.',
  },
];

const lulaEligibilityRequirements = [
  'have been operating for at least 1 year',
  'earn R40,000 or more per month',
  'are registered in South Africa',
  'fall within eligible private-sector business types',
] as const;

function extractRegistrationYear(registrationNumber?: string) {
  const match = registrationNumber?.trim().match(/^(\d{4})\//);
  return match ? Number(match[1]) : null;
}

export function PaymentSelectionPage() {
  const [searchParams] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const purchaseRestriction = getPackagePurchaseRestriction(user);
  const { pushToast } = useToast();
  const orderId = searchParams.get('orderId')?.trim() ?? '';
  const campaignId = searchParams.get('campaignId')?.trim() ?? '';
  const recommendationId = searchParams.get('recommendationId')?.trim() ?? '';
  const proposalPath = searchParams.get('proposalPath')?.trim() ?? '';
  const packageBandId = searchParams.get('packageBandId') ?? '';
  const amount = Number(searchParams.get('amount') ?? '0');
  const selectedArea = searchParams.get('area') ?? 'gauteng';
  const isExistingOrderCheckout = orderId.length > 0;
  const [selectedProvider, setSelectedProvider] = React.useState<PaymentProvider>('lula');
  const [isResendingActivation, setIsResendingActivation] = React.useState(false);
  const authNextPath = `${location.pathname}${location.search}`;
  const registrationYear = extractRegistrationYear(user?.registrationNumber);
  const currentYear = new Date().getFullYear();
  const lulaBlockedByRegistrationYear = registrationYear !== null && currentYear - registrationYear < 1;
  const lulaEligibilityWarning = lulaBlockedByRegistrationYear
    ? `Pay Later is unavailable because the company registration year is ${registrationYear}, which indicates the business has been trading for less than 1 year.`
    : null;

  const packagesQuery = useQuery({ queryKey: ['packages'], queryFn: advertifiedApi.getPackages, ...catalogQueryOptions });
  const existingOrderQuery = useQuery({
    queryKey: ['package-order', user?.id, orderId],
    queryFn: () => advertifiedApi.getOrder(orderId, user!.id),
    enabled: Boolean(user && isExistingOrderCheckout),
  });
  const campaignQuery = useQuery({
    queryKey: ['campaign', campaignId],
    queryFn: () => advertifiedApi.getCampaign(campaignId),
    enabled: Boolean(user && isExistingOrderCheckout && campaignId),
  });
  const selectedBand = isExistingOrderCheckout
    ? packagesQuery.data?.find((item) => item.id === existingOrderQuery.data?.packageBandId)
    : packagesQuery.data?.find((item) => item.id === packageBandId);
  const selectedRecommendation = campaignQuery.data
    ? (campaignQuery.data.recommendations.find((item) => item.id === recommendationId)
      ?? (campaignQuery.data.recommendation?.id === recommendationId ? campaignQuery.data.recommendation : undefined))
    : undefined;
  const chargedAmount = isExistingOrderCheckout
    ? (selectedRecommendation?.totalCost ?? existingOrderQuery.data?.amount ?? 0)
    : amount;

  React.useEffect(() => {
    if (selectedProvider === 'lula' && lulaBlockedByRegistrationYear) {
      setSelectedProvider('vodapay');
    }
  }, [lulaBlockedByRegistrationYear, selectedProvider]);

  const checkoutMutation = useMutation({
    mutationFn: async (paymentProvider: PaymentProvider) => {
      if (!user) {
        throw new Error('Create an account or log in before choosing a payment method.');
      }

      if (!selectedBand) {
        throw new Error('Choose a package before selecting payment.');
      }

      const checkout = isExistingOrderCheckout
        ? await advertifiedApi.initiateOrderCheckout(user.id, orderId, paymentProvider, recommendationId || undefined)
        : await advertifiedApi.createOrder(user.id, selectedBand.id, amount, paymentProvider);
      if (!checkout.checkoutUrl) {
        if (paymentProvider === 'lula') {
          const lulaConfirmationUrl = campaignId
            ? `/checkout/confirmation?provider=lula&orderId=${encodeURIComponent(checkout.order.id)}&campaignId=${encodeURIComponent(campaignId)}${recommendationId ? `&recommendationId=${encodeURIComponent(recommendationId)}` : ''}${proposalPath ? `&proposalPath=${encodeURIComponent(proposalPath)}` : ''}`
            : `/checkout/confirmation?provider=lula&orderId=${encodeURIComponent(checkout.order.id)}`;
          navigate(lulaConfirmationUrl);
          return checkout;
        }

        throw new Error('VodaPay did not return a checkout URL.');
      }

      if (recommendationId && typeof window !== 'undefined') {
        writeCheckoutAutoApproval(checkout.order.id, {
          campaignId,
          recommendationId,
          proposalPath,
        });
      }

      window.location.assign(checkout.checkoutUrl);
      return checkout;
    },
    onError: (error) => {
      pushToast({
        title: selectedProvider === 'lula' ? 'We could not create the invoice.' : 'We could not start the payment.',
        description: error instanceof Error ? error.message : 'Please try again in a moment.',
      }, 'error');
    },
  });

  if (!isExistingOrderCheckout && (!packageBandId || !Number.isFinite(amount) || amount <= 0)) {
    return <Navigate to="/packages" replace />;
  }

  if (packagesQuery.isLoading || (isExistingOrderCheckout && (existingOrderQuery.isLoading || campaignQuery.isLoading))) {
    return <LoadingState label="Loading payment options..." />;
  }

  if (isExistingOrderCheckout && (existingOrderQuery.isError || !existingOrderQuery.data)) {
    return <Navigate to={campaignId ? `/campaigns/${campaignId}/approvals` : '/dashboard'} replace />;
  }

  if (!selectedBand || chargedAmount <= 0) {
    return <Navigate to="/packages" replace />;
  }

  return (
    <section className="page-shell space-y-8 pb-28 md:pb-20">
      {checkoutMutation.isPending || isResendingActivation ? (
        <ProcessingOverlay
          label={
            isResendingActivation
              ? 'Sending a fresh activation email...'
              : selectedProvider === 'lula'
                ? 'Creating your invoice...'
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

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1.05fr_0.95fr] lg:gap-6">
        <div className="space-y-4">
          {providerOptions.map((provider) => {
            const selected = provider.id === selectedProvider;
            const lulaDisabled = provider.id === 'lula' && lulaBlockedByRegistrationYear;

            return (
              <button
                key={provider.id}
                type="button"
                aria-pressed={selected}
                aria-label={`Select ${provider.name}. ${provider.description}`}
                className={`payment-option-card w-full text-left min-h-[108px] px-4 py-4 sm:px-5 sm:py-5 ${selected ? 'payment-option-card-selected' : ''} ${lulaDisabled ? 'cursor-not-allowed opacity-60' : ''}`}
                onClick={() => {
                  if (!lulaDisabled) {
                    setSelectedProvider(provider.id);
                  }
                }}
                disabled={lulaDisabled}
              >
                <div className="min-w-0">
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <p className="text-lg font-semibold tracking-tight text-ink">{provider.name}</p>
                    </div>
                    <div className={`payment-option-indicator ${selected ? 'payment-option-indicator-selected' : ''}`} aria-hidden="true" />
                  </div>
                  <p className="mt-3 text-sm leading-7 text-ink-soft">{provider.description}</p>
                  {lulaDisabled && lulaEligibilityWarning ? (
                    <p className="mt-3 text-sm font-semibold leading-7 text-amber-800">{lulaEligibilityWarning}</p>
                  ) : null}
                </div>
              </button>
            );
          })}
        </div>

        <aside className="package-preview-tab space-y-5" role="complementary" aria-label="Order summary">
          <div>
            <p className="package-preview-tab-label">Order summary</p>
            <h2 className="mt-2 text-2xl font-semibold tracking-tight text-ink">{selectedBand.name}</h2>
            <p className="mt-1 text-sm text-ink-soft">{formatCurrency(chargedAmount)} in the {selectedArea.replace('-', ' ')} planning area.</p>
          </div>

          <div className="space-y-3 rounded-[20px] border border-line bg-white px-4 py-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand">What happens next</p>
            <p className="text-sm leading-7 text-ink-soft">
              {selectedProvider === 'lula'
                ? 'We will process application for review and show you the next steps while approval is pending.'
                : 'You will be taken to secure checkout. Once payment is complete, we will bring you back to Advertified automatically.'}
            </p>
            {selectedRecommendation ? (
              <p className="text-sm font-semibold leading-7 text-ink">
                You are paying the selected recommendation total: {formatCurrency(selectedRecommendation.totalCost)}.
              </p>
            ) : null}
          </div>

          {selectedProvider === 'lula' ? (
            <div className="space-y-3 rounded-[20px] border border-amber-200 bg-amber-50 px-4 py-4">
              <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-amber-800">Check if you qualify for Pay Later</p>
              <p className="text-sm leading-7 text-amber-900">
                Pay Later is designed for South African businesses that:
              </p>
              <ul className="space-y-2 text-sm leading-7 text-amber-800">
                {lulaEligibilityRequirements.map((item) => (
                  <li key={item}>• {item}</li>
                ))}
              </ul>
              <p className="text-sm leading-7 text-amber-900">
                If this option is not the right fit for your business, you can still complete your order with <strong>Pay Now</strong>.
              </p>
              {lulaEligibilityWarning ? (
                <p className="text-sm font-semibold leading-7 text-amber-900">
                  {lulaEligibilityWarning}
                </p>
              ) : null}
            </div>
          ) : null}

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
              <Link to={`/register?next=${encodeURIComponent(authNextPath)}`} className="button-primary flex items-center justify-center gap-2 px-5 py-3">
                Register to continue
              </Link>
              <Link to={`/login?next=${encodeURIComponent(authNextPath)}`} className="button-secondary flex items-center justify-center gap-2 px-5 py-3">
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
                      await advertifiedApi.resendVerification(user.email, authNextPath);
                      pushToast({
                        title: 'A fresh activation email is on its way.',
                        description: 'Check your inbox for the new activation link.',
                      });
                      navigate(`/verify-email?email=${encodeURIComponent(user.email)}&next=${encodeURIComponent(authNextPath)}`);
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

        {user && canBuyPackage(user) && (
          <div className="fixed inset-x-0 bottom-0 z-50 border-t border-line bg-white p-3 shadow-lg md:hidden">
            <button
              type="button"
              disabled={checkoutMutation.isPending}
              onClick={() => checkoutMutation.mutate(selectedProvider)}
              className="button-primary w-full py-4 text-base disabled:opacity-60"
            >
              {checkoutMutation.isPending
                ? `${selectedProvider === 'lula' ? 'Preparing Pay Later' : 'Starting Pay Now'}...`
                : `${selectedProvider === 'lula' ? 'Continue with Pay Later' : 'Continue with Pay Now'}`}
            </button>
          </div>
        )}
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

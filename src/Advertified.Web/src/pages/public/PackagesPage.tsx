import { useQuery } from '@tanstack/react-query';
import { ArrowRight, Lock } from 'lucide-react';
import { useDeferredValue, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { PageHero } from '../../components/marketing/PageHero';
import { BudgetSelector } from '../../features/packages/components/BudgetSelector';
import { PackageCard } from '../../features/packages/components/PackageCard';
import { SpendPreviewPanel } from '../../features/packages/components/SpendPreviewPanel';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../features/auth/auth-context';
import { advertifiedApi } from '../../services/advertifiedApi';
import { formatCurrency } from '../../lib/utils';
import { useToast } from '../../components/ui/toast';
import { canBuyPackage } from '../../lib/access';

export function PackagesPage() {
  const [searchParams] = useSearchParams();
  const packagesQuery = useQuery({ queryKey: ['packages'], queryFn: advertifiedApi.getPackages });
  const packageAreasQuery = useQuery({ queryKey: ['package-areas'], queryFn: advertifiedApi.getPackageAreas });
  const [selectedPackageIdState, setSelectedPackageIdState] = useState<string>();
  const { user } = useAuth();
  const navigate = useNavigate();
  const { pushToast } = useToast();
  const [stepState, setStepState] = useState<1 | 2>();
  const [selectedAreaState, setSelectedAreaState] = useState('');
  const [isResendingActivation, setIsResendingActivation] = useState(false);
  const scrolledSectionKeyRef = useRef<string | null>(null);
  const requestedBandCode = searchParams.get('band')?.trim().toLowerCase();
  const requestedBand = requestedBandCode
    ? packagesQuery.data?.find((item) => item.code.toLowerCase() === requestedBandCode)
    : undefined;
  const selectedPackageId = selectedPackageIdState ?? requestedBand?.id;

  const selectedBand = useMemo(
    () => packagesQuery.data?.find((item) => item.id === selectedPackageId) ?? packagesQuery.data?.[0],
    [packagesQuery.data, selectedPackageId],
  );
  const step = stepState ?? (requestedBand ? 2 : 1);
  const areaOptions = packageAreasQuery.data ?? [];
  const selectedArea = selectedAreaState
    || (user?.province && areaOptions.length > 0 ? mapProvinceToAreaCode(user.province, areaOptions) : '')
    || areaOptions.find((option) => option.code === 'national')?.code
    || areaOptions[0]?.code
    || 'gauteng';

  const [spendState, setSpendState] = useState<number>();
  const spend = spendState ?? selectedBand?.minBudget ?? 50_000;
  const clampedSpend = useMemo(() => {
    if (!selectedBand) {
      return spend;
    }

    return Math.min(selectedBand.maxBudget, Math.max(selectedBand.minBudget, spend));
  }, [selectedBand, spend]);
  const deferredSpend = useDeferredValue(clampedSpend);

  const effectivePreviewSpend = selectedBand
    ? Math.min(selectedBand.maxBudget, Math.max(selectedBand.minBudget, deferredSpend))
    : deferredSpend;

  const previewQuery = useQuery({
    queryKey: ['package-preview', selectedBand?.id, effectivePreviewSpend, selectedArea],
    queryFn: () => advertifiedApi.getPackagePreview(selectedBand!.id, effectivePreviewSpend, selectedArea),
    enabled: Boolean(
      selectedBand?.id
      && selectedArea
      && selectedBand
      && effectivePreviewSpend >= selectedBand.minBudget
      && effectivePreviewSpend <= selectedBand.maxBudget,
    ),
  });

  const showMobilePackageGrid = step === 1;

  if (packagesQuery.isLoading) {
    return <LoadingState label="Loading package bands..." />;
  }

  return (
    <section className="page-shell space-y-8 pb-36">
      {isResendingActivation ? <ProcessingOverlay label="Sending a fresh activation email..." /> : null}
      <PageHero
        kicker="Packages"
        title="Choose your package, then set the exact spend that fits your campaign."
        description="One decision at a time. Pick the right band first, then choose the spend you want to invest."
      />

      <div className="flex flex-wrap gap-3">
        <div className={`pill ${step === 1 ? 'bg-brand text-white border-brand' : ''}`}>Step 1: Choose package</div>
        <div className={`pill ${step === 2 ? 'bg-brand text-white border-brand' : ''}`}>Step 2: Choose spend</div>
      </div>

      {selectedBand && step === 2 ? (
        <div className="panel flex items-center justify-between gap-4 px-5 py-5 sm:hidden">
          <div className="min-w-0">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Selected package</p>
            <p className="mt-2 text-xl font-semibold tracking-tight text-ink">{selectedBand.name}</p>
            <p className="mt-1 text-sm text-ink-soft">
              {formatCurrency(selectedBand.minBudget)} - {formatCurrency(selectedBand.maxBudget)}
            </p>
          </div>
            <button
            type="button"
            className="button-secondary shrink-0 px-4 py-2"
            onClick={() => setStepState(1)}
          >
            Change package
          </button>
        </div>
      ) : null}

      <div className={`${showMobilePackageGrid ? 'card-grid' : 'hidden card-grid sm:grid'}`}>
          {packagesQuery.data?.map((band) => (
            <PackageCard
              key={band.id}
              band={band}
              selected={band.id === selectedBand?.id}
              onSelect={() => {
                setSelectedPackageIdState(band.id);
                setSpendState(band.minBudget);
                setStepState(2);
              }}
            />
          ))}
      </div>

      {selectedBand && step === 2 ? (
        <div
          ref={(node) => {
            if (!node) {
              return;
            }

            const sectionKey = `${selectedBand.id}:${step}`;
            if (scrolledSectionKeyRef.current === sectionKey) {
              return;
            }

            scrolledSectionKeyRef.current = sectionKey;
            node.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }}
          className="grid gap-6 xl:grid-cols-[1.08fr_0.92fr]"
        >
          <div className="space-y-4">
            <BudgetSelector
              band={selectedBand}
              value={clampedSpend}
              preview={previewQuery.data}
              selectedArea={selectedArea}
              areaOptions={areaOptions}
              onAreaChange={setSelectedAreaState}
              onChange={(value) => setSpendState(Math.min(selectedBand.maxBudget, Math.max(selectedBand.minBudget, value || selectedBand.minBudget)))}
            />
          </div>
          <SpendPreviewPanel band={selectedBand} selectedSpend={clampedSpend} livePreview={previewQuery.data} />
        </div>
      ) : null}

      {selectedBand && step === 2 ? (
        <div className="sticky bottom-4 z-20">
          <div className="mx-auto max-w-4xl rounded-[26px] border border-line bg-white/92 px-4 py-4 shadow-[0_24px_70px_rgba(17,24,39,0.14)] backdrop-blur">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
              <div className="min-w-0">
                <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-brand">Your package</p>
                <p className="mt-1 text-lg font-semibold tracking-tight text-ink">
                  {selectedBand.name} <span className="text-ink-soft">at {formatCurrency(clampedSpend)}</span>
                </p>
                <p className="mt-1 text-sm text-ink-soft">Final plan confirmed after payment and brief submission.</p>
              </div>

              {!user ? (
                <div className="flex flex-col gap-3 md:flex-row">
                  <Link to="/register" className="button-primary px-5 py-3 text-center">Register to continue</Link>
                  <Link to="/login" className="button-secondary px-5 py-3 text-center">Log in</Link>
                </div>
              ) : !canBuyPackage(user) ? (
                <div className="flex flex-col gap-3 md:flex-row md:items-center">
                  <div className="flex items-start gap-2 text-sm text-amber-700">
                    <Lock className="mt-0.5 size-4 shrink-0" />
                    <span>Verify your email before you can buy this package.</span>
                  </div>
                  <button
                    type="button"
                    className="button-primary px-5 py-3"
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
                  onClick={() =>
                    navigate(
                      `/checkout/payment?packageBandId=${encodeURIComponent(selectedBand.id)}&amount=${encodeURIComponent(String(clampedSpend))}&area=${encodeURIComponent(selectedArea)}`,
                    )
                  }
                  className="button-primary inline-flex items-center justify-center gap-2 px-5 py-3"
                >
                  Choose payment method
                  <ArrowRight className="size-4" />
                </button>
              )}
            </div>
          </div>
        </div>
      ) : null}
    </section>
  );
}

function mapProvinceToAreaCode(province: string, areaOptions: Array<{ code: string; label: string }>) {
  const normalizedProvince = normalizeAreaToken(province);
  const exactLabelMatch = areaOptions.find((option) => normalizeAreaToken(option.label) === normalizedProvince);
  if (exactLabelMatch) {
    return exactLabelMatch.code;
  }

  const codeMatch = areaOptions.find((option) => normalizeAreaToken(option.code) === normalizedProvince);
  if (codeMatch) {
    return codeMatch.code;
  }

  return areaOptions.find((option) => option.code === 'national')?.code ?? areaOptions[0]?.code ?? 'gauteng';
}

function normalizeAreaToken(value: string) {
  return value.trim().toLowerCase().replace(/[^a-z0-9]+/g, '');
}

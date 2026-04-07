import { useQuery } from '@tanstack/react-query';
import { useDeferredValue, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useSearchParams } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';
import { BudgetSelector } from '../../features/packages/components/BudgetSelector';
import { PackageCard } from '../../features/packages/components/PackageCard';
import { SpendPreviewPanel } from '../../features/packages/components/SpendPreviewPanel';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../features/auth/auth-context';
import { catalogQueryOptions } from '../../lib/catalogQueryOptions';
import { advertifiedApi } from '../../services/advertifiedApi';
import { formatCompactBudget } from '../../lib/utils';

export function PackagesPage() {
  const [searchParams] = useSearchParams();
  const packagesQuery = useQuery({ queryKey: ['packages'], queryFn: advertifiedApi.getPackages, ...catalogQueryOptions });
  const packageAreasQuery = useQuery({ queryKey: ['package-areas'], queryFn: advertifiedApi.getPackageAreas, ...catalogQueryOptions });
  const [selectedPackageIdState, setSelectedPackageIdState] = useState<string>();
  const { user } = useAuth();
  const [stepState, setStepState] = useState<1 | 2>();
  const [selectedAreaState, setSelectedAreaState] = useState('');
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
      <PageHero
        kicker="Packages"
        title="Choose your package, then set the exact spend that fits your campaign."
        description="If you already know your budget band, continue to payment. If you want guidance first, start the questionnaire and we will help shape the right campaign setup."
      />

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-[24px] border border-line bg-white px-5 py-5">
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">This route is best if</p>
          <p className="mt-3 text-sm leading-7 text-ink-soft">
            you already know the budget band you want and are comfortable continuing toward payment before planning starts.
          </p>
        </div>
        <div className="rounded-[24px] border border-brand/15 bg-brand-soft/25 px-5 py-5">
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Not sure yet?</p>
          <p className="mt-3 text-sm leading-7 text-ink-soft">
            Start the questionnaire first if you want Advertified to shape the brief and point you to the right route before checkout.
          </p>
          <Link to="/start-campaign" className="mt-4 inline-flex items-center gap-2 font-semibold text-brand">
            Start questionnaire
          </Link>
        </div>
      </div>

      <div className="flex flex-col gap-2 sm:flex-wrap sm:gap-3">
        <div className={`pill ${step === 1 ? 'bg-brand text-white border-brand' : ''}`}>Step 1: Choose package</div>
        <div className={`pill ${step === 2 ? 'bg-brand text-white border-brand' : ''}`}>Step 2: Choose spend</div>
      </div>

      {selectedBand && step === 2 ? (
        <div className="panel flex items-center justify-between gap-4 px-5 py-4 sm:hidden">
          <div className="min-w-0">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Selected package</p>
            <p className="mt-2 text-xl font-semibold tracking-tight text-ink">{selectedBand.name}</p>
            <p className="mt-1 text-sm text-ink-soft">
              {formatCompactBudget(selectedBand.minBudget)} - {formatCompactBudget(selectedBand.maxBudget)}
            </p>
          </div>
          <button
            type="button"
            className="button-secondary shrink-0 px-3 py-1.5 text-xs"
            onClick={() => setStepState(1)}
          >
            Change package
          </button>
        </div>
      ) : null}

      <div className={showMobilePackageGrid ? '' : 'hidden lg:block'}>
        <div className="card-grid">
          {packagesQuery.data?.map((band) => (
            <PackageCard
              key={band.id}
              band={band}
              selected={band.id === selectedBand?.id}
              onSelect={() => {
                if (typeof document !== 'undefined' && document.activeElement instanceof HTMLElement) {
                  document.activeElement.blur();
                }
                setSelectedPackageIdState(band.id);
                setSpendState(band.minBudget);
                setStepState(2);
              }}
            />
          ))}
        </div>
      </div>

      {selectedBand && step === 2 ? (
        <div className="grid gap-6 grid-cols-1 xl:grid-cols-[1.08fr_0.92fr]">
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
        <div className="fixed inset-x-0 bottom-0 z-40 border-t border-line bg-white/95 px-4 py-3 backdrop-blur sm:px-6">
          <div className="mx-auto flex w-full max-w-[1200px] items-center justify-between gap-3">
            <div className="min-w-0">
              <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-brand">Selected order</p>
              <p className="truncate text-sm font-semibold text-ink">{selectedBand.name} | {formatCompactBudget(clampedSpend)}</p>
            </div>
            <div className="flex shrink-0 flex-wrap items-center justify-end gap-2">
              <Link className="button-secondary px-4 py-2.5 text-sm" to="/start-campaign">
                Not sure? Start questionnaire
              </Link>
              <Link
                className="button-primary px-5 py-2.5 text-sm"
                to={`/checkout/payment?packageBandId=${encodeURIComponent(selectedBand.id)}&amount=${encodeURIComponent(clampedSpend)}&area=${encodeURIComponent(selectedArea)}`}
              >
                Continue to payment
              </Link>
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

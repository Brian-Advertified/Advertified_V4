import { ArrowRight, Sparkle, WalletCards } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { HeroSection } from '../../components/marketing/HeroSection';
import { PackageCard } from '../../features/packages/components/PackageCard';
import { advertifiedApi } from '../../services/advertifiedApi';
import { useQuery } from '@tanstack/react-query';
import { LoadingState } from '../../components/ui/LoadingState';

const steps = [
  ['Register', 'Create your account and complete verification once.'],
  ['Buy package', 'Choose a budget band and select your exact spend.'],
  ['Complete brief', 'Tell us about geography, audience, and campaign goals.'],
  ['Unlock planning', 'Choose AI, agent-assisted, or hybrid planning after purchase.'],
  ['Approve campaign', 'Review the recommendation and move confidently into execution.'],
];

export function HomePage() {
  const navigate = useNavigate();
  const packagesQuery = useQuery({
    queryKey: ['packages'],
    queryFn: advertifiedApi.getPackages,
  });

  return (
    <div className="space-y-16">
      <HeroSection />

      <section className="page-shell pt-8">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <div className="pill bg-highlight-soft text-highlight">Package bands</div>
            <h2 className="section-title mt-4">Choose the budget band that matches your campaign ambition.</h2>
            <p className="section-copy mt-4">
              Keep the first decision commercial and clear. You buy the right package first, then unlock guided planning after payment.
            </p>
          </div>
          <Link to="/packages" className="inline-flex items-center gap-2 text-sm font-semibold text-brand">
            View all packages
            <ArrowRight className="size-4" />
          </Link>
        </div>

        <div className="card-grid mt-10">
          {packagesQuery.isLoading ? (
            <div className="md:col-span-2 xl:col-span-4"><LoadingState label="Loading package bands..." /></div>
          ) : (
            packagesQuery.data?.map((band) => (
              <PackageCard
                key={band.id}
                band={band}
                onSelect={() => navigate(`/packages?band=${encodeURIComponent(band.code)}`)}
              />
            ))
          )}
        </div>
      </section>

      <section id="how-it-works" className="page-shell">
        <div className="panel px-6 py-8 sm:px-8 sm:py-10">
          <div className="grid gap-8 lg:grid-cols-[0.8fr_1.2fr]">
            <div>
              <div className="pill bg-brand-soft text-brand">How it works</div>
              <h2 className="section-title mt-4">A guided path from purchase to campaign recommendation.</h2>
              <p className="section-copy mt-4">
                Advertified keeps the public experience simple, then reveals more planning support only when your package, payment, and brief are in place.
              </p>
            </div>
            <div className="grid gap-4">
              {steps.map(([title, copy], index) => (
                <div key={title} className="flex gap-4 rounded-[24px] border border-line bg-white px-5 py-5">
                  <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-ink text-sm font-bold text-white">
                    {index + 1}
                  </div>
                  <div>
                    <p className="text-base font-semibold text-ink">{title}</p>
                    <p className="mt-2 text-sm leading-7 text-ink-soft">{copy}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section className="page-shell">
        <div className="grid gap-6 lg:grid-cols-2">
          <div className="panel px-6 py-8 sm:px-8">
            <Sparkle className="size-6 text-brand" />
            <h2 className="section-title mt-5">Why this works</h2>
            <p className="section-copy mt-4">
              Media planning is complex. Advertified protects clients from that complexity upfront by letting them buy a package first, then unlock a more guided planning journey once the commercial basics are complete.
            </p>
          </div>
          <div className="panel px-6 py-8 sm:px-8">
            <WalletCards className="size-6 text-highlight" />
            <h2 className="section-title mt-5">Built for confidence</h2>
            <p className="section-copy mt-4">
              Premium cards, clear next actions, and visible gating keep the journey trustworthy. Clients always know what has been unlocked, what comes next, and what still needs to happen.
            </p>
          </div>
        </div>
      </section>

      <section className="page-shell pb-6">
        <div className="panel flex flex-col items-start gap-5 px-6 py-8 sm:flex-row sm:items-center sm:justify-between sm:px-8">
          <div>
            <div className="pill bg-brand-soft text-brand">Ready to begin?</div>
            <h2 className="section-title mt-4">Choose your package, pay, then unlock planning.</h2>
          </div>
          <Link to="/packages" className="button-primary px-6 py-3">
            Buy your package
          </Link>
        </div>
      </section>
    </div>
  );
}

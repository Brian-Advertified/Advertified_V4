import { ArrowRight } from 'lucide-react';
import { useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ChannelShowcaseSection } from '../../components/marketing/ChannelShowcaseSection';
import { DeferredVideo } from '../../components/marketing/DeferredVideo';
import { HeroSection } from '../../components/marketing/HeroSection';
import { advertifiedVideoPoster, loadAdvertifiedVideo } from '../../components/marketing/marketingMedia';
import { PackageCard } from '../../features/packages/components/PackageCard';
import { catalogQueryOptions } from '../../lib/catalogQueryOptions';
import { useNearViewport } from '../../lib/useNearViewport';
import { advertifiedApi } from '../../services/advertifiedApi';
import { useQuery } from '@tanstack/react-query';
import { LoadingState } from '../../components/ui/LoadingState';
import { Seo } from '../../components/seo/Seo';

export function HomePage() {
  const navigate = useNavigate();
  const packageSectionRef = useRef<HTMLElement | null>(null);
  const shouldLoadPackages = useNearViewport(packageSectionRef);
  const packagesQuery = useQuery({
    queryKey: ['packages'],
    queryFn: advertifiedApi.getPackages,
    enabled: shouldLoadPackages,
    ...catalogQueryOptions,
  });

  return (
    <div className="space-y-16">
      <Seo
        title="Advertified | Advertising Packages and Media Buying in South Africa"
        description="Start advertising with clearer package-led media buying in South Africa. Explore packages, guided campaign planning, and multi-channel execution with Advertified."
        path="/"
        type="website"
        jsonLd={[
          {
            '@context': 'https://schema.org',
            '@type': 'Organization',
            name: 'Advertified',
            url: 'https://advertified.com',
            email: 'ad@advertified.com',
            telephone: '+27 11 040 1195',
            address: {
              '@type': 'PostalAddress',
              streetAddress: 'Office 301, 3rd Floor, 43 Andringa Street',
              addressLocality: 'Stellenbosch',
              postalCode: '7559',
              addressCountry: 'ZA',
            },
          },
          {
            '@context': 'https://schema.org',
            '@type': 'WebSite',
            name: 'Advertified',
            url: 'https://advertified.com',
            description: 'Advertising packages and guided media buying in South Africa.',
          },
        ]}
      />
      <HeroSection />
      <ChannelShowcaseSection />

      <section ref={packageSectionRef} className="page-shell pt-8">
        <div className="panel px-6 py-8 sm:px-8 sm:py-10">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <div className="pill self-start bg-highlight-soft text-highlight">Choose your starting path</div>
              <h2 className="section-title mt-4">Start from a package if you know your spend, or start from the questionnaire if you want guidance first.</h2>
              <p className="section-copy mt-4">
                Both routes lead into the same planning engine. The only difference is whether you already know your budget band or want Advertified to shape the brief first.
              </p>
            </div>
            <Link to="/packages" className="inline-flex items-center gap-2 text-sm font-semibold text-brand">
              View all packages
              <ArrowRight className="size-4" />
            </Link>
          </div>

          <div className="mt-8 grid gap-4 lg:grid-cols-2">
            <div className="rounded-[24px] border border-line bg-white px-5 py-5">
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Know your spend?</p>
              <h3 className="mt-3 text-xl font-semibold tracking-tight text-ink">Go straight to package selection.</h3>
              <p className="mt-3 text-sm leading-7 text-ink-soft">
                Best if you already know the budget band you want and are ready to continue toward payment.
              </p>
              <Link to="/packages" className="mt-5 inline-flex items-center gap-2 font-semibold text-brand">
                Browse packages
                <ArrowRight className="size-4" />
              </Link>
            </div>
            <div className="rounded-[24px] border border-brand/15 bg-brand-soft/25 px-5 py-5">
              <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Need guidance first?</p>
              <h3 className="mt-3 text-xl font-semibold tracking-tight text-ink">Start with the questionnaire.</h3>
              <p className="mt-3 text-sm leading-7 text-ink-soft">
                Best if you are not yet sure which package, mix, or route fits your campaign and want us to guide the setup.
              </p>
              <Link to="/start-campaign" className="mt-5 inline-flex items-center gap-2 font-semibold text-brand">
                Start questionnaire
                <ArrowRight className="size-4" />
              </Link>
            </div>
          </div>

          <div className="card-grid mt-10">
            {!shouldLoadPackages ? (
              <div className="md:col-span-2 xl:col-span-4">
                <div className="rounded-[24px] border border-line bg-white px-6 py-8 text-sm leading-7 text-ink-soft">
                  Package bands will load as this section comes into view.
                </div>
              </div>
            ) : packagesQuery.isLoading ? (
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
        </div>
      </section>

      <section className="page-shell">
        <div className="panel overflow-hidden px-6 py-8 sm:px-8 sm:py-10">
          <div className="grid gap-8 lg:grid-cols-[minmax(0,1.2fr)_minmax(280px,0.8fr)] lg:items-start">
            <div className="max-w-2xl">
              <div className="pill self-start bg-highlight-soft px-3 py-1 text-[11px] tracking-[0.16em] text-highlight">Advertise Now. Pay Later.</div>
              <h2 className="mt-4 max-w-2xl text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
                Built to make media buying feel more guided, more flexible, and less intimidating.
              </h2>
              <p className="mt-4 max-w-[520px] text-sm leading-7 text-ink-soft sm:text-[15px]">
                Advertified helps brands start with the right package, then unlock the planning support needed to shape campaigns across billboards, digital screens, radio, TV, social, SMS, and print.
              </p>
              <p className="mt-3 max-w-[520px] text-sm leading-7 text-ink-soft sm:text-[15px]">
                This video is a quick brand introduction: who we are, what we enable, and why the journey is designed to feel clear instead of overwhelming.
              </p>
              <div className="mt-6 flex flex-wrap gap-3">
                <Link to="/packages" className="button-primary px-6 py-3">
                  Browse packages
                </Link>
                <Link to="/register" className="button-secondary px-6 py-3">
                  Create account
                </Link>
              </div>
            </div>
            <div className="rounded-[28px] border border-line bg-white/70 p-4 sm:p-5">
              <div className="mx-auto w-full max-w-[240px] overflow-hidden rounded-[30px] border border-line bg-slate-950 shadow-[0_12px_24px_rgba(15,23,42,0.11)] sm:max-w-[280px]">
                <DeferredVideo
                  title="Advertified brand introduction"
                  loadSrc={loadAdvertifiedVideo}
                  posterSrc={advertifiedVideoPoster}
                  className="aspect-[9/15] w-full bg-slate-950"
                />
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="page-shell pb-6">
        <div className="panel flex flex-col items-start gap-5 px-6 py-8 sm:flex-row sm:items-center sm:justify-between sm:px-8">
          <div>
            <div className="pill bg-brand-soft text-brand">Ready to begin?</div>
            <h2 className="section-title mt-4">Start with packages if you know your spend, or start with the questionnaire if you want us to guide the brief.</h2>
          </div>
          <div className="flex flex-wrap gap-3">
            <Link to="/packages" className="button-primary px-5 py-3">
              Browse packages
            </Link>
            <Link to="/start-campaign" className="button-secondary px-5 py-3">
              Start questionnaire
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}

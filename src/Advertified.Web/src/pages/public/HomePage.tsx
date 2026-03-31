import { ArrowRight } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import advertifiedVideo from '../../assets/Channels/advertified.mp4';
import { ChannelShowcaseSection } from '../../components/marketing/ChannelShowcaseSection';
import { HeroSection } from '../../components/marketing/HeroSection';
import { PackageCard } from '../../features/packages/components/PackageCard';
import { advertifiedApi } from '../../services/advertifiedApi';
import { useQuery } from '@tanstack/react-query';
import { LoadingState } from '../../components/ui/LoadingState';

export function HomePage() {
  const navigate = useNavigate();
  const packagesQuery = useQuery({
    queryKey: ['packages'],
    queryFn: advertifiedApi.getPackages,
  });

  return (
    <div className="space-y-16">
      <HeroSection />
      <ChannelShowcaseSection />

      <section className="page-shell pt-8">
        <div className="panel px-6 py-8 sm:px-8 sm:py-10">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <div className="pill self-start bg-highlight-soft text-highlight">Package bands</div>
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
                <video
                  className="aspect-[9/15] w-full bg-slate-950"
                  controls
                  preload="metadata"
                  playsInline
                >
                  <source src={advertifiedVideo} type="video/mp4" />
                  Your browser does not support the video tag.
                </video>
              </div>
            </div>
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

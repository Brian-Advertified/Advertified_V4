import { ArrowRight, CirclePlay, ShieldCheck, Sparkles } from 'lucide-react';
import { Link } from 'react-router-dom';

export function HeroSection() {
  return (
    <section className="page-shell pt-6">
      <div className="hero-mint overflow-hidden rounded-[32px] px-6 py-10 sm:px-10 sm:py-14 lg:px-14">
        <div className="grid gap-10 lg:grid-cols-[1.2fr_0.8fr] lg:items-center">
          <div>
            <div className="hero-kicker">Advertise Now. Pay Later.</div>
            <h1 className="mt-6 max-w-3xl text-4xl font-semibold tracking-tight text-ink sm:text-6xl">
              Unlock Premium Advertising Without Breaking Your Cash Flow
            </h1>
            <p className="mt-6 max-w-2xl text-base leading-8 text-ink-soft sm:text-lg">
Pay over terms – aligned with your business cycles, not upfront. Access TV, Radio, and Billboards, plus premium digital advertising screens in shopping centres, airports and major traffic routes. Pay when your revenue flows in, not upfront.            </p>
            <div className="mt-8 flex flex-col gap-4 sm:flex-row">
              <Link to="/packages" className="hero-primary-button">
                Buy a package
                <ArrowRight className="size-4" />
              </Link>
              <Link to="/how-it-works" className="hero-secondary-button">
                <CirclePlay className="size-4" />
                How it works
              </Link>
            </div>
          </div>

          <div className="grid gap-4">
            <div className="hero-glass-card rounded-[24px] p-5">
              <p className="text-sm font-semibold text-ink">Simple commercial path</p>
              <ul className="mt-4 space-y-3 text-sm text-ink-soft">
                <li className="flex gap-3"><ShieldCheck className="mt-1 size-4 shrink-0 text-brand" />Register and verify once.</li>
                <li className="flex gap-3"><Sparkles className="mt-1 size-4 shrink-0 text-highlight" />Choose a budget band and exact spend.</li>
                <li className="flex gap-3"><ArrowRight className="mt-1 size-4 shrink-0 text-brand" />Unlock planning only after payment and brief submission.</li>
              </ul>
            </div>
            <div className="hero-glass-card rounded-[24px] p-5 text-ink">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Positioning</p>
              <p className="mt-3 text-2xl font-semibold tracking-tight">
                Choose your package. Tell us about your campaign. Get your tailored media.
              </p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

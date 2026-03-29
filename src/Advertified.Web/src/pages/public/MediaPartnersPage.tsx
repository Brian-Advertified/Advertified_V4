import { ArrowRight, Landmark, Megaphone, Newspaper, Radio, SearchCheck, Tv, Workflow } from 'lucide-react';
import { Link } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';

const partnerTypes = [
  'Billboard Media Owners',
  'Radio Networks',
  'TV Channels',
  'Retail Venue Groups',
  'Publications',
] as const;

const supplyPromise = [
  'Qualified SME demand pipeline with clear campaign intent',
  'Package-led planning to reduce repetitive custom quoting',
  'Defined operational handoff from booking to activation',
  'Transparent communication and partner-aligned timelines',
] as const;

const journey = [
  { step: '1', title: 'Inventory alignment', detail: 'Map available inventory into package structures, regions, and commercial tiers that can be sold with confidence.' },
  { step: '2', title: 'Demand matching', detail: 'Advertiser briefs are matched against your inventory, geography, audience fit, and scheduling windows.' },
  { step: '3', title: 'Commercial confirmation', detail: 'Package, payment path, and campaign direction are confirmed before the activation workflow moves forward.' },
  { step: '4', title: 'Delivery and reporting', detail: 'Campaigns go live with clearer operational handoff, execution communication, and reporting checkpoints.' },
] as const;

const snapshots = [
  { metric: '92%', label: 'Partner retention from recurring campaigns' },
  { metric: '48h', label: 'Average campaign planning turnaround' },
  { metric: '1 200+', label: 'Addressable inventory points in pipeline' },
] as const;

const partnerCards = [
  { label: 'Billboards', icon: Megaphone },
  { label: 'Radio', icon: Radio },
  { label: 'Television', icon: Tv },
  { label: 'Press', icon: Newspaper },
] as const;

export function MediaPartnersPage() {
  return (
    <div className="page-shell space-y-8 pb-10">
      <PageHero
        kicker="Media partners"
        title="Partner with Advertified to turn premium inventory into structured demand."
        description="We work with media owners, networks, and venue operators to connect high-intent advertisers to quality inventory through a planning and activation flow designed for reliability."
        actions={(
          <>
            <Link to="/partner-enquiry" className="hero-primary-button">
              Become a partner
              <ArrowRight className="size-4" />
            </Link>
            <Link to="/how-it-works" className="hero-secondary-button rounded-full font-semibold">
              See advertiser journey
            </Link>
          </>
        )}
        aside={(
          <div className="space-y-4">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Partner types</p>
            <div className="flex flex-wrap gap-2">
              {partnerTypes.map((tag) => (
                <span key={tag} className="rounded-full border border-brand/15 bg-white/80 px-3 py-1.5 text-xs font-semibold text-brand">
                  {tag}
                </span>
              ))}
            </div>
            <div className="grid grid-cols-2 gap-3">
              {partnerCards.map((item) => {
                const Icon = item.icon;
                return (
                  <div key={item.label} className="rounded-[18px] border border-line bg-white/80 px-3 py-3">
                    <Icon className="size-4 text-brand" />
                    <p className="mt-2 text-sm font-semibold text-ink">{item.label}</p>
                  </div>
                );
              })}
            </div>
          </div>
        )}
      />

      <section className="grid gap-5 lg:grid-cols-2">
        <div className="panel px-6 py-7 sm:px-8 sm:py-8">
          <div className="pill self-start bg-brand-soft text-brand">Partner value</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">What you can expect</h2>
          <p className="mt-4 max-w-2xl text-sm leading-7 text-ink-soft sm:text-base">
            Advertified is structured to make supply-side collaboration clearer and more repeatable, not more chaotic.
          </p>

          <ul className="mt-6 space-y-3">
            {supplyPromise.map((item) => (
              <li key={item} className="rounded-[20px] border border-line bg-white px-4 py-4 text-sm leading-7 text-ink-soft shadow-[0_8px_24px_rgba(17,24,39,0.04)]">
                {item}
              </li>
            ))}
          </ul>
        </div>

        <div className="panel px-6 py-7 sm:px-8 sm:py-8">
          <div className="pill self-start bg-highlight-soft text-highlight">Delivery flow</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">How we work together</h2>
          <div className="mt-6 space-y-3">
            {journey.map((item, index) => {
              const icons = [Workflow, SearchCheck, Landmark, ArrowRight] as const;
              const Icon = icons[index] ?? Workflow;
              return (
                <article key={item.step} className="rounded-[20px] border border-line bg-white px-4 py-4 shadow-[0_8px_24px_rgba(17,24,39,0.04)]">
                  <div className="flex items-start gap-3">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-brand-soft text-brand">
                      <Icon className="size-4" />
                    </div>
                    <div>
                      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-brand">Step {item.step}</p>
                      <p className="mt-1 text-base font-semibold text-ink">{item.title}</p>
                      <p className="mt-2 text-sm leading-7 text-ink-soft">{item.detail}</p>
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
        </div>
      </section>

      <section className="panel px-6 py-7 sm:px-8 sm:py-8">
        <div className="max-w-3xl">
          <div className="pill self-start bg-brand-soft text-brand">Commercial snapshot</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">The operating model in numbers.</h2>
        </div>
        <div className="mt-6 grid gap-4 md:grid-cols-3">
          {snapshots.map((item, index) => (
            <div
              key={item.label}
              className={`rounded-[22px] border px-5 py-5 ${index === 1 ? 'border-brand/20 bg-brand-soft/35' : 'border-line bg-white'}`}
            >
              <p className="text-3xl font-semibold tracking-tight text-brand">{item.metric}</p>
              <p className="mt-2 text-sm leading-7 text-ink-soft">{item.label}</p>
            </div>
          ))}
        </div>
        <div className="mt-6 flex flex-wrap gap-3">
          <Link to="/partner-enquiry" className="hero-primary-button">
            Become a partner
            <ArrowRight className="size-4" />
          </Link>
          <Link to="/how-it-works" className="hero-secondary-button rounded-full font-semibold">
            See advertiser journey
          </Link>
        </div>
      </section>
    </div>
  );
}

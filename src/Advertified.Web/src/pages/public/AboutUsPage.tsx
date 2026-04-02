import { ArrowRight, CircleCheckBig, Orbit, RadioTower, RectangleHorizontal, Smartphone, Tv } from 'lucide-react';
import { Link } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';

const processPoints = [
  'Choose a package that fits your budget',
  'Share your campaign goals',
  'Receive guided recommendations',
  'Review and approve your plan',
  'Move into execution',
] as const;

const differentiators = [
  'Stronger alignment from the beginning',
  'Less back-and-forth',
  'A more predictable campaign journey',
] as const;

const channelCards = [
  { label: 'Outdoor', detail: 'Billboards and Digital Screens', icon: RectangleHorizontal },
  { label: 'Radio', detail: 'Audio-led reach and frequency', icon: RadioTower },
  { label: 'TV', detail: 'Broadcast visibility and awareness', icon: Tv },
  { label: 'Digital', detail: 'Social, digital, SMS, and print', icon: Smartphone },
] as const;

const audienceCards = [
  'SMEs looking for a clear starting point',
  'Growing brands scaling their reach',
  'Marketing teams that want guidance without complexity',
] as const;

export function AboutUsPage() {
  return (
    <div className="page-shell space-y-8 pb-10">
      <PageHero
        kicker="Advertise Now. Pay Later."
        title="A clearer way to buy and run advertising."
        description="Advertified helps businesses move from budget to campaign through a structured process that starts with what you want to spend and builds toward a campaign you can launch with confidence."
        actions={(
          <>
            <Link to="/packages" className="hero-primary-button">
              Browse packages
              <ArrowRight className="size-4" />
            </Link>
            <Link to="/how-it-works" className="hero-secondary-button rounded-full font-semibold">
              See how it works
            </Link>
          </>
        )}
        aside={(
          <div className="space-y-4">
            <div className="inline-flex items-center gap-2 rounded-full border border-brand/15 bg-white/85 px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-brand">
              <Orbit className="size-4" />
              Modern advertising
            </div>
            <p className="text-sm leading-7 text-ink-soft">
              Built around clear commercial steps, campaign workspaces, and a growing partner ecosystem so businesses can advertise with more confidence.
            </p>
            <div className="grid gap-3">
              {['Clear commercial steps', 'Structured workflows', 'Visible campaign progress'].map((item) => (
                <div key={item} className="rounded-[18px] border border-line bg-white/85 px-4 py-3 text-sm font-medium text-ink">
                  {item}
                </div>
              ))}
            </div>
          </div>
        )}
      />

      <section className="panel px-6 py-7 sm:px-8 sm:py-8">
        <div className="grid gap-6 lg:grid-cols-[0.95fr_1.05fr]">
          <div>
            <div className="pill self-start bg-brand-soft text-brand">Why Advertified exists</div>
            <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">Advertising should start with clarity.</h2>
            <p className="mt-4 text-sm leading-7 text-ink-soft sm:text-base">
              Advertising can feel fragmented. Pricing is not always clear, quoting takes time, and planning often begins before there is alignment on budget.
            </p>
            <p className="mt-4 text-sm leading-7 text-ink-soft sm:text-base">
              Advertified was built to change that. We believe the process should start with clarity, so businesses can move forward with confidence instead of uncertainty.
            </p>
          </div>

          <div className="grid gap-4">
            {[
              'Pricing is not always clear',
              'Quoting takes time',
              'Planning often starts before budget alignment',
            ].map((item) => (
              <article key={item} className="rounded-[22px] border border-line bg-white px-5 py-5 shadow-[0_10px_30px_rgba(17,24,39,0.04)]">
                <p className="text-base font-semibold text-ink">{item}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="grid gap-5 lg:grid-cols-2">
        <div className="panel px-6 py-7 sm:px-8 sm:py-8">
          <div className="pill self-start bg-highlight-soft text-highlight">What we help businesses do</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">A more direct path from idea to live campaign.</h2>
          <p className="mt-4 text-sm leading-7 text-ink-soft sm:text-base">
            Advertified gives businesses a more structured path into advertising. Instead of navigating a complex, open-ended process, every major step is guided and visible.
          </p>
          <div className="mt-6 space-y-3">
            {processPoints.map((item) => (
              <div key={item} className="flex items-start gap-3 rounded-[20px] border border-line bg-white px-4 py-4 text-sm leading-7 text-ink-soft shadow-[0_8px_24px_rgba(17,24,39,0.04)]">
                <CircleCheckBig className="mt-1 size-4 shrink-0 text-brand" />
                <span>{item}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="panel px-6 py-7 sm:px-8 sm:py-8">
          <div className="pill self-start bg-brand-soft text-brand">What makes our model different</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">Most advertising journeys start with strategy and end with pricing.</h2>
          <p className="mt-4 text-sm leading-7 text-ink-soft sm:text-base">
            Advertified works the other way around. We start with a clear commercial decision, then unlock the planning and execution support around it.
          </p>
          <div className="mt-6 rounded-[24px] border border-brand/20 bg-brand-soft/35 px-5 py-5">
            <p className="text-sm font-semibold uppercase tracking-[0.16em] text-brand">The result</p>
            <div className="mt-4 space-y-3">
              {differentiators.map((item) => (
                <div key={item} className="rounded-[18px] border border-white/80 bg-white/90 px-4 py-3 text-sm text-ink-soft">
                  {item}
                </div>
              ))}
            </div>
            <p className="mt-4 text-sm leading-7 text-ink-soft">
              From payment to launch, every step is structured and visible.
            </p>
          </div>
        </div>
      </section>

      <section className="panel px-6 py-7 sm:px-8 sm:py-8">
        <div className="grid gap-8 lg:grid-cols-[0.9fr_1.1fr]">
          <div>
            <div className="pill self-start bg-highlight-soft text-highlight">Built for real campaigns</div>
            <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">Multi-channel planning without unnecessary friction.</h2>
            <p className="mt-4 text-sm leading-7 text-ink-soft sm:text-base">
              Advertified supports campaigns across Billboards and Digital Screens, radio, TV, social, digital, SMS, and print. Everything is designed to work together so businesses can plan, approve, and execute with less friction.
            </p>
          </div>
          <div className="grid gap-4 md:grid-cols-2">
            {channelCards.map((item, index) => {
              const Icon = item.icon;
              return (
                <article
                  key={item.label}
                  className={`rounded-[22px] border px-5 py-5 shadow-[0_10px_30px_rgba(17,24,39,0.04)] ${index === 0 ? 'border-brand/20 bg-brand-soft/35' : 'border-line bg-white'}`}
                >
                  <div className="flex size-10 items-center justify-center rounded-2xl bg-white text-brand">
                    <Icon className="size-5" />
                  </div>
                  <h3 className="mt-4 text-lg font-semibold text-ink">{item.label}</h3>
                  <p className="mt-2 text-sm leading-7 text-ink-soft">{item.detail}</p>
                </article>
              );
            })}
          </div>
        </div>
      </section>

      <section className="grid gap-5 lg:grid-cols-2">
        <div className="panel px-6 py-7 sm:px-8 sm:py-8">
          <div className="pill self-start bg-brand-soft text-brand">Who we're built for</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">For businesses that want advertising to make sense.</h2>
          <div className="mt-6 space-y-3">
            {audienceCards.map((item) => (
              <div key={item} className="rounded-[20px] border border-line bg-white px-4 py-4 text-sm leading-7 text-ink-soft shadow-[0_8px_24px_rgba(17,24,39,0.04)]">
                {item}
              </div>
            ))}
          </div>
          <p className="mt-5 text-sm leading-7 text-ink-soft sm:text-base">
            If you value clarity, structure, and momentum, Advertified is built for you.
          </p>
        </div>

        <div className="panel px-6 py-7 sm:px-8 sm:py-8">
          <div className="pill self-start bg-highlight-soft text-highlight">Built for modern advertising</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">Designed to work across markets, media environments, and business sizes.</h2>
          <p className="mt-4 text-sm leading-7 text-ink-soft sm:text-base">
            With structured workflows, clear commercial steps, campaign workspaces, and a growing partner ecosystem, we are building a more accessible way to run advertising.
          </p>
          <div className="mt-6 rounded-[24px] border border-line bg-white px-5 py-5">
            <p className="text-sm font-semibold uppercase tracking-[0.16em] text-brand">Core operating model</p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              {['Commercial-first buying', 'Campaign workspaces', 'Approval checkpoints', 'Partner-led delivery'].map((item) => (
                <div key={item} className="rounded-[18px] border border-line bg-slate-50 px-4 py-3 text-sm text-ink-soft">
                  {item}
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section className="panel px-6 py-7 text-center sm:px-8 sm:py-8">
        <h2 className="text-3xl font-semibold tracking-tight text-ink">Start with your budget. Build with confidence. Launch with clarity.</h2>
        <p className="mx-auto mt-3 max-w-2xl text-sm leading-7 text-ink-soft sm:text-base">
          Begin with the right package, then move into planning and execution through a guided campaign journey.
        </p>
        <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
          <Link to="/packages" className="hero-primary-button">
            Browse packages
            <ArrowRight className="size-4" />
          </Link>
          <Link to="/register" className="hero-secondary-button rounded-full font-semibold">
            Create account
          </Link>
        </div>
      </section>
    </div>
  );
}


import { ArrowRight, BadgeCheck, ClipboardPenLine, CreditCard, LayoutGrid, PenSquare, Sparkles, UserRoundPlus } from 'lucide-react';
import { Link } from 'react-router-dom';
import { DeferredVideo } from '../../components/marketing/DeferredVideo';
import { advertifiedVideoPoster, loadAdvertifiedVideo } from '../../components/marketing/marketingMedia';
import { PageHero } from '../../components/marketing/PageHero';

const steps = [
  {
    title: 'Create your account',
    description: 'Sign up in minutes and tell us a bit about your business so we can tailor your campaign.',
    details: ['Quick registration', 'Business profile setup', 'Secure account access'],
    icon: UserRoundPlus,
  },
  {
    title: 'Choose a package',
    description: 'Pick a budget and package that fits your goals — we’ll handle the planning from there.',
    details: ['Clear pricing', 'Defined deliverables', 'Built-in planning support'],
    icon: LayoutGrid,
  },
  {
    title: 'Secure your campaign',
    description: 'Complete payment to unlock your campaign workspace and start the planning process.',
    details: ['Secure checkout', 'Instant campaign activation', 'Invoice provided'],
    icon: CreditCard,
  },
  {
    title: 'Tell us what you need',
    description: 'Share your goals, audience, and preferences so we can build the right plan for you.',
    details: ['Simple guided questions', 'Define your audience', 'Set campaign objectives'],
    icon: ClipboardPenLine,
  },
  {
    title: 'We build your plan',
    description: 'Our system creates a tailored media plan based on your brief, budget, and available inventory.',
    details: ['Data-driven recommendations', 'Optimized for reach and relevance', 'Built within your budget'],
    icon: Sparkles,
  },
  {
    title: 'Review and request changes',
    description: 'Go through your recommendation, approve it, or request adjustments — you stay in control.',
    details: ['Clear breakdown of your plan', 'Request revisions anytime', 'Approve when ready'],
    icon: BadgeCheck,
  },
  {
    title: 'Launch your campaign',
    description: 'Once approved, we activate your campaign and get your media live.',
    details: ['Campaign activation', 'Media placement execution', 'Ongoing support'],
    icon: PenSquare,
  },
] as const;

const highlights = [
  {
    title: 'Package clarity',
    description: "Know exactly what you're buying, with clear deliverables and no hidden surprises.",
  },
  {
    title: 'Guided recommendation flow',
    description: 'We guide you step by step — from brief to plan — so you’re never left guessing.',
  },
  {
    title: 'Multi-channel flexibility',
    description: 'Reach your audience across multiple channels, tailored to your campaign goals.',
  },
] as const;

export function HowItWorksPage() {
  return (
    <div className="page-shell space-y-8 pb-10">
      <PageHero
        kicker="How it works"
        title="From package purchase to campaign recommendation in a clear, guided flow."
        description="Advertified is designed to keep the buying journey commercial first, then unlock the planning support needed to shape a real campaign with confidence."
        actions={(
          <>
            <Link to="/packages" className="hero-primary-button">
              Browse packages
              <ArrowRight className="size-4" />
            </Link>
            <Link to="/register" className="hero-secondary-button rounded-full font-semibold">
              Create account
            </Link>
          </>
        )}
        aside={(
          <div className="flex justify-center">
            <div className="w-full max-w-[210px] overflow-hidden rounded-[24px] border border-line bg-slate-950 shadow-[0_12px_24px_rgba(15,23,42,0.11)]">
              <DeferredVideo
                title="Advertified brand introduction"
                loadSrc={loadAdvertifiedVideo}
                posterSrc={advertifiedVideoPoster}
                className="aspect-[9/15] w-full bg-slate-950"
              />
            </div>
          </div>
        )}
      />

      <section className="panel px-6 py-7 sm:px-8 sm:py-8">
        <div className="max-w-3xl">
          <div className="pill self-start bg-brand-soft text-brand">The journey</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">What happens after you get started</h2>
          <p className="mt-4 max-w-2xl text-sm leading-7 text-ink-soft sm:text-base">
            From signup to campaign approval, here’s exactly how we guide you through the process.
          </p>
        </div>

        <div className="mt-8 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {steps.map((step, index) => {
            const Icon = step.icon;
            return (
              <article key={step.title} className="rounded-[24px] border border-line bg-white px-5 py-5 shadow-[0_10px_30px_rgba(17,24,39,0.04)]">
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-brand">Step {index + 1}</p>
                <div className="mt-3 flex size-10 items-center justify-center rounded-2xl bg-brand-soft text-brand">
                  <Icon className="size-5" />
                </div>
                <h3 className="mt-4 text-lg font-semibold tracking-tight text-ink">{step.title}</h3>
                <p className="mt-2 text-sm leading-7 text-ink-soft">{step.description}</p>
                <ul className="mt-4 space-y-2">
                  {step.details.map((detail) => (
                    <li key={detail} className="flex items-start gap-3 text-sm text-ink-soft">
                      <span className="mt-2 size-1.5 shrink-0 rounded-full bg-highlight" />
                      <span>{detail}</span>
                    </li>
                  ))}
                </ul>
              </article>
            );
          })}
        </div>
      </section>

      <section className="panel px-6 py-7 sm:px-8 sm:py-8">
        <div className="max-w-3xl">
          <div className="pill self-start bg-highlight-soft text-highlight">Why people use it</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">Built to be clear, guided, and trustworthy</h2>
        </div>
        <div className="mt-6 grid gap-4 md:grid-cols-3">
          {highlights.map((item, index) => (
            <div
              key={item.title}
              className={`rounded-[22px] border px-5 py-5 ${index === 1 ? 'border-brand/20 bg-brand-soft/35' : 'border-line bg-white'}`}
            >
              <h3 className="text-lg font-semibold text-ink">{item.title}</h3>
              <p className="mt-2 text-sm leading-7 text-ink-soft">{item.description}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="panel px-6 py-7 text-center sm:px-8 sm:py-8">
        <h2 className="text-3xl font-semibold tracking-tight text-ink">Ready to launch your campaign?</h2>
        <p className="mx-auto mt-3 max-w-2xl text-sm leading-7 text-ink-soft sm:text-base">
          Start with a package, and we’ll guide you all the way to a live campaign.
        </p>
        <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
          <Link to="/packages" className="hero-primary-button">
            Get started
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

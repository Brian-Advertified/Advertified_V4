import { ArrowRight, Bot, BrainCircuit, Clapperboard, Sparkles, WandSparkles } from 'lucide-react';
import { Link } from 'react-router-dom';

const pillars = [
  {
    title: 'Creative Direction',
    body: 'We turn your brief into a visual narrative system with channel-by-channel creative variants.',
    icon: Clapperboard,
  },
  {
    title: 'AI Production',
    body: 'Generate concept directions, scripts, visuals, and edits faster while keeping brand consistency.',
    icon: Bot,
  },
  {
    title: 'Performance Loops',
    body: 'Close the loop between campaign response signals and the next wave of creative decisions.',
    icon: BrainCircuit,
  },
];

export function AiStudioPage() {
  return (
    <div className="page-shell space-y-10 pb-14">
      <section className="relative overflow-hidden rounded-[36px] border border-slate-800 bg-slate-950 px-6 py-10 text-white shadow-[0_32px_70px_rgba(2,6,23,0.5)] sm:px-10 sm:py-14">
        <div className="pointer-events-none absolute -left-10 top-8 h-40 w-40 rounded-full bg-teal-400/20 blur-3xl" />
        <div className="pointer-events-none absolute bottom-0 right-0 h-56 w-56 rounded-full bg-cyan-400/20 blur-3xl" />
        <div className="grid gap-10 lg:grid-cols-[1.1fr_0.9fr] lg:items-center">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.26em] text-teal-100">
              <Sparkles className="size-4" />
              New Product
            </div>
            <h1 className="mt-6 max-w-3xl text-4xl font-semibold tracking-tight sm:text-5xl">
              AI Studio by Advertified
            </h1>
            <p className="mt-5 max-w-2xl text-base leading-8 text-slate-200 sm:text-lg">
              A dedicated creative product for high-output campaign ideation and production. Built for teams that need stronger concepts, faster turnarounds, and measurable creative quality.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link to="/partner-enquiry" className="inline-flex items-center gap-2 rounded-full bg-white px-6 py-3 text-sm font-semibold text-slate-900 transition hover:bg-slate-100">
                Book a studio demo
                <ArrowRight className="size-4" />
              </Link>
              <Link to="/packages" className="inline-flex items-center gap-2 rounded-full border border-white/30 bg-white/5 px-6 py-3 text-sm font-semibold text-white transition hover:bg-white/10">
                Explore packages first
              </Link>
            </div>
          </div>

          <div className="rounded-[28px] border border-white/15 bg-white/5 p-6 backdrop-blur">
            <p className="text-xs font-semibold uppercase tracking-[0.24em] text-teal-100">Studio Flow</p>
            <ol className="mt-4 space-y-3 text-sm text-slate-200">
              <li className="rounded-2xl border border-white/10 bg-white/5 px-4 py-3">1. Brief intake + channel intent mapping</li>
              <li className="rounded-2xl border border-white/10 bg-white/5 px-4 py-3">2. AI-assisted concept sprint and script options</li>
              <li className="rounded-2xl border border-white/10 bg-white/5 px-4 py-3">3. Production-ready asset direction by medium</li>
              <li className="rounded-2xl border border-white/10 bg-white/5 px-4 py-3">4. Review loops with brand and performance controls</li>
            </ol>
          </div>
        </div>
      </section>

      <section className="grid gap-5 lg:grid-cols-3">
        {pillars.map((pillar) => {
          const Icon = pillar.icon;
          return (
            <article key={pillar.title} className="rounded-[28px] border border-line bg-white p-6 shadow-[0_14px_32px_rgba(15,23,42,0.06)]">
              <div className="flex size-11 items-center justify-center rounded-2xl bg-brand-soft text-brand">
                <Icon className="size-5" />
              </div>
              <h2 className="mt-4 text-xl font-semibold tracking-tight text-ink">{pillar.title}</h2>
              <p className="mt-3 text-sm leading-7 text-ink-soft">{pillar.body}</p>
            </article>
          );
        })}
      </section>

      <section className="rounded-[30px] border border-brand/20 bg-brand-soft/40 px-6 py-8 sm:px-8 sm:py-10">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.22em] text-brand">Version 1</p>
            <h2 className="mt-2 text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
              Launching now as a dedicated creative product.
            </h2>
            <p className="mt-3 max-w-3xl text-sm leading-7 text-ink-soft">
              Start with this landing page and guided demo flow, then evolve into a full commercial funnel with sample reels, plan tiers, and a standalone onboarding pipeline.
            </p>
          </div>
          <Link to="/partner-enquiry" className="inline-flex items-center gap-2 rounded-full bg-brand px-6 py-3 text-sm font-semibold text-white transition hover:bg-brand-strong">
            <WandSparkles className="size-4" />
            Start AI Studio conversation
          </Link>
        </div>
      </section>
    </div>
  );
}

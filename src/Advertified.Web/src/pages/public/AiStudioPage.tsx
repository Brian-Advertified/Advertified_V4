import { ArrowRight, Sparkles, WandSparkles } from 'lucide-react';
import { Link } from 'react-router-dom';

const outputs = [
  { title: 'Billboards & Digital Screens', text: 'Generate outdoor headlines, screen copy, and visual direction built for real placements.' },
  { title: 'Radio & TV', text: 'Turn one approved brief into scripts, voice routes, and campaign-ready broadcast lines.' },
  { title: 'Social & Digital', text: 'Create fast, channel-native cutdowns and platform-ready media variants from the same idea.' },
];

const systemChannels = ['Billboards', 'Radio', 'TV', 'Social', 'Search', 'Print'];

export function AiStudioPage() {
  return (
    <div className="bg-slate-950 text-white">
      <section className="relative px-6 pb-8 pt-14 sm:px-10 sm:pt-18">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_18%_18%,rgba(15,118,110,0.28),transparent_40%),radial-gradient(circle_at_82%_72%,rgba(14,116,144,0.22),transparent_36%)]" />
        <div className="page-shell relative grid gap-7 lg:grid-cols-[1.2fr_0.8fr]">
          <div className="rounded-[30px] border border-slate-800 bg-[#090b10] p-7 sm:p-10">
            <div className="ai-fade-up inline-flex items-center gap-2 rounded-full border border-white/20 bg-white/10 px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.26em] text-slate-100">
              <Sparkles className="size-4 text-brand-soft" />
              AI Media Studio By Advertified
            </div>
            <h1 className="ai-fade-up ai-delay-1 mt-7 text-4xl font-semibold leading-tight tracking-tight sm:text-6xl">
              Create campaign media.
              <br />
              Faster.
            </h1>
            <p className="ai-fade-up ai-delay-2 mt-5 max-w-2xl text-base leading-8 text-slate-300 sm:text-lg">
              Use one approved brief to generate campaign media across billboards, radio, TV, social, and digital. The studio is built for making creative outputs and media-ready variants, not for buying or planning media.
            </p>
            <div className="ai-fade-up ai-delay-3 mt-8 flex flex-wrap items-center gap-3">
              <Link to="/partner-enquiry" className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand to-[#14b86e] px-7 py-3 text-sm font-semibold text-white shadow-[0_16px_38px_rgba(15,118,110,0.28)] transition hover:translate-y-[-1px] hover:shadow-[0_20px_42px_rgba(15,118,110,0.32)]">
                Start a media brief
                <ArrowRight className="size-4" />
              </Link>
              <Link to="/packages" className="inline-flex items-center gap-2 rounded-xl border border-slate-600 bg-slate-950 px-7 py-3 text-sm font-semibold text-white transition hover:border-brand/45 hover:bg-slate-900">
                Explore packages
              </Link>
            </div>
          </div>

          <div className="rounded-[30px] border border-slate-800 bg-[#0A0A0A] p-7 sm:p-8">
            <p className="text-xs font-semibold uppercase tracking-[0.24em] text-slate-400">Studio Flow</p>
            <div className="mt-5 space-y-3 text-sm text-slate-200">
              <div className="rounded-2xl border border-slate-700 bg-slate-900/60 px-4 py-3">1. Approved brief + audience + channel mix</div>
              <div className="rounded-2xl border border-slate-700 bg-slate-900/60 px-4 py-3">2. AI concept sprint, headlines, and script routes</div>
              <div className="rounded-2xl border border-slate-700 bg-slate-900/60 px-4 py-3">3. Media-ready creative variants by channel</div>
              <div className="rounded-2xl border border-slate-700 bg-slate-900/60 px-4 py-3">4. Review loops, revisions, and final pack delivery</div>
            </div>
            <div className="ai-sheen mt-6 rounded-2xl border border-slate-700 bg-slate-900 px-4 py-4">
              <p className="text-xs uppercase tracking-[0.2em] text-slate-400">Example prompt</p>
              <p className="mt-2 text-sm leading-7 text-brand-soft">Create billboard copy, radio scripts, and social cutdowns for a premium sneaker launch in Gauteng.</p>
            </div>
          </div>
        </div>
      </section>

      <section className="page-shell px-6 py-6 sm:px-10">
        <div className="grid gap-5 md:grid-cols-3">
          {outputs.map((item, index) => (
            <article key={item.title} className={`ai-fade-up ${index === 0 ? 'ai-delay-1' : index === 1 ? 'ai-delay-2' : 'ai-delay-3'} rounded-3xl border border-slate-800 bg-[#0A0A0A] p-7 shadow-[0_18px_42px_rgba(2,6,23,0.45)] transition hover:-translate-y-1 hover:border-brand/35`}>
              <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-400">{item.title}</p>
              <p className="mt-7 text-xl font-semibold leading-8 text-slate-100">{item.text}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="page-shell px-6 py-10 text-center sm:px-10">
        <h2 className="ai-fade-up text-3xl font-semibold tracking-tight sm:text-4xl">
          One campaign. Everywhere.
        </h2>
        <div className="mt-7 flex flex-wrap justify-center gap-3 text-slate-300">
          {systemChannels.map((item) => (
            <div key={item} className="ai-float rounded-xl border border-slate-700 px-5 py-2.5 text-sm">
              {item}
            </div>
          ))}
        </div>
      </section>

      <section className="page-shell px-6 pb-16 pt-4 text-center sm:px-10">
        <div className="rounded-[30px] border border-slate-800 bg-[#0A0A0A] px-6 py-10 sm:px-10 sm:py-12">
          <h2 className="ai-fade-up text-4xl font-semibold tracking-tight sm:text-5xl">
            Create your campaign media.
          </h2>
          <p className="ai-fade-up ai-delay-1 mx-auto mt-4 max-w-2xl text-base leading-8 text-slate-400">
            Brief once, then generate the media outputs: billboard lines, radio scripts, TV directions, social variants, and final creative packs shaped for each channel.
          </p>
          <div className="ai-fade-up ai-delay-2 mt-8 flex flex-wrap items-center justify-center gap-3">
            <Link to="/partner-enquiry" className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand to-[#14b86e] px-8 py-4 text-base font-semibold text-white shadow-[0_16px_38px_rgba(15,118,110,0.28)] transition hover:translate-y-[-1px] hover:shadow-[0_20px_42px_rgba(15,118,110,0.32)]">
              <WandSparkles className="size-4" />
              Start a media brief
            </Link>
            <Link to="/packages" className="inline-flex items-center gap-2 rounded-xl border border-slate-600 bg-slate-950 px-8 py-4 text-base font-semibold text-white transition hover:border-brand/45 hover:bg-slate-900">
              Explore packages
              <ArrowRight className="size-4" />
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}

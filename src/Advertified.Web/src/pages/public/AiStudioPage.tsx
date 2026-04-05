import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowRight, Sparkles, WandSparkles, Workflow } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { canAccessAiStudioForStatus, getAiStudioAccessMessage } from '../../features/campaigns/aiStudioAccess';
import { getActiveJobPollInterval } from '../../lib/queryPolling';
import { advertifiedApi } from '../../services/advertifiedApi';

const studioHighlights = [
  {
    title: 'Starts from approval',
    text: 'Works directly from your approved brief and media recommendation.',
  },
  {
    title: 'Multi-channel output',
    text: 'Builds billboards, radio, TV, and social from the same campaign direction.',
  },
  {
    title: 'Built for review',
    text: 'Outputs are packaged for client review, sign-off, and approval.',
  },
  {
    title: 'Ready to launch',
    text: 'Approved assets move straight into booking and launch preparation.',
  },
];

const studioChannels = [
  {
    key: 'billboard',
    title: 'Outdoor headlines & visual direction',
    tag: 'Billboards & Digital Screens',
    text: 'Campaign-specific copy, placement notes, and visual direction for real outdoor placements.',
    visualLabel: 'OOH System',
    heroLine: 'MAKE THEM LOOK',
    detailLine: 'Digital roadside billboard suite',
    outputs: ['Headline copy', 'Visual brief', 'Format specs', 'Placement notes'],
  },
  {
    key: 'radio',
    title: 'Scripts, voice routes & broadcast lines',
    tag: 'Radio & TV Audio',
    text: '30-second scripts with CTA, voice direction, and language-ready adaptations for broadcast.',
    visualLabel: 'Broadcast Audio',
    heroLine: 'VOICE. PACE. CTA.',
    detailLine: '30-second multilingual radio route',
    outputs: ['30-sec script', 'Voice direction', 'CTA lines', 'Language variants'],
  },
  {
    key: 'tv',
    title: 'Shot-by-shot scene guides',
    tag: 'TV & Video',
    text: 'Scene structure, visual direction, and production notes for short-form campaign video.',
    visualLabel: 'Video Direction',
    heroLine: 'SHOT 03 PRODUCT REVEAL',
    detailLine: 'Scene board with VO timing',
    outputs: ['Shot list', 'Scene guide', 'VO script', 'Edit notes'],
  },
  {
    key: 'social',
    title: 'Channel-native cutdowns & media variants',
    tag: 'Social & Digital',
    text: 'Platform-ready copy, caption variants, and creative direction for mobile-first placements.',
    visualLabel: 'Social Suite',
    heroLine: 'CAMPAIGN CUTDOWNS',
    detailLine: 'Feed, story, reel variations',
    outputs: ['Captions', 'Story format', 'Feed post', 'Reel guide'],
  },
];

const studioFlow = [
  {
    icon: '1',
    title: 'Package & Campaign',
    text: 'Select your package and define your campaign objectives.',
  },
  {
    icon: '2',
    title: 'Brief & Recommendation',
    text: 'Build the brief and align the media recommendation before production starts.',
  },
  {
    icon: '3',
    title: 'Client Approval',
    text: 'Approve the campaign direction before the studio begins production.',
  },
  {
    icon: '4',
    title: 'Studio Creates',
    text: 'Billboards, radio, social, and video outputs are built from one direction.',
  },
  {
    icon: '5',
    title: 'Bookings & Launch',
    text: 'Approved content moves into booking and goes live across the selected channels.',
  },
];

const studioOutputs = [
  {
    number: '01',
    title: 'Creative Direction',
    text: 'Unified visual and verbal direction across every selected channel.',
    items: ['Campaign concept statement', 'Tone and voice guide', 'Visual direction notes'],
  },
  {
    number: '02',
    title: 'Channel Assets',
    text: 'Format-specific content for the exact channels included in your media plan.',
    items: ['Headline and copy variants', 'Radio and TV scripts', 'Social captions and post briefs', 'Billboard layout notes'],
  },
  {
    number: '03',
    title: 'Launch Pack',
    text: 'Everything packaged for review, approval, and immediate handoff to booking.',
    items: ['Approval-ready summary', 'Booking-ready asset pack', 'Placement specs per channel'],
  },
];

const consoleOutputs = [
  { title: 'Billboards & Digital Screens', text: 'Generate outdoor headlines, screen copy, and visual direction built for real placements.' },
  { title: 'Radio & TV', text: 'Turn one approved brief into scripts, voice routes, and campaign-ready broadcast lines.' },
  { title: 'Social & Digital', text: 'Create fast, channel-native cutdowns and platform-ready media variants from the same idea.' },
];

const systemChannels = ['Billboards', 'Radio', 'TV', 'Social', 'Search', 'Print'];
const objectiveOptions = ['Awareness', 'FootTraffic', 'Leads', 'Sales'] as const;
const toneOptions = ['Balanced', 'Energetic', 'Premium', 'Urgent', 'Conversational'] as const;
const languageOptions = ['English', 'Zulu', 'Xhosa', 'Afrikaans'] as const;
const channelOptions = ['Radio', 'Tv', 'Billboard', 'Newspaper', 'Digital'] as const;
const videoAspectRatioOptions = ['16:9', '9:16', '1:1', '4:5'] as const;
const videoDurationOptions = [6, 10, 15, 30, 45, 60] as const;

type ConsoleStep = 'queue' | 'brief' | 'qa' | 'assets' | 'regenerate';
type StudioChannelKey = (typeof studioChannels)[number]['key'];

function formatChannelLabel(value: string) {
  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}

function renderStudioChannelVisual(channel: (typeof studioChannels)[number]) {
  if (channel.key === 'billboard') {
    return (
      <div className="relative h-full w-full overflow-hidden rounded-[32px] border border-white/10 bg-[radial-gradient(circle_at_22%_18%,rgba(20,184,110,0.18),transparent_22%),linear-gradient(180deg,#05070a_0%,#08090d_58%,#040404_100%)]">
        <div className="absolute inset-x-0 bottom-0 h-[34%] bg-[linear-gradient(180deg,rgba(6,8,10,0)_0%,#090909_100%)]" />
        <div className="absolute inset-x-[12%] bottom-[10%] h-px bg-[repeating-linear-gradient(90deg,rgba(20,184,110,0.22)_0,rgba(20,184,110,0.22)_28px,transparent_28px,transparent_56px)]" />
        <div className="absolute left-1/2 top-[24%] w-[58%] -translate-x-1/2">
          <div className="rounded-[6px] border border-[#2a3a33] bg-[linear-gradient(180deg,#0d130f_0%,#0a0c0b_100%)] px-8 py-7 shadow-[0_30px_90px_rgba(0,0,0,0.45)]">
            <div className="text-[10px] uppercase tracking-[0.28em] text-[#6ef0aa]/80">{channel.visualLabel}</div>
            <div className="mt-3 font-serif text-[clamp(2rem,3vw,3.5rem)] italic leading-[0.9] tracking-[-0.03em] text-[#f6f2eb]">
              {channel.heroLine}
            </div>
          </div>
          <div className="mx-auto flex w-[56%] justify-between">
            <span className="block h-16 w-[5px] bg-white/12" />
            <span className="block h-16 w-[5px] bg-white/12" />
          </div>
        </div>
      </div>
    );
  }

  if (channel.key === 'radio') {
    return (
      <div className="relative flex h-full w-full items-center justify-center overflow-hidden rounded-[32px] border border-white/10 bg-[radial-gradient(circle_at_center,rgba(20,184,110,0.16),transparent_24%),linear-gradient(180deg,#06080b_0%,#040506_100%)]">
        {[0, 1, 2, 3].map((ring) => (
          <div
            key={ring}
            className="absolute rounded-full border border-[#14b86e]/15"
            style={{
              width: `${180 + ring * 120}px`,
              height: `${180 + ring * 120}px`,
              animation: `ai-soft-pulse 4.8s ease-out ${ring * 0.55}s infinite`,
            }}
          />
        ))}
        <div className="relative z-10 text-center">
          <div className="text-[10px] uppercase tracking-[0.3em] text-[#6ef0aa]/80">{channel.visualLabel}</div>
          <div className="mt-5 flex h-16 items-end justify-center gap-[5px]">
            {[10, 22, 34, 48, 32, 56, 38, 24, 14, 28, 44, 18].map((height, index) => (
              <span
                key={`${channel.key}-${height}-${index}`}
                className="block w-[4px] rounded-full bg-[#6ef0aa]"
                style={{ height: `${height}px`, animation: `ai-wave-bounce 1.35s ease-in-out ${index * 0.08}s infinite` }}
              />
            ))}
          </div>
          <div className="mt-6 font-serif text-[clamp(1.4rem,2vw,2.2rem)] italic tracking-[-0.03em] text-[#f6f2eb]">
            {channel.heroLine}
          </div>
        </div>
      </div>
    );
  }

  if (channel.key === 'tv') {
    return (
      <div className="relative h-full w-full overflow-hidden rounded-[32px] border border-white/10 bg-[linear-gradient(180deg,#030303_0%,#07090c_100%)]">
        <div className="absolute inset-x-0 top-0 h-[10%] bg-black" />
        <div className="absolute inset-x-0 bottom-0 h-[10%] bg-black" />
        <div className="absolute inset-0 bg-[repeating-linear-gradient(180deg,transparent,transparent_3px,rgba(255,255,255,0.02)_3px,rgba(255,255,255,0.02)_4px)] opacity-60" />
        <div className="absolute inset-x-6 top-[14%] flex items-center justify-between text-[10px] uppercase tracking-[0.24em] text-[#6ef0aa]/70">
          <span>{channel.visualLabel}</span>
          <span>00:14:22</span>
        </div>
        <div className="relative z-10 flex h-full flex-col items-center justify-center px-8 text-center">
          <div className="flex h-18 w-18 items-center justify-center rounded-full border border-[#14b86e]/35 bg-[#14b86e]/8">
            <div className="ml-1 h-0 w-0 border-y-[12px] border-l-[20px] border-y-transparent border-l-[#14b86e]" />
          </div>
          <div className="mt-7 font-serif text-[clamp(1.8rem,2.8vw,3rem)] italic leading-[1.02] tracking-[-0.03em] text-[#f6f2eb]">
            {channel.heroLine}
          </div>
          <div className="mt-3 text-[11px] uppercase tracking-[0.24em] text-white/42">{channel.detailLine}</div>
        </div>
        <div className="absolute inset-x-[9%] bottom-[13%] h-[2px] bg-white/10">
          <div className="relative h-full w-[38%] bg-[#14b86e]">
            <span className="absolute -right-1.5 top-1/2 block h-3 w-3 -translate-y-1/2 rounded-full bg-[#14b86e]" />
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="relative flex h-full w-full items-center justify-center overflow-hidden rounded-[32px] border border-white/10 bg-[radial-gradient(circle_at_50%_24%,rgba(20,184,110,0.14),transparent_26%),linear-gradient(180deg,#050507_0%,#09090b_100%)] px-8">
      <div className="flex items-end justify-center gap-5">
        {[['-6deg', 'translate-y-8'], ['0deg', ''], ['6deg', 'translate-y-6']].map(([rotation, offset], index) => (
          <div
            key={`${channel.key}-phone-${index}`}
            className={`relative h-[290px] w-[150px] overflow-hidden rounded-[28px] border border-white/12 bg-[#0b0d0f] shadow-[0_24px_60px_rgba(0,0,0,0.34)] ${offset}`}
            style={{ transform: `rotate(${rotation})` }}
          >
            <div className="flex h-7 items-center justify-center border-b border-white/6 bg-black/40">
              <div className="h-1.5 w-12 rounded-full bg-white/10" />
            </div>
            <div className="space-y-3 p-3">
              <div className="flex items-center justify-between">
                <span className="h-5 w-5 rounded-full bg-[#14b86e]" />
                <div className="flex gap-1.5">
                  <span className="h-3 w-3 rounded-[3px] bg-white/8" />
                  <span className="h-3 w-3 rounded-[3px] bg-white/8" />
                </div>
              </div>
              <div className="rounded-[18px] border border-white/6 bg-[radial-gradient(circle_at_50%_30%,rgba(20,184,110,0.12),transparent_26%),linear-gradient(180deg,#101214_0%,#0c0d10_100%)] p-3">
                <div className="text-[9px] uppercase tracking-[0.24em] text-[#6ef0aa]/75">{channel.visualLabel}</div>
                <div className="mt-4 font-serif text-lg italic leading-[0.95] tracking-[-0.03em] text-[#f6f2eb]">
                  {index === 1 ? channel.heroLine : index === 0 ? 'STORY CUT' : 'REEL DROP'}
                </div>
              </div>
              <div className="space-y-2">
                {[0, 1].map((line) => (
                  <div key={`${channel.key}-line-${index}-${line}`} className="h-2 rounded-full bg-white/8" style={{ width: line === 0 ? '100%' : '62%' }} />
                ))}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

export function AiStudioPage() {
  const [activeFilter, setActiveFilter] = useState<'all' | StudioChannelKey>('all');
  const [cursorPosition, setCursorPosition] = useState({ x: 0, y: 0 });
  const [cursorVisible, setCursorVisible] = useState(false);
  const [cursorLarge, setCursorLarge] = useState(false);

  const filteredChannels = useMemo(
    () => studioChannels.filter((channel) => activeFilter === 'all' || channel.key === activeFilter),
    [activeFilter],
  );

  useEffect(() => {
    const mediaQuery = window.matchMedia('(pointer: fine)');
    if (!mediaQuery.matches) {
      return undefined;
    }

    const handleMove = (event: MouseEvent) => {
      setCursorPosition({ x: event.clientX, y: event.clientY });
      setCursorVisible(true);
    };

    const handleLeave = () => setCursorVisible(false);

    window.addEventListener('mousemove', handleMove);
    window.addEventListener('mouseout', handleLeave);

    return () => {
      window.removeEventListener('mousemove', handleMove);
      window.removeEventListener('mouseout', handleLeave);
    };
  }, []);

  const filterOptions: Array<{ key: 'all' | StudioChannelKey; label: string }> = [
    { key: 'all', label: 'All' },
    { key: 'billboard', label: 'Billboard' },
    { key: 'radio', label: 'Radio' },
    { key: 'tv', label: 'TV + Video' },
    { key: 'social', label: 'Social' },
  ];

  return (
    <div className="bg-[#050505] text-[#f6f2eb]">
      <div
        className={`pointer-events-none fixed left-0 top-0 z-[70] hidden rounded-full border border-white/15 bg-[#14b86e]/75 mix-blend-screen transition-[width,height,opacity,transform] duration-300 xl:block ${
          cursorVisible ? 'opacity-100' : 'opacity-0'
        } ${cursorLarge ? 'h-14 w-14' : 'h-3 w-3'}`}
        style={{ transform: `translate(${cursorPosition.x}px, ${cursorPosition.y}px) translate(-50%, -50%)` }}
      />

      <div className="overflow-hidden bg-[#050505]">
        <section className="relative min-h-screen overflow-hidden border-b border-white/8">
          <div
            className="pointer-events-none absolute inset-0 opacity-70"
            style={{
              backgroundImage:
                'linear-gradient(rgba(246,242,235,0.035) 1px, transparent 1px), linear-gradient(90deg, rgba(246,242,235,0.035) 1px, transparent 1px)',
              backgroundSize: '88px 88px',
              animation: 'ai-grid-shift 20s linear infinite',
            }}
          />
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_18%_22%,rgba(20,184,110,0.16),transparent_28%),radial-gradient(circle_at_74%_24%,rgba(255,255,255,0.06),transparent_24%),linear-gradient(180deg,rgba(0,0,0,0.14),rgba(0,0,0,0.78))]" />
          <div className="pointer-events-none absolute left-1/2 top-1/2 hidden -translate-x-1/2 -translate-y-1/2 select-none font-serif text-[clamp(8rem,22vw,24rem)] italic leading-none text-white/[0.03] lg:block">
            Studio
          </div>

          <div className="page-shell relative flex min-h-screen flex-col justify-end px-4 pb-10 pt-28 sm:px-6 sm:pb-14 lg:pt-32">
            <div className="max-w-5xl">
              <div className="ai-fade-up inline-flex items-center gap-2 rounded-full border border-[#14b86e]/30 bg-[#14b86e]/10 px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.26em] text-[#6ef0aa]">
                <Sparkles className="size-4" />
                Advertified Studio - Creative Production
              </div>
              <div className="mt-8 space-y-2">
                <div className="overflow-hidden">
                  <p className="ai-fade-up font-serif text-[clamp(3.2rem,8vw,6.8rem)] italic leading-[0.96] tracking-[-0.03em] text-[#f6f2eb]">
                    From the brief
                  </p>
                </div>
                <div className="overflow-hidden">
                  <p className="ai-fade-up ai-delay-1 font-serif text-[clamp(3.2rem,8vw,6.8rem)] italic leading-[0.96] tracking-[-0.03em] text-[#f6f2eb]">
                    to <span className="not-italic text-[#14b86e]">every channel.</span>
                  </p>
                </div>
                <div className="overflow-hidden">
                  <p className="ai-fade-up ai-delay-2 font-serif text-[clamp(3.2rem,8vw,6.8rem)] italic leading-[0.96] tracking-[-0.03em] text-[#f6f2eb]">
                    In-house.
                  </p>
                </div>
              </div>
              <div className="ai-fade-up ai-delay-3 mt-8 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
                <p className="max-w-2xl text-base leading-8 text-white/58 sm:text-lg">
                  Advertified Studio turns one approved campaign into billboard copy, radio scripts, social variants, and
                  video-ready direction without the usual handoff drag.
                </p>
                <div className="flex items-center gap-4 text-[11px] uppercase tracking-[0.24em] text-white/35">
                  <span
                    className="inline-block h-10 w-px bg-white/20"
                    style={{ animation: 'ai-scroll-pulse 2.3s ease-in-out infinite' }}
                  />
                  Scroll
                </div>
              </div>
              <div className="ai-fade-up ai-delay-4 mt-10 flex flex-wrap gap-3">
                {filterOptions.map((option) => (
                  <button
                    key={option.key}
                    type="button"
                    onClick={() => setActiveFilter(option.key)}
                    onMouseEnter={() => setCursorLarge(true)}
                    onMouseLeave={() => setCursorLarge(false)}
                    className={`rounded-full border px-5 py-3 text-[11px] font-semibold uppercase tracking-[0.18em] transition ${
                      activeFilter === option.key
                        ? 'border-[#f6f2eb] bg-[#f6f2eb] text-[#050505] shadow-[0_18px_36px_rgba(255,255,255,0.12)]'
                        : 'border-white/16 bg-white/[0.02] text-white/55 hover:border-[#14b86e]/45 hover:bg-[#14b86e]/10 hover:text-white'
                    }`}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </div>
          </div>
        </section>

        <section className="border-b border-white/8">
          {filteredChannels.map((channel, index) => (
            <article
              key={channel.key}
              className="group relative min-h-[82vh] overflow-hidden border-b border-white/6 last:border-b-0"
              onMouseEnter={() => setCursorLarge(true)}
              onMouseLeave={() => setCursorLarge(false)}
            >
              <div className="absolute inset-0 bg-[radial-gradient(circle_at_18%_14%,rgba(20,184,110,0.1),transparent_24%),radial-gradient(circle_at_82%_22%,rgba(255,255,255,0.05),transparent_18%),linear-gradient(180deg,#050505_0%,#070708_100%)]" />
              <div className="absolute inset-0 bg-[linear-gradient(180deg,rgba(5,5,5,0.06)_0%,rgba(5,5,5,0.18)_32%,rgba(5,5,5,0.86)_100%)]" />

              <div className="page-shell relative grid min-h-[82vh] items-end gap-10 px-4 py-10 sm:px-6 sm:py-12 lg:grid-cols-[1.05fr_0.95fr] lg:items-center">
                <div className={`ai-fade-up ${index % 2 === 0 ? 'ai-delay-1' : 'ai-delay-2'} order-2 lg:order-1`}>
                  {renderStudioChannelVisual(channel)}
                </div>
                <div
                  className={`ai-fade-up flex w-full flex-col gap-6 ${
                    index % 2 === 0 ? 'ai-delay-1' : 'ai-delay-2'
                  } order-1 lg:order-2 lg:pl-8`}
                >
                  <div className="max-w-2xl">
                    <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-[#14b86e]/25 bg-[#14b86e]/10 px-4 py-2 text-[10px] font-semibold uppercase tracking-[0.24em] text-[#75f2af]">
                      {channel.tag}
                    </div>
                    <h2 className="font-serif text-[clamp(2.4rem,5vw,4.7rem)] italic leading-[0.98] tracking-[-0.03em] text-[#f6f2eb]">
                      {channel.title}
                    </h2>
                    <p className="mt-4 max-w-xl text-sm leading-7 text-white/58 sm:text-base sm:leading-8">{channel.text}</p>
                    <div className="mt-6 flex flex-wrap gap-2">
                      {channel.outputs.map((item) => (
                        <span key={item} className="rounded-full border border-white/12 bg-black/25 px-3 py-1.5 text-[11px] uppercase tracking-[0.15em] text-white/58 backdrop-blur-sm">
                          {item}
                        </span>
                      ))}
                    </div>
                  </div>

                  <div className="flex items-center gap-3 text-[11px] font-semibold uppercase tracking-[0.2em] text-white/70">
                    <Link
                      to="/packages"
                      className="inline-flex items-center gap-2 rounded-full border border-white/16 bg-black/35 px-5 py-3 transition hover:border-[#14b86e]/45 hover:bg-[#14b86e]/12 hover:text-white"
                    >
                      Explore packages
                      <ArrowRight className="size-4" />
                    </Link>
                  </div>
                </div>
              </div>
            </article>
          ))}
        </section>

        <section className="border-b border-white/8 px-4 py-20 sm:px-6">
          <div className="page-shell grid gap-12 lg:grid-cols-[0.92fr_1.08fr] lg:items-center">
            <div className="ai-fade-up">
              <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-[#6ef0aa]">Advertified Studio</div>
              <h2 className="mt-5 font-serif text-[clamp(2.6rem,5vw,4.6rem)] italic leading-[1.02] tracking-[-0.03em] text-[#f6f2eb]">
                Where your approved campaign becomes <span className="not-italic text-[#14b86e]">real content.</span>
              </h2>
            </div>

            <div className="space-y-5">
              {studioHighlights.slice(0, 3).map((item, index) => (
                <div
                  key={item.title}
                  className={`ai-fade-up flex items-start justify-between gap-6 border-t border-white/8 pt-5 ${index === 0 ? 'ai-delay-1' : index === 1 ? 'ai-delay-2' : 'ai-delay-3'}`}
                >
                  <div className="font-serif text-5xl italic leading-none text-[#14b86e]/85">{`0${index + 1}`}</div>
                  <div className="max-w-sm text-right">
                    <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[#f6f2eb]">{item.title}</h3>
                    <p className="mt-2 text-sm leading-7 text-white/55">{item.text}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="border-b border-white/8 px-4 py-20 sm:px-6">
          <div className="page-shell">
            <div className="mb-12 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
              <div className="ai-fade-up">
                <div className="text-[11px] font-semibold uppercase tracking-[0.24em] text-[#6ef0aa]">The flow</div>
                <h2 className="mt-5 font-serif text-[clamp(2.6rem,5vw,4.6rem)] italic leading-[1.02] tracking-[-0.03em] text-[#f6f2eb]">
                  One continuous process.
                </h2>
              </div>
              <p className="ai-fade-up ai-delay-1 max-w-sm text-sm leading-7 text-white/55 lg:text-right">
                No re-formatting. No lost brief. One approved direction moving straight into content, review, and launch.
              </p>
            </div>

            <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-5">
              {studioFlow.map((step, index) => (
                <article
                  key={step.title}
                  className={`ai-fade-up border-l pl-5 ${index > 1 ? 'border-[#14b86e]/45' : 'border-white/10'} ${index === 0 ? 'ai-delay-1' : index === 1 ? 'ai-delay-2' : index === 2 ? 'ai-delay-3' : 'ai-delay-4'}`}
                >
                  <div className={`font-serif text-6xl italic leading-none ${index > 1 ? 'text-[#14b86e]/30' : 'text-white/[0.08]'}`}>{step.icon}</div>
                  <h3 className="mt-5 text-sm font-semibold uppercase tracking-[0.14em] text-[#f6f2eb]">{step.title}</h3>
                  <p className="mt-3 text-sm leading-7 text-white/52">{step.text}</p>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section className="px-4 py-20 sm:px-6">
          <div className="page-shell">
            <div className="mb-10 max-w-2xl">
              <div className="ai-fade-up flex items-center gap-3 text-[11px] font-semibold uppercase tracking-[0.24em] text-[#6ef0aa]">
                <Workflow className="size-4" />
                What you get
              </div>
              <h2 className="ai-fade-up ai-delay-1 mt-5 font-serif text-[clamp(2.6rem,5vw,4.6rem)] italic leading-[1.02] tracking-[-0.03em] text-[#f6f2eb]">
                A launch pack built to move.
              </h2>
            </div>

            <div className="grid gap-5 lg:grid-cols-3">
              {studioOutputs.map((item, index) => (
                <article
                  key={item.number}
                  className={`ai-fade-up rounded-[28px] border border-white/10 bg-[linear-gradient(180deg,rgba(255,255,255,0.03),rgba(255,255,255,0.01))] p-8 shadow-[0_24px_80px_rgba(0,0,0,0.22)] backdrop-blur-sm ${index === 0 ? 'ai-delay-1' : index === 1 ? 'ai-delay-2' : 'ai-delay-3'}`}
                >
                  <div className="font-serif text-6xl italic leading-none text-[#14b86e]/24">{item.number}</div>
                  <h3 className="mt-5 text-xl font-semibold text-[#f6f2eb]">{item.title}</h3>
                  <p className="mt-3 text-sm leading-7 text-white/56">{item.text}</p>
                  <ul className="mt-6 space-y-3">
                    {item.items.map((listItem) => (
                      <li key={listItem} className="flex items-start gap-3 text-sm leading-7 text-white/56">
                        <span className="mt-2 size-1.5 shrink-0 rounded-full bg-[#14b86e]" />
                        <span>{listItem}</span>
                      </li>
                    ))}
                  </ul>
                </article>
              ))}
            </div>
          </div>
        </section>

        <section className="border-t border-white/8 px-4 py-24 text-center sm:px-6">
          <div className="page-shell relative overflow-hidden rounded-[36px] border border-white/10 bg-[radial-gradient(circle_at_50%_32%,rgba(20,184,110,0.16),transparent_32%),linear-gradient(180deg,rgba(255,255,255,0.02),rgba(255,255,255,0.01))] px-6 py-16 shadow-[0_36px_110px_rgba(0,0,0,0.3)] sm:px-10">
            <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top,rgba(255,255,255,0.05),transparent_30%)]" />
            <div className="relative">
              <div className="ai-fade-up text-[11px] font-semibold uppercase tracking-[0.24em] text-[#6ef0aa]">Ready when you are</div>
              <h2 className="ai-fade-up ai-delay-1 mt-5 font-serif text-[clamp(3rem,7vw,6.2rem)] italic leading-[0.95] tracking-[-0.03em] text-[#f6f2eb]">
                Your campaign.
                <br />
                <span className="not-italic text-[#14b86e]">Real content.</span>
                <br />
                Every channel.
              </h2>
              <p className="ai-fade-up ai-delay-2 mx-auto mt-6 max-w-2xl text-base leading-8 text-white/58">
                Start with one brief. Walk away with a production-ready campaign pack designed for review, booking, and launch.
              </p>
              <div className="ai-fade-up ai-delay-3 mt-10 flex flex-wrap justify-center gap-3">
                <Link
                  to="/packages"
                  onMouseEnter={() => setCursorLarge(true)}
                  onMouseLeave={() => setCursorLarge(false)}
                  className="inline-flex items-center gap-2 rounded-full bg-[#14b86e] px-8 py-4 text-sm font-semibold text-[#04110b] shadow-[0_18px_38px_rgba(20,184,110,0.24)] transition hover:-translate-y-0.5 hover:shadow-[0_22px_44px_rgba(20,184,110,0.28)]"
                >
                  Explore packages
                  <ArrowRight className="size-4" />
                </Link>
                <Link
                  to="/partner-enquiry"
                  onMouseEnter={() => setCursorLarge(true)}
                  onMouseLeave={() => setCursorLarge(false)}
                  className="inline-flex items-center gap-2 rounded-full border border-white/16 bg-white/[0.03] px-8 py-4 text-sm font-semibold text-[#f6f2eb] transition hover:border-[#14b86e]/45 hover:bg-[#14b86e]/10"
                >
                  Talk to the team
                </Link>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}

export function AiStudioConsolePage() {
  const [searchParams] = useSearchParams();
  const campaignIdFromUrl = searchParams.get('campaignId')?.trim() ?? '';
  const [campaignId, setCampaignId] = useState(campaignIdFromUrl);
  const autoPrefillCompletedRef = useRef(false);
  const [promptOverride, setPromptOverride] = useState('');
  const [activeJobId, setActiveJobId] = useState('');

  const [regenCreativeId, setRegenCreativeId] = useState('');
  const [regenCampaignId, setRegenCampaignId] = useState('');
  const [regenFeedback, setRegenFeedback] = useState('');

  const [briefBrand, setBriefBrand] = useState('');
  const [briefObjective, setBriefObjective] = useState('Awareness');
  const [briefTone, setBriefTone] = useState('Balanced');
  const [briefMessage, setBriefMessage] = useState('');
  const [briefCta, setBriefCta] = useState('Get started today');
  const [briefAudience, setBriefAudience] = useState('LSM 5-8 commuters');
  const [briefLanguages, setBriefLanguages] = useState<string[]>(['English', 'Zulu']);
  const [briefChannels, setBriefChannels] = useState<string[]>(['Radio', 'Billboard', 'Digital']);

  const [qaCampaignId, setQaCampaignId] = useState('');

  const [assetCampaignId, setAssetCampaignId] = useState('');
  const [assetCreativeId, setAssetCreativeId] = useState('');
  const [assetVoiceScript, setAssetVoiceScript] = useState('');
  const [assetProduct, setAssetProduct] = useState('Advertified AI Creative Studio');
  const [assetIndustry, setAssetIndustry] = useState('Advertising');
  const [assetGoal, setAssetGoal] = useState('conversion');
  const [assetPlatform, setAssetPlatform] = useState('radio');
  const [assetLanguage, setAssetLanguage] = useState('english');
  const [assetBudgetTier, setAssetBudgetTier] = useState('mid');
  const [assetAudience, setAssetAudience] = useState('SME owners and growth-focused businesses');
  const [assetObjective, setAssetObjective] = useState('Awareness');
  const [assetPackageBudget, setAssetPackageBudget] = useState(50000);
  const [assetCampaignTier, setAssetCampaignTier] = useState('standard');
  const [assetVoicePackId, setAssetVoicePackId] = useState('');
  const [assetAllowTierUpsell, setAssetAllowTierUpsell] = useState(false);
  const [assetGenerateVariants, setAssetGenerateVariants] = useState(true);
  const [assetVisualDirection, setAssetVisualDirection] = useState('');
  const [assetVideoSceneJson, setAssetVideoSceneJson] = useState('{"scene":1,"visual":"Product hero shot","audio":"Upbeat track"}');
  const [assetVideoScript, setAssetVideoScript] = useState('');
  const [assetVideoAspectRatio, setAssetVideoAspectRatio] = useState('16:9');
  const [assetVideoDurationSeconds, setAssetVideoDurationSeconds] = useState(30);
  const [assetJobId, setAssetJobId] = useState('');
  const [prefillMessage, setPrefillMessage] = useState('');

  const [activeConsoleStep, setActiveConsoleStep] = useState<ConsoleStep>('queue');
  const campaignAccessQuery = useQuery({
    queryKey: ['ai-studio-campaign-access', campaignId],
    queryFn: () => advertifiedApi.getCampaign(campaignId.trim()),
    enabled: campaignId.trim().length > 0,
    retry: false,
  });
  const selectedCampaign = campaignAccessQuery.data;
  const aiStudioAccessAllowed = campaignId.trim().length === 0
    || !selectedCampaign
    || canAccessAiStudioForStatus(selectedCampaign.status);
  const aiStudioAccessMessage = selectedCampaign
    ? getAiStudioAccessMessage(selectedCampaign.status)
    : 'AI Studio becomes available only after a purchased campaign is complete and ready to go live.';

  const submitJobMutation = useMutation({
    mutationFn: () => advertifiedApi.submitAiPlatformJob({
      campaignId: campaignId.trim(),
      promptOverride: promptOverride.trim() || undefined,
    }),
    onSuccess: (response) => {
      setActiveJobId(response.jobId);
      setActiveConsoleStep('brief');
    },
  });

  const statusQuery = useQuery({
    queryKey: ['ai-platform-job-status', activeJobId],
    queryFn: () => advertifiedApi.getAiPlatformJobStatus(activeJobId),
    enabled: activeJobId.trim().length > 0,
    refetchInterval: (query) => getActiveJobPollInterval(query.state.data?.status, 3_000),
  });

  const regenerateMutation = useMutation({
    mutationFn: () => advertifiedApi.regenerateAiPlatformCreative({
      creativeId: regenCreativeId.trim(),
      campaignId: regenCampaignId.trim(),
      feedback: regenFeedback.trim(),
    }),
  });

  const generateFromBriefMutation = useMutation({
    mutationFn: () => advertifiedApi.generateAiPlatformCreativesFromBrief({
      brief: {
        campaignId: campaignId.trim(),
        brand: briefBrand.trim(),
        objective: briefObjective.trim(),
        tone: briefTone.trim(),
        keyMessage: briefMessage.trim(),
        callToAction: briefCta.trim(),
        audienceInsights: briefAudience.split(',').map((item) => item.trim()).filter(Boolean),
        languages: briefLanguages,
        channels: briefChannels,
      },
    }),
    onSuccess: () => setActiveConsoleStep('qa'),
  });

  const qaQuery = useQuery({
    queryKey: ['ai-platform-qa-results', qaCampaignId],
    queryFn: () => advertifiedApi.getAiPlatformQaResults(qaCampaignId),
    enabled: qaCampaignId.trim().length > 0,
  });

  const voicePacksQuery = useQuery({
    queryKey: ['ai-platform-voice-packs', assetCampaignId, assetPackageBudget, assetCampaignTier],
    queryFn: () => advertifiedApi.getAiPlatformVoicePacks('ElevenLabs', {
      campaignId: assetCampaignId.trim() || undefined,
      packageBudget: assetPackageBudget,
      campaignTier: assetCampaignTier,
    }),
  });

  const recommendVoicePackMutation = useMutation({
    mutationFn: () => advertifiedApi.getAiPlatformVoicePackRecommendation({
      campaignId: assetCampaignId.trim(),
      audience: assetAudience.trim(),
      objective: assetObjective.trim(),
      packageBudget: assetPackageBudget,
      campaignTier: assetCampaignTier,
    }),
    onSuccess: (response) => setAssetVoicePackId(response.voicePackId),
  });

  const autoTemplateMutation = useMutation({
    mutationFn: () => advertifiedApi.selectAiPlatformVoiceTemplate({
      campaignId: assetCampaignId.trim(),
      product: assetProduct.trim(),
      industry: assetIndustry.trim(),
      audience: assetAudience.trim(),
      goal: assetGoal.trim(),
      budgetTier: assetBudgetTier.trim(),
      language: assetLanguage.trim(),
      platform: assetPlatform.trim(),
      objective: assetObjective.trim(),
      brand: 'Advertified',
      business: 'Advertified',
    }),
    onSuccess: (response) => {
      setAssetVoiceScript(response.finalPrompt);
      if (response.primaryVoicePackId) {
        setAssetVoicePackId(response.primaryVoicePackId);
      }
    },
  });

  const queueVoiceAssetMutation = useMutation({
    mutationFn: () => advertifiedApi.queueAiPlatformVoiceAsset({
      campaignId: assetCampaignId.trim(),
      creativeId: assetCreativeId.trim(),
      script: assetVoiceScript.trim(),
      voicePackId: assetVoicePackId || undefined,
      voiceType: 'Standard',
      language: 'English',
      audience: assetAudience.trim(),
      objective: assetObjective.trim(),
      packageBudget: assetPackageBudget,
      campaignTier: assetCampaignTier,
      allowTierUpsell: assetAllowTierUpsell,
      generateSaLanguageVariants: assetGenerateVariants,
      requestedLanguages: ['Zulu', 'Afrikaans'],
    }),
    onSuccess: (response) => setAssetJobId(response.jobId),
  });

  const queueImageAssetMutation = useMutation({
    mutationFn: () => advertifiedApi.queueAiPlatformImageAsset({
      campaignId: assetCampaignId.trim(),
      creativeId: assetCreativeId.trim(),
      visualDirection: assetVisualDirection.trim(),
      style: 'Bold',
      variations: 1,
    }),
    onSuccess: (response) => setAssetJobId(response.jobId),
  });

  const queueVideoAssetMutation = useMutation({
    mutationFn: () => advertifiedApi.queueAiPlatformVideoAsset({
      campaignId: assetCampaignId.trim(),
      creativeId: assetCreativeId.trim(),
      sceneBreakdownJson: assetVideoSceneJson.trim(),
      script: assetVideoScript.trim(),
      language: 'English',
      aspectRatio: assetVideoAspectRatio,
      durationSeconds: assetVideoDurationSeconds,
    }),
    onSuccess: (response) => setAssetJobId(response.jobId),
  });

  const assetStatusQuery = useQuery({
    queryKey: ['ai-platform-asset-job-status', assetJobId],
    queryFn: () => advertifiedApi.getAiPlatformAssetJobStatus(assetJobId),
    enabled: assetJobId.trim().length > 0,
    refetchInterval: (query) => getActiveJobPollInterval(query.state.data?.status, 2_500),
  });

  const canSubmit = useMemo(
    () => campaignId.trim().length > 0 && aiStudioAccessAllowed && !submitJobMutation.isPending,
    [campaignId, aiStudioAccessAllowed, submitJobMutation.isPending],
  );

  const canRegenerate = useMemo(
    () => regenCreativeId.trim().length > 0
      && regenCampaignId.trim().length > 0
      && regenFeedback.trim().length > 0
      && !regenerateMutation.isPending,
    [regenCreativeId, regenCampaignId, regenFeedback, regenerateMutation.isPending],
  );

  const canGenerateFromBrief = useMemo(
    () => campaignId.trim().length > 0
      && aiStudioAccessAllowed
      && briefBrand.trim().length > 0
      && briefLanguages.length > 0
      && briefChannels.length > 0
      && !generateFromBriefMutation.isPending,
    [campaignId, aiStudioAccessAllowed, briefBrand, briefLanguages, briefChannels, generateFromBriefMutation.isPending],
  );

  const canQueueAssetJob = useMemo(
    () => assetCampaignId.trim().length > 0 && assetCreativeId.trim().length > 0 && aiStudioAccessAllowed,
    [assetCampaignId, assetCreativeId, aiStudioAccessAllowed],
  );

  const prefillFromCampaignMutation = useMutation({
    mutationFn: async () => advertifiedApi.getCampaign(campaignId.trim()),
    onSuccess: (campaign) => {
      if (!canAccessAiStudioForStatus(campaign.status)) {
        setPrefillMessage(getAiStudioAccessMessage(campaign.status));
        return;
      }

      const recommendation = campaign.recommendations.find((item) => item.status === 'approved')
        ?? (campaign.recommendation?.status === 'approved' ? campaign.recommendation : campaign.recommendations[0] ?? campaign.recommendation);
      const mappedObjective = mapObjectiveToStudio(campaign.brief?.objective);
      const mappedChannels = mapChannelsToStudio(campaign, recommendation);
      const mappedLanguages = mapLanguagesToStudio(campaign, recommendation);
      const mappedAudience = mapAudienceToStudio(campaign);
      const mappedBrand = campaign.businessName?.trim() || campaign.campaignName?.trim() || 'Advertified';
      const mappedMessage = recommendation?.summary?.trim()
        || campaign.brief?.specialRequirements?.trim()
        || campaign.brief?.creativeNotes?.trim()
        || '';

      setBriefBrand(mappedBrand);
      setBriefObjective(mappedObjective);
      setBriefMessage(mappedMessage);
      setBriefAudience(mappedAudience);
      setBriefChannels(mappedChannels.length > 0 ? mappedChannels : ['Radio', 'Billboard', 'Digital']);
      setBriefLanguages(mappedLanguages.length > 0 ? mappedLanguages : ['English']);

      setQaCampaignId(campaign.id);
      setRegenCampaignId(campaign.id);
      setAssetCampaignId(campaign.id);
      setAssetAudience(mappedAudience);
      setAssetObjective(mappedObjective);
      setAssetPackageBudget(campaign.selectedBudget);
      setAssetPlatform(derivePlatformFromChannels(mappedChannels));
      setAssetLanguage(derivePrimaryLanguage(mappedLanguages));
      setAssetVoiceScript(mappedMessage || assetVoiceScript);

      setPrefillMessage(recommendation?.status === 'approved'
        ? 'Loaded campaign brief and approved recommendation into AI Studio.'
        : 'Loaded campaign details. No approved recommendation found, so brief defaults were used.');
      setActiveConsoleStep('brief');
    },
    onError: () => {
      setPrefillMessage('Could not load campaign details. Confirm campaign ID and access permissions.');
    },
  });

  useEffect(() => {
    if (
      autoPrefillCompletedRef.current
      || !campaignIdFromUrl
      || prefillFromCampaignMutation.isPending
    ) {
      return;
    }

    autoPrefillCompletedRef.current = true;
    prefillFromCampaignMutation.mutate();
  }, [campaignIdFromUrl, prefillFromCampaignMutation]);

  const selectedVoicePack = useMemo(
    () => (voicePacksQuery.data ?? []).find((item) => item.id === assetVoicePackId),
    [assetVoicePackId, voicePacksQuery.data],
  );

  const toggleBriefLanguage = (language: string) => {
    setBriefLanguages((current) => (current.includes(language)
      ? current.filter((item) => item !== language)
      : [...current, language]));
  };

  const toggleBriefChannel = (channel: string) => {
    setBriefChannels((current) => (current.includes(channel)
      ? current.filter((item) => item !== channel)
      : [...current, channel]));
  };

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
              Use one approved brief to generate campaign media across billboards, radio, TV, social, and digital.
            </p>
            <div className="ai-fade-up ai-delay-3 mt-8 flex flex-wrap items-center gap-3">
              <Link to="/creative" className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand to-[#14b86e] px-7 py-3 text-sm font-semibold text-white shadow-[0_16px_38px_rgba(15,118,110,0.28)] transition hover:translate-y-[-1px] hover:shadow-[0_20px_42px_rgba(15,118,110,0.32)]">
                Back to creative dashboard
                <ArrowRight className="size-4" />
              </Link>
              <Link to="/creative/studio-demo" className="inline-flex items-center gap-2 rounded-xl border border-slate-600 bg-slate-950 px-7 py-3 text-sm font-semibold text-white transition hover:border-brand/45 hover:bg-slate-900">
                Reset to demo
              </Link>
            </div>
          </div>

          <div className="rounded-[30px] border border-slate-800 bg-[#0A0A0A] p-7 sm:p-8">
            <p className="text-xs font-semibold uppercase tracking-[0.24em] text-slate-400">Studio Flow</p>
            <div className="mt-5 space-y-3 text-sm text-slate-200">
              <div className="rounded-2xl border border-slate-700 bg-slate-900/60 px-4 py-3">1. Queue campaign job</div>
              <div className="rounded-2xl border border-slate-700 bg-slate-900/60 px-4 py-3">2. Generate from brief</div>
              <div className="rounded-2xl border border-slate-700 bg-slate-900/60 px-4 py-3">3. Review QA signals</div>
              <div className="rounded-2xl border border-slate-700 bg-slate-900/60 px-4 py-3">4. Queue voice/image/video assets</div>
            </div>
          </div>
        </div>
      </section>

      <section className="page-shell px-6 py-6 sm:px-10">
        <div className="grid gap-5 md:grid-cols-3">
          {consoleOutputs.map((item, index) => (
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

      <section className="page-shell px-6 pb-20 sm:px-10">
        <div className="rounded-[30px] border border-slate-800 bg-[#0A0A0A] p-6 sm:p-8">
          <h3 className="text-2xl font-semibold text-slate-100">AI Platform Console</h3>
          <p className="mt-2 text-sm text-slate-400">This console is now step-based. Start at step 1 and move right.</p>

          <div className={`mt-5 rounded-2xl border p-4 text-sm ${
            aiStudioAccessAllowed
              ? 'border-emerald-500/30 bg-emerald-500/10 text-emerald-200'
              : 'border-amber-500/30 bg-amber-500/10 text-amber-200'
          }`}>
            {aiStudioAccessMessage}
          </div>

          <div className="mt-5 rounded-2xl border border-slate-800 bg-slate-900/50 p-4">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-400">How To Use</p>
            <ol className="mt-3 list-decimal space-y-1 pl-5 text-sm text-slate-300">
              <li>Enter campaign id and queue job.</li>
              <li>Generate creatives from brief.</li>
              <li>Load QA, confirm quality.</li>
              <li>Queue assets and monitor status.</li>
              <li>Regenerate only when feedback is needed.</li>
            </ol>
          </div>

          <div className="mt-5 flex flex-wrap gap-2">
            {[
              ['queue', '1. Queue'],
              ['brief', '2. Brief'],
              ['qa', '3. QA'],
              ['assets', '4. Assets'],
              ['regenerate', '5. Regenerate'],
            ].map(([id, label]) => (
              <button
                key={id}
                type="button"
                onClick={() => setActiveConsoleStep(id as ConsoleStep)}
                className={`rounded-full border px-4 py-2 text-xs font-semibold uppercase tracking-[0.16em] ${activeConsoleStep === id ? 'border-brand bg-brand/20 text-brand-soft' : 'border-slate-700 bg-slate-950 text-slate-300'}`}
              >
                {label}
              </button>
            ))}
          </div>

          {activeConsoleStep === 'queue' ? (
            <div className="mt-6">
              <h4 className="text-lg font-semibold text-slate-100">Queue AI Job</h4>
              <div className="mt-4 grid gap-4 md:grid-cols-2">
                <input value={campaignId} onChange={(event) => setCampaignId(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Campaign Id" />
                <input value={promptOverride} onChange={(event) => setPromptOverride(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Prompt override (optional)" />
              </div>
              <div className="mt-4 flex flex-wrap gap-3">
                <button type="button" onClick={() => submitJobMutation.mutate()} disabled={!canSubmit} className="rounded-xl bg-gradient-to-r from-brand to-[#14b86e] px-5 py-3 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">
                  {submitJobMutation.isPending ? 'Queueing...' : 'Queue AI job'}
                </button>
                <button
                  type="button"
                  onClick={() => prefillFromCampaignMutation.mutate()}
                  disabled={campaignId.trim().length === 0 || prefillFromCampaignMutation.isPending || campaignAccessQuery.isLoading}
                  className="rounded-xl border border-slate-600 bg-slate-900 px-5 py-3 text-sm font-semibold text-slate-100 transition hover:border-brand/45 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {prefillFromCampaignMutation.isPending ? 'Loading recommendation...' : 'Start from approved recommendation'}
                </button>
                <input value={activeJobId} onChange={(event) => setActiveJobId(event.target.value)} className="min-w-[320px] flex-1 rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Job id to track" />
              </div>
              {prefillMessage ? <p className="mt-3 text-sm text-emerald-300">{prefillMessage}</p> : null}
              <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-900/60 p-4 text-sm">
                <p><span className="text-slate-400">Status:</span> {statusQuery.data?.status ?? 'not started'}</p>
                <p><span className="text-slate-400">Campaign:</span> {statusQuery.data?.campaignId ?? '-'}</p>
                {statusQuery.data?.error ? <p className="text-rose-300"><span className="text-slate-400">Error:</span> {statusQuery.data.error}</p> : null}
              </div>
            </div>
          ) : null}

          {activeConsoleStep === 'brief' ? (
            <div className="mt-6 border-t border-slate-800 pt-6">
              <h4 className="text-lg font-semibold text-slate-100">Generate Creatives From Brief</h4>
              <div className="mt-4 grid gap-4 md:grid-cols-2">
                <input value={briefBrand} onChange={(event) => setBriefBrand(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Brand" />
                <select value={briefObjective} onChange={(event) => setBriefObjective(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white">
                  {objectiveOptions.map((objective) => <option key={objective} value={objective}>{objective}</option>)}
                </select>
                <select value={briefTone} onChange={(event) => setBriefTone(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white">
                  {toneOptions.map((tone) => <option key={tone} value={tone}>{tone}</option>)}
                </select>
                <input value={briefCta} onChange={(event) => setBriefCta(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="CTA" />
                <input value={briefAudience} onChange={(event) => setBriefAudience(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Audience insights (comma-separated)" />
                <input value={briefMessage} onChange={(event) => setBriefMessage(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Key message" />
                <div className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3">
                  <p className="text-xs uppercase tracking-[0.16em] text-slate-400">Languages</p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {languageOptions.map((language) => (
                      <button key={language} type="button" onClick={() => toggleBriefLanguage(language)} className={`rounded-full border px-3 py-1 text-xs ${briefLanguages.includes(language) ? 'border-brand bg-brand/20 text-brand-soft' : 'border-slate-600 text-slate-300'}`}>
                        {language}
                      </button>
                    ))}
                  </div>
                </div>
                <div className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3">
                  <p className="text-xs uppercase tracking-[0.16em] text-slate-400">Channels</p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {channelOptions.map((channel) => (
                      <button key={channel} type="button" onClick={() => toggleBriefChannel(channel)} className={`rounded-full border px-3 py-1 text-xs ${briefChannels.includes(channel) ? 'border-brand bg-brand/20 text-brand-soft' : 'border-slate-600 text-slate-300'}`}>
                        {channel}
                      </button>
                    ))}
                  </div>
                </div>
              </div>
              <button type="button" onClick={() => generateFromBriefMutation.mutate()} disabled={!canGenerateFromBrief} className="mt-4 rounded-xl bg-gradient-to-r from-brand to-[#14b86e] px-5 py-3 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">
                {generateFromBriefMutation.isPending ? 'Generating...' : 'Generate creatives'}
              </button>
              {generateFromBriefMutation.data ? <p className="mt-3 text-sm text-emerald-300">Generated: {generateFromBriefMutation.data.creatives.length} creatives.</p> : null}
            </div>
          ) : null}

          {activeConsoleStep === 'qa' ? (
            <div className="mt-6 border-t border-slate-800 pt-6">
              <h4 className="text-lg font-semibold text-slate-100">QA Dashboard</h4>
              <div className="mt-4 flex gap-3">
                <input value={qaCampaignId} onChange={(event) => setQaCampaignId(event.target.value)} className="flex-1 rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Campaign Id" />
                <button type="button" onClick={() => qaQuery.refetch()} disabled={!aiStudioAccessAllowed} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">Load QA</button>
              </div>
              <div className="mt-4 space-y-2 text-sm">
                {(qaQuery.data ?? []).slice(0, 10).map((item) => (
                  <div key={`${item.creativeId}-${item.createdAt}`} className="rounded-lg border border-slate-800 bg-slate-900/60 p-3">
                    <p className="text-slate-200">{formatChannelLabel(item.channel)} · {item.language} · <span className="font-semibold">{item.status}</span></p>
                    <p className="text-slate-400">Score: {item.finalScore} | Risk: {item.riskLevel}</p>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {activeConsoleStep === 'assets' ? (
            <div className="mt-6 border-t border-slate-800 pt-6">
              <h4 className="text-lg font-semibold text-slate-100">Asset Queue</h4>
              <div className="mt-4 grid gap-4 md:grid-cols-2">
                <input value={assetCampaignId} onChange={(event) => setAssetCampaignId(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Campaign Id" />
                <input value={assetCreativeId} onChange={(event) => setAssetCreativeId(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Creative Id" />
                <input value={assetProduct} onChange={(event) => setAssetProduct(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Product" />
                <input value={assetIndustry} onChange={(event) => setAssetIndustry(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Industry" />
                <input value={assetGoal} onChange={(event) => setAssetGoal(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Goal (awareness/conversion/engagement)" />
                <input value={assetPlatform} onChange={(event) => setAssetPlatform(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Platform (radio/social/billboard/tv)" />
                <input value={assetLanguage} onChange={(event) => setAssetLanguage(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Language (english/zulu/afrikaans/mixed)" />
                <input value={assetBudgetTier} onChange={(event) => setAssetBudgetTier(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Budget tier (low/mid/high)" />
                <input value={assetAudience} onChange={(event) => setAssetAudience(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white md:col-span-2" placeholder="Audience summary" />
                <input value={assetObjective} onChange={(event) => setAssetObjective(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Objective" />
                <input value={assetCampaignTier} onChange={(event) => setAssetCampaignTier(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Campaign tier (standard/premium/exclusive)" />
                <input value={assetPackageBudget} onChange={(event) => setAssetPackageBudget(Number(event.target.value) || 0)} type="number" className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Package budget" />
                <select value={assetVoicePackId} onChange={(event) => setAssetVoicePackId(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white md:col-span-2">
                  <option value="">Select voice pack (optional)</option>
                  {(voicePacksQuery.data ?? []).map((pack) => (
                    <option key={pack.id} value={pack.id}>{`${pack.name} (${pack.pricingTier})`}</option>
                  ))}
                </select>
                {selectedVoicePack ? (
                  <div className="rounded-xl border border-slate-700 bg-slate-900/70 px-4 py-3 text-xs text-slate-300 md:col-span-2">
                    <p className="font-semibold text-slate-100">{selectedVoicePack.name}</p>
                    <p>{selectedVoicePack.promptTemplate}</p>
                  </div>
                ) : null}
                <input value={assetVoiceScript} onChange={(event) => setAssetVoiceScript(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white md:col-span-2" placeholder="Voice script" />
                <input value={assetVisualDirection} onChange={(event) => setAssetVisualDirection(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white md:col-span-2" placeholder="Image visual direction" />
                <input value={assetVideoSceneJson} onChange={(event) => setAssetVideoSceneJson(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white md:col-span-2" placeholder="Video scene JSON" />
                <input value={assetVideoScript} onChange={(event) => setAssetVideoScript(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white md:col-span-2" placeholder="Video script" />
                <select value={assetVideoAspectRatio} onChange={(event) => setAssetVideoAspectRatio(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white">
                  {videoAspectRatioOptions.map((ratio) => (
                    <option key={ratio} value={ratio}>{`Video aspect ratio: ${ratio}`}</option>
                  ))}
                </select>
                <select value={assetVideoDurationSeconds} onChange={(event) => setAssetVideoDurationSeconds(Number(event.target.value))} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white">
                  {videoDurationOptions.map((seconds) => (
                    <option key={seconds} value={seconds}>{`Video duration: ${seconds}s`}</option>
                  ))}
                </select>
              </div>
              <div className="mt-4 flex flex-wrap gap-3">
                <button type="button" onClick={() => autoTemplateMutation.mutate()} disabled={!assetCampaignId.trim() || !assetProduct.trim() || autoTemplateMutation.isPending} className="rounded-xl border border-emerald-400/40 bg-emerald-500/10 px-5 py-3 text-sm font-semibold text-emerald-200 disabled:opacity-50">
                  {autoTemplateMutation.isPending ? 'Selecting...' : 'Auto template + voice'}
                </button>
                <button type="button" onClick={() => recommendVoicePackMutation.mutate()} disabled={!assetCampaignId.trim() || recommendVoicePackMutation.isPending} className="rounded-xl border border-brand/40 bg-brand/10 px-5 py-3 text-sm font-semibold text-brand-soft disabled:opacity-50">
                  {recommendVoicePackMutation.isPending ? 'Recommending...' : 'Recommend voice pack'}
                </button>
                <button type="button" onClick={() => queueVoiceAssetMutation.mutate()} disabled={!canQueueAssetJob || !assetVoiceScript.trim() || queueVoiceAssetMutation.isPending} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white disabled:opacity-50">Queue voice</button>
                <button type="button" onClick={() => queueImageAssetMutation.mutate()} disabled={!canQueueAssetJob || !assetVisualDirection.trim() || queueImageAssetMutation.isPending} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white disabled:opacity-50">Queue image</button>
                <button type="button" onClick={() => queueVideoAssetMutation.mutate()} disabled={!canQueueAssetJob || !assetVideoScript.trim() || queueVideoAssetMutation.isPending} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white disabled:opacity-50">Queue video</button>
              </div>
              <div className="mt-3 flex flex-wrap gap-4 text-xs text-slate-300">
                <label className="inline-flex items-center gap-2">
                  <input type="checkbox" checked={assetAllowTierUpsell} onChange={(event) => setAssetAllowTierUpsell(event.target.checked)} />
                  Allow tier upsell
                </label>
                <label className="inline-flex items-center gap-2">
                  <input type="checkbox" checked={assetGenerateVariants} onChange={(event) => setAssetGenerateVariants(event.target.checked)} />
                  Auto Zulu/Afrikaans variants
                </label>
              </div>
              <div className="mt-4 flex gap-3">
                <input value={assetJobId} onChange={(event) => setAssetJobId(event.target.value)} className="flex-1 rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Asset job id" />
                <button type="button" onClick={() => assetStatusQuery.refetch()} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white">Check job</button>
              </div>
              <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-900/60 p-4 text-sm">
                <p><span className="text-slate-400">Job status:</span> {assetStatusQuery.data?.status ?? 'not started'}</p>
                <p><span className="text-slate-400">Asset kind:</span> {assetStatusQuery.data?.assetKind ?? '-'}</p>
                <p><span className="text-slate-400">Asset URL:</span> {assetStatusQuery.data?.assetUrl ?? '-'}</p>
                <p><span className="text-slate-400">Applied language:</span> {queueVoiceAssetMutation.data?.appliedLanguage ?? '-'}</p>
                <p><span className="text-slate-400">Upsell:</span> {queueVoiceAssetMutation.data?.upsellRequired ? queueVoiceAssetMutation.data?.upsellMessage ?? 'Required' : 'Not required'}</p>
                {queueVoiceAssetMutation.data?.voiceQa ? (
                  <p><span className="text-slate-400">Voice QA:</span> A {queueVoiceAssetMutation.data.voiceQa.authenticity} | C {queueVoiceAssetMutation.data.voiceQa.clarity} | Conv {queueVoiceAssetMutation.data.voiceQa.conversionPotential}</p>
                ) : null}
                {queueVoiceAssetMutation.data?.variantJobIds?.length ? (
                  <p><span className="text-slate-400">Variant jobs:</span> {queueVoiceAssetMutation.data.variantJobIds.join(', ')}</p>
                ) : null}
                {autoTemplateMutation.data ? (
                  <p><span className="text-slate-400">Template:</span> #{autoTemplateMutation.data.templateNumber} {autoTemplateMutation.data.templateName}</p>
                ) : null}
              </div>
            </div>
          ) : null}

          {activeConsoleStep === 'regenerate' ? (
            <div className="mt-6 border-t border-slate-800 pt-6">
              <h4 className="text-lg font-semibold text-slate-100">Regenerate With Feedback</h4>
              <div className="mt-4 grid gap-4 md:grid-cols-3">
                <input value={regenCreativeId} onChange={(event) => setRegenCreativeId(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Creative Id" />
                <input value={regenCampaignId} onChange={(event) => setRegenCampaignId(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Campaign Id" />
                <input value={regenFeedback} onChange={(event) => setRegenFeedback(event.target.value)} className="rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Feedback" />
              </div>
              <button type="button" onClick={() => regenerateMutation.mutate()} disabled={!canRegenerate} className="mt-4 rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">
                {regenerateMutation.isPending ? 'Regenerating...' : 'Regenerate creative'}
              </button>
            </div>
          ) : null}
        </div>
      </section>

      <section className="page-shell px-6 pb-16 pt-2 text-center sm:px-10">
        <div className="rounded-[30px] border border-slate-800 bg-[#0A0A0A] px-6 py-10 sm:px-10 sm:py-12">
          <h2 className="ai-fade-up text-4xl font-semibold tracking-tight sm:text-5xl">Create your campaign media.</h2>
          <p className="ai-fade-up ai-delay-1 mx-auto mt-4 max-w-2xl text-base leading-8 text-slate-400">
            Brief once, then generate channel-ready outputs with QA and asset pipelines.
          </p>
          <div className="ai-fade-up ai-delay-2 mt-8 flex flex-wrap items-center justify-center gap-3">
            <Link to="/creative" className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand to-[#14b86e] px-8 py-4 text-base font-semibold text-white shadow-[0_16px_38px_rgba(15,118,110,0.28)] transition hover:translate-y-[-1px] hover:shadow-[0_20px_42px_rgba(15,118,110,0.32)]">
              <WandSparkles className="size-4" />
              Back to creative dashboard
            </Link>
            <Link to="/creative/studio-demo" className="inline-flex items-center gap-2 rounded-xl border border-slate-600 bg-slate-950 px-8 py-4 text-base font-semibold text-white transition hover:border-brand/45 hover:bg-slate-900">
              Reset to demo
              <ArrowRight className="size-4" />
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}

function mapObjectiveToStudio(objective?: string) {
  const normalized = objective?.trim().toLowerCase() ?? '';
  if (normalized.includes('foot') || normalized.includes('traffic')) {
    return 'FootTraffic';
  }

  if (normalized.includes('lead')) {
    return 'Leads';
  }

  if (normalized.includes('sale') || normalized.includes('conversion')) {
    return 'Sales';
  }

  return 'Awareness';
}

function mapChannelsToStudio(campaign: Awaited<ReturnType<typeof advertifiedApi.getCampaign>>, recommendation?: { items: Array<{ channel: string }> }) {
  const normalized = new Set<string>();
  for (const item of recommendation?.items ?? []) {
    const channel = item.channel.trim().toLowerCase();
    if (channel.includes('radio')) {
      normalized.add('Radio');
      continue;
    }

    if (channel.includes('tv') || channel.includes('television')) {
      normalized.add('Tv');
      continue;
    }

    if (channel.includes('billboard') || channel.includes('ooh') || channel.includes('screen')) {
      normalized.add('Billboard');
      continue;
    }

    if (channel.includes('digital') || channel.includes('social')) {
      normalized.add('Digital');
      continue;
    }

    if (channel.includes('print') || channel.includes('newspaper')) {
      normalized.add('Newspaper');
    }
  }

  for (const mediaType of campaign.brief?.preferredMediaTypes ?? []) {
    const value = mediaType.trim().toLowerCase();
    if (value.includes('radio')) {
      normalized.add('Radio');
    } else if (value.includes('tv')) {
      normalized.add('Tv');
    } else if (value.includes('billboard') || value.includes('ooh')) {
      normalized.add('Billboard');
    } else if (value.includes('digital') || value.includes('social')) {
      normalized.add('Digital');
    } else if (value.includes('print')) {
      normalized.add('Newspaper');
    }
  }

  return Array.from(normalized);
}

function mapLanguagesToStudio(campaign: Awaited<ReturnType<typeof advertifiedApi.getCampaign>>, recommendation?: { items: Array<{ language?: string }> }) {
  const normalized = new Set<string>();
  for (const language of campaign.brief?.targetLanguages ?? []) {
    const mapped = normalizeLanguage(language);
    if (mapped) {
      normalized.add(mapped);
    }
  }

  for (const item of recommendation?.items ?? []) {
    const mapped = normalizeLanguage(item.language ?? '');
    if (mapped) {
      normalized.add(mapped);
    }
  }

  return Array.from(normalized);
}

function mapAudienceToStudio(campaign: Awaited<ReturnType<typeof advertifiedApi.getCampaign>>) {
  if (campaign.brief?.targetAudienceNotes?.trim()) {
    return campaign.brief.targetAudienceNotes.trim();
  }

  const parts: string[] = [];
  if (campaign.brief?.targetAgeMin && campaign.brief?.targetAgeMax) {
    parts.push(`Age ${campaign.brief.targetAgeMin}-${campaign.brief.targetAgeMax}`);
  }

  if (campaign.brief?.targetLsmMin && campaign.brief?.targetLsmMax) {
    parts.push(`LSM ${campaign.brief.targetLsmMin}-${campaign.brief.targetLsmMax}`);
  }

  if (campaign.brief?.geographyScope) {
    parts.push(`${campaign.brief.geographyScope} focus`);
  }

  return parts.length > 0 ? parts.join(', ') : 'LSM 5-8 commuters';
}

function normalizeLanguage(value: string) {
  const normalized = value.trim().toLowerCase();
  if (!normalized) {
    return '';
  }

  if (normalized.includes('zulu')) {
    return 'Zulu';
  }

  if (normalized.includes('xhosa')) {
    return 'Xhosa';
  }

  if (normalized.includes('afrikaans')) {
    return 'Afrikaans';
  }

  if (normalized.includes('english')) {
    return 'English';
  }

  return '';
}

function derivePlatformFromChannels(channels: string[]) {
  const lower = channels.map((item) => item.toLowerCase());
  if (lower.includes('radio')) {
    return 'radio';
  }

  if (lower.includes('digital')) {
    return 'social';
  }

  if (lower.includes('tv')) {
    return 'tv';
  }

  return 'radio';
}

function derivePrimaryLanguage(languages: string[]) {
  const first = languages[0]?.toLowerCase();
  if (!first) {
    return 'english';
  }

  if (first.includes('zulu')) {
    return 'zulu';
  }

  if (first.includes('afrikaans')) {
    return 'afrikaans';
  }

  if (first.includes('xhosa')) {
    return 'xhosa';
  }

  return 'english';
}



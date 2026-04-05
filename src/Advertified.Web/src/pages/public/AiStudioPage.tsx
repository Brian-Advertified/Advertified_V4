import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowRight, CheckCircle2, Clapperboard, Mic2, Sparkles, WandSparkles, Workflow } from 'lucide-react';
import { Link, useSearchParams } from 'react-router-dom';
import { canAccessAiStudioForStatus, getAiStudioAccessMessage } from '../../features/campaigns/aiStudioAccess';
import { getActiveJobPollInterval } from '../../lib/queryPolling';
import { advertifiedApi } from '../../services/advertifiedApi';
import billboardImage from '../../assets/Channels/optimized/billboard-sa-optimized.jpg';
import radioImage from '../../assets/Channels/optimized/radio-sa-optimized.jpg';
import socialImage from '../../assets/Channels/optimized/social-platforms-optimized.jpg';
import tvImage from '../../assets/Channels/optimized/tv-sa-optimized.jpg';

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
    title: 'Outdoor headlines & visual direction',
    tag: 'Billboards & Digital Screens',
    text: 'Campaign-specific copy, placement notes, and visual direction for real outdoor placements.',
    image: billboardImage,
    alt: 'Billboard campaign preview',
    outputs: ['Headline copy', 'Visual brief', 'Format specs', 'Placement notes'],
  },
  {
    title: 'Scripts, voice routes & broadcast lines',
    tag: 'Radio & TV Audio',
    text: '30-second scripts with CTA, voice direction, and language-ready adaptations for broadcast.',
    image: radioImage,
    alt: 'Radio campaign preview',
    outputs: ['30-sec script', 'Voice direction', 'CTA lines', 'Language variants'],
  },
  {
    title: 'Shot-by-shot scene guides',
    tag: 'TV & Video',
    text: 'Scene structure, visual direction, and production notes for short-form campaign video.',
    image: tvImage,
    alt: 'TV and video campaign preview',
    outputs: ['Shot list', 'Scene guide', 'VO script', 'Edit notes'],
  },
  {
    title: 'Channel-native cutdowns & media variants',
    tag: 'Social & Digital',
    text: 'Platform-ready copy, caption variants, and creative direction for mobile-first placements.',
    image: socialImage,
    alt: 'Social campaign preview',
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

function formatChannelLabel(value: string) {
  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}

export function AiStudioPage() {
  return (
    <div className="min-h-screen bg-[#070707] text-[#F7F4EF]">
      <div className="sticky top-0 z-30 border-b border-white/10 bg-[#070707]/85 backdrop-blur-xl">
        <div className="page-shell flex items-center justify-between px-6 py-5 sm:px-10">
          <Link to="/" className="text-sm font-semibold uppercase tracking-[0.14em] text-[#F7F4EF]">
            <span className="text-[#F5A623]">A</span>dvertified
          </Link>
          <div className="hidden items-center gap-8 text-xs uppercase tracking-[0.18em] text-white/45 md:flex">
            <Link to="/ai-studio" className="transition hover:text-white">Studio</Link>
            <Link to="/packages" className="transition hover:text-white">Packages</Link>
            <Link to="/how-it-works" className="transition hover:text-white">How It Works</Link>
          </div>
          <Link to="/packages" className="rounded-md bg-[#F5A623] px-5 py-2.5 text-xs font-semibold uppercase tracking-[0.12em] text-black transition hover:opacity-90">
            Get Started
          </Link>
        </div>
      </div>

      <section className="relative overflow-hidden px-6 pb-14 pt-10 sm:px-10 sm:pt-16">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_72%_40%,rgba(245,166,35,0.08),transparent_32%),radial-gradient(circle_at_18%_18%,rgba(255,255,255,0.04),transparent_26%)]" />
        <div className="page-shell relative grid gap-10 xl:grid-cols-[0.95fr_1.05fr]">
          <div className="flex flex-col justify-center pb-6 xl:pr-10">
            <div className="mb-8 inline-flex w-fit items-center gap-2 rounded-full border border-[#F5A623]/30 bg-[#F5A623]/12 px-4 py-2 text-[11px] font-medium uppercase tracking-[0.22em] text-[#F5A623]">
              <Sparkles className="size-4" />
              Advertified Studio
            </div>
            <h1 className="font-display text-[clamp(4rem,9vw,7rem)] uppercase leading-[0.88] tracking-[0.01em] text-[#F7F4EF]">
              One Brief.
              <br />
              <span className="text-[#F5A623]">Every Channel.</span>
              <br />
              Launch-Ready.
            </h1>
            <p className="mt-7 max-w-xl text-base font-light leading-8 text-white/60 sm:text-lg">
              Your approved campaign turned into real, production-ready creative across billboards, radio, TV, and social from a single brief.
            </p>
            <div className="mt-10 flex flex-wrap gap-3">
              <Link to="/packages" className="inline-flex items-center gap-2 rounded-md bg-[#F5A623] px-7 py-4 text-sm font-semibold text-black transition hover:opacity-90">
                Explore packages
                <ArrowRight className="size-4" />
              </Link>
              <Link to="/partner-enquiry" className="inline-flex items-center gap-2 rounded-md border border-white/20 px-7 py-4 text-sm font-medium text-white transition hover:border-white/40">
                Talk to the team
              </Link>
            </div>
            <div className="mt-12 grid gap-6 border-t border-white/10 pt-8 sm:grid-cols-3">
              {[
                ['Channels', '4+'],
                ['Asset types', '12+'],
                ['From brief to pack', '48h'],
              ].map(([label, value]) => (
                <div key={label}>
                  <div className="text-[11px] uppercase tracking-[0.18em] text-white/45">{label}</div>
                  <div className="mt-1 font-display text-4xl uppercase tracking-[0.04em] text-[#F7F4EF]">
                    <span className="text-[#F5A623]">{value}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="flex items-center justify-center py-4 xl:py-10">
            <div className="grid w-full max-w-[560px] gap-3 md:grid-cols-2">
              <article className="overflow-hidden rounded-2xl border border-white/10 bg-[#111111] md:col-span-2">
                <div className="absolute ml-4 mt-4 rounded-full border border-white/10 bg-black/55 px-3 py-1 text-[10px] font-medium uppercase tracking-[0.18em] text-white/60">
                  Billboard
                </div>
                <img src={billboardImage} alt="Billboard campaign preview" className="h-[220px] w-full object-cover" />
                <div className="border-t border-white/10 bg-[#111111] px-5 py-4">
                  <div className="font-display text-3xl uppercase leading-none tracking-[0.04em] text-[#F5A623]">Your Brand Here</div>
                  <div className="mt-2 text-[11px] uppercase tracking-[0.18em] text-white/45">Outdoor · 6x3m · Digital</div>
                </div>
              </article>

              <article className="overflow-hidden rounded-2xl border border-white/10 bg-[#111111]">
                <div className="absolute ml-4 mt-4 rounded-full border border-white/10 bg-black/55 px-3 py-1 text-[10px] font-medium uppercase tracking-[0.18em] text-white/60">
                  Radio
                </div>
                <img src={radioImage} alt="Radio campaign preview" className="h-[190px] w-full object-cover" />
                <div className="border-t border-white/10 bg-[#111111] px-4 py-4">
                  <div className="flex items-center gap-3">
                    <Mic2 className="size-4 text-[#F5A623]" />
                    <div className="text-sm font-semibold text-white">30-sec spot</div>
                  </div>
                  <div className="mt-2 font-mono text-[11px] uppercase tracking-[0.16em] text-white/45">On air</div>
                </div>
              </article>

              <article className="overflow-hidden rounded-2xl border border-white/10 bg-[#111111]">
                <div className="absolute ml-4 mt-4 rounded-full border border-white/10 bg-black/55 px-3 py-1 text-[10px] font-medium uppercase tracking-[0.18em] text-white/60">
                  TV / Video
                </div>
                <img src={tvImage} alt="Video campaign preview" className="h-[190px] w-full object-cover" />
                <div className="border-t border-white/10 bg-[#111111] px-4 py-4">
                  <div className="flex items-center gap-3">
                    <Clapperboard className="size-4 text-[#F5A623]" />
                    <div className="text-sm font-semibold text-white">Scene-ready cut</div>
                  </div>
                  <div className="mt-2 font-mono text-[11px] uppercase tracking-[0.16em] text-white/45">00:23</div>
                </div>
              </article>
            </div>
          </div>
        </div>
      </section>

      <section className="border-y border-white/10 bg-[#111111] px-6 py-16 sm:px-10">
        <div className="page-shell grid gap-10 xl:grid-cols-[0.95fr_1.05fr] xl:items-center">
          <div>
            <div className="text-[11px] font-medium uppercase tracking-[0.24em] text-[#F5A623]">What the studio does</div>
            <h2 className="mt-4 font-display text-[clamp(3rem,7vw,5rem)] uppercase leading-[0.9] tracking-[0.01em] text-[#F7F4EF]">
              Where campaigns
              <br />
              become content.
            </h2>
            <p className="mt-5 max-w-xl text-base font-light leading-8 text-white/60">
              Once your campaign is approved, Studio takes over. It translates your brief into creative direction and adapts it across every format in your media plan, ready for review, booking, and launch.
            </p>
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            {studioHighlights.map((item) => (
              <article key={item.title} className="rounded-2xl border border-white/10 bg-[#181818] p-6">
                <div className="mb-4 flex size-10 items-center justify-center rounded-xl bg-[#F5A623]/12">
                  <CheckCircle2 className="size-5 text-[#F5A623]" />
                </div>
                <h3 className="text-sm font-semibold text-white">{item.title}</h3>
                <p className="mt-2 text-sm leading-6 text-white/55">{item.text}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="px-6 py-16 sm:px-10">
        <div className="page-shell">
          <div className="mb-10 flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <div className="text-[11px] font-medium uppercase tracking-[0.24em] text-[#F5A623]">Channels</div>
              <h2 className="mt-4 font-display text-[clamp(3rem,7vw,5rem)] uppercase leading-[0.9] tracking-[0.01em] text-[#F7F4EF]">
                Every format,
                <br />
                one campaign.
              </h2>
            </div>
            <p className="max-w-sm text-sm font-light leading-7 text-white/55 lg:text-right">
              Studio produces channel-native assets for each placement in your media plan.
            </p>
          </div>

          <div className="grid gap-4 xl:grid-cols-4 md:grid-cols-2">
            {studioChannels.map((channel) => (
              <article key={channel.title} className="overflow-hidden rounded-2xl border border-white/10 bg-[#111111] transition hover:-translate-y-1 hover:border-[#F5A623]/40">
                <div className="relative">
                  <img src={channel.image} alt={channel.alt} className="h-[250px] w-full object-cover" />
                  <div className="absolute left-4 top-4 rounded-full border border-white/10 bg-black/60 px-3 py-1 text-[10px] font-medium uppercase tracking-[0.16em] text-white/60">
                    {channel.tag}
                  </div>
                </div>
                <div className="p-6">
                  <div className="text-[11px] font-medium uppercase tracking-[0.18em] text-[#F5A623]">{channel.tag}</div>
                  <h3 className="mt-3 text-lg font-semibold leading-7 text-white">{channel.title}</h3>
                  <p className="mt-3 text-sm leading-7 text-white/55">{channel.text}</p>
                </div>
                <div className="flex flex-wrap gap-2 px-6 pb-6">
                  {channel.outputs.map((item) => (
                    <span key={item} className="rounded-full border border-white/10 bg-white/[0.04] px-3 py-1 text-xs text-white/55">
                      {item}
                    </span>
                  ))}
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="border-y border-white/10 bg-[#111111] px-6 py-16 sm:px-10">
        <div className="page-shell">
          <div className="max-w-2xl">
            <div className="flex items-center gap-3">
              <Workflow className="size-5 text-[#F5A623]" />
              <div className="text-[11px] font-medium uppercase tracking-[0.24em] text-[#F5A623]">Studio flow</div>
            </div>
            <h2 className="mt-4 font-display text-[clamp(3rem,7vw,5rem)] uppercase leading-[0.9] tracking-[0.01em] text-[#F7F4EF]">
              From brief
              <br />
              to launch.
            </h2>
            <p className="mt-5 max-w-xl text-base font-light leading-8 text-white/60">
              One continuous process. Brief in, campaign pack out.
            </p>
          </div>

          <div className="mt-12 grid gap-4 lg:grid-cols-5 md:grid-cols-2">
            {studioFlow.map((step, index) => (
              <article key={step.title} className="rounded-2xl border border-white/10 bg-[#181818] p-6 text-center">
                <div className={`mx-auto flex size-14 items-center justify-center rounded-full border ${index < 4 ? 'border-[#F5A623]/45 bg-[#F5A623]/12 text-[#F5A623]' : 'border-white/10 bg-[#111111] text-white/60'} font-mono text-sm`}>
                  {step.icon}
                </div>
                <h3 className="mt-5 text-sm font-semibold text-white">{step.title}</h3>
                <p className="mt-3 text-sm leading-6 text-white/55">{step.text}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="px-6 py-16 sm:px-10">
        <div className="page-shell">
          <div className="max-w-2xl">
            <div className="text-[11px] font-medium uppercase tracking-[0.24em] text-[#F5A623]">What you get</div>
            <h2 className="mt-4 font-display text-[clamp(3rem,7vw,5rem)] uppercase leading-[0.9] tracking-[0.01em] text-[#F7F4EF]">
              A complete
              <br />
              campaign pack.
            </h2>
            <p className="mt-5 text-base font-light leading-8 text-white/60">
              Every output is structured, reviewed, and ready to book.
            </p>
          </div>

          <div className="mt-10 grid gap-4 lg:grid-cols-3">
            {studioOutputs.map((item) => (
              <article key={item.number} className="rounded-2xl border border-white/10 bg-[#111111] p-7">
                <div className="font-display text-6xl uppercase leading-none text-[#F5A623]/25">{item.number}</div>
                <h3 className="mt-4 text-lg font-semibold text-white">{item.title}</h3>
                <p className="mt-3 text-sm leading-7 text-white/55">{item.text}</p>
                <ul className="mt-5 space-y-2">
                  {item.items.map((listItem) => (
                    <li key={listItem} className="flex items-start gap-3 text-sm text-white/55">
                      <span className="mt-2 size-1.5 shrink-0 rounded-full bg-[#F5A623]" />
                      <span>{listItem}</span>
                    </li>
                  ))}
                </ul>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="relative overflow-hidden px-6 py-20 text-center sm:px-10">
        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_50%_50%,rgba(245,166,35,0.1),transparent_38%)]" />
        <div className="page-shell relative">
          <div className="text-[11px] font-medium uppercase tracking-[0.24em] text-[#F5A623]">Ready when you are</div>
          <h2 className="mt-5 font-display text-[clamp(4rem,9vw,6.6rem)] uppercase leading-[0.9] tracking-[0.01em] text-[#F7F4EF]">
            Your campaign.
            <br />
            <span className="text-[#F5A623]">Real content.</span>
            <br />
            Every channel.
          </h2>
          <p className="mx-auto mt-5 max-w-lg text-base font-light leading-8 text-white/60">
            Start with a brief. Walk away with a full campaign pack built to run.
          </p>
          <div className="mt-10 flex flex-wrap items-center justify-center gap-3">
            <Link to="/packages" className="inline-flex items-center gap-2 rounded-md bg-[#F5A623] px-7 py-4 text-sm font-semibold text-black transition hover:opacity-90">
              Explore packages
              <ArrowRight className="size-4" />
            </Link>
            <Link to="/partner-enquiry" className="inline-flex items-center gap-2 rounded-md border border-white/20 px-7 py-4 text-sm font-medium text-white transition hover:border-white/40">
              Talk to the team
            </Link>
          </div>
          <div className="mt-8 text-xs uppercase tracking-[0.14em] text-white/40">
            <span className="text-emerald-400">●</span> No lock-in. One campaign at a time.
          </div>
        </div>
      </section>

      <footer className="border-t border-white/10 px-6 py-8 sm:px-10">
        <div className="page-shell flex flex-col gap-3 text-center sm:flex-row sm:items-center sm:justify-between sm:text-left">
          <div className="text-xs font-semibold uppercase tracking-[0.14em] text-white/45">
            <span className="text-[#F5A623]">A</span>dvertified Studio
          </div>
          <div className="text-xs text-white/25">© 2026 Advertified. All rights reserved.</div>
        </div>
      </footer>
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


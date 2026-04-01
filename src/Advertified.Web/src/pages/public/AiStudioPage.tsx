import { useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowRight, Sparkles, WandSparkles } from 'lucide-react';
import { Link } from 'react-router-dom';
import { advertifiedApi } from '../../services/advertifiedApi';

const outputs = [
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

export function AiStudioPage() {
  const [campaignId, setCampaignId] = useState('');
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
  const [assetVoicePackId, setAssetVoicePackId] = useState('');
  const [assetVisualDirection, setAssetVisualDirection] = useState('');
  const [assetVideoSceneJson, setAssetVideoSceneJson] = useState('{"scene":1,"visual":"Product hero shot","audio":"Upbeat track"}');
  const [assetVideoScript, setAssetVideoScript] = useState('');
  const [assetVideoAspectRatio, setAssetVideoAspectRatio] = useState('16:9');
  const [assetVideoDurationSeconds, setAssetVideoDurationSeconds] = useState(30);
  const [assetJobId, setAssetJobId] = useState('');

  const [activeConsoleStep, setActiveConsoleStep] = useState<ConsoleStep>('queue');

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
    refetchInterval: (query) => {
      const status = query.state.data?.status?.toLowerCase();
      return status === 'completed' || status === 'failed' ? false : 3000;
    },
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
    queryKey: ['ai-platform-voice-packs'],
    queryFn: () => advertifiedApi.getAiPlatformVoicePacks('ElevenLabs'),
  });

  const queueVoiceAssetMutation = useMutation({
    mutationFn: () => advertifiedApi.queueAiPlatformVoiceAsset({
      campaignId: assetCampaignId.trim(),
      creativeId: assetCreativeId.trim(),
      script: assetVoiceScript.trim(),
      voicePackId: assetVoicePackId || undefined,
      voiceType: 'Standard',
      language: 'English',
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
    refetchInterval: (query) => {
      const status = query.state.data?.status?.toLowerCase();
      return status === 'completed' || status === 'failed' ? false : 2500;
    },
  });

  const canSubmit = useMemo(
    () => campaignId.trim().length > 0 && !submitJobMutation.isPending,
    [campaignId, submitJobMutation.isPending],
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
      && briefBrand.trim().length > 0
      && briefLanguages.length > 0
      && briefChannels.length > 0
      && !generateFromBriefMutation.isPending,
    [campaignId, briefBrand, briefLanguages, briefChannels, generateFromBriefMutation.isPending],
  );

  const canQueueAssetJob = useMemo(
    () => assetCampaignId.trim().length > 0 && assetCreativeId.trim().length > 0,
    [assetCampaignId, assetCreativeId],
  );

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

      <section className="page-shell px-6 pb-20 sm:px-10">
        <div className="rounded-[30px] border border-slate-800 bg-[#0A0A0A] p-6 sm:p-8">
          <h3 className="text-2xl font-semibold text-slate-100">AI Platform Console</h3>
          <p className="mt-2 text-sm text-slate-400">This console is now step-based. Start at step 1 and move right.</p>

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
                <input value={activeJobId} onChange={(event) => setActiveJobId(event.target.value)} className="min-w-[320px] flex-1 rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Job id to track" />
              </div>
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
                <button type="button" onClick={() => qaQuery.refetch()} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white">Load QA</button>
              </div>
              <div className="mt-4 space-y-2 text-sm">
                {(qaQuery.data ?? []).slice(0, 10).map((item) => (
                  <div key={`${item.creativeId}-${item.createdAt}`} className="rounded-lg border border-slate-800 bg-slate-900/60 p-3">
                    <p className="text-slate-200">{item.channel} · {item.language} · <span className="font-semibold">{item.status}</span></p>
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
                <button type="button" onClick={() => queueVoiceAssetMutation.mutate()} disabled={!canQueueAssetJob || !assetVoiceScript.trim() || queueVoiceAssetMutation.isPending} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white disabled:opacity-50">Queue voice</button>
                <button type="button" onClick={() => queueImageAssetMutation.mutate()} disabled={!canQueueAssetJob || !assetVisualDirection.trim() || queueImageAssetMutation.isPending} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white disabled:opacity-50">Queue image</button>
                <button type="button" onClick={() => queueVideoAssetMutation.mutate()} disabled={!canQueueAssetJob || !assetVideoScript.trim() || queueVideoAssetMutation.isPending} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white disabled:opacity-50">Queue video</button>
              </div>
              <div className="mt-4 flex gap-3">
                <input value={assetJobId} onChange={(event) => setAssetJobId(event.target.value)} className="flex-1 rounded-xl border border-slate-700 bg-slate-900 px-4 py-3 text-sm text-white" placeholder="Asset job id" />
                <button type="button" onClick={() => assetStatusQuery.refetch()} className="rounded-xl border border-slate-600 bg-slate-950 px-5 py-3 text-sm font-semibold text-white">Check job</button>
              </div>
              <div className="mt-4 rounded-2xl border border-slate-800 bg-slate-900/60 p-4 text-sm">
                <p><span className="text-slate-400">Job status:</span> {assetStatusQuery.data?.status ?? 'not started'}</p>
                <p><span className="text-slate-400">Asset kind:</span> {assetStatusQuery.data?.assetKind ?? '-'}</p>
                <p><span className="text-slate-400">Asset URL:</span> {assetStatusQuery.data?.assetUrl ?? '-'}</p>
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


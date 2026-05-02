import type { Dispatch, SetStateAction } from 'react';
import type { Campaign } from '../../../types/domain';

const creativeIterationOptions = [
  { label: 'Shorter', instruction: 'Compress the campaign into a shorter, sharper version without losing the master idea.' },
  { label: 'Bolder', instruction: 'Make the campaign bolder, more distinctive, and higher contrast while keeping it commercially usable.' },
  { label: 'More premium', instruction: 'Elevate the campaign so it feels more premium, restrained, and polished.' },
  { label: 'More Gen Z', instruction: 'Shift the campaign language and pacing to feel more Gen Z-native without losing clarity.' },
  { label: 'More performance', instruction: 'Sharpen the campaign for stronger response, clearer value, and a harder-working CTA.' },
] as const;

interface CreativeStudioEnginePanelProps {
  campaign: Campaign;
  isPreview: boolean;
  lastIterationLabel: string | null;
  prompt: string;
  setPrompt: Dispatch<SetStateAction<string>>;
  brandInput: string;
  setBrandInput: Dispatch<SetStateAction<string>>;
  productInput: string;
  setProductInput: Dispatch<SetStateAction<string>>;
  audienceInput: string;
  setAudienceInput: Dispatch<SetStateAction<string>>;
  objectiveInput: string;
  setObjectiveInput: Dispatch<SetStateAction<string>>;
  toneInput: string;
  setToneInput: Dispatch<SetStateAction<string>>;
  channelsInput: string;
  setChannelsInput: Dispatch<SetStateAction<string>>;
  ctaInput: string;
  setCtaInput: Dispatch<SetStateAction<string>>;
  constraintsInput: string;
  setConstraintsInput: Dispatch<SetStateAction<string>>;
  selectedVoicePackId: string;
  setSelectedVoicePackId: Dispatch<SetStateAction<string>>;
  voicePackOptions: Array<{
    id: string;
    name: string;
    accent?: string | null;
    language?: string | null;
    tone?: string | null;
    pricingTier?: string | null;
  }>;
  isLoadingVoicePacks: boolean;
  onGenerateCreativeSystem: (iteration?: { iterationLabel: string; iterationInstruction: string }) => void;
  isGeneratingCreativeSystem: boolean;
  onQueueAiJob: () => void;
  isQueueingAiJob: boolean;
  activeAiJobId: string;
  setActiveAiJobId: Dispatch<SetStateAction<string>>;
  aiJobStatus?: {
    status?: string;
    updatedAt?: string;
    error?: string | null;
  };
  onUseLatestCreativeId: () => void;
  canUseLatestCreativeId: boolean;
  regenCreativeId: string;
  setRegenCreativeId: Dispatch<SetStateAction<string>>;
  regenFeedback: string;
  setRegenFeedback: Dispatch<SetStateAction<string>>;
  onRegenerateWithFeedback: () => void;
  isRegenerating: boolean;
  campaignCreativeOptions: Array<{
    id: string;
    channel: string;
    language?: string | null;
    score?: number | null;
    createdAt: string;
  }>;
  formatChannelLabel: (value: string) => string;
}

export function CreativeStudioEnginePanel(props: CreativeStudioEnginePanelProps) {
  const {
    campaign,
    isPreview,
    lastIterationLabel,
    prompt,
    setPrompt,
    brandInput,
    setBrandInput,
    productInput,
    setProductInput,
    audienceInput,
    setAudienceInput,
    objectiveInput,
    setObjectiveInput,
    toneInput,
    setToneInput,
    channelsInput,
    setChannelsInput,
    ctaInput,
    setCtaInput,
    constraintsInput,
    setConstraintsInput,
    selectedVoicePackId,
    setSelectedVoicePackId,
    voicePackOptions,
    isLoadingVoicePacks,
    onGenerateCreativeSystem,
    isGeneratingCreativeSystem,
    onQueueAiJob,
    isQueueingAiJob,
    activeAiJobId,
    setActiveAiJobId,
    aiJobStatus,
    onUseLatestCreativeId,
    canUseLatestCreativeId,
    regenCreativeId,
    setRegenCreativeId,
    regenFeedback,
    setRegenFeedback,
    onRegenerateWithFeedback,
    isRegenerating,
    campaignCreativeOptions,
    formatChannelLabel,
  } = props;

  return (
    <div className="user-card">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h3>Creative system engine</h3>
          <p className="mt-2 text-sm leading-7 text-slate-600">
            Turn the approved campaign into a full creative system with one sharp idea, native channel outputs, and production notes.
          </p>
        </div>
        {lastIterationLabel ? (
          <span className="rounded-full border border-brand/20 bg-brand-soft px-3 py-1 text-xs font-semibold text-brand">
            Latest pass: {lastIterationLabel}
          </span>
        ) : null}
      </div>

      <div className="mt-5 space-y-4">
        <label className="block space-y-2">
          <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Prompt</span>
          <textarea
            value={prompt}
            onChange={(event) => setPrompt(event.target.value)}
            rows={6}
            disabled={isPreview}
            className="input-base min-h-[160px]"
          />
        </label>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="block space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Brand</span>
            <input value={brandInput} onChange={(event) => setBrandInput(event.target.value)} disabled={isPreview} className="input-base" />
          </label>
          <label className="block space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Product</span>
            <input value={productInput} onChange={(event) => setProductInput(event.target.value)} disabled={isPreview} className="input-base" />
          </label>
          <label className="block space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Audience</span>
            <input value={audienceInput} onChange={(event) => setAudienceInput(event.target.value)} disabled={isPreview} className="input-base" />
          </label>
          <label className="block space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Objective</span>
            <input value={objectiveInput} onChange={(event) => setObjectiveInput(event.target.value)} disabled={isPreview} className="input-base" />
          </label>
          <label className="block space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Tone</span>
            <input value={toneInput} onChange={(event) => setToneInput(event.target.value)} disabled={isPreview} className="input-base" />
          </label>
          <label className="block space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">CTA</span>
            <input value={ctaInput} onChange={(event) => setCtaInput(event.target.value)} disabled={isPreview} className="input-base" />
          </label>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="block space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Channels</span>
            <textarea
              value={channelsInput}
              onChange={(event) => setChannelsInput(event.target.value)}
              disabled={isPreview}
              rows={3}
              className="input-base"
              placeholder="Billboard, TikTok, Radio, Display"
            />
          </label>
          <label className="block space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Constraints</span>
            <textarea
              value={constraintsInput}
              onChange={(event) => setConstraintsInput(event.target.value)}
              disabled={isPreview}
              rows={3}
              className="input-base"
              placeholder="One line per constraint, or comma-separated"
            />
          </label>
        </div>

        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            onClick={() => onGenerateCreativeSystem()}
            disabled={isPreview || !prompt.trim() || isGeneratingCreativeSystem}
            className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isPreview ? 'Preview only' : isGeneratingCreativeSystem ? 'Generating...' : 'Generate creative system'}
          </button>
          {creativeIterationOptions.map((option) => (
            <button
              key={option.label}
              type="button"
              onClick={() => onGenerateCreativeSystem({ iterationLabel: option.label, iterationInstruction: option.instruction })}
              disabled={isPreview || !prompt.trim() || isGeneratingCreativeSystem}
              className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
            >
              {option.label}
            </button>
          ))}
        </div>

        <div className="user-wire text-sm leading-7">
          {isPreview
            ? 'Preview mode keeps generation disabled, but this is where the Creative Manager brief controls live in the real studio.'
            : `Use the base generation first, then push quick creative shifts with the iteration buttons without rebuilding the whole brief.${campaign.creativeSystems.length ? ` ${campaign.creativeSystems.length} saved version${campaign.creativeSystems.length === 1 ? '' : 's'} available for this campaign.` : ''}`}
        </div>

        <div className="rounded-[22px] border border-slate-200/80 bg-slate-50/70 p-4">
          <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">AI Queue Orchestration</div>
          <p className="mt-2 text-sm leading-7 text-slate-700">
            Queue the multi-provider AI pipeline and track job execution directly from the studio.
          </p>
          <label className="mt-3 block space-y-2">
            <span className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Radio voice pack</span>
            <select
              value={selectedVoicePackId}
              onChange={(event) => setSelectedVoicePackId(event.target.value)}
              disabled={isPreview || isLoadingVoicePacks}
              className="input-base"
            >
              <option value="">{isLoadingVoicePacks ? 'Loading voice packs...' : 'Use default voice'}</option>
              {voicePackOptions.map((pack) => (
                <option key={pack.id} value={pack.id}>
                  {[pack.name, pack.language, pack.accent, pack.tone, pack.pricingTier].filter(Boolean).join(' | ')}
                </option>
              ))}
            </select>
          </label>
          <div className="mt-3 flex flex-wrap gap-3">
            <button
              type="button"
              onClick={onQueueAiJob}
              disabled={isPreview || !prompt.trim() || isQueueingAiJob}
              className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isQueueingAiJob ? 'Queueing job...' : 'Queue AI platform job'}
            </button>
            <input
              value={activeAiJobId}
              onChange={(event) => setActiveAiJobId(event.target.value)}
              disabled={isPreview}
              className="input-base min-w-[260px] flex-1"
              placeholder="Paste job id to track status"
            />
          </div>
          <div className="mt-3 text-sm leading-7 text-slate-700">
            <div><strong>Status:</strong> {aiJobStatus?.status ?? 'not started'}</div>
            <div><strong>Updated:</strong> {aiJobStatus?.updatedAt ?? '-'}</div>
            {aiJobStatus?.error ? <div><strong>Error:</strong> {aiJobStatus.error}</div> : null}
          </div>
        </div>

        <div className="rounded-[22px] border border-slate-200/80 bg-slate-50/70 p-4">
          <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Feedback Regeneration</div>
          <p className="mt-2 text-sm leading-7 text-slate-700">
            Regenerate by feedback using a creative id from the generated inventory pipeline.
          </p>
          <div className="mt-3 flex flex-wrap gap-3">
            <button
              type="button"
              onClick={onUseLatestCreativeId}
              disabled={isPreview || !canUseLatestCreativeId}
              className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
            >
              Use latest creative id
            </button>
            <select
              value={regenCreativeId}
              onChange={(event) => setRegenCreativeId(event.target.value)}
              disabled={isPreview || !campaignCreativeOptions.length}
              className="input-base min-w-[340px]"
            >
              <option value="">Select creative id</option>
              {campaignCreativeOptions.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.id.slice(0, 8)} | {formatChannelLabel(item.channel)} | {item.language} | {item.score ?? '-'} | {new Date(item.createdAt).toLocaleString()}
                </option>
              ))}
            </select>
          </div>
          <div className="mt-3 grid gap-3 md:grid-cols-2">
            <input
              value={regenCreativeId}
              onChange={(event) => setRegenCreativeId(event.target.value)}
              disabled={isPreview}
              className="input-base"
              placeholder="Creative id"
            />
            <input
              value={regenFeedback}
              onChange={(event) => setRegenFeedback(event.target.value)}
              disabled={isPreview}
              className="input-base"
              placeholder="Make it more urgent and add discount CTA"
            />
          </div>
          <div className="mt-3">
            <button
              type="button"
              onClick={onRegenerateWithFeedback}
              disabled={isPreview || !regenCreativeId.trim() || !regenFeedback.trim() || isRegenerating}
              className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isRegenerating ? 'Regenerating...' : 'Regenerate with feedback'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

import type { Campaign, CampaignCreativeSystemRecord, CreativeSystem } from '../../../types/domain';
import { formatChannelLabel } from '../creativeStudioUtils';

interface CreativeStudioOutputPanelProps {
  campaign: Campaign;
  creativeSystem: CreativeSystem | null;
  isPreview: boolean;
  onSelectSavedVersion: (savedSystem: CampaignCreativeSystemRecord) => void;
}

export function CreativeStudioOutputPanel({
  campaign,
  creativeSystem,
  isPreview,
  onSelectSavedVersion,
}: CreativeStudioOutputPanelProps) {
  return (
    <>
      <div className="user-card">
        <h3>Creative output</h3>
        <div className="mt-4 space-y-4">
          {!creativeSystem ? (
            <div className="user-wire">
              {isPreview
                ? 'The read-only preview does not generate live outputs.'
                : 'Generate the creative system to get a campaign summary, master idea, storyboard, channel adaptations, visual direction, and production notes.'}
            </div>
          ) : (
            <>
              <div className="rounded-[24px] border border-slate-200/80 bg-slate-50/70 p-5">
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">1. Campaign Summary</div>
                <div className="mt-3 grid gap-3 text-sm leading-7 text-slate-700 md:grid-cols-2">
                  <div><strong>Brand:</strong> {creativeSystem.campaignSummary.brand}</div>
                  <div><strong>Product:</strong> {creativeSystem.campaignSummary.product}</div>
                  <div><strong>Audience:</strong> {creativeSystem.campaignSummary.audience}</div>
                  <div><strong>Objective:</strong> {creativeSystem.campaignSummary.objective}</div>
                  <div><strong>Tone:</strong> {creativeSystem.campaignSummary.tone}</div>
                  <div><strong>CTA:</strong> {creativeSystem.campaignSummary.cta}</div>
                </div>
                <div className="mt-3 text-sm leading-7 text-slate-700">
                  <strong>Channels:</strong> {creativeSystem.campaignSummary.channels.map(formatChannelLabel).join(' • ')}
                </div>
                {creativeSystem.campaignSummary.constraints.length ? (
                  <div className="mt-3 text-sm leading-7 text-slate-700">
                    <strong>Constraints:</strong> {creativeSystem.campaignSummary.constraints.join(' • ')}
                  </div>
                ) : null}
                {creativeSystem.campaignSummary.assumptions.length ? (
                  <div className="mt-3 text-sm leading-7 text-slate-700">
                    <strong>Assumptions:</strong> {creativeSystem.campaignSummary.assumptions.join(' • ')}
                  </div>
                ) : null}
              </div>

              <div className="rounded-[24px] border border-slate-200/80 bg-slate-50/70 p-5">
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">2. Master Idea</div>
                <div className="mt-3 space-y-3 text-sm leading-7 text-slate-700">
                  <div><strong>Core concept:</strong> {creativeSystem.masterIdea.coreConcept}</div>
                  <div><strong>Central message:</strong> {creativeSystem.masterIdea.centralMessage}</div>
                  <div><strong>Emotional angle:</strong> {creativeSystem.masterIdea.emotionalAngle}</div>
                  <div><strong>Value proposition:</strong> {creativeSystem.masterIdea.valueProposition}</div>
                  <div><strong>Platform idea:</strong> {creativeSystem.masterIdea.platformIdea}</div>
                </div>
              </div>

              <div className="rounded-[24px] border border-slate-200/80 bg-slate-50/70 p-5">
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">3. Campaign Line Options</div>
                <div className="mt-3 space-y-2 text-sm leading-7 text-slate-700">
                  {creativeSystem.campaignLineOptions.map((line) => <div key={line} className="user-wire">{line}</div>)}
                </div>
              </div>
            </>
          )}
        </div>
      </div>

      {campaign.creativeSystems.length ? (
        <div className="user-card">
          <h3>Saved creative versions</h3>
          <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {campaign.creativeSystems.map((savedSystem) => (
              <button
                key={savedSystem.id}
                type="button"
                onClick={() => onSelectSavedVersion(savedSystem)}
                className="rounded-[22px] border border-slate-200/80 bg-slate-50/70 p-4 text-left transition hover:border-brand/40 hover:bg-white"
              >
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">
                  {savedSystem.iterationLabel ?? 'Base version'}
                </div>
                <div className="mt-2 text-sm font-semibold text-slate-900">{new Date(savedSystem.createdAt).toLocaleString()}</div>
                <p className="mt-2 text-sm leading-6 text-slate-600">
                  {savedSystem.output.masterIdea.platformIdea || savedSystem.output.masterIdea.coreConcept}
                </p>
              </button>
            ))}
          </div>
        </div>
      ) : null}

      {creativeSystem ? (
        <div className="grid gap-6 xl:grid-cols-2">
          <div className="user-card">
            <h3>4. Storyboard / Narrative</h3>
            <div className="mt-4 space-y-3 text-sm leading-7 text-slate-700">
              <div><strong>Hook:</strong> {creativeSystem.storyboard.hook}</div>
              <div><strong>Setup:</strong> {creativeSystem.storyboard.setup}</div>
              <div><strong>Tension / problem:</strong> {creativeSystem.storyboard.tensionOrProblem}</div>
              <div><strong>Solution:</strong> {creativeSystem.storyboard.solution}</div>
              <div><strong>Payoff:</strong> {creativeSystem.storyboard.payoff}</div>
              <div><strong>CTA:</strong> {creativeSystem.storyboard.cta}</div>
            </div>
            <div className="mt-5 space-y-3">
              {creativeSystem.storyboard.scenes.map((scene) => (
                <article key={`${scene.order}-${scene.title}`} className="rounded-[22px] border border-slate-200/80 bg-slate-50/70 p-4">
                  <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">Scene {scene.order}{scene.duration ? ` • ${scene.duration}` : ''}</div>
                  <h4 className="mt-2 text-lg font-semibold text-slate-900">{scene.title}</h4>
                  <div className="mt-2 text-sm leading-7 text-slate-700"><strong>Purpose:</strong> {scene.purpose}</div>
                  <div className="mt-2 text-sm leading-7 text-slate-700"><strong>Visual:</strong> {scene.visual}</div>
                  <div className="mt-2 text-sm leading-7 text-slate-700"><strong>Copy / dialogue:</strong> {scene.copyOrDialogue}</div>
                  {scene.onScreenText ? <div className="mt-2 text-sm leading-7 text-slate-700"><strong>On-screen text:</strong> {scene.onScreenText}</div> : null}
                </article>
              ))}
            </div>
          </div>

          <div className="user-card">
            <h3>5. Channel Adaptations</h3>
            <div className="mt-4 space-y-4">
              {creativeSystem.channelAdaptations.map((adaptation) => (
                <article key={`${adaptation.channel}-${adaptation.format}`} className="rounded-[22px] border border-slate-200/80 bg-slate-50/70 p-4">
                  <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">{formatChannelLabel(adaptation.channel)} • {adaptation.format}</div>
                  <div className="mt-2 text-sm leading-7 text-slate-700"><strong>Headline / hook:</strong> {adaptation.headlineOrHook}</div>
                  <div className="mt-2 text-sm leading-7 text-slate-700"><strong>Primary copy:</strong> {adaptation.primaryCopy}</div>
                  <div className="mt-2 text-sm leading-7 text-slate-700"><strong>CTA:</strong> {adaptation.cta}</div>
                  <div className="mt-2 text-sm leading-7 text-slate-700"><strong>Visual direction:</strong> {adaptation.visualDirection}</div>
                  {adaptation.voiceoverOrAudio ? <div className="mt-2 text-sm leading-7 text-slate-700"><strong>Voiceover / audio:</strong> {adaptation.voiceoverOrAudio}</div> : null}
                  {adaptation.recommendedDirection ? <div className="mt-2 text-sm leading-7 text-slate-700"><strong>Recommended direction:</strong> {adaptation.recommendedDirection}</div> : null}
                  {adaptation.productionAssets.length ? (
                    <div className="mt-2 text-sm leading-7 text-slate-700"><strong>Production assets:</strong> {adaptation.productionAssets.join(' • ')}</div>
                  ) : null}
                  {adaptation.sections.length ? (
                    <div className="mt-4 space-y-2">
                      {adaptation.sections.map((section) => (
                        <div key={`${adaptation.channel}-${section.label}`} className="rounded-2xl border border-slate-200/70 bg-white/70 px-3 py-3 text-sm leading-7 text-slate-700">
                          <strong>{section.label}:</strong> {section.content}
                        </div>
                      ))}
                    </div>
                  ) : null}
                  {adaptation.versions.length ? (
                    <div className="mt-4 grid gap-3 lg:grid-cols-3">
                      {adaptation.versions.map((version) => (
                        <div key={`${adaptation.channel}-${version.label}`} className="rounded-2xl border border-slate-200/70 bg-white/80 p-3 text-sm leading-7 text-slate-700">
                          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{version.label} • {version.intent}</div>
                          <div className="mt-2"><strong>Hook:</strong> {version.headlineOrHook}</div>
                          <div className="mt-2"><strong>Copy:</strong> {version.primaryCopy}</div>
                          <div className="mt-2"><strong>CTA:</strong> {version.cta}</div>
                        </div>
                      ))}
                    </div>
                  ) : null}
                  {adaptation.adapterPrompt ? (
                    <details className="mt-4 rounded-2xl border border-slate-200/70 bg-white/70 p-3">
                      <summary className="cursor-pointer text-sm font-semibold text-slate-700">Adapter prompt</summary>
                      <div className="mt-3 whitespace-pre-wrap text-sm leading-7 text-slate-600">{adaptation.adapterPrompt}</div>
                    </details>
                  ) : null}
                </article>
              ))}
            </div>
          </div>

          <div className="user-card">
            <h3>6. Visual Direction</h3>
            <div className="mt-4 space-y-3 text-sm leading-7 text-slate-700">
              <div><strong>Look and feel:</strong> {creativeSystem.visualDirection.lookAndFeel}</div>
              <div><strong>Typography:</strong> {creativeSystem.visualDirection.typography}</div>
              <div><strong>Color direction:</strong> {creativeSystem.visualDirection.colorDirection}</div>
              <div><strong>Composition:</strong> {creativeSystem.visualDirection.composition}</div>
            </div>
            {creativeSystem.visualDirection.imageGenerationPrompts.length ? (
              <div className="mt-5 space-y-2">
                {creativeSystem.visualDirection.imageGenerationPrompts.map((promptLine) => (
                  <div key={promptLine} className="user-wire text-sm leading-7">{promptLine}</div>
                ))}
              </div>
            ) : null}
          </div>

          <div className="user-card">
            <h3>7. Audio / Voice Notes</h3>
            <div className="mt-4 space-y-2">
              {creativeSystem.audioVoiceNotes.map((note) => <div key={note} className="user-wire text-sm leading-7">{note}</div>)}
            </div>

            <h3 className="mt-6">8. Production Notes</h3>
            <div className="mt-4 space-y-2">
              {creativeSystem.productionNotes.map((note) => <div key={note} className="user-wire text-sm leading-7">{note}</div>)}
            </div>

            <h3 className="mt-6">9. Optional Variations</h3>
            <div className="mt-4 space-y-2">
              {creativeSystem.optionalVariations.map((variation) => <div key={variation} className="user-wire text-sm leading-7">{variation}</div>)}
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}

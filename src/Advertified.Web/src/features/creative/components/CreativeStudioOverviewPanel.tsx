import { ArrowRight, CheckCircle2, ClipboardList, ImagePlus, Palette, RadioTower, Sparkles, WandSparkles } from 'lucide-react';
import { Link } from 'react-router-dom';
import { formatCurrency } from '../../../lib/utils';
import type { Campaign, CampaignBrief, CampaignRecommendation } from '../../../types/domain';
import type { CreativeStudioCollection, CreativeStudioSignal } from '../creativeStudioTypes';

interface CreativeStudioOverviewPanelProps {
  campaign: Campaign;
  brief?: CampaignBrief;
  recommendation?: CampaignRecommendation;
  channelMood: string[];
  geographyFocus: string[];
  studioCollections: CreativeStudioCollection[];
  productionSignals: CreativeStudioSignal[];
  isPreview: boolean;
  isBookingStage: boolean;
  isAwaitingFinalApproval: boolean;
  isSendingFinishedMedia: boolean;
  onSendFinishedMedia?: () => void;
}

export function CreativeStudioOverviewPanel({
  campaign,
  brief,
  recommendation,
  channelMood,
  geographyFocus,
  studioCollections,
  productionSignals,
  isPreview,
  isBookingStage,
  isAwaitingFinalApproval,
  isSendingFinishedMedia,
  onSendFinishedMedia,
}: CreativeStudioOverviewPanelProps) {
  return (
    <>
      <section className="overflow-hidden rounded-[36px] border border-white/70 bg-[radial-gradient(circle_at_top_left,_rgba(45,212,191,0.28),_transparent_32%),radial-gradient(circle_at_bottom_right,_rgba(251,191,36,0.22),_transparent_28%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(239,250,246,0.95))] p-8 shadow-[0_30px_90px_rgba(15,23,42,0.09)]">
        <div className="grid gap-8 lg:grid-cols-[1.5fr_0.9fr]">
          <div className="space-y-5">
            <div className="inline-flex items-center gap-2 rounded-full border border-white/80 bg-white/75 px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.35em] text-slate-700">
              <Sparkles className="h-4 w-4 text-brand" />
              {isPreview ? 'Creative Studio Preview' : 'Creative Director Studio'}
            </div>
            <div className="space-y-3">
              <h2 className="font-display text-4xl leading-tight text-slate-900">
                {isBookingStage ? `${campaign.campaignName} is in booking and launch prep.` : `${campaign.campaignName} is ready for production.`}
              </h2>
              <p className="max-w-3xl text-base leading-7 text-slate-600">
                {isBookingStage
                  ? 'Creative approval is complete, so this workspace now shifts to supplier outreach, booking confirmation, and launch readiness updates.'
                  : 'The recommendation is already approved, so this studio is focused on production, polish, and preparing the final creative pack for client approval.'}
              </p>
            </div>
            <div className="grid gap-4 sm:grid-cols-3">
              {productionSignals.map((signal) => (
                <div key={signal.label} className="rounded-[24px] border border-white/80 bg-white/80 p-5 shadow-[0_18px_40px_rgba(15,23,42,0.05)]">
                  <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-2xl bg-brand-soft text-brand">
                    <CheckCircle2 className="h-5 w-5" />
                  </div>
                  <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">{signal.label}</div>
                  <div className="mt-2 text-lg font-semibold text-slate-900">{signal.value}</div>
                  <p className="mt-2 text-sm leading-6 text-slate-600">{signal.helper}</p>
                </div>
              ))}
            </div>
          </div>
          <div className="rounded-[30px] border border-white/80 bg-white/75 p-6 shadow-[0_24px_55px_rgba(15,23,42,0.07)]">
            <div className="mb-4 flex items-center gap-3">
              <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-slate-900 text-white">
                <ImagePlus className="h-5 w-5" />
              </div>
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.28em] text-slate-500">Production brief</div>
                <div className="text-lg font-semibold text-slate-900">{campaign.campaignName}</div>
              </div>
            </div>
            <div className="space-y-4 text-sm leading-6 text-slate-600">
              <div>
                <div className="font-semibold text-slate-900">Client</div>
                <div>{campaign.clientName ?? campaign.businessName ?? 'Client not captured yet'}</div>
              </div>
              <div>
                <div className="font-semibold text-slate-900">Geography focus</div>
                <div>{geographyFocus.length ? geographyFocus.join(' • ') : 'No area focus captured yet'}</div>
              </div>
              <div>
                <div className="font-semibold text-slate-900">Budget frame</div>
                <div>{formatCurrency(campaign.selectedBudget)} • {campaign.packageBandName ?? 'Package selected'}</div>
              </div>
              <div>
                <div className="font-semibold text-slate-900">Audience notes</div>
                <div>{brief?.targetAudienceNotes ?? 'Audience direction has not been captured yet.'}</div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <div className="grid gap-5 xl:grid-cols-3">
        {studioCollections.map((collection) => {
          const Icon = collection.icon;
          return (
            <article key={collection.title} className={`rounded-[30px] border border-slate-200/70 bg-gradient-to-br ${collection.accent} p-6 shadow-[0_24px_55px_rgba(15,23,42,0.05)]`}>
              <div className="mb-4 flex h-11 w-11 items-center justify-center rounded-2xl bg-white/90 text-slate-900 shadow-sm">
                <Icon className="h-5 w-5" />
              </div>
              <h3 className="text-xl font-semibold text-slate-900">{collection.title}</h3>
              <p className="mt-3 text-sm leading-7 text-slate-600">{collection.body}</p>
            </article>
          );
        })}
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
        <div className="user-card">
          <h3>Production composition</h3>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <div className="rounded-[24px] border border-slate-200/80 bg-slate-50/70 p-5">
              <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.24em] text-slate-500">
                <RadioTower className="h-4 w-4 text-brand" />
                Approved Channel Mix
              </div>
              <div className="text-sm leading-7 text-slate-700">{channelMood.length ? channelMood.join(' • ') : 'Channel mix will appear here when approved placements are available.'}</div>
            </div>
            <div className="rounded-[24px] border border-slate-200/80 bg-slate-50/70 p-5">
              <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.24em] text-slate-500">
                <Palette className="h-4 w-4 text-amber-500" />
                Production Notes
              </div>
              <div className="text-sm leading-7 text-slate-700">{brief?.specialRequirements ?? 'No production notes yet. Build from the approved recommendation and package rules.'}</div>
            </div>
            <div className="rounded-[24px] border border-slate-200/80 bg-slate-50/70 p-5 md:col-span-2">
              <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.24em] text-slate-500">
                <ClipboardList className="h-4 w-4 text-brand" />
                Creative Objective Frame
              </div>
              <div className="text-sm leading-7 text-slate-700">{recommendation?.summary ?? 'The approved recommendation summary will appear here as the strategic anchor for production.'}</div>
            </div>
          </div>
        </div>

        <div className="user-card">
          <h3>Client approval handoff</h3>
          <div className="mt-4 space-y-4">
            {isPreview ? (
              <div className="rounded-[24px] border border-brand/20 bg-brand-soft/70 p-4">
                <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.24em] text-brand">
                  <WandSparkles className="h-4 w-4" />
                  Preview mode
                </div>
                <p className="text-sm leading-7 text-slate-700">
                  This route is read-only. It lets you see the studio layout and flow without unlocking creative-director actions for regular users.
                </p>
              </div>
            ) : null}
            <div className="rounded-[24px] border border-brand/20 bg-brand-soft/70 p-4">
              <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.24em] text-brand">
                <WandSparkles className="h-4 w-4" />
                Role boundary
              </div>
              <p className="text-sm leading-7 text-slate-700">
                The studio is now reserved for creative directors. The client sees a waiting-for-approval handoff until the finished media is returned for sign-off.
              </p>
            </div>
            <div className="user-wire">
              {isBookingStage
                ? 'The client approval step is complete. Use this workspace to confirm supplier bookings and keep the client updated as launch preparation moves forward.'
                : isAwaitingFinalApproval
                  ? 'Finished media has already been sent. The campaign is now in its final client-approval state.'
                  : 'The production send step is now modeled as a creative-director-owned handoff. Use it when the finished media pack is ready for client approval.'}
            </div>
            <button
              type="button"
              onClick={onSendFinishedMedia}
              disabled={isPreview || isAwaitingFinalApproval || isBookingStage || isSendingFinishedMedia}
              className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isPreview
                ? 'Preview only'
                : isBookingStage
                  ? 'Client approval complete'
                  : isAwaitingFinalApproval
                    ? 'Finished media already sent'
                    : isSendingFinishedMedia
                      ? 'Sending to client...'
                      : 'Send finished media to client'}
            </button>
            <div className="flex flex-wrap gap-3">
              <Link to={isPreview ? `/campaigns/${campaign.id}` : '/creative'} className="user-btn-primary inline-flex items-center gap-2">
                {isPreview ? 'Back to campaign workspace' : 'Back to creative dashboard'}
                <ArrowRight className="size-4" />
              </Link>
              {!isPreview ? <Link to={`/campaigns/${campaign.id}`} className="user-btn-secondary">View client workspace</Link> : null}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

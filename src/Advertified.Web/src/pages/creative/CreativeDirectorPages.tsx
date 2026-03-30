import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, CheckCircle2, ClipboardList, ImagePlus, Palette, RadioTower, Sparkles, WandSparkles } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link, NavLink, useParams } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { getPrimaryRecommendation } from '../client/clientWorkspace';

function CreativePageShell({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: ReactNode;
}) {
  const { user, logout } = useAuth();

  return (
    <section className="page-shell space-y-10">
      <div className="grid gap-8 xl:grid-cols-[280px_1fr]">
        <aside className="sticky top-24 h-fit rounded-[28px] border border-line bg-white p-6 shadow-[0_18px_60px_rgba(17,24,39,0.04)]">
          <div className="space-y-6">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Workspace</p>
              <h2 className="mt-4 text-2xl font-semibold text-ink">Creative studio</h2>
              <p className="mt-2 text-sm leading-6 text-ink-soft">Production-led campaign creation after recommendation approval, with client sign-off handled after the media pack is ready.</p>
            </div>
            <NavLink
              to="/creative"
              end
              className={({ isActive }) => `flex items-center gap-3 rounded-2xl px-4 py-3 text-sm font-semibold transition ${isActive ? 'bg-brand text-white' : 'text-ink hover:bg-brand-soft hover:text-brand'}`}
            >
              <WandSparkles className="size-4" />
              Dashboard
            </NavLink>
            <div className="rounded-3xl bg-brand-soft p-4 text-sm text-brand">
              <p className="font-semibold">Creative-director access</p>
              <p className="mt-2 text-ink-soft">This area is reserved for production work after the recommendation has already been approved.</p>
            </div>
          </div>
        </aside>

        <main className="space-y-10">
          <PageHero
            kicker="Advertified creative"
            title={title}
            description={description}
            aside={(
              <div className="space-y-4">
                <p className="text-sm text-ink-soft">Working as {user?.fullName?.split(' ')[0] ?? 'Creative director'}</p>
                <div className="flex flex-wrap gap-3">
                  <button type="button" onClick={() => logout('manual')} className="button-secondary rounded-full font-semibold">Logout</button>
                  <Link to="/creative" className="button-primary inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold">
                    Open dashboard
                    <ArrowRight className="size-4" />
                  </Link>
                </div>
              </div>
            )}
          />
          {children}
        </main>
      </div>
    </section>
  );
}

export function CreativeDirectorDashboardPage() {
  const inboxQuery = useQuery({ queryKey: ['creative-director-inbox'], queryFn: advertifiedApi.getAgentInbox });

  if (inboxQuery.isLoading) {
    return <LoadingState label="Loading creative studio..." />;
  }

  if (inboxQuery.isError || !inboxQuery.data) {
    return (
      <section className="page-shell">
        <div className="panel mx-auto max-w-3xl p-8">
          <h1 className="text-2xl font-semibold text-ink">Creative studio unavailable</h1>
          <p className="mt-3 text-sm leading-6 text-ink-soft">{inboxQuery.error instanceof Error ? inboxQuery.error.message : 'The creative workspace could not be loaded.'}</p>
        </div>
      </section>
    );
  }

  const readyCampaigns = inboxQuery.data.items.filter((item) => item.status === 'approved' || item.status === 'creative_sent_to_client_for_approval');

  return (
    <CreativePageShell
      title="Creative production dashboard"
      description="A focused queue for approved campaigns that are ready for media creation and client sign-off preparation."
    >
      <section className="space-y-6">
        <div className="grid gap-4 md:grid-cols-3">
          {[
            { label: 'Approved campaigns', value: readyCampaigns.length, helper: 'Campaigns ready for creative production.' },
            { label: 'Time-sensitive work', value: readyCampaigns.filter((item) => item.isUrgent).length, helper: 'Approved work that looks time-sensitive.' },
            { label: 'Client sign-off next', value: readyCampaigns.length, helper: 'These campaigns are headed toward client approval after production.' },
          ].map((stat) => (
            <div key={stat.label} className="panel bg-white/90 px-5 py-5">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">{stat.label}</p>
              <p className="mt-4 text-3xl font-semibold text-ink">{stat.value}</p>
              <p className="mt-2 text-sm text-ink-soft">{stat.helper}</p>
            </div>
          ))}
        </div>

        {readyCampaigns.length > 0 ? (
          <div className="grid gap-5 xl:grid-cols-2">
            {readyCampaigns.map((item) => (
              <article key={item.id} className="rounded-[30px] border border-line bg-white p-6 shadow-[0_18px_60px_rgba(17,24,39,0.05)]">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <div className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">
                      {item.status === 'creative_sent_to_client_for_approval' ? 'Awaiting final client approval' : 'Approved and ready'}
                    </div>
                    <h3 className="mt-3 text-2xl font-semibold text-ink">{item.campaignName}</h3>
                    <p className="mt-2 text-sm text-ink-soft">{item.clientName} · {item.packageBandName}</p>
                  </div>
                  <span className="rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
                    {item.queueLabel}
                  </span>
                </div>
                <div className="mt-6 grid gap-3 sm:grid-cols-2">
                  <div className="rounded-2xl bg-slate-50 px-4 py-3 text-sm text-ink-soft">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Budget</div>
                    <div className="mt-2 text-lg font-semibold text-ink">{formatCurrency(item.selectedBudget)}</div>
                  </div>
                  <div className="rounded-2xl bg-slate-50 px-4 py-3 text-sm text-ink-soft">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Next step</div>
                    <div className="mt-2 text-sm text-ink">{item.nextAction}</div>
                  </div>
                </div>
                <div className="mt-6 flex justify-end">
                  <Link to={`/creative/campaigns/${item.id}/studio`} className="button-primary inline-flex items-center gap-2 px-5 py-3">
                    Open creative studio
                    <ArrowRight className="size-4" />
                  </Link>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <EmptyState
            title="No approved campaigns are waiting for creative production"
            description="Once a recommendation is approved, the campaign will appear here for the creative director."
            ctaHref="/creative"
            ctaLabel="Refresh studio"
          />
        )}
      </section>
    </CreativePageShell>
  );
}

export function CreativeDirectorStudioPage() {
  const { id = '' } = useParams();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const campaignQuery = useQuery({ queryKey: ['creative-campaign', id], queryFn: () => advertifiedApi.getAgentCampaign(id) });

  if (campaignQuery.isLoading) {
    return <LoadingState label="Loading creative production brief..." />;
  }

  if (campaignQuery.isError || !campaignQuery.data) {
    return (
      <EmptyState
        title="Campaign not found"
        description="We could not load this approved campaign in the creative director workspace."
        ctaHref="/creative"
        ctaLabel="Back to creative dashboard"
      />
    );
  }

  const campaign = campaignQuery.data;
  const sendFinishedMediaMutation = useMutation({
    mutationFn: () => advertifiedApi.sendFinishedMediaToClientForApproval(id),
    onSuccess: async (updatedCampaign) => {
      queryClient.setQueryData(['creative-campaign', id], updatedCampaign);
      await queryClient.invalidateQueries({ queryKey: ['creative-director-inbox'] });
      pushToast({ title: 'Finished media sent to client.', description: 'The campaign is now waiting for final client approval.' });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not send finished media.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });
  const recommendation = getPrimaryRecommendation(campaign);
  const brief = campaign.brief;
  const channelMood = recommendation ? Array.from(new Set(recommendation.items.map((item) => item.channel))).filter(Boolean) : [];
  const geographyFocus = [brief?.areas, brief?.cities, brief?.provinces].flatMap((items) => items ?? []).filter(Boolean);
  const isAwaitingFinalApproval = campaign.status === 'creative_sent_to_client_for_approval';
  const studioCollections = [
    {
      icon: Palette,
      title: 'Creative direction',
      body: brief?.creativeNotes ?? 'Translate the approved recommendation into polished media that feels brand-right, channel-right, and campaign-ready.',
      accent: 'from-amber-100 via-white to-rose-100',
    },
    {
      icon: RadioTower,
      title: 'Channel mood',
      body: channelMood.length ? channelMood.join(' • ') : 'The approved media mix will appear here once placements are attached.',
      accent: 'from-sky-100 via-white to-cyan-100',
    },
    {
      icon: ClipboardList,
      title: 'Production notes',
      body: brief?.specialRequirements ?? 'No production notes were captured. Build from the approved recommendation and package envelope.',
      accent: 'from-emerald-100 via-white to-lime-100',
    },
  ];
  const productionSignals = [
    {
      label: 'Creative readiness',
      value: brief?.creativeReady ? 'Assets available' : 'Awaiting source assets',
      helper: brief?.creativeReady ? 'Brand files or usable source materials are already available.' : 'Source materials may still need follow-up before final production.',
    },
    {
      label: 'Audience frame',
      value: brief?.targetAudienceNotes?.trim() ? 'Defined' : 'To refine',
      helper: brief?.targetAudienceNotes?.trim() || 'Audience direction has not been captured in detail yet.',
    },
    {
      label: 'Approved formats',
      value: recommendation ? String(recommendation.items.length) : '0',
      helper: recommendation ? 'These placements now shape the creative pack.' : 'No approved placements are attached yet.',
    },
  ];

  return (
    <CreativePageShell
      title="Campaign creative studio"
      description="A production-led studio surface for turning an approved recommendation into client-ready media."
    >
      <section className="space-y-6">
        <section className="overflow-hidden rounded-[36px] border border-white/70 bg-[radial-gradient(circle_at_top_left,_rgba(45,212,191,0.28),_transparent_32%),radial-gradient(circle_at_bottom_right,_rgba(251,191,36,0.22),_transparent_28%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(239,250,246,0.95))] p-8 shadow-[0_30px_90px_rgba(15,23,42,0.09)]">
          <div className="grid gap-8 lg:grid-cols-[1.5fr_0.9fr]">
            <div className="space-y-5">
              <div className="inline-flex items-center gap-2 rounded-full border border-white/80 bg-white/75 px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.35em] text-slate-700">
                <Sparkles className="h-4 w-4 text-emerald-500" />
                Creative Director Studio
              </div>
              <div className="space-y-3">
                <h2 className="font-display text-4xl leading-tight text-slate-900">{campaign.campaignName} is ready for production.</h2>
                <p className="max-w-3xl text-base leading-7 text-slate-600">
                  The recommendation is already approved, so this studio is focused on production, polish, and preparing the final creative pack for client approval.
                </p>
              </div>
              <div className="grid gap-4 sm:grid-cols-3">
                {productionSignals.map((signal) => (
                  <div key={signal.label} className="rounded-[24px] border border-white/80 bg-white/80 p-5 shadow-[0_18px_40px_rgba(15,23,42,0.05)]">
                    <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-2xl bg-emerald-50 text-emerald-600">
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
                  <div>{formatCurrency(campaign.selectedBudget)} · {campaign.packageBandName ?? 'Package selected'}</div>
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
                  <RadioTower className="h-4 w-4 text-sky-500" />
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
                  <ClipboardList className="h-4 w-4 text-emerald-500" />
                  Creative Objective Frame
                </div>
                <div className="text-sm leading-7 text-slate-700">{recommendation?.summary ?? 'The approved recommendation summary will appear here as the strategic anchor for production.'}</div>
              </div>
            </div>
          </div>

          <div className="user-card">
            <h3>Client approval handoff</h3>
            <div className="mt-4 space-y-4">
              <div className="rounded-[24px] border border-emerald-100 bg-emerald-50/70 p-4">
                <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.24em] text-emerald-700">
                  <WandSparkles className="h-4 w-4" />
                  Role boundary
                </div>
                <p className="text-sm leading-7 text-slate-700">
                  The studio is now reserved for creative directors. The client sees a waiting-for-approval handoff until the finished media is returned for sign-off.
                </p>
              </div>
              <div className="user-wire">
                {isAwaitingFinalApproval
                  ? 'Finished media has already been sent. The campaign is now in its final client-approval state.'
                  : 'The production send step is now modeled as a creative-director-owned handoff. Use it when the finished media pack is ready for client approval.'}
              </div>
              <button
                type="button"
                onClick={() => sendFinishedMediaMutation.mutate()}
                disabled={isAwaitingFinalApproval || sendFinishedMediaMutation.isPending}
                className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isAwaitingFinalApproval
                  ? 'Finished media already sent'
                  : sendFinishedMediaMutation.isPending
                    ? 'Sending to client...'
                    : 'Send finished media to client'}
              </button>
              <div className="flex flex-wrap gap-3">
                <Link to="/creative" className="user-btn-primary">Back to creative dashboard</Link>
                <Link to={`/campaigns/${campaign.id}`} className="user-btn-secondary">View client workspace</Link>
              </div>
            </div>
          </div>
        </div>
      </section>
    </CreativePageShell>
  );
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, ClipboardList, Palette, RadioTower, WandSparkles } from 'lucide-react';
import type { ChangeEvent, ReactNode } from 'react';
import { useState } from 'react';
import { Link, NavLink, useParams } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { QueryStateBoundary } from '../../components/ui/QueryStateBoundary';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { CreativeStudioAssetsPanel } from '../../features/creative/components/CreativeStudioAssetsPanel';
import { CreativeStudioBookingPanel } from '../../features/creative/components/CreativeStudioBookingPanel';
import { CreativeStudioEnginePanel } from '../../features/creative/components/CreativeStudioEnginePanel';
import { CreativeStudioOutputPanel } from '../../features/creative/components/CreativeStudioOutputPanel';
import { CreativeStudioOverviewPanel } from '../../features/creative/components/CreativeStudioOverviewPanel';
import type { CreativeBookingDraft, CreativeStudioCollection, CreativeStudioSignal } from '../../features/creative/creativeStudioTypes';
import { buildDefaultCreativePrompt, formatChannelLabel, parseDelimitedInput } from '../../features/creative/creativeStudioUtils';
import { canAccessCreativeStudio, canAccessOperations, isAdmin } from '../../lib/access';
import { getPrimaryRecommendation } from '../../lib/campaignStatus';
import { invalidateCreativeCampaignQueries, queryKeys } from '../../lib/queryKeys';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { Campaign } from '../../types/domain';

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
  const inboxQuery = useQuery({ queryKey: queryKeys.creative.inbox, queryFn: advertifiedApi.getCreativeInbox });
  return (
    <QueryStateBoundary
      query={inboxQuery}
      loadingLabel="Loading creative studio..."
      errorTitle="Creative studio unavailable"
      errorDescription="The creative workspace could not be loaded."
    >
      {(inbox) => {
        const previewCampaign = inbox.items[0] ?? null;
        const readyCampaigns = inbox.items.filter((item) =>
          item.status === 'approved'
          || item.status === 'creative_changes_requested'
          || item.status === 'creative_sent_to_client_for_approval'
          || item.status === 'creative_approved'
          || item.status === 'booking_in_progress');

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
                            {item.status === 'creative_sent_to_client_for_approval'
                              ? 'Awaiting final client approval'
                              : item.status === 'booking_in_progress'
                                ? 'Booking and launch prep'
                                : item.status === 'creative_changes_requested'
                                  ? 'Creative revision needed'
                                  : 'Approved and ready'}
                          </div>
                          <h3 className="mt-3 text-2xl font-semibold text-ink">{item.campaignName}</h3>
                          <p className="mt-2 text-sm text-ink-soft">{item.clientName} • {item.packageBandName}</p>
                        </div>
                        <span className="rounded-full border border-brand/20 bg-brand-soft px-3 py-1 text-xs font-semibold text-brand">
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
                  description={previewCampaign
                    ? 'There is nothing in the approved creative queue yet, but you can still open the studio demo or inspect a read-only campaign preview.'
                    : 'There is nothing in the approved creative queue yet, but you can still open the studio demo to inspect the full creative surface.'}
                  ctaHref="/creative/studio-demo"
                  ctaLabel="Open studio demo"
                />
              )}

              {!readyCampaigns.length && previewCampaign ? (
                <div className="flex justify-center">
                  <Link to={`/campaigns/${previewCampaign.id}/studio-preview`} className="button-secondary inline-flex items-center gap-2 px-5 py-3">
                    Open campaign studio preview
                    <ArrowRight className="size-4" />
                  </Link>
                </div>
              ) : null}
            </section>
          </CreativePageShell>
        );
      }}
    </QueryStateBoundary>
  );
}

export function CreativeDirectorStudioPage() {
  const { id = '' } = useParams();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [assetFile, setAssetFile] = useState<File | null>(null);
  const [assetType, setAssetType] = useState('creative_pack');
  const [bookingDraft, setBookingDraft] = useState<CreativeBookingDraft>({
    supplierOrStation: '',
    channel: 'radio',
    bookingStatus: 'planned',
    committedAmount: '',
    liveFrom: '',
    liveTo: '',
    notes: '',
  });
  const campaignQuery = useQuery({ queryKey: queryKeys.creative.campaign(id), queryFn: () => advertifiedApi.getCreativeCampaign(id) });
  const sendFinishedMediaMutation = useMutation({
    mutationFn: () => advertifiedApi.sendFinishedMediaToClientForApproval(id),
    onSuccess: async (updatedCampaign) => {
      queryClient.setQueryData(queryKeys.creative.campaign(id), updatedCampaign);
      await invalidateCreativeCampaignQueries(queryClient, id);
      pushToast({ title: 'Finished media sent to client.', description: 'The campaign is now waiting for final client approval.' });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not send finished media.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });
  const uploadAssetMutation = useMutation({
    mutationFn: ({ file, type }: { file: File; type: string }) => advertifiedApi.uploadCreativeCampaignAsset(id, file, type),
    onSuccess: async () => {
      setAssetFile(null);
      await invalidateCreativeCampaignQueries(queryClient, id);
      pushToast({ title: 'Creative file uploaded.', description: 'The file is now part of the campaign studio asset set.' });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not upload creative file.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });
  const saveBookingMutation = useMutation({
    mutationFn: () => advertifiedApi.saveSupplierBooking(id, {
      supplierOrStation: bookingDraft.supplierOrStation,
      channel: bookingDraft.channel,
      bookingStatus: bookingDraft.bookingStatus,
      committedAmount: Number(bookingDraft.committedAmount || 0),
      liveFrom: bookingDraft.liveFrom || undefined,
      liveTo: bookingDraft.liveTo || undefined,
      notes: bookingDraft.notes || undefined,
    }),
    onSuccess: async () => {
      setBookingDraft({
        supplierOrStation: '',
        channel: 'radio',
        bookingStatus: 'planned',
        committedAmount: '',
        liveFrom: '',
        liveTo: '',
        notes: '',
      });
      await invalidateCreativeCampaignQueries(queryClient, id);
      pushToast({
        title: 'Supplier booking saved.',
        description: 'The campaign has moved into booking and the client can now see that progress.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not save supplier booking.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });
  const markLiveMutation = useMutation({
    mutationFn: () => advertifiedApi.markCampaignLaunched(id),
    onSuccess: async () => {
      await invalidateCreativeCampaignQueries(queryClient, id);
      pushToast({
        title: 'Campaign marked live.',
        description: 'The client workspace now shows the campaign as live.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not mark campaign live.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

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
  const recommendation = getPrimaryRecommendation(campaign);
  const brief = campaign.brief;
  const channelMood = recommendation ? Array.from(new Set(recommendation.items.map((item) => formatChannelLabel(item.channel)))).filter(Boolean) : [];
  const geographyFocus = [brief?.areas, brief?.cities, brief?.provinces].flatMap((items) => items ?? []).filter(Boolean);
  const isAwaitingFinalApproval = campaign.status === 'creative_sent_to_client_for_approval';
  const isBookingStage = campaign.status === 'creative_approved' || campaign.status === 'booking_in_progress' || campaign.status === 'launched';
  const studioCollections: CreativeStudioCollection[] = [
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
      accent: 'from-brand-soft via-white to-brand-soft',
    },
    {
      icon: ClipboardList,
      title: 'Production notes',
      body: brief?.specialRequirements ?? 'No production notes were captured. Build from the approved recommendation and package envelope.',
      accent: 'from-brand-soft via-white to-brand-soft',
    },
  ];
  const productionSignals: CreativeStudioSignal[] = [
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
    <CreativeStudioContent
      campaign={campaign}
      channelMood={channelMood}
      geographyFocus={geographyFocus}
      brief={brief}
      recommendation={recommendation}
      productionSignals={productionSignals}
      studioCollections={studioCollections}
      isAwaitingFinalApproval={isAwaitingFinalApproval}
      isPreview={false}
      onSendFinishedMedia={() => sendFinishedMediaMutation.mutate()}
      isSendingFinishedMedia={sendFinishedMediaMutation.isPending}
      isBookingStage={isBookingStage}
      bookingDraft={bookingDraft}
      onBookingDraftChange={setBookingDraft}
      onSaveBooking={() => saveBookingMutation.mutate()}
      isSavingBooking={saveBookingMutation.isPending}
      onMarkLive={() => markLiveMutation.mutate()}
      isMarkingLive={markLiveMutation.isPending}
      assetFileName={assetFile?.name}
      assetType={assetType}
      onAssetTypeChange={setAssetType}
      onAssetFileChange={(event) => setAssetFile(event.target.files?.[0] ?? null)}
      onUploadAsset={() => {
        if (!assetFile) {
          return;
        }

        uploadAssetMutation.mutate({ file: assetFile, type: assetType });
      }}
      isUploadingAsset={uploadAssetMutation.isPending}
    />
  );
}

export function CreativeStudioPreviewPage() {
  const { id = '' } = useParams();
  const { user } = useAuth();
  const canUseOperationsApi = canAccessOperations(user) || isAdmin(user);
  const campaignQuery = useQuery({
    queryKey: queryKeys.creative.preview(id, canUseOperationsApi ? 'ops' : 'client'),
    queryFn: () => (canAccessCreativeStudio(user) ? advertifiedApi.getCreativeCampaign(id) : canUseOperationsApi ? advertifiedApi.getAgentCampaign(id) : advertifiedApi.getCampaign(id)),
  });

  if (campaignQuery.isLoading) {
    return <LoadingState label="Loading studio preview..." />;
  }

  if (campaignQuery.isError || !campaignQuery.data) {
    return (
      <EmptyState
        title="Studio preview unavailable"
        description="We could not load this campaign into the read-only creative studio preview."
        ctaHref="/dashboard"
        ctaLabel="Back to workspace"
      />
    );
  }

  const campaign = campaignQuery.data;
  const recommendation = getPrimaryRecommendation(campaign);
  const brief = campaign.brief;
  const channelMood = recommendation ? Array.from(new Set(recommendation.items.map((item) => formatChannelLabel(item.channel)))).filter(Boolean) : [];
  const geographyFocus = [brief?.areas, brief?.cities, brief?.provinces].flatMap((items) => items ?? []).filter(Boolean);
  const isAwaitingFinalApproval = campaign.status === 'creative_sent_to_client_for_approval';
  const studioCollections: CreativeStudioCollection[] = [
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
      accent: 'from-brand-soft via-white to-brand-soft',
    },
    {
      icon: ClipboardList,
      title: 'Production notes',
      body: brief?.specialRequirements ?? 'No production notes were captured. Build from the approved recommendation and package envelope.',
      accent: 'from-brand-soft via-white to-brand-soft',
    },
  ];
  const productionSignals: CreativeStudioSignal[] = [
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
    <CreativeStudioContent
      campaign={campaign}
      channelMood={channelMood}
      geographyFocus={geographyFocus}
      brief={brief}
      recommendation={recommendation}
      productionSignals={productionSignals}
      studioCollections={studioCollections}
      isAwaitingFinalApproval={isAwaitingFinalApproval}
      isPreview
      isSendingFinishedMedia={false}
      isBookingStage={campaign.status === 'creative_approved' || campaign.status === 'booking_in_progress' || campaign.status === 'launched'}
      bookingDraft={{
        supplierOrStation: '',
        channel: 'radio',
        bookingStatus: 'planned',
        committedAmount: '',
        liveFrom: '',
        liveTo: '',
        notes: '',
      }}
      onBookingDraftChange={() => {}}
      onSaveBooking={() => {}}
      isSavingBooking={false}
      onMarkLive={() => {}}
      isMarkingLive={false}
      assetFileName={undefined}
      assetType="creative_pack"
      onAssetTypeChange={() => {}}
      onAssetFileChange={() => {}}
      isUploadingAsset={false}
    />
  );
}

function buildDemoCampaign(): Campaign {
  return {
    id: 'studio-demo',
    userId: 'studio-demo-user',
    clientName: 'Advertified Demo Client',
    clientEmail: 'demo@advertified.local',
    businessName: 'Demo Brand Co.',
    industry: 'Retail',
    packageOrderId: 'studio-demo-order',
    packageBandId: 'studio-demo-band',
    packageBandName: 'Scale',
    selectedBudget: 38000,
    paymentStatus: 'paid',
    status: 'approved',
    planningMode: 'ai_assisted',
    aiUnlocked: true,
    agentAssistanceRequested: false,
    campaignName: 'Autumn Growth Push',
    nextAction: 'Creative production is in progress',
    timeline: [
      { key: 'payment', label: 'Payment confirmed', description: 'Demo state', state: 'complete' },
      { key: 'brief', label: 'Brief submitted', description: 'Demo state', state: 'complete' },
      { key: 'recommendation', label: 'Recommendation prepared', description: 'Demo state', state: 'complete' },
      { key: 'review', label: 'Client review', description: 'Demo state', state: 'complete' },
      { key: 'creative-production', label: 'Creative production', description: 'Demo state', state: 'current' },
    ],
    brief: {
      objective: 'Drive stronger in-store foot traffic and short-term sales momentum.',
      startDate: '2026-04-06',
      endDate: '2026-04-30',
      durationWeeks: 4,
      geographyScope: 'regional',
      provinces: ['Gauteng'],
      cities: ['Johannesburg', 'Pretoria'],
      suburbs: ['Sandton', 'Rosebank'],
      areas: ['Northern suburbs retail corridor'],
      targetAgeMin: 24,
      targetAgeMax: 44,
      targetGender: 'all',
      targetLanguages: ['English', 'Zulu'],
      targetLsmMin: 7,
      targetLsmMax: 10,
      targetInterests: ['Shopping', 'Family', 'Lifestyle'],
      targetAudienceNotes: 'Urban working adults and young families who respond to practical value and strong visual retail cues.',
      preferredMediaTypes: ['Billboards and Digital Screens', 'Radio', 'Digital'],
      excludedMediaTypes: [],
      mustHaveAreas: ['Johannesburg North'],
      excludedAreas: [],
      creativeReady: true,
      creativeNotes: 'Confident retail energy, premium-but-accessible styling, and a clear urgency-led CTA.',
      maxMediaItems: 6,
      openToUpsell: true,
      additionalBudget: 0,
      specialRequirements: 'Keep the line short enough to adapt to billboard, radio, and social cutdowns.',
    },
    recommendations: [
      {
        id: 'studio-demo-rec',
        campaignId: 'studio-demo',
        proposalLabel: 'Proposal A',
        proposalStrategy: 'Balanced mix',
        summary: 'A retail-focused media mix using roadside presence, commuter radio, and digital reinforcement around Johannesburg.',
        rationale: 'Balanced recommendation focused on fast recall and repeat exposure.',
        clientFeedbackNotes: undefined,
        manualReviewRequired: false,
        fallbackFlags: [],
        status: 'approved',
        totalCost: 41800,
        items: [
          {
            id: 'studio-demo-item-1',
            sourceInventoryId: 'demo-ooh-1',
            region: 'Johannesburg North',
            language: 'English',
            showDaypart: 'Drive time',
            timeBand: '15:00-18:00',
            slotType: 'Placement',
            duration: '4 weeks',
            restrictions: 'Artwork lock required',
            confidenceScore: 0.91,
            selectionReasons: ['High commuter visibility', 'Premium retail adjacency'],
            policyFlags: [],
            quantity: 2,
            flighting: '4 week run',
            itemNotes: 'Primary retail corridor coverage.',
            title: 'Billboards and Digital Screens premium roadside',
            channel: 'Billboards and Digital Screens',
            rationale: 'Delivers broad local visibility and strong route frequency.',
            cost: 0,
            type: 'base',
          },
          {
            id: 'studio-demo-item-2',
            sourceInventoryId: 'demo-radio-1',
            region: 'Gauteng',
            language: 'English',
            showDaypart: 'Breakfast',
            timeBand: '06:00-09:00',
            slotType: 'Radio spot',
            duration: '30s',
            restrictions: 'Script sign-off required',
            confidenceScore: 0.88,
            selectionReasons: ['High-frequency recall', 'Morning shopping intent'],
            policyFlags: [],
            quantity: 12,
            flighting: '3 bursts',
            itemNotes: 'Supports launch-week urgency.',
            title: 'Regional radio burst',
            channel: 'Radio',
            rationale: 'Builds repeated message recall around commute moments.',
            cost: 0,
            type: 'base',
          },
          {
            id: 'studio-demo-item-3',
            sourceInventoryId: 'demo-digital-1',
            region: 'Johannesburg',
            language: 'English',
            showDaypart: 'All day',
            timeBand: '00:00-23:59',
            slotType: 'Digital slot',
            duration: '2 weeks',
            restrictions: 'Static and motion variants recommended',
            confidenceScore: 0.86,
            selectionReasons: ['Supports retargeting', 'Flexible cutdowns'],
            policyFlags: [],
            quantity: 1,
            flighting: 'Always-on support',
            itemNotes: 'Useful for social/static adaptation references.',
            title: 'Digital support layer',
            channel: 'Display',
            rationale: 'Adds flexible lower-funnel creative extension.',
            cost: 0,
            type: 'base',
          },
        ],
      },
    ],
    recommendation: undefined,
    recommendationPdfUrl: undefined,
    creativeSystems: [],
    latestCreativeSystem: undefined,
    assets: [],
    supplierBookings: [],
    deliveryReports: [],
    effectiveEndDate: undefined,
    daysLeft: 18,
    createdAt: '2026-03-30T05:00:00Z',
  };
}

export function CreativeStudioDemoPage() {
  const campaign = buildDemoCampaign();
  const recommendation = getPrimaryRecommendation(campaign);
  const brief = campaign.brief;
  const channelMood = recommendation ? Array.from(new Set(recommendation.items.map((item) => formatChannelLabel(item.channel)))).filter(Boolean) : [];
  const geographyFocus = [brief?.areas, brief?.cities, brief?.provinces].flatMap((items) => items ?? []).filter(Boolean);
  const studioCollections: CreativeStudioCollection[] = [
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
      accent: 'from-brand-soft via-white to-brand-soft',
    },
    {
      icon: ClipboardList,
      title: 'Production notes',
      body: brief?.specialRequirements ?? 'No production notes were captured. Build from the approved recommendation and package envelope.',
      accent: 'from-brand-soft via-white to-brand-soft',
    },
  ];
  const productionSignals: CreativeStudioSignal[] = [
    {
      label: 'Creative readiness',
      value: 'Assets available',
      helper: 'This demo campaign is set up to show the studio in an active production state.',
    },
    {
      label: 'Audience frame',
      value: 'Defined',
      helper: brief?.targetAudienceNotes ?? 'Audience direction is already captured.',
    },
    {
      label: 'Approved formats',
      value: recommendation ? String(recommendation.items.length) : '0',
      helper: 'These approved placements feed the studio prompt and adaptation surface.',
    },
  ];

  return (
    <CreativeStudioContent
      campaign={campaign}
      channelMood={channelMood}
      geographyFocus={geographyFocus}
      brief={brief}
      recommendation={recommendation}
      productionSignals={productionSignals}
      studioCollections={studioCollections}
      isAwaitingFinalApproval={false}
      isPreview
      isSendingFinishedMedia={false}
      isBookingStage={campaign.status === 'creative_approved' || campaign.status === 'booking_in_progress' || campaign.status === 'launched'}
      bookingDraft={{
        supplierOrStation: '',
        channel: 'radio',
        bookingStatus: 'planned',
        committedAmount: '',
        liveFrom: '',
        liveTo: '',
        notes: '',
      }}
      onBookingDraftChange={() => {}}
      onSaveBooking={() => {}}
      isSavingBooking={false}
      onMarkLive={() => {}}
      isMarkingLive={false}
      assetFileName={undefined}
      assetType="creative_pack"
      onAssetTypeChange={() => {}}
      onAssetFileChange={() => {}}
      isUploadingAsset={false}
    />
  );
}

function CreativeStudioContent({
  campaign,
  channelMood,
  geographyFocus,
  brief,
  recommendation,
  productionSignals,
  studioCollections,
  isAwaitingFinalApproval,
  isPreview,
  onSendFinishedMedia,
  isSendingFinishedMedia,
  isBookingStage,
  bookingDraft,
  onBookingDraftChange,
  onSaveBooking,
  isSavingBooking,
  onMarkLive,
  isMarkingLive,
  assetFileName,
  assetType,
  onAssetTypeChange,
  onAssetFileChange,
  onUploadAsset,
  isUploadingAsset,
}: {
  campaign: Awaited<ReturnType<typeof advertifiedApi.getCampaign>>;
  channelMood: string[];
  geographyFocus: string[];
  brief: Awaited<ReturnType<typeof advertifiedApi.getCampaign>>['brief'];
  recommendation: ReturnType<typeof getPrimaryRecommendation>;
  productionSignals: Array<{ label: string; value: string; helper: string }>;
  studioCollections: Array<{ icon: typeof Palette; title: string; body: string; accent: string }>;
  isAwaitingFinalApproval: boolean;
  isPreview: boolean;
  onSendFinishedMedia?: () => void;
  isSendingFinishedMedia: boolean;
  isBookingStage: boolean;
  bookingDraft: {
    supplierOrStation: string;
    channel: string;
    bookingStatus: string;
    committedAmount: string;
    liveFrom: string;
    liveTo: string;
    notes: string;
  };
  onBookingDraftChange: (value: {
    supplierOrStation: string;
    channel: string;
    bookingStatus: string;
    committedAmount: string;
    liveFrom: string;
    liveTo: string;
    notes: string;
  }) => void;
  onSaveBooking: () => void;
  isSavingBooking: boolean;
  onMarkLive: () => void;
  isMarkingLive: boolean;
  assetFileName?: string;
  assetType: string;
  onAssetTypeChange: (value: string) => void;
  onAssetFileChange: (event: ChangeEvent<HTMLInputElement>) => void;
  onUploadAsset?: () => void;
  isUploadingAsset: boolean;
}) {
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const supplierBookings = campaign.supplierBookings ?? [];
  const canSaveBooking = bookingDraft.supplierOrStation.trim().length > 0;
  const canMarkLive = !isPreview && campaign.status === 'booking_in_progress';
  const [prompt, setPrompt] = useState(() => buildDefaultCreativePrompt({
    campaignName: campaign.campaignName,
    businessName: campaign.businessName ?? campaign.clientName,
    packageBandName: campaign.packageBandName,
    briefObjective: brief?.objective,
    audience: brief?.targetAudienceNotes,
    creativeNotes: brief?.creativeNotes,
    channelMood,
  }));
  const [brandInput, setBrandInput] = useState(campaign.businessName ?? campaign.clientName ?? '');
  const [productInput, setProductInput] = useState(campaign.campaignName ?? '');
  const [audienceInput, setAudienceInput] = useState(brief?.targetAudienceNotes ?? '');
  const [objectiveInput, setObjectiveInput] = useState(brief?.objective ?? '');
  const [toneInput, setToneInput] = useState(brief?.creativeNotes ?? '');
  const [channelsInput, setChannelsInput] = useState(channelMood.join(', '));
  const [ctaInput, setCtaInput] = useState('');
  const [constraintsInput, setConstraintsInput] = useState(brief?.specialRequirements ?? '');
  const [activeAiJobId, setActiveAiJobId] = useState('');
  const [regenCreativeId, setRegenCreativeId] = useState('');
  const [regenFeedback, setRegenFeedback] = useState('');
  const [scopedCreativeState, setScopedCreativeState] = useState<{
    key: string;
    creativeSystem: Awaited<ReturnType<typeof advertifiedApi.generateCreativeSystem>> | null;
    lastIterationLabel: string | null;
  } | null>(null);
  const creativeStateKey = campaign.latestCreativeSystem?.id ?? `campaign:${campaign.id}:none`;
  const creativeSystem = scopedCreativeState?.key === creativeStateKey
    ? scopedCreativeState.creativeSystem
    : (campaign.latestCreativeSystem?.output ?? null);
  const lastIterationLabel = scopedCreativeState?.key === creativeStateKey
    ? scopedCreativeState.lastIterationLabel
    : (campaign.latestCreativeSystem?.iterationLabel ?? null);

  const creativeSystemMutation = useMutation({
    mutationFn: async (variables: { iterationLabel?: string; iterationInstruction?: string } = {}) => {
      const { iterationInstruction } = variables;
      const normalizedPrompt = iterationInstruction
        ? `${prompt.trim()}\n\nIteration direction: ${iterationInstruction}`
        : prompt.trim();

      return advertifiedApi.generateCreativeSystem(campaign.id, {
        prompt: normalizedPrompt,
        iterationLabel: variables.iterationLabel,
        brand: brandInput.trim() || undefined,
        product: productInput.trim() || undefined,
        audience: audienceInput.trim() || undefined,
        objective: objectiveInput.trim() || undefined,
        tone: toneInput.trim() || undefined,
        channels: parseDelimitedInput(channelsInput),
        cta: ctaInput.trim() || undefined,
        constraints: parseDelimitedInput(constraintsInput),
      });
    },
    onSuccess: (result, variables) => {
      setScopedCreativeState({
        key: creativeStateKey,
        creativeSystem: result,
        lastIterationLabel: variables.iterationLabel ?? null,
      });
      queryClient.invalidateQueries({ queryKey: queryKeys.creative.campaign(campaign.id) }).catch(() => {});
      pushToast({
        title: variables.iterationLabel ? `Creative system updated: ${variables.iterationLabel}.` : 'Creative system generated.',
        description: 'The studio output is ready for review and handoff.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Creative system could not be generated.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const campaignCreativesQuery = useQuery({
    queryKey: ['ai-platform-campaign-creatives', campaign.id],
    queryFn: () => advertifiedApi.getAiPlatformCampaignCreatives(campaign.id),
    enabled: !isPreview,
  });

  const submitAiJobMutation = useMutation({
    mutationFn: async () => advertifiedApi.submitAiPlatformJob({
      campaignId: campaign.id,
      promptOverride: prompt.trim() || undefined,
    }),
    onSuccess: (response) => {
      setActiveAiJobId(response.jobId);
      pushToast({
        title: 'AI platform job queued.',
        description: `Job ${response.jobId.slice(0, 8)} is running in the queue.`,
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not queue AI platform job.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const aiJobStatusQuery = useQuery({
    queryKey: ['ai-platform-job-status', activeAiJobId],
    queryFn: () => advertifiedApi.getAiPlatformJobStatus(activeAiJobId),
    enabled: activeAiJobId.trim().length > 0,
    refetchInterval: (query) => {
      const status = query.state.data?.status?.toLowerCase();
      return status === 'completed' || status === 'failed' ? false : 3000;
    },
  });

  const regenerateAiMutation = useMutation({
    mutationFn: async () => advertifiedApi.regenerateAiPlatformCreative({
      creativeId: regenCreativeId.trim(),
      campaignId: campaign.id,
      feedback: regenFeedback.trim(),
    }),
    onSuccess: (response) => {
      campaignCreativesQuery.refetch().catch(() => {});
      pushToast({
        title: 'AI creative regenerated.',
        description: `Created ${response.creativeCount} creatives and ${response.assetCount} assets.`,
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Regeneration failed.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  return (
    <CreativePageShell
      title={isPreview ? 'Creative studio preview' : 'Campaign creative studio'}
      description={isPreview ? 'A read-only preview of the creative studio so you can see the production surface without changing campaign state.' : 'A production-led studio surface for turning an approved recommendation into client-ready media.'}
    >
      <section className="space-y-6">
        <CreativeStudioOverviewPanel
          campaign={campaign}
          brief={brief}
          recommendation={recommendation ?? undefined}
          channelMood={channelMood}
          geographyFocus={geographyFocus}
          studioCollections={studioCollections}
          productionSignals={productionSignals}
          isPreview={isPreview}
          isBookingStage={isBookingStage}
          isAwaitingFinalApproval={isAwaitingFinalApproval}
          isSendingFinishedMedia={isSendingFinishedMedia}
          onSendFinishedMedia={onSendFinishedMedia}
        />        {isBookingStage ? (
          <CreativeStudioBookingPanel
            campaign={campaign}
            isPreview={isPreview}
            bookingDraft={bookingDraft}
            onBookingDraftChange={onBookingDraftChange}
            onSaveBooking={onSaveBooking}
            isSavingBooking={isSavingBooking}
            canSaveBooking={canSaveBooking}
            canMarkLive={canMarkLive}
            onMarkLive={onMarkLive}
            isMarkingLive={isMarkingLive}
            supplierBookings={supplierBookings}
          />
        ) : null}        <div className="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
          <CreativeStudioEnginePanel
            campaign={campaign}
            isPreview={isPreview}
            lastIterationLabel={lastIterationLabel}
            prompt={prompt}
            setPrompt={setPrompt}
            brandInput={brandInput}
            setBrandInput={setBrandInput}
            productInput={productInput}
            setProductInput={setProductInput}
            audienceInput={audienceInput}
            setAudienceInput={setAudienceInput}
            objectiveInput={objectiveInput}
            setObjectiveInput={setObjectiveInput}
            toneInput={toneInput}
            setToneInput={setToneInput}
            channelsInput={channelsInput}
            setChannelsInput={setChannelsInput}
            ctaInput={ctaInput}
            setCtaInput={setCtaInput}
            constraintsInput={constraintsInput}
            setConstraintsInput={setConstraintsInput}
            onGenerateCreativeSystem={(iteration) => creativeSystemMutation.mutate(iteration ?? {})}
            isGeneratingCreativeSystem={creativeSystemMutation.isPending}
            onQueueAiJob={() => submitAiJobMutation.mutate()}
            isQueueingAiJob={submitAiJobMutation.isPending}
            activeAiJobId={activeAiJobId}
            setActiveAiJobId={setActiveAiJobId}
            aiJobStatus={aiJobStatusQuery.data}
            onUseLatestCreativeId={() => {
              const latestId = campaignCreativesQuery.data?.[0]?.id ?? '';
              if (latestId) {
                setRegenCreativeId(latestId);
              }
            }}
            canUseLatestCreativeId={Boolean(campaignCreativesQuery.data?.length)}
            regenCreativeId={regenCreativeId}
            setRegenCreativeId={setRegenCreativeId}
            regenFeedback={regenFeedback}
            setRegenFeedback={setRegenFeedback}
            onRegenerateWithFeedback={() => regenerateAiMutation.mutate()}
            isRegenerating={regenerateAiMutation.isPending}
            campaignCreativeOptions={campaignCreativesQuery.data ?? []}
            formatChannelLabel={formatChannelLabel}
          />
          <CreativeStudioOutputPanel
            campaign={campaign}
            creativeSystem={creativeSystem}
            isPreview={isPreview}
            onSelectSavedVersion={(savedSystem) => {
              setScopedCreativeState({
                key: creativeStateKey,
                creativeSystem: savedSystem.output,
                lastIterationLabel: savedSystem.iterationLabel ?? null,
              });
              setPrompt(savedSystem.prompt);
            }}
          />
        </div>
        <CreativeStudioAssetsPanel
          campaign={campaign}
          isPreview={isPreview}
          assetType={assetType}
          assetFileName={assetFileName}
          onAssetTypeChange={onAssetTypeChange}
          onAssetFileChange={onAssetFileChange}
          onUploadAsset={onUploadAsset}
          isUploadingAsset={isUploadingAsset}
        />      </section>
    </CreativePageShell>
  );
}




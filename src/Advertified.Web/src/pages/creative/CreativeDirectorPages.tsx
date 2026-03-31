import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, CheckCircle2, ClipboardList, ImagePlus, Palette, RadioTower, Sparkles, WandSparkles } from 'lucide-react';
import type { ChangeEvent, ReactNode } from 'react';
import { useState } from 'react';
import { Link, NavLink, useParams } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { canAccessCreativeStudio, canAccessOperations, isAdmin } from '../../lib/access';
import { invalidateCreativeCampaignQueries, queryKeys } from '../../lib/queryKeys';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { Campaign } from '../../types/domain';
import { getPrimaryRecommendation } from '../client/clientWorkspace';

const creativeIterationOptions = [
  { label: 'Shorter', instruction: 'Compress the campaign into a shorter, sharper version without losing the master idea.' },
  { label: 'Bolder', instruction: 'Make the campaign bolder, more distinctive, and higher contrast while keeping it commercially usable.' },
  { label: 'More premium', instruction: 'Elevate the campaign so it feels more premium, restrained, and polished.' },
  { label: 'More Gen Z', instruction: 'Shift the campaign language and pacing to feel more Gen Z-native without losing clarity.' },
  { label: 'More performance', instruction: 'Sharpen the campaign for stronger response, clearer value, and a harder-working CTA.' },
] as const;

function parseDelimitedInput(value: string) {
  return value
    .split(/\r?\n|,/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function buildDefaultCreativePrompt({
  campaignName,
  businessName,
  packageBandName,
  briefObjective,
  audience,
  creativeNotes,
  channelMood,
}: {
  campaignName: string;
  businessName?: string;
  packageBandName?: string;
  briefObjective?: string;
  audience?: string;
  creativeNotes?: string;
  channelMood: string[];
}) {
  const context = [
    `Build a production-ready campaign system for ${campaignName}.`,
    businessName ? `Brand: ${businessName}.` : undefined,
    packageBandName ? `Package frame: ${packageBandName}.` : undefined,
    briefObjective ? `Objective: ${briefObjective}.` : undefined,
    audience ? `Audience: ${audience}.` : undefined,
    channelMood.length ? `Channels to cover: ${channelMood.join(', ')}.` : undefined,
    creativeNotes ? `Creative notes: ${creativeNotes}.` : undefined,
    'Give me one strong master idea, a clear narrative spine, native channel adaptations, and production-ready outputs.',
  ];

  return context.filter(Boolean).join(' ');
}

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

  const previewCampaign = inboxQuery.data.items[0] ?? null;
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
}

export function CreativeDirectorStudioPage() {
  const { id = '' } = useParams();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [assetFile, setAssetFile] = useState<File | null>(null);
  const [assetType, setAssetType] = useState('creative_pack');
  const campaignQuery = useQuery({ queryKey: queryKeys.creative.campaign(id), queryFn: () => advertifiedApi.getCreativeCampaign(id) });

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
      accent: 'from-brand-soft via-white to-brand-soft',
    },
    {
      icon: ClipboardList,
      title: 'Production notes',
      body: brief?.specialRequirements ?? 'No production notes were captured. Build from the approved recommendation and package envelope.',
      accent: 'from-brand-soft via-white to-brand-soft',
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
      accent: 'from-brand-soft via-white to-brand-soft',
    },
    {
      icon: ClipboardList,
      title: 'Production notes',
      body: brief?.specialRequirements ?? 'No production notes were captured. Build from the approved recommendation and package envelope.',
      accent: 'from-brand-soft via-white to-brand-soft',
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
      preferredMediaTypes: ['Billboards and digital screens', 'Radio', 'Digital'],
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
            title: 'Billboards and digital screens premium roadside',
            channel: 'Billboards and digital screens',
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
  const channelMood = recommendation ? Array.from(new Set(recommendation.items.map((item) => item.channel))).filter(Boolean) : [];
  const geographyFocus = [brief?.areas, brief?.cities, brief?.provinces].flatMap((items) => items ?? []).filter(Boolean);
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
      accent: 'from-brand-soft via-white to-brand-soft',
    },
    {
      icon: ClipboardList,
      title: 'Production notes',
      body: brief?.specialRequirements ?? 'No production notes were captured. Build from the approved recommendation and package envelope.',
      accent: 'from-brand-soft via-white to-brand-soft',
    },
  ];
  const productionSignals = [
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
  assetFileName?: string;
  assetType: string;
  onAssetTypeChange: (value: string) => void;
  onAssetFileChange: (event: ChangeEvent<HTMLInputElement>) => void;
  onUploadAsset?: () => void;
  isUploadingAsset: boolean;
}) {
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
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

  return (
    <CreativePageShell
      title={isPreview ? 'Creative studio preview' : 'Campaign creative studio'}
      description={isPreview ? 'A read-only preview of the creative studio so you can see the production surface without changing campaign state.' : 'A production-led studio surface for turning an approved recommendation into client-ready media.'}
    >
      <section className="space-y-6">
        <section className="overflow-hidden rounded-[36px] border border-white/70 bg-[radial-gradient(circle_at_top_left,_rgba(45,212,191,0.28),_transparent_32%),radial-gradient(circle_at_bottom_right,_rgba(251,191,36,0.22),_transparent_28%),linear-gradient(135deg,rgba(255,255,255,0.98),rgba(239,250,246,0.95))] p-8 shadow-[0_30px_90px_rgba(15,23,42,0.09)]">
          <div className="grid gap-8 lg:grid-cols-[1.5fr_0.9fr]">
            <div className="space-y-5">
              <div className="inline-flex items-center gap-2 rounded-full border border-white/80 bg-white/75 px-4 py-2 text-[11px] font-semibold uppercase tracking-[0.35em] text-slate-700">
                <Sparkles className="h-4 w-4 text-brand" />
                {isPreview ? 'Creative Studio Preview' : 'Creative Director Studio'}
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
                {isAwaitingFinalApproval
                  ? 'Finished media has already been sent. The campaign is now in its final client-approval state.'
                  : 'The production send step is now modeled as a creative-director-owned handoff. Use it when the finished media pack is ready for client approval.'}
              </div>
              <button
                type="button"
                onClick={onSendFinishedMedia}
                disabled={isPreview || isAwaitingFinalApproval || isSendingFinishedMedia}
                className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isPreview
                  ? 'Preview only'
                  : isAwaitingFinalApproval
                  ? 'Finished media already sent'
                  : isSendingFinishedMedia
                    ? 'Sending to client...'
                    : 'Send finished media to client'}
              </button>
              <div className="flex flex-wrap gap-3">
                <Link to={isPreview ? `/campaigns/${campaign.id}` : '/creative'} className="user-btn-primary">
                  {isPreview ? 'Back to campaign workspace' : 'Back to creative dashboard'}
                </Link>
                {!isPreview ? <Link to={`/campaigns/${campaign.id}`} className="user-btn-secondary">View client workspace</Link> : null}
              </div>
            </div>
          </div>
        </div>

        <div className="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
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
                  onClick={() => creativeSystemMutation.mutate({})}
                  disabled={isPreview || !prompt.trim() || creativeSystemMutation.isPending}
                  className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {isPreview ? 'Preview only' : creativeSystemMutation.isPending ? 'Generating...' : 'Generate creative system'}
                </button>
                {creativeIterationOptions.map((option) => (
                  <button
                    key={option.label}
                    type="button"
                    onClick={() => creativeSystemMutation.mutate({ iterationLabel: option.label, iterationInstruction: option.instruction })}
                    disabled={isPreview || !prompt.trim() || creativeSystemMutation.isPending}
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
            </div>
          </div>

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
                      <strong>Channels:</strong> {creativeSystem.campaignSummary.channels.join(' • ')}
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
        </div>

        {campaign.creativeSystems.length ? (
          <div className="user-card">
            <h3>Saved creative versions</h3>
            <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {campaign.creativeSystems.map((savedSystem) => (
                <button
                  key={savedSystem.id}
                  type="button"
                  onClick={() => {
                    setScopedCreativeState({
                      key: creativeStateKey,
                      creativeSystem: savedSystem.output,
                      lastIterationLabel: savedSystem.iterationLabel ?? null,
                    });
                    setPrompt(savedSystem.prompt);
                  }}
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
                    <div className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">{adaptation.channel} • {adaptation.format}</div>
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

        <div className="grid gap-6 xl:grid-cols-[1fr_1fr]">
          <div className="user-card">
            <h3>Studio files</h3>
            <div className="mt-4 space-y-3">
              {campaign.assets.length > 0 ? campaign.assets.map((asset) => (
                <div key={asset.id} className="user-wire">
                  <strong>{asset.displayName}</strong>
                  <div>{asset.assetType.replace(/_/g, ' ')}</div>
                  {asset.publicUrl ? (
                    <a href={asset.publicUrl} target="_blank" rel="noreferrer" className="user-btn-secondary mt-3 inline-flex">
                      Open file
                    </a>
                  ) : null}
                </div>
              )) : (
                <div className="user-wire">No creative files uploaded yet.</div>
              )}
            </div>
          </div>

          <div className="user-card">
            <h3>{isPreview ? 'Studio uploads' : 'Upload creative files'}</h3>
            <div className="mt-4 space-y-3">
              {isPreview ? (
                <div className="user-wire">Preview mode keeps uploads disabled, but this is where the creative team’s files will appear in the real studio.</div>
              ) : (
                <>
                  <select value={assetType} onChange={(event) => onAssetTypeChange(event.target.value)} className="input-base">
                    <option value="creative_pack">Creative pack</option>
                    <option value="brand_asset">Brand asset</option>
                    <option value="final_media">Final media</option>
                  </select>
                  <input type="file" onChange={onAssetFileChange} className="input-base" />
                  <div className="user-wire">{assetFileName ?? 'Choose a file to upload into the studio asset set.'}</div>
                  <button
                    type="button"
                    onClick={onUploadAsset}
                    disabled={!assetFileName || isUploadingAsset}
                    className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {isUploadingAsset ? 'Uploading...' : 'Upload file'}
                  </button>
                </>
              )}
            </div>
          </div>
        </div>
      </section>
    </CreativePageShell>
  );
}


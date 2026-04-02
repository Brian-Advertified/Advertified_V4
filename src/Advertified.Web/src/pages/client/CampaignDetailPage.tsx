import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MessageSquareText, Sparkles } from 'lucide-react';
import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { canAccessAiStudioForStatus } from '../../features/campaigns/aiStudioAccess';
import { RecommendationViewer } from '../../features/campaigns/components/RecommendationViewer';
import { buildApprovalDetails, getApprovalContent, getHeroContent } from '../../features/campaigns/clientCampaignDetailContent';
import { invalidateClientCampaignQueries, queryKeys } from '../../lib/queryKeys';
import { formatCurrency, formatDate, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { ClientCampaignShell, getCampaignProgressPercent } from './clientWorkspace';

function formatChannelLabel(value: string) {
  return value.replace(/\booh\b/gi, 'Billboards and Digital Screens');
}

export function CampaignDetailPage() {
  const { id = '' } = useParams();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const campaignQuery = useQuery({ queryKey: queryKeys.campaigns.detail(id), queryFn: () => advertifiedApi.getCampaign(id) });
  const threadQuery = useQuery({ queryKey: queryKeys.campaigns.messages(id), queryFn: () => advertifiedApi.getCampaignMessages(id) });
  const recommendations = campaignQuery.data
    ? (campaignQuery.data.recommendations.length > 0
      ? campaignQuery.data.recommendations
      : (campaignQuery.data.recommendation ? [campaignQuery.data.recommendation] : []))
    : [];

  const [messageDraft, setMessageDraft] = useState('');
  const [changeNotes, setChangeNotes] = useState('');
  const [selectedRecommendationId, setSelectedRecommendationId] = useState('');
  const requestedRecommendationId = searchParams.get('recommendationId')?.trim() ?? '';
  const requestedAction = searchParams.get('action')?.trim() ?? '';
  const showRejectAllFlow = requestedAction === 'reject_all';
  const resolvedRecommendationId = recommendations.some((item) => item.id === selectedRecommendationId)
    ? selectedRecommendationId
    : recommendations.some((item) => item.id === requestedRecommendationId)
      ? requestedRecommendationId
    : (recommendations.find((item) => item.status === 'sent_to_client')?.id ?? recommendations[0]?.id ?? '');

  const approveMutation = useMutation({
    mutationFn: (recommendationId?: string) => advertifiedApi.approveRecommendation(id, recommendationId),
    onSuccess: async () => {
      await invalidateClientCampaignQueries(queryClient, id, user?.id);
      pushToast({
        title: 'Recommendation approved.',
        description: 'Advertified will now move this campaign into creative production.',
      });
      navigate(`/campaigns/${id}`);
    },
    onError: (error) => {
      pushToast({
        title: 'Could not approve recommendation.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const requestChangesMutation = useMutation({
    mutationFn: (notes: string) => advertifiedApi.requestRecommendationChanges(id, notes),
    onSuccess: async () => {
      setChangeNotes('');
      await invalidateClientCampaignQueries(queryClient, id, user?.id);
      pushToast({
        title: 'Change request sent.',
        description: 'Your feedback has been sent to the Advertified team.',
      });
      navigate(`/campaigns/${id}/messages`);
    },
    onError: (error) => {
      pushToast({
        title: 'Could not request changes.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const rejectAllMutation = useMutation({
    mutationFn: (notes: string) => advertifiedApi.requestRecommendationChanges(id, notes),
    onSuccess: async () => {
      setChangeNotes('');
      await invalidateClientCampaignQueries(queryClient, id, user?.id);
      pushToast({
        title: 'All proposals rejected.',
        description: 'Your agent will prepare a fresh proposal set based on your notes.',
      });
      navigate(`/campaigns/${id}/messages`);
    },
    onError: (error) => {
      pushToast({
        title: 'Could not reject all proposals.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const approveCreativeMutation = useMutation({
    mutationFn: () => advertifiedApi.approveCreative(id),
    onSuccess: async () => {
      await invalidateClientCampaignQueries(queryClient, id, user?.id);
      pushToast({
        title: 'Creative approved.',
        description: 'Final creative approval has been captured for this campaign.',
      });
      navigate(`/campaigns/${id}`);
    },
    onError: (error) => {
      pushToast({
        title: 'Could not approve creative.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const requestCreativeChangesMutation = useMutation({
    mutationFn: () => advertifiedApi.requestCreativeChanges(id, changeNotes.trim()),
    onSuccess: async () => {
      setChangeNotes('');
      await invalidateClientCampaignQueries(queryClient, id, user?.id, true);
      pushToast({
        title: 'Creative changes requested.',
        description: 'Your creative feedback has been sent back for revision.',
      });
      navigate(`/campaigns/${id}/messages`);
    },
    onError: (error) => {
      pushToast({
        title: 'Could not request creative changes.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const sendMessageMutation = useMutation({
    mutationFn: (body: string) => advertifiedApi.sendCampaignMessage(id, body),
    onSuccess: (thread) => {
      setMessageDraft('');
      queryClient.setQueryData(queryKeys.campaigns.messages(id), thread);
      pushToast({
        title: 'Message sent.',
        description: 'Your agent will see it in the campaign conversation.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not send message.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  if (campaignQuery.isLoading || threadQuery.isLoading) {
    return <LoadingState label="Loading campaign workspace..." />;
  }

  if (user?.role === 'creative_director') {
    return <Navigate to={`/creative/campaigns/${id}/studio`} replace />;
  }

  if (user?.role === 'agent' || user?.role === 'admin') {
    return <Navigate to={`/agent/campaigns/${id}`} replace />;
  }

  if (campaignQuery.isError || !campaignQuery.data || threadQuery.isError || !threadQuery.data) {
    return (
      <EmptyState
        title="Campaign not found"
        description="We could not load this campaign workspace."
        ctaHref="/dashboard"
        ctaLabel="Back to dashboard"
      />
    );
  }

  const campaign = campaignQuery.data;
  const thread = threadQuery.data;
  const recommendation = recommendations.find((item) => item.id === resolvedRecommendationId) ?? recommendations[0];
  const progress = getCampaignProgressPercent(campaign);
  const campaignReadiness = campaign.status === 'launched'
    ? 100
    : campaign.status === 'creative_sent_to_client_for_approval'
      ? Math.max(progress, 90)
      : campaign.status === 'creative_approved'
        ? Math.max(progress, 96)
        : progress;
  const recommendationAwaitingDecision = recommendation?.status === 'sent_to_client';
  const paymentRequiredBeforeApproval = campaign.status === 'awaiting_purchase';
  const canApproveRecommendation = Boolean(
    recommendation
      && recommendationAwaitingDecision
      && !paymentRequiredBeforeApproval
      && campaign.status !== 'approved'
      && campaign.status !== 'creative_changes_requested'
      && campaign.status !== 'creative_sent_to_client_for_approval'
      && campaign.status !== 'creative_approved'
      && campaign.status !== 'launched'
      && recommendation.status !== 'approved',
  );
  const canApproveCreative = campaign.status === 'creative_sent_to_client_for_approval';
  const hero = getHeroContent(campaign, recommendation?.status);
  const approval = getApprovalContent(campaign, recommendation?.status);
  const details = buildApprovalDetails(campaign, recommendation);
  const latestAgentMessage = [...thread.messages].reverse().find((message) => message.senderRole === 'agent');
  const activeView = location.pathname.endsWith('/approvals')
    ? 'approvals'
    : location.pathname.endsWith('/messages')
      ? 'messages'
      : 'overview';
  const campaignBasePath = `/campaigns/${campaign.id}`;

  function buildSelectedProposalFeedback(noteBody: string) {
    const selectedLabel = recommendation?.proposalLabel ?? recommendation?.id ?? 'Selected proposal';
    return `Client selected proposal for revision: ${selectedLabel}\nClient notes: ${noteBody.trim()}`;
  }

  function buildRejectAllFeedback(noteBody: string) {
    return `Client rejected all proposals.\nReason: ${noteBody.trim()}`;
  }

  async function handleDownloadRecommendationPdf() {
    if (!campaign.recommendationPdfUrl) {
      return;
    }

    try {
      await advertifiedApi.downloadProtectedFile(campaign.recommendationPdfUrl, `recommendation-${campaign.id}.pdf`);
    } catch (error) {
      pushToast({
        title: 'Could not download recommendation PDF.',
        description: error instanceof Error ? error.message : 'Please sign in again and retry.',
      }, 'error');
    }
  }

  return (
    <ClientCampaignShell
      campaign={campaign}
      title={campaign.campaignName}
      description="A simplified campaign workspace that only shows the current approval and a direct line to your Advertified team."
      activeView={activeView}
    >
      <div className="space-y-6">
        {activeView === 'overview' ? (
          <>
            <section id="overview" className="rounded-[30px] border border-line bg-white p-7 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
          <div className="inline-flex items-center gap-2 rounded-full bg-brand-soft px-4 py-2 text-xs font-semibold uppercase tracking-[0.22em] text-brand">
            <Sparkles className="size-4" />
            One thing to do
          </div>

          <div className="mt-5 grid gap-5 xl:grid-cols-[1.6fr_0.8fr]">
            <div className="rounded-[22px] border border-brand/15 bg-[linear-gradient(180deg,#f7fcfa_0%,#eef8f4_100%)] p-6">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Right now</div>
              <h2 className="mt-3 text-3xl font-semibold tracking-tight text-ink">{hero.title}</h2>
              <p className="mt-3 max-w-3xl text-sm leading-7 text-ink-soft">{hero.description}</p>
              <div className="mt-5 flex flex-wrap gap-3">
                <Link to={`${campaignBasePath}/approvals`} className="user-btn-primary">{hero.primaryAction}</Link>
                <Link to={`${campaignBasePath}/messages`} className="user-btn-secondary">Ask a question</Link>
                <Link to={`/campaigns/${campaign.id}/studio-preview`} className="user-btn-secondary">Preview studio</Link>
                {canAccessAiStudioForStatus(campaign.status) ? (
                  <Link to={`/ai-studio?campaignId=${campaign.id}`} className="user-btn-secondary">Prefill from approved recommendation</Link>
                ) : null}
              </div>
              <div className="mt-4 flex flex-wrap gap-2">
                <span className="user-pill">{hero.timeLabel}</span>
                <span className="user-pill">Reviewed by Advertified team</span>
                <span className="user-pill">{hero.reassurance}</span>
              </div>
            </div>

            <div className="rounded-[22px] border border-line bg-white p-5">
              <h3 className="text-lg font-semibold text-ink">Progress</h3>
              <div className="mt-4">
                <div className="mb-2 flex items-center justify-between gap-3 text-sm text-ink-soft">
                  <span>Campaign readiness</span>
                  <strong className="text-ink">{campaignReadiness}%</strong>
                </div>
                <progress className="h-3 w-full overflow-hidden rounded-full [&::-webkit-progress-bar]:bg-slate-100 [&::-webkit-progress-value]:bg-[linear-gradient(90deg,#0d5c4f,#4eb193)] [&::-moz-progress-bar]:bg-[linear-gradient(90deg,#0d5c4f,#4eb193)]" max={100} value={campaignReadiness} />
              </div>
              <div className="mt-4 grid gap-3">
                <div className="rounded-[16px] border border-line bg-slate-50/70 p-4">
                  <div className="mb-1 text-sm font-semibold text-ink">Current status</div>
                  <div className="text-sm leading-6 text-ink-soft">{approval.statusText}</div>
                </div>
                <div className="rounded-[16px] border border-line bg-slate-50/70 p-4">
                  <div className="mb-1 text-sm font-semibold text-ink">What happens after</div>
                  <div className="text-sm leading-6 text-ink-soft">{approval.nextPhaseText}</div>
                </div>
              </div>
            </div>
            </div>
            </section>
            {campaign.deliveryReports.length > 0 || campaign.supplierBookings.length > 0 || campaign.assets.length > 0 || campaign.daysLeft != null ? (
              <section className="rounded-[30px] border border-line bg-white p-7 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
            <div className="mb-5">
              <h3 className="text-xl font-semibold text-ink">Delivery updates</h3>
              <p className="mt-2 text-sm leading-7 text-ink-soft">
                Live execution, supplier confirmations, and campaign files appear here as the team moves the campaign through delivery.
              </p>
            </div>

            <div className="grid gap-5 xl:grid-cols-[0.9fr_1.1fr]">
              <div className="space-y-4">
                {campaign.daysLeft != null ? (
                  <div className="rounded-[18px] border border-line bg-slate-50/70 p-5">
                    <div className="mb-2 text-sm font-semibold text-ink">Days left</div>
                    <p className="text-3xl font-semibold text-ink">{campaign.daysLeft}</p>
                    <p className="mt-2 text-sm leading-6 text-ink-soft">
                      Effective end date {campaign.effectiveEndDate ? formatDate(`${campaign.effectiveEndDate}T00:00:00`) : 'will appear here once the schedule is fully set.'}
                    </p>
                  </div>
                ) : null}

                <div className="rounded-[18px] border border-line bg-slate-50/70 p-5">
                  <div className="mb-2 text-sm font-semibold text-ink">Supplier bookings</div>
                  {campaign.supplierBookings.length > 0 ? (
                    <div className="space-y-3">
                      {campaign.supplierBookings.slice(0, 3).map((booking) => (
                        <div key={booking.id} className="user-wire">
                          <strong>{booking.supplierOrStation}</strong>
                          <div>{formatChannelLabel(booking.channel)} | {titleCase(booking.bookingStatus)}</div>
                          <div>{booking.liveFrom || booking.liveTo ? `${booking.liveFrom ?? 'Start TBC'} to ${booking.liveTo ?? 'End TBC'}` : 'Dates still being confirmed'}</div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-sm leading-6 text-ink-soft">Supplier confirmations will appear here once bookings start being logged.</p>
                  )}
                </div>
              </div>

              <div className="space-y-4">
                <div className="rounded-[18px] border border-line bg-slate-50/70 p-5">
                  <div className="mb-2 text-sm font-semibold text-ink">Latest reports</div>
                  {campaign.deliveryReports.length > 0 ? (
                    <div className="space-y-3">
                      {campaign.deliveryReports.slice(0, 3).map((report) => (
                        <div key={report.id} className="user-wire">
                          <strong>{report.headline}</strong>
                          <div>{titleCase(report.reportType)} | {report.reportedAt ? formatDate(report.reportedAt) : 'Reported now'}</div>
                          <div>{report.summary ?? 'No summary provided yet.'}</div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-sm leading-6 text-ink-soft">Performance and proof-of-delivery updates will land here after launch starts.</p>
                  )}
                </div>

                <div className="rounded-[18px] border border-line bg-slate-50/70 p-5">
                  <div className="mb-2 text-sm font-semibold text-ink">Campaign files</div>
                  {campaign.assets.length > 0 ? (
                    <div className="space-y-3">
                      {campaign.assets.slice(0, 4).map((asset) => (
                        <div key={asset.id} className="flex items-center justify-between gap-3 rounded-[14px] border border-line bg-white px-4 py-3">
                          <div>
                            <div className="text-sm font-semibold text-ink">{asset.displayName}</div>
                            <div className="text-xs text-ink-soft">{asset.assetType.replace(/_/g, ' ')}</div>
                          </div>
                          {asset.publicUrl ? <a href={asset.publicUrl} target="_blank" rel="noreferrer" className="user-btn-secondary">Open</a> : null}
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-sm leading-6 text-ink-soft">Creative packs, proofs, and related files will appear here once the team uploads them.</p>
                  )}
                </div>
              </div>
            </div>
              </section>
            ) : null}
          </>
        ) : null}

        {activeView === 'approvals' ? (
          <section id="approvals" className="rounded-[30px] border border-line bg-white p-7 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
          <div className="mb-5">
            <h3 className="text-xl font-semibold text-ink">Approvals</h3>
            <p className="mt-2 text-sm leading-7 text-ink-soft">
              Everything that needs your approval appears here. If there&apos;s nothing here, you&apos;re done for now.
            </p>
          </div>

          {recommendations.length > 1 ? (
            <div className="mb-6 rounded-[18px] border border-line bg-slate-50/70 p-5">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Proposal options</p>
              <p className="mt-2 text-sm text-ink-soft">Select the proposal you want to accept or revise. You can also reject all with comments.</p>
              <div className="mt-4 grid gap-3 md:grid-cols-3">
                {recommendations.map((proposal, index) => {
                  const selected = proposal.id === recommendation?.id;
                  return (
                    <button
                      key={proposal.id}
                      type="button"
                      onClick={() => setSelectedRecommendationId(proposal.id)}
                      className={`rounded-[14px] border px-4 py-3 text-left transition ${
                        selected ? 'border-brand bg-white' : 'border-line bg-white hover:border-brand/35'
                      }`}
                    >
                      <p className="text-sm font-semibold text-ink">{proposal.proposalLabel ?? `Proposal ${index + 1}`}</p>
                      <p className="mt-1 text-xs text-ink-soft">{proposal.proposalStrategy ?? 'Media plan option'}</p>
                      <p className="mt-2 text-sm font-semibold text-ink">{formatCurrency(proposal.totalCost)}</p>
                      <p className="mt-2 text-[11px] uppercase tracking-[0.12em] text-ink-soft">
                        {titleCase(proposal.status.replace(/_/g, ' '))}
                      </p>
                    </button>
                  );
                })}
              </div>
            </div>
          ) : null}

          {requestedRecommendationId && recommendation ? (
            <div className="mb-6 rounded-[16px] border border-brand/30 bg-brand-soft/40 px-4 py-3 text-sm text-ink">
              <strong>{recommendation.proposalLabel ?? 'Selected proposal'}</strong> was preselected from your email/PDF link. Review details, then confirm with
              {' '}
              <strong>Accept as final</strong>
              .
            </div>
          ) : null}

          {showRejectAllFlow ? (
            <div className="mb-6 rounded-[16px] border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
              Share why these proposals do not work for you, then click <strong>Reject all and request new set</strong>. Your agent will prepare new options from your notes.
            </div>
          ) : null}

          {recommendation ? (
            <div className="mb-6">
              <RecommendationViewer recommendation={recommendation} recommendationPdfUrl={campaign.recommendationPdfUrl} />
            </div>
          ) : (
            <div className="mb-6 rounded-[18px] border border-line bg-slate-50/70 p-5 text-sm leading-7 text-ink-soft">
              Your recommendation is still being prepared. Once it is ready, you&apos;ll see the full channel mix and placements here before you approve.
            </div>
          )}

          <div className="grid gap-5 xl:grid-cols-[1.2fr_0.8fr]">
            <div className={`rounded-[18px] border p-5 ${approval.highlightClass}`}>
              <div className="mb-3 flex items-start justify-between gap-3">
                <div>
                  <div className="text-lg font-semibold text-ink">{approval.title}</div>
                  <p className="mt-2 text-sm leading-7 text-ink-soft">{approval.body}</p>
                </div>
                <span className={`rounded-full border px-3 py-1 text-xs font-semibold ${approval.badgeClass}`}>
                  {approval.badge}
                </span>
              </div>

              {details.length > 0 ? (
                <div className="mt-4 flex flex-wrap gap-2">
                  {details.map((detail) => (
                    <span key={detail} className="rounded-full border border-line bg-white px-3 py-1.5 text-xs font-semibold text-ink-soft">
                      {detail}
                    </span>
                  ))}
                </div>
              ) : null}

              {recommendation?.summary ? (
                <div className="mt-4 rounded-[14px] border border-dashed border-line bg-slate-50/80 p-4 text-sm leading-7 text-ink-soft">
                  {recommendation.summary}
                </div>
              ) : null}
            </div>

            <div className="space-y-4">
              <div className="rounded-[18px] border border-line bg-slate-50/70 p-5">
                <div className="mb-2 text-sm font-semibold text-ink">What you need to do</div>
                <p className="text-sm leading-7 text-ink-soft">{approval.guidance}</p>
              </div>
              {paymentRequiredBeforeApproval ? (
                <div className="rounded-[18px] border border-amber-200 bg-amber-50 p-5">
                  <div className="mb-2 text-sm font-semibold text-amber-900">Payment required first</div>
                  <p className="text-sm leading-7 text-amber-800">
                    Continue to payment and we will automatically accept the currently selected proposal after payment succeeds.
                  </p>
                  <div className="mt-3">
                    <Link
                      to={`/checkout/payment?orderId=${encodeURIComponent(campaign.packageOrderId)}&campaignId=${encodeURIComponent(campaign.id)}${recommendation?.id ? `&recommendationId=${encodeURIComponent(recommendation.id)}` : ''}`}
                      className="user-btn-primary"
                    >
                      Pay and accept selected proposal
                    </Link>
                  </div>
                </div>
              ) : null}
              <div className="rounded-[18px] border border-line bg-slate-50/70 p-5">
                <div className="mb-2 text-sm font-semibold text-ink">Why this should feel safe</div>
                <p className="text-sm leading-7 text-ink-soft">{approval.reassurance}</p>
              </div>

              {canApproveRecommendation ? (
                <div className="space-y-3">
                  <textarea
                    value={changeNotes}
                    onChange={(event) => setChangeNotes(event.target.value)}
                    className="input-base min-h-[120px]"
                    placeholder="Comments for revisions, or reason if rejecting all proposals..."
                  />
                  <div className="flex flex-wrap gap-3">
                    <button
                      type="button"
                      onClick={() => approveMutation.mutate(recommendation?.id)}
                      disabled={approveMutation.isPending || requestChangesMutation.isPending || rejectAllMutation.isPending}
                      className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {approveMutation.isPending ? 'Accepting...' : 'Accept as final'}
                    </button>
                    <button
                      type="button"
                      onClick={() => requestChangesMutation.mutate(buildSelectedProposalFeedback(changeNotes))}
                      disabled={approveMutation.isPending || requestChangesMutation.isPending || rejectAllMutation.isPending || !changeNotes.trim()}
                      className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {requestChangesMutation.isPending ? 'Sending...' : 'Request changes on selected proposal'}
                    </button>
                    <button
                      type="button"
                      onClick={() => rejectAllMutation.mutate(buildRejectAllFeedback(changeNotes))}
                      disabled={approveMutation.isPending || requestChangesMutation.isPending || rejectAllMutation.isPending || !changeNotes.trim()}
                      className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {rejectAllMutation.isPending ? 'Sending...' : 'Reject all and request new set'}
                    </button>
                    <Link to={`${campaignBasePath}/messages`} className="user-btn">Ask question</Link>
                  </div>
                </div>
              ) : canApproveCreative ? (
                <div className="space-y-3">
                  <textarea
                    value={changeNotes}
                    onChange={(event) => setChangeNotes(event.target.value)}
                    className="input-base min-h-[120px]"
                    placeholder="Optional notes if you want the creative revised before approval..."
                  />
                  <div className="flex flex-wrap gap-3">
                    <button
                      type="button"
                      onClick={() => approveCreativeMutation.mutate()}
                      disabled={approveCreativeMutation.isPending || requestCreativeChangesMutation.isPending}
                      className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {approveCreativeMutation.isPending ? 'Approving...' : 'Approve creative'}
                    </button>
                    <button
                      type="button"
                      onClick={() => requestCreativeChangesMutation.mutate()}
                      disabled={approveCreativeMutation.isPending || requestCreativeChangesMutation.isPending || !changeNotes.trim()}
                      className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {requestCreativeChangesMutation.isPending ? 'Sending...' : 'Request creative changes'}
                    </button>
                    <Link to={`${campaignBasePath}/messages`} className="user-btn">Ask question</Link>
                  </div>
                </div>
              ) : (
                <div className="flex flex-wrap gap-3">
                  <Link to={`${campaignBasePath}/messages`} className="user-btn-primary">Ask question</Link>
                  {campaign.recommendationPdfUrl ? (
                    <button type="button" onClick={() => void handleDownloadRecommendationPdf()} className="user-btn-secondary">Open document</button>
                  ) : null}
                </div>
              )}
            </div>
          </div>
          </section>
        ) : null}

        {activeView === 'messages' ? (
          <section id="messages" className="rounded-[30px] border border-line bg-white p-7 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
          <div className="mb-5">
            <h3 className="text-xl font-semibold text-ink">Need help?</h3>
            <p className="mt-2 text-sm leading-7 text-ink-soft">
              If you&apos;re unsure, ask here instead of digging through campaign details. This should feel like support, not extra work.
            </p>
          </div>

          <div className="grid gap-5 xl:grid-cols-[1fr_0.9fr]">
            <div className="rounded-[22px] border border-line bg-[linear-gradient(180deg,#fbfefd_0%,#f5faf8_100%)] p-5">
              <label className="mb-3 block text-sm font-semibold text-ink">Message to agent</label>
              <textarea
                value={messageDraft}
                onChange={(event) => setMessageDraft(event.target.value)}
                className="input-base min-h-[170px]"
                placeholder="Ask a question about the recommendation, timing, or anything that feels unclear..."
              />
              <div className="mt-4 flex flex-wrap gap-3">
                <button
                  type="button"
                  onClick={() => sendMessageMutation.mutate(messageDraft.trim())}
                  disabled={sendMessageMutation.isPending || !messageDraft.trim()}
                  className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {sendMessageMutation.isPending ? 'Sending...' : 'Send message'}
                </button>
                <Link to={`${campaignBasePath}/approvals`} className="user-btn-secondary">Back to approval</Link>
              </div>
            </div>

            <div className="space-y-4">
              {latestAgentMessage ? (
                <div className="rounded-[18px] border border-brand/20 bg-[linear-gradient(180deg,#f4fbf8_0%,#eef8f4_100%)] p-5">
                  <div className="mb-2 flex items-start justify-between gap-3">
                    <div className="text-lg font-semibold text-ink">{thread.assignedAgentName ?? latestAgentMessage.senderName}</div>
                    <span className="rounded-full border border-sky-200 bg-sky-50 px-3 py-1 text-xs font-semibold text-sky-700">
                      {thread.unreadCount > 0 ? `${thread.unreadCount} unread` : 'Workspace chat'}
                    </span>
                  </div>
                  <p className="text-sm leading-7 text-ink-soft">{latestAgentMessage.body}</p>
                  <div className="mt-3 flex flex-wrap gap-2">
                    <span className="user-pill">{formatDate(latestAgentMessage.createdAt)}</span>
                    <span className="user-pill">{latestAgentMessage.senderName}</span>
                  </div>
                </div>
              ) : (
                <div className="rounded-[18px] border border-line bg-slate-50/70 p-5">
                  <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-ink">
                    <MessageSquareText className="size-4 text-brand" />
                    No messages yet
                  </div>
                  <p className="text-sm leading-7 text-ink-soft">
                    Your campaign conversation will appear here as soon as you or your assigned agent sends the first in-app message.
                  </p>
                </div>
              )}

              {[...thread.messages].reverse().slice(0, 4).map((message) => (
                <div key={message.id} className="rounded-[18px] border border-line bg-slate-50/50 p-5">
                  <div className="mb-2 flex items-start justify-between gap-3">
                    <div className="text-base font-semibold text-ink">{message.senderRole === 'client' ? 'Your message' : message.senderName}</div>
                    <span className={`rounded-full border px-3 py-1 text-xs font-semibold ${
                      message.senderRole === 'client'
                        ? 'border-blue-200 bg-blue-50 text-blue-700'
                        : 'border-emerald-200 bg-emerald-50 text-emerald-700'
                    }`}>
                      {message.senderRole === 'client' ? 'Sent' : message.isRead ? 'Read' : 'New'}
                    </span>
                  </div>
                  <p className="text-sm leading-7 text-ink-soft">{message.body}</p>
                  <div className="mt-3 flex flex-wrap gap-2">
                    <span className="user-pill">{formatDate(message.createdAt)}</span>
                    <span className="user-pill">{titleCase(message.senderRole)}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
          </section>
        ) : null}
      </div>
    </ClientCampaignShell>
  );
}

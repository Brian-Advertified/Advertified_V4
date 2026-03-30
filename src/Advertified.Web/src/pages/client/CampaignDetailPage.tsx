import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MessageSquareText, Sparkles } from 'lucide-react';
import { useState } from 'react';
import { Navigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { formatCurrency, formatDate, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { ClientCampaignShell, getCampaignProgressPercent, getPrimaryRecommendation } from './clientWorkspace';

export function CampaignDetailPage() {
  const { id = '' } = useParams();
  const { user } = useAuth();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const campaignQuery = useQuery({ queryKey: ['campaign', id], queryFn: () => advertifiedApi.getCampaign(id) });
  const threadQuery = useQuery({ queryKey: ['campaign-messages', id], queryFn: () => advertifiedApi.getCampaignMessages(id) });

  const [messageDraft, setMessageDraft] = useState('');
  const [changeNotes, setChangeNotes] = useState('');

  const approveMutation = useMutation({
    mutationFn: (recommendationId?: string) => advertifiedApi.approveRecommendation(id, recommendationId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaigns', user?.id] }),
      ]);
      pushToast({
        title: 'Recommendation approved.',
        description: 'Advertified will now move this campaign into creative production.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not approve recommendation.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const requestChangesMutation = useMutation({
    mutationFn: () => advertifiedApi.requestRecommendationChanges(id, changeNotes.trim()),
    onSuccess: async () => {
      setChangeNotes('');
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaigns', user?.id] }),
      ]);
      pushToast({
        title: 'Change request sent.',
        description: 'Your feedback has been sent to the Advertified team.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not request changes.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const approveCreativeMutation = useMutation({
    mutationFn: () => advertifiedApi.approveCreative(id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaigns', user?.id] }),
      ]);
      pushToast({
        title: 'Creative approved.',
        description: 'Final creative approval has been captured for this campaign.',
      });
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
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['campaign', id] }),
        queryClient.invalidateQueries({ queryKey: ['campaigns', user?.id] }),
        queryClient.invalidateQueries({ queryKey: ['campaign-messages', id] }),
      ]);
      pushToast({
        title: 'Creative changes requested.',
        description: 'Your creative feedback has been sent back for revision.',
      });
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
      queryClient.setQueryData(['campaign-messages', id], thread);
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
  const recommendation = getPrimaryRecommendation(campaign);
  const progress = getCampaignProgressPercent(campaign);
  const campaignReadiness = campaign.status === 'launched'
    ? 100
    : campaign.status === 'creative_sent_to_client_for_approval'
      ? Math.max(progress, 90)
      : campaign.status === 'creative_approved'
        ? Math.max(progress, 96)
        : progress;
  const canApproveRecommendation = Boolean(
    recommendation
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
  const details = buildApprovalDetails(campaign);
  const latestAgentMessage = [...thread.messages].reverse().find((message) => message.senderRole === 'agent');

  return (
    <ClientCampaignShell
      campaign={campaign}
      title={campaign.campaignName}
      description="A simplified campaign workspace that only shows the current approval and a direct line to your Advertified team."
    >
      <div className="space-y-6">
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
                <a href="#approvals" className="user-btn-primary">{hero.primaryAction}</a>
                <a href="#messages" className="user-btn-secondary">Ask a question</a>
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

        <section id="approvals" className="rounded-[30px] border border-line bg-white p-7 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
          <div className="mb-5">
            <h3 className="text-xl font-semibold text-ink">Approvals</h3>
            <p className="mt-2 text-sm leading-7 text-ink-soft">
              Everything that needs your approval appears here. If there&apos;s nothing here, you&apos;re done for now.
            </p>
          </div>

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
                    placeholder="Optional notes if you want changes before approval..."
                  />
                  <div className="flex flex-wrap gap-3">
                    <button
                      type="button"
                      onClick={() => approveMutation.mutate(recommendation?.id)}
                      disabled={approveMutation.isPending || requestChangesMutation.isPending}
                      className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {approveMutation.isPending ? 'Approving...' : 'Looks good - approve'}
                    </button>
                    <button
                      type="button"
                      onClick={() => requestChangesMutation.mutate()}
                      disabled={approveMutation.isPending || requestChangesMutation.isPending || !changeNotes.trim()}
                      className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {requestChangesMutation.isPending ? 'Sending...' : 'Request changes'}
                    </button>
                    <a href="#messages" className="user-btn">Ask question</a>
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
                    <a href="#messages" className="user-btn">Ask question</a>
                  </div>
                </div>
              ) : (
                <div className="flex flex-wrap gap-3">
                  <a href="#messages" className="user-btn-primary">Ask question</a>
                  {campaign.recommendationPdfUrl ? (
                    <a href={campaign.recommendationPdfUrl} className="user-btn-secondary">Open document</a>
                  ) : null}
                </div>
              )}
            </div>
          </div>
        </section>

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
                <a href="#approvals" className="user-btn-secondary">Back to approval</a>
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
      </div>
    </ClientCampaignShell>
  );
}

function getHeroContent(
  campaign: Awaited<ReturnType<typeof advertifiedApi.getCampaign>>,
  recommendationStatus?: string,
) {
  if (campaign.status === 'launched') {
    return {
      title: 'Your campaign is now live',
      description: 'Operations has activated your campaign. There are no more approvals waiting for you in this workspace right now.',
      primaryAction: 'Review live status',
      timeLabel: 'No action required',
      reassurance: 'We will keep using this workspace for important updates and support',
    };
  }

  if (campaign.status === 'creative_approved') {
    return {
      title: 'Your campaign is approved and waiting for activation',
      description: 'Final creative approval has been captured. Operations will mark the campaign live once activation begins, so you do not need to complete any more approvals right now.',
      primaryAction: 'Review final status',
      timeLabel: 'No action required',
      reassurance: 'We will notify you if anything new needs attention',
    };
  }

  if (campaign.status === 'creative_changes_requested') {
    return {
      title: 'Creative revisions are in progress',
      description: 'Your feedback has been sent back to Advertified and the creative team is preparing a revised handoff for you.',
      primaryAction: 'Check revision status',
      timeLabel: 'No action required',
      reassurance: 'You will see the next approval here once the revision is ready',
    };
  }

  if (campaign.status === 'creative_sent_to_client_for_approval') {
    return {
      title: 'Review the finished campaign handoff',
      description: 'Your finished media has been returned for final client approval. Use the approval section below to review the current campaign state and message the team if anything needs to change.',
      primaryAction: 'Review approval status',
      timeLabel: 'Short review',
      reassurance: 'Support is one message away',
    };
  }

  if (campaign.status === 'approved') {
    return {
      title: 'Your recommendation is approved',
      description: 'Advertified is now taking the next step for you. Creative production is in motion, so you do not need to manage the rest of the workflow from separate campaign pages.',
      primaryAction: 'Check approval status',
      timeLabel: 'No action required',
      reassurance: 'We will notify you when something needs attention',
    };
  }

  if (recommendationStatus === 'sent_to_client' || campaign.status === 'review_ready' || campaign.status === 'planning_in_progress') {
    return {
      title: 'Approve your campaign recommendation',
      description: 'We have simplified the workspace so you only see what matters now: one approval, one way to ask for help, and a calm handoff to the Advertified team after that.',
      primaryAction: 'Review recommendation',
      timeLabel: 'Takes about 2 minutes',
      reassurance: 'You can still request changes later',
    };
  }

  return {
    title: 'Your campaign is moving through setup',
    description: campaign.nextAction,
    primaryAction: 'Open campaign status',
    timeLabel: 'Quick check',
    reassurance: 'Ask your agent if anything feels unclear',
  };
}

function getApprovalContent(
  campaign: Awaited<ReturnType<typeof advertifiedApi.getCampaign>>,
  recommendationStatus?: string,
) {
  if (campaign.status === 'launched') {
    return {
      title: 'Your campaign is live',
      body: 'Operations has activated the campaign and it is now live.',
      badge: 'Live',
      badgeClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      highlightClass: 'border-emerald-200 bg-[linear-gradient(180deg,#f4fbf8_0%,#eef8f4_100%)]',
      guidance: 'There is nothing left to approve here. Use messages if you need support or want to check in with the team.',
      reassurance: 'Client approval and live activation are now tracked separately, so you can see exactly when the campaign moved from approval into execution.',
      statusText: 'Campaign live',
      nextPhaseText: 'Campaign execution is underway',
    };
  }

  if (campaign.status === 'creative_approved') {
    return {
      title: 'You are all set for activation',
      body: 'Final creative approval has been captured. Operations can now activate the campaign as a separate backend step.',
      badge: 'Done',
      badgeClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      highlightClass: 'border-emerald-200 bg-[linear-gradient(180deg,#f4fbf8_0%,#eef8f4_100%)]',
      guidance: 'There is nothing else you need to approve right now. Use messages if you want to speak to the team.',
      reassurance: 'The final creative approval is persisted in the backend, and activation will only happen once operations explicitly marks the campaign live.',
      statusText: 'Final creative approved',
      nextPhaseText: 'Operations activates the campaign',
    };
  }

  if (campaign.status === 'creative_changes_requested') {
    return {
      title: 'Creative changes requested',
      body: 'Your creative feedback has been sent back to Advertified and the team is revising the finished media.',
      badge: 'Sent back',
      badgeClass: 'border-amber-200 bg-amber-50 text-amber-700',
      highlightClass: 'border-amber-200 bg-[linear-gradient(180deg,#fffaf0_0%,#fff5dc_100%)]',
      guidance: 'There is nothing else you need to do right now. The revised creative handoff will return here once it is ready.',
      reassurance: 'Your revision request is now a formal backend state, so the workflow will not skip ahead while the team is updating the creative.',
      statusText: 'Waiting on revised creative',
      nextPhaseText: 'Advertified prepares a revised creative handoff',
    };
  }

  if (campaign.status === 'creative_sent_to_client_for_approval') {
    return {
      title: 'Finished media sent to client',
      body: 'Advertified has moved the campaign into the final client-approval state. Approve the finished creative or send it back with revision notes.',
      badge: 'Needs approval',
      badgeClass: 'border-sky-200 bg-sky-50 text-sky-700',
      highlightClass: 'border-sky-200 bg-[linear-gradient(180deg,#f4fbff_0%,#eef6ff_100%)]',
      guidance: 'Review the finished creative here. Approve it if it is ready, or request changes with specific notes if you want the team to revise it.',
      reassurance: 'This is a real persisted approval step in the backend, so your decision here controls the next workflow state.',
      statusText: 'Waiting for final creative approval',
      nextPhaseText: 'Launch preparation continues after final sign-off',
    };
  }

  if (campaign.status === 'approved') {
    return {
      title: 'You are all set for now',
      body: 'Thanks. Your recommendation approval has already been captured and the campaign is moving through creative production.',
      badge: 'Done',
      badgeClass: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      highlightClass: 'border-emerald-200 bg-[linear-gradient(180deg,#f4fbf8_0%,#eef8f4_100%)]',
      guidance: 'There is nothing else you need to approve right now. If something feels unclear, send a message and the team will help.',
      reassurance: 'This campaign has already moved past recommendation review and into the next backend step.',
      statusText: 'Recommendation approved',
      nextPhaseText: 'Our team starts creative production',
    };
  }

  if (recommendationStatus === 'sent_to_client' || campaign.status === 'review_ready' || campaign.status === 'planning_in_progress') {
    return {
      title: 'Approve recommendation',
      body: 'Review the recommended media plan and approve it so the Advertified team can continue. If anything feels unclear, ask your agent before deciding.',
      badge: 'Needs approval',
      badgeClass: 'border-blue-200 bg-blue-50 text-blue-700',
      highlightClass: 'border-brand/25 bg-[linear-gradient(180deg,#f4fbf8_0%,#eef8f4_100%)]',
      guidance: 'Approve this plan so we can continue, or send it back with notes if you want changes before creative production starts.',
      reassurance: 'This recommendation has already been reviewed by the Advertified team, and you can still request adjustments after approving.',
      statusText: 'Waiting for your approval',
      nextPhaseText: 'Our team starts creative production',
    };
  }

  return {
    title: 'No approval needed right now',
    body: campaign.nextAction,
    badge: 'Up to date',
    badgeClass: 'border-slate-200 bg-slate-50 text-slate-700',
    highlightClass: 'border-line bg-slate-50/70',
    guidance: 'You do not need to complete any approval on this screen at the moment.',
    reassurance: 'When something truly needs your attention, it will appear here as the main task.',
    statusText: titleCase(campaign.status),
    nextPhaseText: campaign.nextAction,
  };
}

function buildApprovalDetails(campaign: Awaited<ReturnType<typeof advertifiedApi.getCampaign>>) {
  const recommendation = getPrimaryRecommendation(campaign);
  const channels = Array.from(new Set(recommendation?.items.map((item) => item.channel).filter(Boolean) ?? []));
  const details = [];

  if (recommendation?.items.length) {
    details.push(`${recommendation.items.length} placement${recommendation.items.length === 1 ? '' : 's'}`);
  }

  if (channels.length) {
    details.push(`Channels: ${channels.join(', ')}`);
  }

  if (campaign.brief?.durationWeeks) {
    details.push(`Timeline: ${campaign.brief.durationWeeks} weeks`);
  }

  if (campaign.brief?.objective) {
    details.push(`Goal: ${titleCase(campaign.brief.objective)}`);
  }

  if (campaign.selectedBudget) {
    details.push(`Budget: ${formatCurrency(campaign.selectedBudget)}`);
  }

  return details;
}

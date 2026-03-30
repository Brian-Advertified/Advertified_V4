import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MessageSquareText, Sparkles } from 'lucide-react';
import { useState } from 'react';
import { Link, Navigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { buildApprovalDetails, getApprovalContent, getHeroContent } from '../../features/campaigns/clientCampaignDetailContent';
import { invalidateClientCampaignQueries, queryKeys } from '../../lib/queryKeys';
import { formatDate, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { ClientCampaignShell, getCampaignProgressPercent, getPrimaryRecommendation } from './clientWorkspace';

export function CampaignDetailPage() {
  const { id = '' } = useParams();
  const { user } = useAuth();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const campaignQuery = useQuery({ queryKey: queryKeys.campaigns.detail(id), queryFn: () => advertifiedApi.getCampaign(id) });
  const threadQuery = useQuery({ queryKey: queryKeys.campaigns.messages(id), queryFn: () => advertifiedApi.getCampaignMessages(id) });

  const [messageDraft, setMessageDraft] = useState('');
  const [changeNotes, setChangeNotes] = useState('');

  const approveMutation = useMutation({
    mutationFn: (recommendationId?: string) => advertifiedApi.approveRecommendation(id, recommendationId),
    onSuccess: async () => {
      await invalidateClientCampaignQueries(queryClient, id, user?.id);
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
      await invalidateClientCampaignQueries(queryClient, id, user?.id);
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
      await invalidateClientCampaignQueries(queryClient, id, user?.id);
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
      await invalidateClientCampaignQueries(queryClient, id, user?.id, true);
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
                <Link to={`/campaigns/${campaign.id}/studio-preview`} className="user-btn-secondary">Preview studio</Link>
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
                          <div>{booking.channel} | {titleCase(booking.bookingStatus)}</div>
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

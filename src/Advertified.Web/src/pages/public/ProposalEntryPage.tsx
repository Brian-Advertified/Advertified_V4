import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowRight, FileText, Layers3, Sparkles, TrendingUp } from 'lucide-react';
import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageHero } from '../../components/marketing/PageHero';
import { RecommendationViewer } from '../../features/campaigns/components/RecommendationViewer';
import { getCampaignRecommendations, selectRecommendation } from '../../features/campaigns/recommendationSelection';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { hasRecommendationApprovalCompleted } from '../../lib/campaignStatus';
import { getCampaignPaymentState } from '../../lib/access';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { parseCampaignOpportunityContext } from '../../features/campaigns/briefModel';
import type { Campaign, CampaignRecommendation } from '../../types/domain';

function getSafeNextPath(raw: string | null) {
  if (!raw) {
    return null;
  }

  const trimmed = raw.trim();
  return trimmed.startsWith('/') ? trimmed : null;
}

function buildOfferHeadline(campaign: Campaign, recommendation?: CampaignRecommendation) {
  const strategy = recommendation?.proposalStrategy?.trim();
  if (strategy) {
    return `${strategy} offer for ${campaign.campaignName}`;
  }

  return `${campaign.packageBandName} offer for ${campaign.campaignName}`;
}

function buildFitBullets(campaign: Campaign, recommendation?: CampaignRecommendation) {
  const reasons = Array.from(new Set(recommendation?.items.flatMap((item) => item.selectionReasons) ?? []))
    .filter((reason) => reason.trim().length > 0)
    .slice(0, 3);

  if (reasons.length > 0) {
    return reasons;
  }

  return [
    `Built inside the ${campaign.packageBandName} route for this campaign stage.`,
    'Structured to keep the strongest placements in view first.',
    'Designed so the plan can be refined instead of rejected outright.',
  ];
}

function buildIncludedItems(recommendation?: CampaignRecommendation) {
  const channels = Array.from(new Set(recommendation?.items.map((item) => item.channel.replace(/\booh\b/gi, 'Billboards and Digital Screens')) ?? []));
  const placements = recommendation?.items.length ?? 0;

  return [
    channels.length > 0 ? `Channels included: ${channels.join(', ')}` : 'Channels are being prepared for this offer.',
    placements > 0 ? `${placements} planned placement${placements === 1 ? '' : 's'} in the main route.` : 'Placement detail will appear as soon as the route is finalised.',
    'Creative, approvals, and campaign support stay inside the Advertified workflow.',
  ];
}

function buildFlexibleRoutes(campaign: Campaign, recommendation?: CampaignRecommendation) {
  const bestFitAmount = recommendation?.totalCost ?? campaign.selectedBudget;
  const baseAmount = campaign.selectedBudget > 0 ? campaign.selectedBudget : bestFitAmount;
  const leanStartAmount = Math.min(bestFitAmount, baseAmount);
  const phasedStartAmount = Math.max(leanStartAmount, Math.round(bestFitAmount * 0.6));

  return [
    {
      title: 'Best-fit route',
      badge: 'Recommended',
      amountLabel: formatCurrency(bestFitAmount),
      description: 'This is the strongest route for the full campaign shape shown in the proposal.',
      supportCopy: 'Choose this when you want the clearest version of the full recommendation.',
      toneClassName: 'border-brand/20 bg-white shadow-[0_12px_30px_rgba(15,118,110,0.08)]',
      badgeClassName: 'border-brand/15 bg-brand-soft text-brand',
      icon: Sparkles,
    },
    {
      title: 'Lean start option',
      badge: 'Lower entry point',
      amountLabel: `From ${formatCurrency(leanStartAmount)}`,
      description: 'Start with the core placements first and reduce the footprint if you need a lighter way in.',
      supportCopy: 'Best when the full route feels heavy today but you still want to move forward.',
      toneClassName: 'border-amber-200 bg-amber-50/80 shadow-[0_12px_28px_rgba(245,158,11,0.08)]',
      badgeClassName: 'border-amber-200 bg-white text-amber-900',
      icon: Layers3,
    },
    {
      title: 'Phased rollout',
      badge: 'Spread the timing',
      amountLabel: `${formatCurrency(phasedStartAmount)} first wave`,
      description: 'Launch the first wave now, then expand the rollout once timing, cash flow, or results allow.',
      supportCopy: 'Useful when you want the bigger plan but need to stagger spend or execution.',
      toneClassName: 'border-sky-200 bg-sky-50/80 shadow-[0_12px_28px_rgba(14,165,233,0.08)]',
      badgeClassName: 'border-sky-200 bg-white text-sky-900',
      icon: TrendingUp,
    },
  ];
}

export function ProposalEntryPage() {
  const { id = '' } = useParams();
  const [searchParams] = useSearchParams();
  const location = useLocation();
  const { isAuthenticated } = useAuth();
  const { pushToast } = useToast();
  const navigate = useNavigate();
  const [changeNotes, setChangeNotes] = useState('');
  const [selectedRecommendationId, setSelectedRecommendationId] = useState('');
  const recommendationId = searchParams.get('recommendationId')?.trim() ?? '';
  const requestedAction = searchParams.get('action')?.trim() ?? '';
  const action = requestedAction === 'reject_all' ? 'reject_all' : '';
  const token = searchParams.get('token')?.trim() ?? '';
  const hasPublicAccessToken = token.length > 0;

  const publicProposalQuery = useQuery({
    queryKey: ['public-proposal', id, token],
    queryFn: () => advertifiedApi.getPublicProposal(id, token),
    enabled: Boolean(id && token),
    retry: false,
  });

  const recommendations = getCampaignRecommendations(publicProposalQuery.data);
  const opportunityContext = parseCampaignOpportunityContext(publicProposalQuery.data?.brief);
  const showRejectAllFlow = action === 'reject_all';
  const recommendation = selectRecommendation(recommendations, {
    currentSelectionId: selectedRecommendationId,
    requestedRecommendationId: recommendationId,
  });
  const offerHeadline = publicProposalQuery.data
    ? buildOfferHeadline(publicProposalQuery.data, recommendation)
    : '';
  const fitBullets = publicProposalQuery.data
    ? buildFitBullets(publicProposalQuery.data, recommendation)
    : [];
  const includedItems = buildIncludedItems(recommendation);
  const flexibleRoutes = publicProposalQuery.data
    ? buildFlexibleRoutes(publicProposalQuery.data, recommendation)
    : [];
  const paymentState = publicProposalQuery.data
    ? getCampaignPaymentState(publicProposalQuery.data)
    : 'payment_required';
  const paymentAwaitingReview = paymentState === 'manual_review';
  const paymentRequiredBeforeApproval = paymentState === 'payment_required';
  const approvalAlreadyCompleted = hasRecommendationApprovalCompleted(
    publicProposalQuery.data?.status,
    recommendation,
    publicProposalQuery.data?.lifecycle,
  );
  const currentProposalPath = `${location.pathname}${location.search}`;
  const checkoutPath = publicProposalQuery.data && recommendation
    ? `/checkout/payment?orderId=${encodeURIComponent(publicProposalQuery.data.packageOrderId)}&campaignId=${encodeURIComponent(publicProposalQuery.data.id)}&recommendationId=${encodeURIComponent(recommendation.id)}&proposalPath=${encodeURIComponent(currentProposalPath)}`
    : null;
  const loginCheckoutPath = checkoutPath
    ? `/login?next=${encodeURIComponent(checkoutPath)}`
    : null;

  const approveMutation = useMutation({
    mutationFn: (selectedId?: string) => advertifiedApi.approvePublicProposal(id, token, selectedId),
    onSuccess: async () => {
      await publicProposalQuery.refetch();
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

  const prepareCheckoutMutation = useMutation({
    mutationFn: (selectedId: string) => advertifiedApi.preparePublicProposalCheckout(id, token, selectedId),
    onError: (error) => {
      pushToast({
        title: 'Could not prepare payment.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const requestChangesMutation = useMutation({
    mutationFn: (notes: string) => advertifiedApi.requestPublicProposalChanges(id, token, notes),
    onSuccess: async () => {
      setChangeNotes('');
      await publicProposalQuery.refetch();
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

  const rejectAllMutation = useMutation({
    mutationFn: (notes: string) => advertifiedApi.rejectAllPublicProposals(id, token, notes),
    onSuccess: async () => {
      setChangeNotes('');
      await publicProposalQuery.refetch();
      pushToast({
        title: 'All proposals rejected.',
        description: 'Your agent will prepare a fresh proposal set based on your notes.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not reject all proposals.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  const approvalQueryParams = new URLSearchParams();
  if (recommendationId) {
    approvalQueryParams.set('recommendationId', recommendationId);
  }
  if (action) {
    approvalQueryParams.set('action', action);
  }
  const approvalsQuery = approvalQueryParams.toString();
  const approvalsPath = approvalsQuery
    ? `/campaigns/${encodeURIComponent(id)}/approvals?${approvalsQuery}`
    : `/campaigns/${encodeURIComponent(id)}/approvals`;

  if (!id) {
    return <Navigate to="/" replace />;
  }

  if (hasPublicAccessToken) {
    if (publicProposalQuery.isLoading) {
      return <LoadingState label="Loading proposal..." />;
    }

    if (publicProposalQuery.isError || !publicProposalQuery.data) {
      return (
        <section className="page-shell space-y-8 pb-20">
          <PageHero
            kicker="Proposal review"
            title="This secure proposal link is unavailable."
            description="The link may have expired or is no longer valid. Ask your Advertified contact to send a fresh proposal link."
          />
        </section>
      );
    }

    function buildSelectedProposalFeedback(noteBody: string) {
      const selectedLabel = recommendation?.proposalLabel ?? recommendation?.id ?? 'Selected proposal';
      return `Client selected proposal for revision: ${selectedLabel}\nClient notes: ${noteBody.trim()}`;
    }

    function buildRejectAllFeedback(noteBody: string) {
      return `Client rejected all proposals.\nReason: ${noteBody.trim()}`;
    }

    async function handleDownloadRecommendationPdf() {
      await advertifiedApi.downloadPublicFile(
        `/public/proposals/${encodeURIComponent(id)}/recommendation-pdf?token=${encodeURIComponent(token)}`,
        `recommendation-${id}.pdf`,
      );
    }

    async function handlePrimaryAction() {
      if (paymentRequiredBeforeApproval) {
        if (recommendation?.id) {
          try {
            await prepareCheckoutMutation.mutateAsync(recommendation.id);
          } catch {
            return;
          }
        }

        if (!isAuthenticated && loginCheckoutPath) {
          navigate(loginCheckoutPath);
          return;
        }

        if (checkoutPath) {
          navigate(checkoutPath);
        }

        return;
      }

      approveMutation.mutate(recommendation?.id);
    }

    return (
      <section className="page-shell space-y-8 pb-20">
        <PageHero
          kicker="Proposal review"
          title={offerHeadline || publicProposalQuery.data.campaignName}
          description={approvalAlreadyCompleted
            ? 'This proposal has already been approved. You can still reopen the proposal page from any email link.'
            : paymentAwaitingReview
              ? 'Your proposal is saved. Finance Partner is reviewing your Pay Later application, so there is nothing else you need to do right now.'
              : 'Review the offer first, then choose the route that feels right for your business and timing.'}
        />

        {recommendations.length > 1 ? (
          <div className="rounded-[18px] border border-line bg-slate-50/70 p-5">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Offer options</p>
            <p className="mt-2 text-sm text-ink-soft">Select the route that feels closest to your business need. You can still request changes or ask for a lighter starting point.</p>
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
                    <p className="mt-2 text-sm font-semibold text-ink">Recommended investment {formatCurrency(proposal.totalCost)}</p>
                  </button>
                );
              })}
            </div>
          </div>
        ) : null}

        {showRejectAllFlow ? (
          <div className="rounded-[16px] border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
            Share why these proposals do not work for you, then click <strong>Reject all and request new set</strong>. Your agent will prepare new options from your notes.
          </div>
        ) : null}

        <div className="grid gap-5 xl:grid-cols-[minmax(0,0.95fr)_minmax(340px,420px)]">
          <div className="rounded-[22px] border border-line bg-white p-6 shadow-[0_12px_36px_rgba(15,23,42,0.04)]">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Offer overview</p>
            <div className="mt-4 grid gap-4 sm:grid-cols-2">
              <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Offer headline</p>
                <p className="mt-2 text-base font-semibold text-ink">{offerHeadline}</p>
                <p className="mt-1 text-sm text-ink-soft">Built around your current campaign route and business context.</p>
              </div>
              <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Selected route</p>
                <p className="mt-2 text-base font-semibold text-ink">{recommendation?.proposalLabel ?? 'Current proposal'}</p>
                <p className="mt-1 text-sm text-ink-soft">{recommendation ? `Recommended investment ${formatCurrency(recommendation.totalCost)}` : 'Preparing details'}</p>
              </div>
              <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Why this fits</p>
                <div className="mt-2 space-y-2 text-sm text-ink-soft">
                  {fitBullets.map((reason) => (
                    <p key={reason}>- {reason}</p>
                  ))}
                </div>
              </div>
              <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">What is included</p>
                <div className="mt-2 space-y-2 text-sm text-ink-soft">
                  {includedItems.map((item) => (
                    <p key={item}>- {item}</p>
                  ))}
                </div>
              </div>
            </div>

            <div className="mt-5 rounded-[18px] border border-brand/15 bg-brand-soft/25 px-5 py-5">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Choose the way in</p>
              <p className="mt-2 text-2xl font-semibold tracking-tight text-ink">You do not have to say yes or no to one big number.</p>
              <p className="mt-2 text-sm leading-7 text-ink-soft">
                If the full route feels too high right now, you do not need to reject the whole idea. Choose the strongest route, start leaner, or phase the rollout around what is workable for your business.
              </p>
              <div className="mt-4 grid gap-3 md:grid-cols-3">
                {flexibleRoutes.map((route) => {
                  const Icon = route.icon;
                  return (
                    <div key={route.title} className={`rounded-[18px] border px-4 py-4 ${route.toneClassName}`}>
                      <div className="flex items-start justify-between gap-3">
                        <div className={`inline-flex rounded-full border px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.16em] ${route.badgeClassName}`}>
                          {route.badge}
                        </div>
                        <div className="inline-flex rounded-full border border-brand/15 bg-white px-3 py-2 text-brand">
                          <Icon className="size-4" />
                        </div>
                      </div>
                      <p className="mt-4 text-sm font-semibold text-ink">{route.title}</p>
                      <p className="mt-1 text-lg font-semibold text-ink">{route.amountLabel}</p>
                      <p className="mt-2 text-sm leading-6 text-ink-soft">{route.description}</p>
                      <p className="mt-3 text-xs font-medium uppercase tracking-[0.14em] text-ink-soft">{route.supportCopy}</p>
                    </div>
                  );
                })}
              </div>
            </div>

            <div className="mt-5 rounded-[18px] border border-amber-200 bg-amber-50 px-5 py-4 text-sm text-amber-900">
              If affordability is the issue, ask for a leaner start or phased rollout instead of rejecting the offer. We can rework the footprint around what is realistic for you.
            </div>
          </div>

          <div className="rounded-[22px] border border-brand/20 bg-[linear-gradient(180deg,#f7fcfa_0%,#ffffff_100%)] p-6 shadow-[0_18px_44px_rgba(15,118,110,0.08)]">
            <div className="text-lg font-semibold text-ink">{approvalAlreadyCompleted ? 'Offer approved' : 'Choose your next step'}</div>
            <p className="mt-2 text-sm leading-7 text-ink-soft">
              {approvalAlreadyCompleted
                ? 'The offer has already been approved. The actions below stay visible so every proposal email still opens the same review layout.'
                : 'Accept the route, ask for changes, or use the notes box to request a lighter entry point or phased rollout.'}
            </p>
            {approvalAlreadyCompleted ? (
              <div className="mt-4 rounded-[14px] border border-brand/20 bg-brand/[0.06] px-4 py-3 text-sm text-ink">
                This offer has already been accepted. You can still review it here from any proposal email link.
              </div>
            ) : paymentAwaitingReview ? (
              <div className="mt-4 rounded-[14px] border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900">
                Your Finance Partner application has already been submitted. There is nothing else you need to approve or pay while this review is pending.
              </div>
            ) : paymentRequiredBeforeApproval ? (
              <div className="mt-4 rounded-[14px] border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                Payment is still required before this route can be finally approved.
              </div>
            ) : null}
            <div className="mt-5 space-y-3">
              <div className="rounded-[14px] border border-line bg-white/70 px-4 py-3 text-sm text-ink-soft">
                Best response style: tell us whether you want the <strong>full route</strong>, a <strong>leaner start</strong>, or a <strong>phased rollout</strong>. That helps us rework the offer quickly.
              </div>
              <label className="block text-sm font-semibold text-ink" htmlFor="proposal-review-notes">
                Notes or adjustment request
              </label>
              <textarea
                id="proposal-review-notes"
                value={changeNotes}
                onChange={(event) => setChangeNotes(event.target.value)}
                className="input-base min-h-[110px]"
                placeholder="Example: We like this route, but need a lighter start, fewer placements, or a phased rollout."
                disabled={approvalAlreadyCompleted}
              />
              <div className="grid gap-3">
                <button
                  type="button"
                  onClick={() => void handlePrimaryAction()}
                  disabled={approvalAlreadyCompleted || paymentAwaitingReview || approveMutation.isPending || prepareCheckoutMutation.isPending || requestChangesMutation.isPending || rejectAllMutation.isPending || (!checkoutPath && paymentRequiredBeforeApproval)}
                  className={`w-full justify-center text-center whitespace-normal px-4 py-3 text-sm ${
                    approvalAlreadyCompleted
                      ? 'user-btn-secondary border-line bg-slate-100 text-ink-soft opacity-100'
                      : paymentAwaitingReview
                      ? 'user-btn-secondary border-sky-200 bg-sky-50 text-sky-900 opacity-100'
                      : paymentRequiredBeforeApproval
                      ? 'user-btn-secondary border-amber-200 bg-amber-50 text-amber-900 opacity-100'
                      : 'user-btn-primary'
                  } disabled:cursor-not-allowed disabled:opacity-100`}
                >
                  {approvalAlreadyCompleted
                    ? 'Proposal already approved'
                    : paymentAwaitingReview
                    ? 'Awaiting Pay Later review'
                    : paymentRequiredBeforeApproval
                    ? (prepareCheckoutMutation.isPending
                      ? 'Preparing payment...'
                      : (isAuthenticated ? 'Continue with this offer' : 'Sign in to continue'))
                    : (approveMutation.isPending ? 'Accepting...' : 'Accept this offer')}
                </button>
                <button
                  type="button"
                  onClick={() => requestChangesMutation.mutate(buildSelectedProposalFeedback(changeNotes))}
                  disabled={approvalAlreadyCompleted || approveMutation.isPending || requestChangesMutation.isPending || rejectAllMutation.isPending || !changeNotes.trim()}
                  className="user-btn-secondary w-full justify-center text-center whitespace-normal px-4 py-3 text-sm disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {requestChangesMutation.isPending ? 'Sending...' : 'Request adjustment'}
                </button>
                <button
                  type="button"
                  onClick={() => rejectAllMutation.mutate(buildRejectAllFeedback(changeNotes))}
                  disabled={approvalAlreadyCompleted || approveMutation.isPending || requestChangesMutation.isPending || rejectAllMutation.isPending || !changeNotes.trim()}
                  className="user-btn-secondary w-full justify-center text-center whitespace-normal px-4 py-3 text-sm disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {rejectAllMutation.isPending ? 'Sending...' : 'Reject this set'}
                </button>
              </div>
            </div>
          </div>
        </div>

        {recommendation ? (
          <details className="rounded-[22px] border border-line bg-white p-5 shadow-[0_12px_36px_rgba(15,23,42,0.04)]">
            <summary className="cursor-pointer list-none text-sm font-semibold text-ink">
              View full proposal details
            </summary>
            <div className="mt-5">
              <RecommendationViewer
                recommendation={recommendation}
                recommendationPdfUrl={`/public/proposals/${id}/recommendation-pdf?token=${encodeURIComponent(token)}`}
                onDownloadPdf={() => handleDownloadRecommendationPdf()}
                opportunityContext={opportunityContext}
              />
            </div>
          </details>
        ) : (
          <div className="rounded-[18px] border border-line bg-slate-50/70 p-5 text-sm leading-7 text-ink-soft">
            Your offer is still being prepared.
          </div>
        )}
      </section>
    );
  }

  const safeNextPath = getSafeNextPath(approvalsPath) ?? `/campaigns/${encodeURIComponent(id)}/approvals`;

  if (isAuthenticated) {
    return <Navigate to={safeNextPath} replace />;
  }

  const loginPath = `/login?next=${encodeURIComponent(safeNextPath)}`;
  const registerPath = `/register?next=${encodeURIComponent(safeNextPath)}`;

  return (
    <section className="page-shell space-y-8 pb-20">
      <PageHero
        kicker="Proposal review"
        title="Your Advertified proposal is ready."
        description="If you already have an Advertified account, sign in to open this proposal. If you are new, create your account once and continue from the same step."
      />

      <div className="mx-auto grid max-w-4xl gap-6 lg:grid-cols-[1.15fr_0.85fr]">
        <div className="rounded-[24px] border border-line bg-white p-6 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
          <div className="inline-flex items-center gap-2 rounded-full border border-brand/20 bg-brand-soft px-3 py-1 text-xs font-semibold uppercase tracking-[0.14em] text-brand">
            <FileText className="size-4" />
            Proposal access
          </div>
          <p className="mt-4 text-sm leading-7 text-ink-soft">
            This secure link opens your campaign proposal workspace where you can review Proposal A/B/C, accept one option, or request changes.
          </p>
          <div className="mt-4 rounded-[14px] border border-brand/20 bg-brand/[0.06] px-4 py-3 text-sm text-ink">
            Existing clients should sign in with the same email used before. Only create an account if this is your first time using Advertified.
          </div>
          {action === 'reject_all' ? (
            <div className="mt-4 rounded-[14px] border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
              We&apos;ll take you straight to the feedback form so you can request a new proposal set.
            </div>
          ) : null}
          {recommendationId ? (
            <div className="mt-4 rounded-[14px] border border-brand/20 bg-brand/[0.06] px-4 py-3 text-sm text-ink">
              A specific proposal was preselected from your email or PDF link.
            </div>
          ) : null}
        </div>

        <aside className="rounded-[24px] border border-line bg-white p-6 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
          <p className="text-sm font-semibold text-ink">Continue securely</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">
            Sign in if you already have an Advertified account. Create an account only if this is your first proposal with us.
          </p>
          <div className="mt-4 space-y-3">
            <Link to={loginPath} className="button-primary flex w-full items-center justify-center gap-2 px-5 py-3">
              Sign in to review
              <ArrowRight className="size-4" />
            </Link>
            <Link to={registerPath} className="button-secondary flex w-full items-center justify-center gap-2 px-5 py-3">
              New here? Create account
            </Link>
          </div>
        </aside>
      </div>
    </section>
  );
}

import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowRight, FileText } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageHero } from '../../components/marketing/PageHero';
import { RecommendationViewer } from '../../features/campaigns/components/RecommendationViewer';
import { getCampaignRecommendations, selectRecommendation } from '../../features/campaigns/recommendationSelection';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { hasRecommendationApprovalCompleted } from '../../lib/campaignStatus';
import { isPaymentAwaitingManualReview } from '../../lib/access';
import { formatCurrency, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';

function formatPaymentStatusLabel(status?: string) {
  if (!status) {
    return 'Pending';
  }

  return titleCase(status.replace(/_/g, ' '));
}

function getSafeNextPath(raw: string | null) {
  if (!raw) {
    return null;
  }

  const trimmed = raw.trim();
  return trimmed.startsWith('/') ? trimmed : null;
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
  const showRejectAllFlow = action === 'reject_all';
  const recommendation = selectRecommendation(recommendations, {
    currentSelectionId: selectedRecommendationId,
    requestedRecommendationId: recommendationId,
  });
  const paymentAwaitingReview = publicProposalQuery.data
    ? isPaymentAwaitingManualReview(publicProposalQuery.data.paymentProvider, publicProposalQuery.data.paymentStatus)
    : false;
  const paymentRequiredBeforeApproval = Boolean(
    publicProposalQuery.data?.paymentStatus !== 'paid' && !paymentAwaitingReview,
  );
  const approvalAlreadyCompleted = hasRecommendationApprovalCompleted(publicProposalQuery.data?.status, recommendation);
  const currentProposalPath = `${location.pathname}${location.search}`;
  const checkoutPath = publicProposalQuery.data && recommendation
    ? `/checkout/payment?orderId=${encodeURIComponent(publicProposalQuery.data.packageOrderId)}&campaignId=${encodeURIComponent(publicProposalQuery.data.id)}&recommendationId=${encodeURIComponent(recommendation.id)}&proposalPath=${encodeURIComponent(currentProposalPath)}`
    : null;
  const registerCheckoutPath = checkoutPath
    ? `/register?next=${encodeURIComponent(checkoutPath)}`
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
    mutationFn: (notes: string) => advertifiedApi.requestPublicProposalChanges(id, token, notes),
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

  const approvalsPath = useMemo(() => {
    const queryParams = new URLSearchParams();
    if (recommendationId) {
      queryParams.set('recommendationId', recommendationId);
    }
    if (action) {
      queryParams.set('action', action);
    }
    const query = queryParams.toString();
    return query
      ? `/campaigns/${encodeURIComponent(id)}/approvals?${query}`
      : `/campaigns/${encodeURIComponent(id)}/approvals`;
  }, [action, id, recommendationId]);

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

        if (!isAuthenticated && registerCheckoutPath) {
          navigate(registerCheckoutPath);
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
          title={publicProposalQuery.data.campaignName}
          description={approvalAlreadyCompleted
            ? 'This proposal has already been approved. You can still reopen the proposal page from any email link.'
            : paymentAwaitingReview
              ? 'Your proposal is saved. Finance Partner is reviewing your Pay Later application, so there is nothing else you need to do right now.'
              : 'Review the options and tell us how you want to proceed.'}
        />

        {recommendations.length > 1 ? (
          <div className="rounded-[18px] border border-line bg-slate-50/70 p-5">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">Proposal options</p>
            <p className="mt-2 text-sm text-ink-soft">Select the proposal you want to approve or revise. You can also reject all with comments.</p>
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
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">What you are reviewing</p>
            <div className="mt-4 grid gap-4 sm:grid-cols-2">
              <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Campaign</p>
                <p className="mt-2 text-base font-semibold text-ink">{publicProposalQuery.data.packageBandName}</p>
                <p className="mt-1 text-sm text-ink-soft">{publicProposalQuery.data.campaignName}</p>
              </div>
              <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Selected option</p>
                <p className="mt-2 text-base font-semibold text-ink">{recommendation?.proposalLabel ?? 'Current proposal'}</p>
                <p className="mt-1 text-sm text-ink-soft">{recommendation ? formatCurrency(recommendation.totalCost) : 'Preparing details'}</p>
              </div>
              <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Options prepared</p>
                <p className="mt-2 text-base font-semibold text-ink">{recommendations.length || 0}</p>
                <p className="mt-1 text-sm text-ink-soft">You can switch between proposals above.</p>
              </div>
              <div className="rounded-[18px] border border-line bg-slate-50/80 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Payment status</p>
                <p className="mt-2 text-base font-semibold text-ink">
                  {paymentAwaitingReview ? 'Pay Later under review' : formatPaymentStatusLabel(publicProposalQuery.data.paymentStatus)}
                </p>
                <p className="mt-1 text-sm text-ink-soft">
                  {approvalAlreadyCompleted
                    ? 'Approval is complete and Advertified is moving this campaign forward.'
                    : paymentAwaitingReview
                      ? 'Your Finance Partner application is under review. We will bring you back here when the next client action is available.'
                    : paymentRequiredBeforeApproval
                      ? 'Payment must be completed before final approval.'
                      : 'This proposal can be approved now.'}
                </p>
              </div>
            </div>
          </div>

          <div className="rounded-[22px] border border-brand/20 bg-[linear-gradient(180deg,#f7fcfa_0%,#ffffff_100%)] p-6 shadow-[0_18px_44px_rgba(15,118,110,0.08)]">
            <div className="text-lg font-semibold text-ink">{approvalAlreadyCompleted ? 'Proposal approved' : 'Choose your next step'}</div>
            <p className="mt-2 text-sm leading-7 text-ink-soft">
              {approvalAlreadyCompleted
                ? 'The recommendation has already been approved. The actions below stay visible so every proposal email still opens the same review layout.'
                : 'Pick one action below. If you want changes or a new set, add a short note first.'}
            </p>
            {approvalAlreadyCompleted ? (
              <div className="mt-4 rounded-[14px] border border-brand/20 bg-brand/[0.06] px-4 py-3 text-sm text-ink">
                This proposal has already been accepted. You can still review it here from any proposal email link.
              </div>
            ) : paymentAwaitingReview ? (
              <div className="mt-4 rounded-[14px] border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900">
                Your Finance Partner application has already been submitted. There is nothing else you need to approve or pay while this review is pending.
              </div>
            ) : paymentRequiredBeforeApproval ? (
              <div className="mt-4 rounded-[14px] border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                Payment is still required before this recommendation can be finally approved.
              </div>
            ) : null}
            <div className="mt-5 space-y-3">
              <label className="block text-sm font-semibold text-ink" htmlFor="proposal-review-notes">
                Notes
              </label>
              <textarea
                id="proposal-review-notes"
                value={changeNotes}
                onChange={(event) => setChangeNotes(event.target.value)}
                className="input-base min-h-[110px]"
                placeholder="Add feedback if you want changes or want to reject all proposals."
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
                      : (isAuthenticated ? 'Pay and approve selected' : 'Create account and pay to approve'))
                    : (approveMutation.isPending ? 'Accepting...' : 'Approve selected')}
                </button>
                <button
                  type="button"
                  onClick={() => requestChangesMutation.mutate(buildSelectedProposalFeedback(changeNotes))}
                  disabled={approvalAlreadyCompleted || approveMutation.isPending || requestChangesMutation.isPending || rejectAllMutation.isPending || !changeNotes.trim()}
                  className="user-btn-secondary w-full justify-center text-center whitespace-normal px-4 py-3 text-sm disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {requestChangesMutation.isPending ? 'Sending...' : 'Request changes'}
                </button>
                <button
                  type="button"
                  onClick={() => rejectAllMutation.mutate(buildRejectAllFeedback(changeNotes))}
                  disabled={approvalAlreadyCompleted || approveMutation.isPending || requestChangesMutation.isPending || rejectAllMutation.isPending || !changeNotes.trim()}
                  className="user-btn-secondary w-full justify-center text-center whitespace-normal px-4 py-3 text-sm disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {rejectAllMutation.isPending ? 'Sending...' : 'Reject all'}
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
              />
            </div>
          </details>
        ) : (
          <div className="rounded-[18px] border border-line bg-slate-50/70 p-5 text-sm leading-7 text-ink-soft">
            Your recommendation is still being prepared.
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
        description="Sign in or activate your account to open this proposal and continue to approval and payment."
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
          <div className="mt-4 space-y-3">
            <Link to={loginPath} className="button-primary flex w-full items-center justify-center gap-2 px-5 py-3">
              Sign in to review
              <ArrowRight className="size-4" />
            </Link>
            <Link to={registerPath} className="button-secondary flex w-full items-center justify-center gap-2 px-5 py-3">
              Activate or create account
            </Link>
          </div>
        </aside>
      </div>
    </section>
  );
}

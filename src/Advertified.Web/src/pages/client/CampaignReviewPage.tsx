import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, RefreshCcw } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { CampaignTimeline } from '../../features/campaigns/components/CampaignTimeline';
import { RecommendationViewer } from '../../features/campaigns/components/RecommendationViewer';
import { UpsellPanel } from '../../features/campaigns/components/UpsellPanel';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import { formatCurrency } from '../../lib/utils';

export function CampaignReviewPage() {
  const { id = '' } = useParams();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const [changeNotes, setChangeNotes] = useState('');
  const [selectedRecommendationId, setSelectedRecommendationId] = useState<string>('');
  const campaignQuery = useQuery({ queryKey: ['campaign', id], queryFn: () => advertifiedApi.getCampaign(id) });
  const recommendations = campaignQuery.data
    ? (campaignQuery.data.recommendations.length > 0
      ? campaignQuery.data.recommendations
      : (campaignQuery.data.recommendation ? [campaignQuery.data.recommendation] : []))
    : [];

  useEffect(() => {
    if (!selectedRecommendationId && recommendations[0]?.id) {
      setSelectedRecommendationId(recommendations[0].id);
    }
  }, [recommendations, selectedRecommendationId]);

  const approveMutation = useMutation({
    mutationFn: (recommendationId?: string) => advertifiedApi.approveRecommendation(id, recommendationId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['campaign', id] });
      pushToast({
        title: 'Recommendation approved.',
        description: 'Your campaign can now move into the next activation step.',
      });
    },
  });
  const requestChangesMutation = useMutation({
    mutationFn: () => advertifiedApi.requestRecommendationChanges(id, changeNotes),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['campaign', id] });
      setChangeNotes('');
      pushToast({
        title: 'Changes requested.',
        description: 'Your agent has been asked to revise this recommendation.',
      });
    },
  });

  if (campaignQuery.isLoading || !campaignQuery.data) {
    return <LoadingState label="Loading recommendation review..." />;
  }

  const campaign = campaignQuery.data;
  const isProcessing = approveMutation.isPending || requestChangesMutation.isPending;
  const recommendation = recommendations.find((item) => item.id === selectedRecommendationId) ?? recommendations[0];
  const isApproved = recommendation?.status === 'approved' || campaign.status === 'approved';
  const withinBudget = (recommendation?.totalCost ?? 0) <= campaign.selectedBudget;
  const explainabilityCount = recommendation?.items.reduce((count, item) => count + item.selectionReasons.length, 0) ?? 0;

  return (
    <section className="page-shell space-y-8">
      {isProcessing ? <ProcessingOverlay label={approveMutation.isPending ? 'Approving this recommendation...' : 'Sending your change request...'} /> : null}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="max-w-3xl">
          <div className="pill bg-brand-soft text-brand">Review</div>
          <h1 className="mt-4 text-4xl font-semibold tracking-tight text-ink">Review your tailored recommendation with confidence.</h1>
        </div>
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            disabled={isProcessing || isApproved}
            onClick={() => approveMutation.mutate(recommendation?.id)}
            className="inline-flex items-center gap-2 rounded-full bg-emerald-600 px-5 py-3 text-sm font-semibold text-white disabled:opacity-60"
          >
            <CheckCircle2 className="size-4" />
            {isApproved ? 'Recommendation approved' : 'Approve recommendation'}
          </button>
          <button
            type="button"
            disabled={isProcessing}
            onClick={() => requestChangesMutation.mutate()}
            className="inline-flex items-center gap-2 rounded-full bg-ink px-5 py-3 text-sm font-semibold text-white disabled:opacity-60"
          >
            <RefreshCcw className="size-4" />
            Request changes
          </button>
        </div>
      </div>

      <CampaignTimeline steps={campaign.timeline} />

      {recommendation ? (
        <div className="grid gap-4 lg:grid-cols-4">
          <div className="panel px-5 py-5">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Selected budget</p>
            <p className="mt-3 text-xl font-semibold text-ink">{formatCurrency(campaign.selectedBudget)}</p>
          </div>
          <div className="panel px-5 py-5">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Recommendation total</p>
            <p className="mt-3 text-xl font-semibold text-ink">{formatCurrency(recommendation.totalCost)}</p>
          </div>
          <div className="panel px-5 py-5">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Budget fit</p>
            <p className={`mt-3 text-xl font-semibold ${withinBudget ? 'text-emerald-700' : 'text-rose-700'}`}>
              {withinBudget ? 'Within budget' : 'Over budget'}
            </p>
          </div>
          <div className="panel px-5 py-5">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Explainability</p>
            <p className="mt-3 text-xl font-semibold text-ink">{explainabilityCount} reason tag(s)</p>
          </div>
        </div>
      ) : null}

      {recommendations.length > 1 ? (
        <div className="panel px-6 py-6">
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Proposal options</p>
          <div className="mt-4 flex flex-wrap gap-3">
            {recommendations.map((option) => {
              const isActive = option.id === recommendation?.id;
              return (
                <button
                  key={option.id}
                  type="button"
                  onClick={() => setSelectedRecommendationId(option.id)}
                  className={`rounded-[18px] border px-4 py-3 text-left transition ${
                    isActive ? 'border-brand bg-brand-soft' : 'border-line bg-slate-50 hover:border-brand/30'
                  }`}
                >
                  <p className="text-sm font-semibold text-ink">{option.proposalLabel ?? 'Proposal'}</p>
                  <p className="mt-1 text-sm text-ink-soft">{option.proposalStrategy ?? 'Recommendation option'}</p>
                  <p className="mt-2 text-xs uppercase tracking-[0.14em] text-ink-soft">{option.items.length} line item(s)</p>
                </button>
              );
            })}
          </div>
        </div>
      ) : null}

      {recommendation ? (
        <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
          <RecommendationViewer recommendation={recommendation} recommendationPdfUrl={campaign.recommendationPdfUrl} />
          <div className="space-y-6">
            <div className="panel space-y-4 px-6 py-6">
              <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Approval confidence</p>
              <div className="space-y-3 text-sm text-ink-soft">
                <div className="flex items-start gap-3 rounded-[16px] border border-line bg-slate-50 px-4 py-3">
                  <CheckCircle2 className={`mt-0.5 size-4 ${withinBudget ? 'text-emerald-600' : 'text-amber-600'}`} />
                  <div>
                    <p className="font-semibold text-ink">{withinBudget ? 'Within your selected budget' : 'Needs budget review'}</p>
                    <p className="mt-1 leading-6">
                      {withinBudget
                        ? 'The current recommendation total fits within what you selected and paid for.'
                        : 'This recommendation needs agent review before approval because it exceeds the selected budget.'}
                    </p>
                  </div>
                </div>
                <div className="flex items-start gap-3 rounded-[16px] border border-line bg-slate-50 px-4 py-3">
                  <CheckCircle2 className={`mt-0.5 size-4 ${recommendation.manualReviewRequired ? 'text-amber-600' : 'text-emerald-600'}`} />
                  <div>
                    <p className="font-semibold text-ink">
                      {recommendation.manualReviewRequired ? 'Agent review still important' : 'Recommendation checks passed'}
                    </p>
                    <p className="mt-1 leading-6">
                      {recommendation.manualReviewRequired
                        ? 'This plan contains caution flags, so please review the details carefully before approving.'
                        : 'This recommendation passed the current planning checks for package fit and inventory selection.'}
                    </p>
                  </div>
                </div>
              </div>
            </div>
            <div className="panel space-y-4 px-6 py-6">
              <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Client decision</p>
              <p className="text-sm leading-7 text-ink-soft">
                Approve this proposal if you are happy to move forward, or send the full recommendation set back to your agent with change notes.
              </p>
              <label className="grid gap-2">
                <span className="text-sm font-medium text-ink">Change notes</span>
                <textarea
                  value={changeNotes}
                  onChange={(event) => setChangeNotes(event.target.value)}
                  className="input-base min-h-32 resize-y"
                  placeholder="Tell your agent what needs to change, for example audience mix, location focus, or spend split."
                />
              </label>
            </div>
            <UpsellPanel recommendation={recommendation} />
          </div>
        </div>
      ) : (
        <LoadingState label="Recommendation is being prepared..." />
      )}
    </section>
  );
}

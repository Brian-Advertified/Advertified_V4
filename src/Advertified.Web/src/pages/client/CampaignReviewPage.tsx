import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, RefreshCcw } from 'lucide-react';
import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { RecommendationViewer } from '../../features/campaigns/components/RecommendationViewer';
import { UpsellPanel } from '../../features/campaigns/components/UpsellPanel';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';

export function CampaignReviewPage() {
  const { id = '' } = useParams();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const [changeNotes, setChangeNotes] = useState('');
  const campaignQuery = useQuery({ queryKey: ['campaign', id], queryFn: () => advertifiedApi.getCampaign(id) });
  const approveMutation = useMutation({
    mutationFn: () => advertifiedApi.approveRecommendation(id),
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
  const isApproved = campaign.recommendation?.status === 'approved' || campaign.status === 'approved';

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
            onClick={() => approveMutation.mutate()}
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

      {campaign.recommendation ? (
        <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
          <RecommendationViewer recommendation={campaign.recommendation} />
          <div className="space-y-6">
            <div className="panel space-y-4 px-6 py-6">
              <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Client decision</p>
              <p className="text-sm leading-7 text-ink-soft">
                Approve this recommendation if you are happy to move forward, or send it back to your agent with change notes.
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
            <UpsellPanel recommendation={campaign.recommendation} />
          </div>
        </div>
      ) : (
        <LoadingState label="Recommendation is being prepared..." />
      )}
    </section>
  );
}

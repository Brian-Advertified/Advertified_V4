import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { LockKeyhole } from 'lucide-react';
import { useParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { PlanningModeSelector } from '../../features/campaigns/components/PlanningModeSelector';
import { RecommendationViewer } from '../../features/campaigns/components/RecommendationViewer';
import { UpsellPanel } from '../../features/campaigns/components/UpsellPanel';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { PlanningMode } from '../../types/domain';

export function CampaignPlanningPage() {
  const { id = '' } = useParams();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const campaignQuery = useQuery({ queryKey: ['campaign', id], queryFn: () => advertifiedApi.getCampaign(id) });
  const planningMutation = useMutation({
    mutationFn: (mode: PlanningMode) => advertifiedApi.setPlanningMode(id, mode),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['campaign', id] });
      pushToast({
        title: 'Planning mode updated.',
        description: 'Your campaign will now follow this planning path.',
      });
    },
    onError: (error) => pushToast({
      title: 'We could not update the planning mode.',
      description: error instanceof Error ? error.message : 'Please try again in a moment.',
    }, 'error'),
  });

  if (campaignQuery.isLoading || !campaignQuery.data) {
    return <LoadingState label="Loading planning workspace..." />;
  }

  const campaign = campaignQuery.data;

  if (!campaign.aiUnlocked) {
    return (
      <section className="page-shell">
        <div className="panel flex min-h-[320px] flex-col items-center justify-center gap-4 px-8 py-12 text-center">
          <div className="rounded-3xl bg-brand-soft p-4 text-brand"><LockKeyhole className="size-8" /></div>
          <h1 className="text-3xl font-semibold tracking-tight text-ink">Planning is still locked.</h1>
          <p className="max-w-xl text-sm leading-7 text-ink-soft">Pay for the package and submit the campaign brief before AI planning or agent assistance becomes available.</p>
        </div>
      </section>
    );
  }

  return (
    <section className="page-shell space-y-8">
      {planningMutation.isPending ? <ProcessingOverlay label="Updating your planning mode..." /> : null}
      <div className="max-w-3xl">
        <div className="pill bg-highlight-soft text-highlight">Planning</div>
        <h1 className="mt-4 text-4xl font-semibold tracking-tight text-ink">Unlock the right planning mode for this campaign.</h1>
      </div>

      <PlanningModeSelector value={campaign.planningMode} onChange={(mode) => planningMutation.mutate(mode)} />

      {campaign.recommendation ? (
        <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
          <RecommendationViewer recommendation={campaign.recommendation} />
          <div className="space-y-6">
            <UpsellPanel recommendation={campaign.recommendation} />
            <div className="panel px-6 py-6">
              <p className="text-sm font-semibold text-ink">Status timeline</p>
              <div className="mt-5 space-y-4 text-sm text-ink-soft">
                <div>1. Package paid</div>
                <div>2. Brief submitted</div>
                <div>3. Planning mode selected</div>
                <div>4. Recommendation under review</div>
              </div>
            </div>
          </div>
        </div>
      ) : (
        <EmptyState title="No recommendation yet" description="Submit the brief to generate the first recommendation for this package." />
      )}
    </section>
  );
}

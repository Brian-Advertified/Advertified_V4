import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { CampaignBriefForm } from '../../features/campaigns/components/CampaignBriefForm';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { CampaignBrief } from '../../types/domain';

export function CampaignBriefPage() {
  const { id = '' } = useParams();
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const campaignQuery = useQuery({ queryKey: ['campaign', id], queryFn: () => advertifiedApi.getCampaign(id) });
  const saveMutation = useMutation({
    mutationFn: (brief: CampaignBrief) => advertifiedApi.saveCampaignBrief(id, brief),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['campaign', id] });
      pushToast({
        title: 'Draft saved.',
        description: 'Your campaign brief has been saved and you can keep editing it.',
      });
    },
    onError: (error) => pushToast({
      title: 'We could not save your draft.',
      description: error instanceof Error ? error.message : 'Please try again in a moment.',
    }, 'error'),
  });
  const submitMutation = useMutation({
    mutationFn: async (brief: CampaignBrief) => {
      await advertifiedApi.saveCampaignBrief(id, brief);
      return advertifiedApi.submitCampaignBrief(id);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['campaign', id] });
      pushToast({
        title: 'Brief submitted.',
        description: 'Planning is now unlocked for this campaign.',
      });
    },
    onError: (error) => pushToast({
      title: 'We could not submit your brief.',
      description: error instanceof Error ? error.message : 'Please try again in a moment.',
    }, 'error'),
  });

  if (campaignQuery.isLoading || !campaignQuery.data) {
    return <LoadingState label="Loading campaign brief..." />;
  }

  return (
    <section className="page-shell space-y-8">
      {saveMutation.isPending || submitMutation.isPending ? (
        <ProcessingOverlay label={submitMutation.isPending ? 'Submitting your campaign brief...' : 'Saving your campaign brief...'} />
      ) : null}
      <div className="max-w-3xl">
        <div className="pill bg-brand-soft text-brand">Campaign brief</div>
        <h1 className="mt-4 text-4xl font-semibold tracking-tight text-ink sm:text-5xl">Tell us about your campaign in a guided, commercial way.</h1>
        <p className="section-copy mt-4">
          Grouped sections keep the brief clear and useful without overwhelming the client with raw media planning complexity.
        </p>
      </div>
      <CampaignBriefForm
        initialValue={campaignQuery.data.brief}
        loading={saveMutation.isPending || submitMutation.isPending}
        onSave={async (brief) => {
          await saveMutation.mutateAsync(brief);
        }}
        onSubmitBrief={async (brief) => {
          await submitMutation.mutateAsync(brief);
        }}
      />
    </section>
  );
}

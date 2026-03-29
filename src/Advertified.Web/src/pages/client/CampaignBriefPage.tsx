import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, CheckCircle2, FileText, Sparkles } from 'lucide-react';
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { CampaignTimeline } from '../../features/campaigns/components/CampaignTimeline';
import { CampaignBriefForm } from '../../features/campaigns/components/CampaignBriefForm';
import { useAuth } from '../../features/auth/auth-context';
import { useToast } from '../../components/ui/toast';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { CampaignBrief } from '../../types/domain';

export function CampaignBriefPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();
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
        description: 'Your brief is complete. Next, choose how Advertified should plan your recommendation.',
      });
      navigate(`/campaigns/${id}/planning`);
    },
    onError: (error) => pushToast({
      title: 'We could not submit your brief.',
      description: error instanceof Error ? error.message : 'Please try again in a moment.',
    }, 'error'),
  });

  if (campaignQuery.isLoading || !campaignQuery.data) {
    return <LoadingState label="Loading campaign brief..." />;
  }

  const campaign = campaignQuery.data;
  if (user?.role === 'agent' || user?.role === 'admin') {
    return <Navigate to={`/agent/recommendations/new?campaignId=${campaign.id}`} replace />;
  }

  const isSubmitted = campaign.status !== 'paid' && campaign.status !== 'brief_in_progress';

  return (
    <section className="page-shell space-y-8">
      {saveMutation.isPending || submitMutation.isPending ? (
        <ProcessingOverlay label={submitMutation.isPending ? 'Submitting your brief and unlocking planning...' : 'Saving your campaign brief...'} />
      ) : null}

      <div className="panel hero-glow px-6 py-8 text-white sm:px-8">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-3xl">
            <div className="pill border-white/10 bg-white/10 text-white/75">Campaign brief</div>
            <h1 className="mt-4 text-4xl font-semibold tracking-tight sm:text-5xl">Complete your brief so we can start planning your recommendation.</h1>
            <p className="mt-4 text-base leading-8 text-white/75">
              This page is the bridge between purchase and planning. Tell us about your objective, geography, audience, and channel preferences, then submit the brief to unlock the planning path for this campaign.
            </p>
          </div>
          <div className="rounded-[28px] border border-white/10 bg-white/10 px-5 py-5 text-sm text-white/85">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-white/60">Selected budget</p>
            <p className="mt-3 text-2xl font-semibold text-white">{formatCurrency(campaign.selectedBudget)}</p>
            <p className="mt-3 text-sm leading-7 text-white/70">
              Package: {campaign.packageBandName}
            </p>
          </div>
        </div>
      </div>

      <CampaignTimeline steps={campaign.timeline} />

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="panel px-6 py-6">
          <div className="flex items-center gap-3">
            <div className="rounded-2xl bg-brand-soft p-3 text-brand"><FileText className="size-5" /></div>
            <div>
              <p className="text-sm font-semibold text-ink">Step 1</p>
              <p className="text-lg font-semibold text-ink">Complete the brief</p>
            </div>
          </div>
          <p className="mt-4 text-sm leading-7 text-ink-soft">
            Give us the business context, audience, geography, and channel direction we need to shape the recommendation properly.
          </p>
        </div>

        <div className="panel px-6 py-6">
          <div className="flex items-center gap-3">
            <div className="rounded-2xl bg-brand-soft p-3 text-brand"><Sparkles className="size-5" /></div>
            <div>
              <p className="text-sm font-semibold text-ink">Step 2</p>
              <p className="text-lg font-semibold text-ink">Choose planning mode</p>
            </div>
          </div>
          <p className="mt-4 text-sm leading-7 text-ink-soft">
            After submission, choose whether Advertified should plan this through AI-assisted, agent-assisted, or hybrid workflow.
          </p>
        </div>

        <div className="panel px-6 py-6">
          <div className="flex items-center gap-3">
            <div className="rounded-2xl bg-brand-soft p-3 text-brand"><CheckCircle2 className="size-5" /></div>
            <div>
              <p className="text-sm font-semibold text-ink">Step 3</p>
              <p className="text-lg font-semibold text-ink">Review your recommendation</p>
            </div>
          </div>
          <p className="mt-4 text-sm leading-7 text-ink-soft">
            Once planning starts, you will move into recommendation review where you can approve it or request changes.
          </p>
        </div>
      </div>

      {isSubmitted ? (
        <div className="panel flex flex-col gap-4 border-brand/20 bg-brand-soft/40 px-6 py-6 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Next step unlocked</p>
            <h2 className="mt-2 text-2xl font-semibold text-ink">Your brief is complete. Choose how you want Advertified to plan this campaign.</h2>
            <p className="mt-2 text-sm leading-7 text-ink-soft">
              You do not need to edit this brief again unless you want to change the direction. Continue into planning to select AI, agent-assisted, or hybrid workflow.
            </p>
          </div>
          <Link to={`/campaigns/${campaign.id}/planning`} className="inline-flex items-center justify-center gap-2 rounded-full bg-brand px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand-dark">
            Continue to planning
            <ArrowRight className="size-4" />
          </Link>
        </div>
      ) : null}

      <div className="max-w-3xl">
        <div className="pill bg-brand-soft text-brand">What happens here</div>
        <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">This page is not the final recommendation. It is the setup step before planning begins.</h2>
        <p className="section-copy mt-4">
          Save your draft if you want to come back later. Submit the brief when you are ready for planning to start. After that, the next page will ask you how Advertified should build the recommendation for this campaign.
        </p>
      </div>

      <CampaignBriefForm
        initialValue={campaign.brief}
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

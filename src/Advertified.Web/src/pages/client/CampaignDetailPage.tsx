import { useQuery } from '@tanstack/react-query';
import { Link, Navigate, useParams } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { StatusBadge } from '../../components/ui/StatusBadge';
import { useAuth } from '../../features/auth/auth-context';
import { CampaignTimeline } from '../../features/campaigns/components/CampaignTimeline';
import { getCampaignPrimaryAction } from '../../lib/access';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';

export function CampaignDetailPage() {
  const { id = '' } = useParams();
  const { user } = useAuth();
  const campaignQuery = useQuery({ queryKey: ['campaign', id], queryFn: () => advertifiedApi.getCampaign(id) });

  if (campaignQuery.isLoading) {
    return <LoadingState label="Loading campaign..." />;
  }

  if (user?.role === 'agent' || user?.role === 'admin') {
    return <Navigate to={`/agent/campaigns/${id}`} replace />;
  }

  if (campaignQuery.isError || !campaignQuery.data) {
    return (
      <EmptyState
        title="Campaign not found"
        description="We could not load this campaign in the client workspace. If this is an agent campaign, open it from the agent dashboard instead."
        ctaHref="/dashboard"
        ctaLabel="Back to dashboard"
      />
    );
  }

  const campaign = campaignQuery.data;
  const primaryAction = getCampaignPrimaryAction(campaign);

  return (
    <section className="page-shell space-y-8">
      <div className="panel hero-glow px-6 py-8 text-white sm:px-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="pill border-white/10 bg-white/10 text-white/75">{campaign.packageBandName}</div>
            <h1 className="mt-4 text-4xl font-semibold tracking-tight">{campaign.campaignName}</h1>
            <p className="mt-4 max-w-2xl text-base leading-8 text-white/75">{campaign.nextAction}</p>
          </div>
          <StatusBadge status={campaign.status} />
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="panel px-6 py-6">
          <p className="text-sm font-semibold text-ink-soft">Selected budget</p>
          <p className="mt-2 text-3xl font-semibold text-ink">{formatCurrency(campaign.selectedBudget)}</p>
        </div>
        <div className="panel px-6 py-6">
          <p className="text-sm font-semibold text-ink-soft">Planning unlock</p>
          <p className="mt-2 text-lg font-semibold text-ink">{campaign.aiUnlocked ? 'Unlocked' : 'Locked until brief submission'}</p>
        </div>
        <div className="panel px-6 py-6">
          <p className="text-sm font-semibold text-ink-soft">Planning mode</p>
          <p className="mt-2 text-lg font-semibold text-ink">{campaign.planningMode?.replaceAll('_', ' ') ?? 'Not selected yet'}</p>
        </div>
      </div>

      <CampaignTimeline steps={campaign.timeline} />

      <div className="panel flex flex-col gap-4 border-brand/20 bg-brand-soft/40 px-6 py-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">{primaryAction.stepLabel}</p>
          <h2 className="mt-2 text-2xl font-semibold text-ink">{primaryAction.label}</h2>
          <p className="mt-2 text-sm leading-7 text-ink-soft">{primaryAction.description}</p>
        </div>
        <Link to={primaryAction.href} className="inline-flex items-center justify-center rounded-full bg-brand px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand-dark">
          Continue
        </Link>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Link to={`/campaigns/${campaign.id}/brief`} className="panel px-6 py-6 transition hover:border-brand/30">
          <p className="text-lg font-semibold text-ink">Complete brief</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">Step 1. Tell us about objectives, geography, audience, and media preferences.</p>
        </Link>
        <Link to={`/campaigns/${campaign.id}/planning`} className="panel px-6 py-6 transition hover:border-brand/30">
          <p className="text-lg font-semibold text-ink">Choose planning mode</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">Step 2. Choose AI or agent-assisted planning once your brief is submitted.</p>
        </Link>
        <Link to={`/campaigns/${campaign.id}/review`} className="panel px-6 py-6 transition hover:border-brand/30">
          <p className="text-lg font-semibold text-ink">Review recommendation</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">Step 3. See the current recommendation pack and optional upsells.</p>
        </Link>
      </div>
    </section>
  );
}

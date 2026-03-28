import { useQuery } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { StatusBadge } from '../../components/ui/StatusBadge';
import { formatCurrency } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';

export function CampaignDetailPage() {
  const { id = '' } = useParams();
  const campaignQuery = useQuery({ queryKey: ['campaign', id], queryFn: () => advertifiedApi.getCampaign(id) });

  if (campaignQuery.isLoading || !campaignQuery.data) {
    return <LoadingState label="Loading campaign..." />;
  }

  const campaign = campaignQuery.data;

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

      <div className="grid gap-4 md:grid-cols-3">
        <Link to={`/campaigns/${campaign.id}/brief`} className="panel px-6 py-6 transition hover:border-brand/30">
          <p className="text-lg font-semibold text-ink">Complete brief</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">Tell us about objectives, geography, audience, and media preferences.</p>
        </Link>
        <Link to={`/campaigns/${campaign.id}/planning`} className="panel px-6 py-6 transition hover:border-brand/30">
          <p className="text-lg font-semibold text-ink">Planning</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">Choose AI or agent-assisted planning once your brief is submitted.</p>
        </Link>
        <Link to={`/campaigns/${campaign.id}/review`} className="panel px-6 py-6 transition hover:border-brand/30">
          <p className="text-lg font-semibold text-ink">Review recommendation</p>
          <p className="mt-2 text-sm leading-7 text-ink-soft">See the current recommendation pack and optional upsells.</p>
        </Link>
      </div>
    </section>
  );
}

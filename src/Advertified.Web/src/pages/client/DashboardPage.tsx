import { useQuery } from '@tanstack/react-query';
import { BarChart3, BriefcaseBusiness, FolderKanban, Sparkles } from 'lucide-react';
import { Link } from 'react-router-dom';
import { DashboardSummaryCard } from '../../components/ui/DashboardSummaryCard';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { StatusBadge } from '../../components/ui/StatusBadge';
import { useAuth } from '../../features/auth/auth-context';
import { getCampaignPrimaryAction } from '../../lib/access';
import { advertifiedApi } from '../../services/advertifiedApi';
import { PageHero } from '../../components/marketing/PageHero';

export function DashboardPage() {
  const { user } = useAuth();
  const campaignsQuery = useQuery({
    queryKey: ['campaigns', user?.id],
    queryFn: () => advertifiedApi.getCampaigns(user!.id),
    enabled: Boolean(user),
  });
  const ordersQuery = useQuery({
    queryKey: ['orders', user?.id],
    queryFn: () => advertifiedApi.getOrders(user!.id),
    enabled: Boolean(user),
  });

  if (campaignsQuery.isLoading || ordersQuery.isLoading) {
    return <LoadingState label="Loading your dashboard..." />;
  }

  const campaigns = campaignsQuery.data ?? [];
  const orders = ordersQuery.data ?? [];
  const nextCampaign = campaigns[0];
  const nextAction = nextCampaign ? getCampaignPrimaryAction(nextCampaign) : null;

  return (
    <section className="page-shell space-y-8">
      <PageHero
        kicker="Client dashboard"
        title="Your package, campaign, and planning progress in one view."
        description="Track what you have unlocked, what happens next, and where each campaign sits in the journey."
      />

      <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        <DashboardSummaryCard label="Active campaigns" value={String(campaigns.length)} helper="Track every campaign from payment through recommendation review." icon={<FolderKanban className="size-5" />} />
        <DashboardSummaryCard label="Package orders" value={String(orders.length)} helper="See every purchased package and its payment state." icon={<BriefcaseBusiness className="size-5" />} />
        <DashboardSummaryCard label="Planning unlocked" value={String(campaigns.filter((item) => item.aiUnlocked).length)} helper="Unlocked only after payment and brief submission." icon={<Sparkles className="size-5" />} />
        <DashboardSummaryCard label="Next actions" value={nextAction?.label ?? 'Buy a package'} helper="The dashboard keeps the path guided, not technical." icon={<BarChart3 className="size-5" />} />
      </div>

      {nextCampaign && nextAction ? (
        <div className="panel hero-glow px-6 py-8 text-white sm:px-8">
          <div className="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-3xl">
              <div className="pill border-white/10 bg-white/10 text-white/75">{nextAction.stepLabel}</div>
              <h2 className="mt-4 text-3xl font-semibold tracking-tight">{nextAction.label}</h2>
              <p className="mt-3 text-base leading-8 text-white/75">{nextAction.description}</p>
              <p className="mt-3 text-sm leading-7 text-white/65">
                Campaign: {nextCampaign.campaignName} • Package: {nextCampaign.packageBandName}
              </p>
            </div>
            <Link to={nextAction.href} className="inline-flex items-center justify-center rounded-full bg-white px-5 py-3 text-sm font-semibold text-ink transition hover:bg-white/90">
              Continue
            </Link>
          </div>
        </div>
      ) : null}

      {campaigns.length ? (
        <div className="grid gap-5 xl:grid-cols-2">
          {campaigns.map((campaign) => {
            const action = getCampaignPrimaryAction(campaign);
            return (
            <Link key={campaign.id} to={action.href} className="panel flex flex-col gap-5 px-6 py-6 transition hover:-translate-y-1 hover:border-brand/30">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">{campaign.packageBandName}</p>
                  <h3 className="mt-2 text-2xl font-semibold tracking-tight text-ink">{campaign.campaignName}</h3>
                </div>
                <StatusBadge status={campaign.status} />
              </div>
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">{action.stepLabel}</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">{action.description}</p>
              </div>
              <div className="text-sm font-semibold text-brand">{action.label}</div>
            </Link>
          );})}
        </div>
      ) : (
        <EmptyState title="No campaigns yet" description="Buy your first package to unlock a guided campaign journey and planning recommendations." ctaHref="/packages" ctaLabel="Browse packages" />
      )}
    </section>
  );
}

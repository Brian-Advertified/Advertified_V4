import { useQuery } from '@tanstack/react-query';
import { BarChart3, BriefcaseBusiness, FolderKanban, Sparkles } from 'lucide-react';
import { Link } from 'react-router-dom';
import { DashboardSummaryCard } from '../../components/ui/DashboardSummaryCard';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { StatusBadge } from '../../components/ui/StatusBadge';
import { useAuth } from '../../features/auth/auth-context';
import { advertifiedApi } from '../../services/advertifiedApi';

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

  return (
    <section className="page-shell space-y-8">
      <div className="max-w-3xl">
        <div className="pill bg-brand-soft text-brand">Client dashboard</div>
        <h1 className="mt-4 text-4xl font-semibold tracking-tight text-ink sm:text-5xl">Your package, campaign, and planning progress in one view.</h1>
      </div>

      <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        <DashboardSummaryCard label="Active campaigns" value={String(campaigns.length)} helper="Track every campaign from payment through recommendation review." icon={<FolderKanban className="size-5" />} />
        <DashboardSummaryCard label="Package orders" value={String(orders.length)} helper="See every purchased package and its payment state." icon={<BriefcaseBusiness className="size-5" />} />
        <DashboardSummaryCard label="Planning unlocked" value={String(campaigns.filter((item) => item.aiUnlocked).length)} helper="Unlocked only after payment and brief submission." icon={<Sparkles className="size-5" />} />
        <DashboardSummaryCard label="Next actions" value={campaigns[0]?.nextAction ?? 'Buy a package'} helper="The dashboard keeps the path guided, not technical." icon={<BarChart3 className="size-5" />} />
      </div>

      {campaigns.length ? (
        <div className="grid gap-5 xl:grid-cols-2">
          {campaigns.map((campaign) => (
            <Link key={campaign.id} to={`/campaigns/${campaign.id}`} className="panel flex flex-col gap-5 px-6 py-6 transition hover:-translate-y-1 hover:border-brand/30">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">{campaign.packageBandName}</p>
                  <h3 className="mt-2 text-2xl font-semibold tracking-tight text-ink">{campaign.campaignName}</h3>
                </div>
                <StatusBadge status={campaign.status} />
              </div>
              <p className="text-sm leading-7 text-ink-soft">{campaign.nextAction}</p>
              <div className="text-sm font-semibold text-brand">Open campaign</div>
            </Link>
          ))}
        </div>
      ) : (
        <EmptyState title="No campaigns yet" description="Buy your first package to unlock a guided campaign journey and planning recommendations." ctaHref="/packages" ctaLabel="Browse packages" />
      )}
    </section>
  );
}

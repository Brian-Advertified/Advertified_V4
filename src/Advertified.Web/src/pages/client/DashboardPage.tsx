import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { EmptyState } from '../../components/ui/EmptyState';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../features/auth/auth-context';
import { getCampaignPrimaryAction } from '../../lib/access';
import { formatCurrency, formatDate, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import { ClientPortalShell, getCampaignProgressPercent } from './clientWorkspace';

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
    <ClientPortalShell
      campaigns={campaigns}
      activeNav="dashboard"
      title="Client Portal"
      description="Your new overview for active campaigns, purchased packages, and the next workspace each campaign should open."
    >
      <div className="user-banner">
        <strong>How it works:</strong> this is now the top-level client portal. Choose a campaign below to enter its simplified workspace with the current approval and direct team messaging.
      </div>

      <div className="user-grid-4">
        <div className="user-card">
          <h3>Active Campaigns</h3>
          <div className="user-metric">{campaigns.length}</div>
          <div className="user-muted">Campaign workspaces available in the new client flow.</div>
        </div>
        <div className="user-card">
          <h3>Orders</h3>
          <div className="user-metric">{orders.length}</div>
          <div className="user-muted">Purchased packages linked to your account.</div>
        </div>
        <div className="user-card">
          <h3>Planning Unlocked</h3>
          <div className="user-metric">{campaigns.filter((campaign) => campaign.aiUnlocked).length}</div>
          <div className="user-muted">Campaigns ready for planning and recommendation flow.</div>
        </div>
        <div className="user-card">
          <h3>Approved</h3>
          <div className="user-metric">{campaigns.filter((campaign) => campaign.status === 'approved' || campaign.status === 'creative_approved' || campaign.status === 'launched').length}</div>
          <div className="user-muted">Campaigns that have reached approval in the live workflow.</div>
        </div>
      </div>

      {orders.length ? (
        <div className="mt-6">
          <div className="user-card">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
              <div>
                <h3 className="!mb-2">Recent Package Orders</h3>
                <p className="user-muted">Billing and purchase history stays available here, without pulling you out of the main campaign flow.</p>
              </div>
              <Link to="/orders" className="user-btn-secondary">Open full order history</Link>
            </div>
            <div className="mt-4 space-y-3">
              {orders.slice(0, 3).map((order) => (
                <div key={order.id} className="user-wire">
                  <div className="flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
                    <div>
                      <strong>{order.packageBandName}</strong>
                      <div>{formatDate(order.createdAt)} | {formatCurrency(order.amount)}</div>
                    </div>
                    <div>{titleCase(order.paymentStatus)}{order.paymentReference ? ` | ${order.paymentReference}` : ''}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      ) : null}

      {campaigns.length ? (
        <div className="mt-6 space-y-4">
          {campaigns.map((campaign) => {
            const action = getCampaignPrimaryAction(campaign);
            return (
              <div key={campaign.id} className="user-card">
                <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                  <div className="space-y-3">
                    <div className="flex flex-wrap gap-2">
                      <span className="user-pill">{campaign.packageBandName}</span>
                      <span className="user-pill">{formatCurrency(campaign.selectedBudget)}</span>
                      <span className="user-pill">{titleCase(campaign.status)}</span>
                    </div>
                    <div>
                      <h3 className="!mb-2">{campaign.campaignName}</h3>
                      <p className="user-muted">{campaign.nextAction}</p>
                    </div>
                  </div>
                  <div className="text-left lg:text-right">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Progress</div>
                    <div className="mt-2 text-2xl font-semibold text-ink">{getCampaignProgressPercent(campaign)}%</div>
                    <div className="mt-2 user-muted">Created {formatDate(campaign.createdAt)}</div>
                  </div>
                </div>

                <div className="user-grid-3 mt-4">
                  <div className="user-wire">
                    <strong>Primary action</strong>
                    <div>{action.label}</div>
                    <div className="mt-2">{action.description}</div>
                  </div>
                  <div className="user-wire">
                    <strong>Planning mode</strong>
                    <div>{campaign.planningMode ? titleCase(campaign.planningMode) : 'Not selected yet'}</div>
                  </div>
                  <div className="user-wire">
                    <strong>Workspace</strong>
                    <div>Open the campaign workspace to review the current approval and message your Advertified team from one screen.</div>
                  </div>
                </div>

                <div className="user-toolbar mt-4">
                  <Link to={`/campaigns/${campaign.id}`} className="user-btn-primary">Open Campaign Workspace</Link>
                  <Link to={action.href} className="user-btn-secondary">Continue Next Step</Link>
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="mt-6">
          <EmptyState
            title="No campaigns yet"
            description="Buy your first package to unlock the new client portal workflow."
            ctaHref="/packages"
            ctaLabel="Browse packages"
          />
        </div>
      )}
    </ClientPortalShell>
  );
}

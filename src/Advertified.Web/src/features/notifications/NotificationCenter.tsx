import { useQuery } from '@tanstack/react-query';
import { Bell, CheckCircle2, CircleAlert, Clock3, FileText } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/auth-context';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AdminDashboard, AgentInbox, Campaign, PackageOrder } from '../../types/domain';

type NotificationItem = {
  id: string;
  title: string;
  description: string;
  href: string;
  tone: 'info' | 'success' | 'warning';
};

function buildCampaignNotifications(campaigns: Campaign[]): NotificationItem[] {
  return campaigns.flatMap<NotificationItem>((campaign) => {
    if (campaign.status === 'paid') {
      return [{
        id: `campaign-paid-${campaign.id}`,
        title: 'Campaign workspace ready',
        description: `${campaign.campaignName} is paid and ready in your simplified client workspace.`,
        href: `/campaigns/${campaign.id}`,
        tone: 'info',
      }];
    }

    if (campaign.status === 'review_ready' && campaign.recommendation?.status === 'sent_to_client') {
      return [{
        id: `campaign-review-${campaign.id}`,
        title: 'Recommendation ready for review',
        description: `${campaign.campaignName} is ready for your approval or change request.`,
        href: `/campaigns/${campaign.id}`,
        tone: 'success',
      }];
    }

    if (campaign.status === 'planning_in_progress') {
      return [{
        id: `campaign-planning-${campaign.id}`,
        title: 'Recommendation in progress',
        description: `${campaign.campaignName} is currently being prepared.`,
        href: `/campaigns/${campaign.id}`,
        tone: 'info',
      }];
    }

    if (campaign.status === 'approved') {
      return [{
        id: `campaign-approved-${campaign.id}`,
        title: 'Recommendation approved',
        description: `${campaign.campaignName} is approved and ready for the next step.`,
        href: `/campaigns/${campaign.id}`,
        tone: 'success',
      }];
    }

    if (campaign.status === 'creative_changes_requested') {
      return [{
        id: `campaign-creative-changes-${campaign.id}`,
        title: 'Creative changes requested',
        description: `${campaign.campaignName} has been sent back for creative revision.`,
        href: `/campaigns/${campaign.id}`,
        tone: 'warning',
      }];
    }

    if (campaign.status === 'creative_sent_to_client_for_approval') {
      return [{
        id: `campaign-creative-review-${campaign.id}`,
        title: 'Finished media ready for approval',
        description: `${campaign.campaignName} has been sent back for your final approval.`,
        href: `/campaigns/${campaign.id}`,
        tone: 'success',
      }];
    }

    if (campaign.status === 'creative_approved') {
      return [{
        id: `campaign-creative-approved-${campaign.id}`,
        title: 'Final creative approved',
        description: `${campaign.campaignName} has completed final creative approval and is waiting for activation.`,
        href: `/campaigns/${campaign.id}`,
        tone: 'success',
      }];
    }

    if (campaign.status === 'launched') {
      return [{
        id: `campaign-live-${campaign.id}`,
        title: 'Campaign live',
        description: `${campaign.campaignName} is now live.`,
        href: `/campaigns/${campaign.id}`,
        tone: 'success',
      }];
    }

    return [];
  });
}

function buildOrderNotifications(orders: PackageOrder[]): NotificationItem[] {
  return orders.flatMap<NotificationItem>((order) => {
    if (order.paymentStatus === 'failed') {
      return [{
        id: `order-failed-${order.id}`,
        title: 'Payment was not successful',
        description: `${order.packageBandName} could not be confirmed. You can try again or contact support.`,
        href: '/orders',
        tone: 'warning',
      }];
    }

    if (order.paymentStatus === 'paid' && order.invoicePdfUrl) {
      return [{
        id: `invoice-ready-${order.id}`,
        title: 'Invoice ready',
        description: `Your paid invoice for ${order.packageBandName} is available.`,
        href: '/orders',
        tone: 'success',
      }];
    }

    return [];
  });
}

function buildAgentNotifications(inbox: AgentInbox): NotificationItem[] {
  return inbox.items.flatMap<NotificationItem>((item) => {
    if (item.queueStage === 'planning_ready') {
      return [{
        id: `agent-planning-ready-${item.id}`,
        title: 'Campaign ready for recommendation',
        description: `${item.campaignName} can now move into recommendation planning.`,
        href: `/agent/campaigns/${item.id}`,
        tone: item.isUrgent ? 'warning' : 'info',
      }];
    }

    if (item.queueStage === 'ready_to_send') {
      return [{
        id: `agent-ready-to-send-${item.id}`,
        title: 'Recommendation ready to send',
        description: `${item.campaignName} is prepared and waiting for client delivery.`,
        href: `/agent/campaigns/${item.id}`,
        tone: 'success',
      }];
    }

    if (item.queueStage === 'agent_review' || item.manualReviewRequired) {
      return [{
        id: `agent-review-${item.id}`,
        title: 'Recommendation needs strategist review',
        description: `${item.campaignName} has checks or flags that need your attention.`,
        href: `/agent/campaigns/${item.id}`,
        tone: 'warning',
      }];
    }

    if (item.queueStage === 'waiting_on_client') {
      return [{
        id: `agent-client-wait-${item.id}`,
        title: 'Waiting on client feedback',
        description: `${item.campaignName} is with the client for approval or revisions.`,
        href: `/agent/campaigns/${item.id}`,
        tone: 'info',
      }];
    }

    if (item.queueStage === 'newly_paid' || item.queueStage === 'brief_waiting') {
      return [{
        id: `agent-brief-${item.id}`,
        title: 'Campaign entered the strategist queue',
        description: `${item.campaignName} is newly paid and moving through intake.`,
        href: `/agent/campaigns/${item.id}`,
        tone: 'info',
      }];
    }

    return [];
  });
}

function buildAdminNotifications(dashboard: AdminDashboard): NotificationItem[] {
  const alertItems = dashboard.alerts.map<NotificationItem>((alert, index) => ({
    id: `admin-alert-${index}-${alert.title}`,
    title: alert.title,
    description: alert.context,
    href: '/admin/health',
    tone: alert.severity.toLowerCase().includes('critical') ? 'warning' : 'info',
  }));

  const pricingItems = dashboard.healthIssues
    .filter((item) => item.issue.toLowerCase().includes('pricing') || item.issue.toLowerCase().includes('inventory'))
    .slice(0, 3)
    .map<NotificationItem>((item) => ({
      id: `admin-pricing-${item.outletCode}`,
      title: `${item.outletName} needs pricing attention`,
      description: item.suggestedFix,
      href: `/admin/pricing?outlet=${encodeURIComponent(item.outletCode)}`,
      tone: 'warning',
    }));

  const monitoringItems: NotificationItem[] = dashboard.monitoring.waitingOnClientCount > 0
    ? [{
        id: 'admin-waiting-on-client',
        title: 'Campaigns are waiting on client approval',
        description: `${dashboard.monitoring.waitingOnClientCount} recommendation set(s) are currently with clients.`,
        href: '/admin/monitoring',
        tone: 'info',
      }]
    : [];

  return [...alertItems, ...pricingItems, ...monitoringItems];
}

function NotificationIcon({ tone }: { tone: NotificationItem['tone'] }) {
  if (tone === 'success') {
    return <CheckCircle2 className="size-4 text-emerald-600" />;
  }

  if (tone === 'warning') {
    return <CircleAlert className="size-4 text-amber-600" />;
  }

  return <Clock3 className="size-4 text-brand" />;
}

export function NotificationCenter() {
  const [open, setOpen] = useState(false);
  const { user } = useAuth();

  const campaignsQuery = useQuery({
    queryKey: ['campaigns', user?.id],
    queryFn: () => advertifiedApi.getCampaigns(user!.id),
    enabled: Boolean(user && user.role === 'client'),
  });

  const ordersQuery = useQuery({
    queryKey: ['orders', user?.id],
    queryFn: () => advertifiedApi.getOrders(user!.id),
    enabled: Boolean(user && user.role === 'client'),
  });

  const agentInboxQuery = useQuery({
    queryKey: ['agent-inbox', user?.id],
    queryFn: () => advertifiedApi.getAgentInbox(),
    enabled: Boolean(user && user.role === 'agent'),
  });

  const adminDashboardQuery = useQuery({
    queryKey: ['admin-dashboard-notifications', user?.id],
    queryFn: () => advertifiedApi.getAdminDashboard(),
    enabled: Boolean(user && user.role === 'admin'),
  });

  const notifications = useMemo(() => {
    if (!user) {
      return [];
    }

    if (user.role === 'client') {
      const campaigns = campaignsQuery.data ?? [];
      const orders = ordersQuery.data ?? [];
      return [...buildCampaignNotifications(campaigns), ...buildOrderNotifications(orders)].slice(0, 6);
    }

    if (user.role === 'agent') {
      const inbox = agentInboxQuery.data;
      return inbox ? buildAgentNotifications(inbox).slice(0, 6) : [];
    }

    const dashboard = adminDashboardQuery.data;
    return dashboard ? buildAdminNotifications(dashboard).slice(0, 6) : [];
  }, [adminDashboardQuery.data, agentInboxQuery.data, campaignsQuery.data, ordersQuery.data, user]);

  if (!user) {
    return null;
  }

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        className="relative inline-flex items-center justify-center rounded-full border border-line bg-white p-3 text-ink transition hover:border-brand/30"
        aria-label="Open notifications"
      >
        <Bell className="size-4" />
        {notifications.length > 0 ? (
          <span className="absolute -right-1 -top-1 inline-flex min-w-5 items-center justify-center rounded-full bg-brand px-1.5 py-0.5 text-[11px] font-semibold text-white">
            {notifications.length}
          </span>
        ) : null}
      </button>

      {open ? (
        <div className="absolute right-0 top-[calc(100%+12px)] z-50 w-[360px] overflow-hidden rounded-[24px] border border-line bg-white shadow-[0_18px_55px_rgba(15,23,42,0.12)]">
          <div className="border-b border-line px-5 py-4">
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Notifications</p>
            <p className="mt-2 text-sm text-ink-soft">Important campaign and payment updates in one place.</p>
          </div>

          <div className="max-h-[420px] overflow-y-auto p-3">
            {notifications.length > 0 ? (
              <div className="space-y-2">
                {notifications.map((notification) => (
                  <Link
                    key={notification.id}
                    to={notification.href}
                    onClick={() => setOpen(false)}
                    className="flex gap-3 rounded-[18px] border border-line bg-slate-50 px-4 py-3 transition hover:border-brand/20 hover:bg-brand-soft/20"
                  >
                    <div className="mt-0.5">
                      <NotificationIcon tone={notification.tone} />
                    </div>
                    <div>
                      <p className="text-sm font-semibold text-ink">{notification.title}</p>
                      <p className="mt-1 text-sm leading-6 text-ink-soft">{notification.description}</p>
                    </div>
                  </Link>
                ))}
              </div>
            ) : (
              <div className="flex flex-col items-center justify-center gap-3 px-4 py-8 text-center">
                <FileText className="size-5 text-ink-soft" />
                <p className="text-sm font-semibold text-ink">No new notifications</p>
                <p className="text-sm text-ink-soft">We’ll surface payment, recommendation, and approval updates here.</p>
              </div>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}

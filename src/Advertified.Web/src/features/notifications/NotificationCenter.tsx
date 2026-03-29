import { useQuery } from '@tanstack/react-query';
import { Bell, CheckCircle2, CircleAlert, Clock3, FileText } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/auth-context';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { Campaign, PackageOrder } from '../../types/domain';

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
        title: 'Complete your campaign brief',
        description: `${campaign.campaignName} is paid and ready for your brief details.`,
        href: `/campaigns/${campaign.id}/brief`,
        tone: 'info',
      }];
    }

    if (campaign.status === 'review_ready' && campaign.recommendation?.status === 'sent_to_client') {
      return [{
        id: `campaign-review-${campaign.id}`,
        title: 'Recommendation ready for review',
        description: `${campaign.campaignName} is ready for your approval or change request.`,
        href: `/campaigns/${campaign.id}/review`,
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
        href: `/campaigns/${campaign.id}/review`,
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

  const notifications = useMemo(() => {
    if (!user || user.role !== 'client') {
      return [];
    }

    const campaigns = campaignsQuery.data ?? [];
    const orders = ordersQuery.data ?? [];
    return [...buildCampaignNotifications(campaigns), ...buildOrderNotifications(orders)].slice(0, 6);
  }, [campaignsQuery.data, ordersQuery.data, user]);

  if (!user || user.role !== 'client') {
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

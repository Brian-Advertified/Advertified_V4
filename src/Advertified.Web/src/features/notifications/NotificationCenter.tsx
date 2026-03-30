import { useQuery } from '@tanstack/react-query';
import { Bell, CheckCircle2, CircleAlert, Clock3, FileText } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/auth-context';
import { queryKeys } from '../../lib/queryKeys';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { NotificationSummaryItem } from '../../types/domain';

function NotificationIcon({ tone }: { tone: NotificationSummaryItem['tone'] }) {
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

  const summaryQuery = useQuery({
    queryKey: queryKeys.notifications.summary(user?.role),
    queryFn: advertifiedApi.getNotificationSummary,
    enabled: Boolean(user),
  });

  const notifications = summaryQuery.data?.items ?? [];

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
        {summaryQuery.data && summaryQuery.data.unreadCount > 0 ? (
          <span className="absolute -right-1 -top-1 inline-flex min-w-5 items-center justify-center rounded-full bg-brand px-1.5 py-0.5 text-[11px] font-semibold text-white">
            {summaryQuery.data.unreadCount}
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

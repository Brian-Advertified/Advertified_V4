import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bell, CheckCircle2, CircleAlert, Clock3, FileText } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/auth-context';
import { queryKeys } from '../../lib/queryKeys';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { NotificationSummary, NotificationSummaryItem } from '../../types/domain';

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
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const summaryQuery = useQuery({
    queryKey: queryKeys.notifications.summary(user?.role),
    queryFn: advertifiedApi.getNotificationSummary,
    enabled: Boolean(user),
  });

  const notifications = summaryQuery.data?.items ?? [];
  const unreadCount = summaryQuery.data?.unreadCount ?? 0;

  const markNotificationReadMutation = useMutation({
    mutationFn: advertifiedApi.markNotificationRead,
    onMutate: async (notificationId) => {
      const queryKey = queryKeys.notifications.summary(user?.role);
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<NotificationSummary>(queryKey);

      if (previous) {
        queryClient.setQueryData<NotificationSummary>(queryKey, {
          unreadCount: Math.max(0, previous.unreadCount - (previous.items.some((item) => item.id === notificationId && !item.isRead) ? 1 : 0)),
          items: previous.items.map((item) => item.id === notificationId ? { ...item, isRead: true } : item),
        });
      }

      return { previous, queryKey };
    },
    onError: (_error, _notificationId, context) => {
      if (context?.previous) {
        queryClient.setQueryData(context.queryKey, context.previous);
      }
    },
    onSettled: (_data, _error, _notificationId, context) => {
      queryClient.invalidateQueries({ queryKey: context?.queryKey ?? queryKeys.notifications.summary(user?.role) });
    },
  });

  const markAllNotificationsReadMutation = useMutation({
    mutationFn: advertifiedApi.markAllNotificationsRead,
    onMutate: async () => {
      const queryKey = queryKeys.notifications.summary(user?.role);
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<NotificationSummary>(queryKey);

      if (previous) {
        queryClient.setQueryData<NotificationSummary>(queryKey, {
          unreadCount: 0,
          items: previous.items.map((item) => ({ ...item, isRead: true })),
        });
      }

      return { previous, queryKey };
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) {
        queryClient.setQueryData(context.queryKey, context.previous);
      }
    },
    onSettled: (_data, _error, _variables, context) => {
      queryClient.invalidateQueries({ queryKey: context?.queryKey ?? queryKeys.notifications.summary(user?.role) });
    },
  });

  function handleNotificationClick(notification: NotificationSummaryItem) {
    if (!notification.isRead) {
      markNotificationReadMutation.mutate(notification.id);
    }

    setOpen(false);
    navigate(notification.href);
  }

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
        {unreadCount > 0 ? (
          <span className="absolute -right-1 -top-1 inline-flex min-w-5 items-center justify-center rounded-full bg-brand px-1.5 py-0.5 text-[11px] font-semibold text-white">
            {unreadCount}
          </span>
        ) : null}
      </button>

      {open ? (
        <>
          <button
            type="button"
            aria-label="Close notifications"
            className="fixed inset-0 z-40 cursor-default bg-transparent"
            onClick={() => setOpen(false)}
          />
          <div className="absolute right-0 top-[calc(100%+12px)] z-50 w-[360px] overflow-hidden rounded-[24px] border border-line bg-white shadow-[0_18px_55px_rgba(15,23,42,0.12)]">
            <div className="border-b border-line px-5 py-4">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Notifications</p>
                  <p className="mt-2 text-sm text-ink-soft">Important campaign and payment updates in one place.</p>
                </div>
                {unreadCount > 0 ? (
                  <button
                    type="button"
                    className="text-xs font-semibold uppercase tracking-[0.18em] text-brand transition hover:text-brand/80"
                    disabled={markAllNotificationsReadMutation.isPending}
                    onClick={() => markAllNotificationsReadMutation.mutate()}
                  >
                    Read all
                  </button>
                ) : null}
              </div>
            </div>

            <div className="max-h-[420px] overflow-y-auto p-3">
              {notifications.length > 0 ? (
                <div className="space-y-2">
                  {notifications.map((notification) => (
                    <button
                      type="button"
                      key={notification.id}
                      onClick={() => handleNotificationClick(notification)}
                      className={`flex w-full gap-3 rounded-[18px] border px-4 py-3 text-left transition ${
                        notification.isRead
                          ? 'border-line bg-white hover:border-brand/15 hover:bg-slate-50'
                          : 'border-brand/15 bg-brand-soft/20 hover:border-brand/30 hover:bg-brand-soft/30'
                      }`}
                    >
                      <div className="mt-0.5">
                        <NotificationIcon tone={notification.tone} />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-semibold text-ink">{notification.title}</p>
                        <p className="mt-1 text-sm leading-6 text-ink-soft">{notification.description}</p>
                      </div>
                      {!notification.isRead ? <span className="mt-1 size-2 rounded-full bg-brand" aria-hidden="true" /> : null}
                    </button>
                  ))}
                </div>
              ) : (
                <div className="flex flex-col items-center justify-center gap-3 px-4 py-8 text-center">
                  <FileText className="size-5 text-ink-soft" />
                  <p className="text-sm font-semibold text-ink">No new notifications</p>
                  <p className="text-sm text-ink-soft">We'll surface payment, recommendation, and approval updates here.</p>
                </div>
              )}
            </div>
          </div>
        </>
      ) : null}
    </div>
  );
}

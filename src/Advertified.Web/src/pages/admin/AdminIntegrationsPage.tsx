import { Globe2, MapPin, Zap } from 'lucide-react';
import { ReadOnlyNotice } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, fmtDate, useAdminDashboardQuery } from './adminWorkspace';

export function AdminIntegrationsPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const platformStatus = dashboard.integrations.lastPaymentWebhookAt || dashboard.integrations.lastPaymentRequestAt ? 'Connected' : 'Waiting for first activity';

        return (
          <AdminPageShell title="Integrations" description="Track the current health of live integration activity, payment requests, and webhook streams.">
            <ReadOnlyNotice label="Integration health is read-only here and is derived from live request and webhook activity rather than admin-managed settings." />
            <div className="grid gap-4 xl:grid-cols-3">
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><Zap className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Payment requests</p></div><p className="mt-4 text-3xl font-semibold text-ink">{dashboard.integrations.paymentRequestAuditCount}</p><p className="mt-3 text-sm text-ink-soft">Requests recorded against the payment provider integration.</p><p className="mt-3 text-xs text-ink-soft">Last request {fmtDate(dashboard.integrations.lastPaymentRequestAt)}</p></div>
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><MapPin className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Payment webhooks</p></div><p className="mt-4 text-3xl font-semibold text-ink">{dashboard.integrations.paymentWebhookAuditCount}</p><p className="mt-3 text-sm text-ink-soft">Webhook callbacks received from the payment provider.</p><p className="mt-3 text-xs text-ink-soft">Last webhook {fmtDate(dashboard.integrations.lastPaymentWebhookAt)}</p></div>
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><Globe2 className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Platform status</p></div><p className="mt-4 text-2xl font-semibold text-ink">{platformStatus}</p><p className="mt-3 text-sm text-ink-soft">Status is derived from live integration activity rather than a hardcoded admin label.</p></div>
            </div>
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

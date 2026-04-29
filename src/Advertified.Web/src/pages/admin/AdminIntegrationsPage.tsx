import { Globe2, Mail, MapPin, Send, Zap } from 'lucide-react';
import { ReadOnlyNotice } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, fmtDate, useAdminDashboardQuery } from './adminWorkspace';

export function AdminIntegrationsPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const { integrations } = dashboard;
        const platformStatus = integrations.resendSendConfigured
          ? (integrations.resendWebhookEnabled ? 'Resend live and webhooks enabled' : 'Resend live send configured')
          : (integrations.resendArchiveFallbackEnabled ? 'Archiving locally until Resend is configured' : 'Resend send not configured');
        const sendStatus = integrations.resendSendConfigured ? 'Live send ready' : 'Missing API key or sender';
        const webhookStatus = integrations.resendWebhookEnabled
          ? (integrations.resendWebhookSigningSecretConfigured ? 'Webhook verification ready' : 'Webhook enabled but secret missing')
          : 'Webhook disabled';

        return (
          <AdminPageShell title="Integrations" description="Track Resend readiness, outbound email delivery, and payment-provider activity from one operational view.">
            <ReadOnlyNotice label="Resend send credentials come from application configuration, while webhook verification stays in admin-managed provider settings." />
            <div className="grid gap-4 xl:grid-cols-4">
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><Send className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Resend send</p></div><p className="mt-4 text-2xl font-semibold text-ink">{sendStatus}</p><p className="mt-3 text-sm text-ink-soft">{integrations.resendArchiveFallbackEnabled ? 'Archive fallback is enabled while credentials are incomplete.' : 'Archive fallback is disabled, so missing credentials will block live delivery.'}</p><p className="mt-3 text-xs text-ink-soft">Last accepted {fmtDate(integrations.lastEmailAcceptedAt)}</p></div>
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><Mail className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Email outbox</p></div><p className="mt-4 text-3xl font-semibold text-ink">{integrations.emailPendingCount}</p><p className="mt-3 text-sm text-ink-soft">Pending emails waiting for dispatch. Delivered {integrations.emailDeliveredCount}, failed {integrations.emailFailedCount}, archived {integrations.emailArchivedCount}.</p><p className="mt-3 text-xs text-ink-soft">Last delivered {fmtDate(integrations.lastEmailDeliveredAt)}</p></div>
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><Globe2 className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Resend webhooks</p></div><p className="mt-4 text-2xl font-semibold text-ink">{webhookStatus}</p><p className="mt-3 text-sm text-ink-soft">Endpoint {integrations.resendWebhookEndpointPath ?? '/webhooks/email-delivery/resend'}</p><p className="mt-3 text-xs text-ink-soft">Last webhook {fmtDate(integrations.lastEmailWebhookAt)}</p></div>
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><Zap className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Payments</p></div><p className="mt-4 text-3xl font-semibold text-ink">{integrations.paymentRequestAuditCount}</p><p className="mt-3 text-sm text-ink-soft">Payment requests recorded {integrations.paymentWebhookAuditCount} webhook callbacks received.</p><p className="mt-3 text-xs text-ink-soft">Last payment webhook {fmtDate(integrations.lastPaymentWebhookAt)}</p></div>
            </div>
            <div className="grid gap-4 xl:grid-cols-3">
              <div className="panel p-6"><div className="flex items-center gap-3 text-brand"><MapPin className="size-5" /><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Platform status</p></div><p className="mt-4 text-2xl font-semibold text-ink">{platformStatus}</p><p className="mt-3 text-sm text-ink-soft">This summary combines runtime Resend configuration with actual outbox and webhook activity.</p></div>
              <div className="panel p-6"><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">Delivery totals</p><p className="mt-4 text-sm text-ink-soft">Accepted {integrations.emailAcceptedCount}</p><p className="mt-2 text-sm text-ink-soft">Delivered {integrations.emailDeliveredCount}</p><p className="mt-2 text-sm text-ink-soft">Failed {integrations.emailFailedCount}</p><p className="mt-2 text-sm text-ink-soft">Archived {integrations.emailArchivedCount}</p><p className="mt-3 text-xs text-ink-soft">Last failure {fmtDate(integrations.lastEmailFailedAt)}</p></div>
              <div className="panel p-6"><p className="text-sm font-semibold uppercase tracking-[0.24em] text-brand">What to configure</p><p className="mt-4 text-sm text-ink-soft">Application config: `Resend:ApiKey`, sender addresses, and archive fallback policy.</p><p className="mt-2 text-sm text-ink-soft">Admin settings: enable the `resend` provider webhook and store its signing secret.</p></div>
            </div>
          </AdminPageShell>
        );
      }}
    </AdminQueryBoundary>
  );
}

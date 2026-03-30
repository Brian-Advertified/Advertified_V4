import { ReadOnlyNotice } from './adminSectionShared';
import { AdminPageShell, AdminQueryBoundary, fmtDate, titleize, useAdminDashboardQuery } from './adminWorkspace';

export function AdminAuditPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => (
        <AdminPageShell title="Audit log" description="Review recent admin and agent changes alongside key payment integration events in one immutable timeline.">
          <ReadOnlyNotice label="Audit entries are immutable records. This page is intentionally read-only so operational history stays trustworthy across admin, agent, and system activity." />
          <div className="rounded-[28px] border border-line bg-white p-6">
            <div className="overflow-hidden rounded-[24px] border border-line">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Source</th><th className="px-4 py-4">Actor</th><th className="px-4 py-4">Action</th><th className="px-4 py-4">Entity</th><th className="px-4 py-4">Context</th><th className="px-4 py-4">When</th></tr></thead>
                <tbody>
                  {dashboard.auditEntries.map((entry) => <tr key={entry.id} className="border-t border-line"><td className="px-4 py-4 text-ink">{entry.source}</td><td className="px-4 py-4 text-ink-soft"><div>{entry.actorName}</div><div className="text-xs">{titleize(entry.actorRole || 'system')}</div></td><td className="px-4 py-4 text-ink-soft"><div>{titleize(entry.eventType)}</div>{entry.statusLabel ? <div className="text-xs">{entry.statusLabel}</div> : null}</td><td className="px-4 py-4 text-ink-soft"><div>{entry.entityLabel ?? 'Platform event'}</div><div className="text-xs">{entry.entityType ? titleize(entry.entityType) : 'System'}</div></td><td className="px-4 py-4 text-ink-soft">{entry.context}</td><td className="px-4 py-4 text-ink-soft">{fmtDate(entry.createdAt)}</td></tr>)}
                </tbody>
              </table>
            </div>
          </div>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}

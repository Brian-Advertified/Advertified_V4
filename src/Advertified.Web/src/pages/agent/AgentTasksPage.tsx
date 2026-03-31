import { Eye, Pencil } from 'lucide-react';
import { Link } from 'react-router-dom';
import {
  AgentPageShell,
  AgentQueryBoundary,
  useAgentInboxQuery,
} from './agentWorkspace';
import { buildTasks } from './agentSectionShared';

export function AgentTasksPage() {
  const inboxQuery = useAgentInboxQuery();

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading tasks...">
      <AgentPageShell title="Tasks" description="A focused task view built from live queue urgency, strategist review work, and campaigns waiting for client follow-up.">
        {(() => {
          const inbox = inboxQuery.data;
          if (!inbox) {
            return null;
          }
          const tasks = buildTasks(inbox);
          const columns = [
            { title: 'Urgent', items: tasks.urgent, tone: 'border-rose-200 bg-rose-50' },
            { title: 'Needs review', items: tasks.review, tone: 'border-brand/20 bg-brand-soft' },
            { title: 'Waiting on client', items: tasks.waiting, tone: 'border-amber-200 bg-amber-50' },
          ];
          return (
            <section className="grid gap-6 xl:grid-cols-3">
              {columns.map((column) => (
                <div key={column.title} className={`rounded-[28px] border ${column.tone} p-5`}>
                  <h3 className="text-lg font-semibold text-ink">{column.title}</h3>
                  <div className="mt-4 space-y-3">
                    {column.items.length > 0 ? column.items.map((item) => (
                      <div key={item.id} className="rounded-2xl border border-line bg-white p-4 text-sm">
                        <p className="font-semibold text-ink">{item.clientName}</p>
                        <p className="mt-1 text-xs text-ink-soft">{item.campaignName}</p>
                        <p className="mt-2 text-sm text-ink-soft">{item.nextAction}</p>
                        <div className="mt-3 flex justify-end gap-2">
                          <Link to={`/agent/campaigns/${item.id}`} className="button-secondary p-2" title={`View ${item.campaignName}`}>
                            <Eye className="size-4" />
                          </Link>
                          <Link to={`/agent/recommendations/new?campaignId=${item.id}`} className="button-secondary p-2" title={`Edit ${item.campaignName}`}>
                            <Pencil className="size-4" />
                          </Link>
                        </div>
                      </div>
                    )) : <p className="text-sm text-ink-soft">Nothing in this list right now.</p>}
                  </div>
                </div>
              ))}
            </section>
          );
        })()}
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}

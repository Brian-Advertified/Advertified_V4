import type { AgentInbox } from '../../../types/domain';
import { AgentInsightCard } from '../../../pages/agent/agentSectionShared';

export function AgentCampaignQueueSidebar({
  inbox,
  visibleCampaignCount,
}: {
  inbox: AgentInbox;
  visibleCampaignCount: number;
}) {
  return (
    <AgentInsightCard
      eyebrow="Queue guide"
      title="Keep the queue moving with less context switching."
      description="Use the queue to handle the next action first, then open the full campaign only when you need deeper context."
      tone="soft"
    >
      <div className="rounded-[18px] border border-brand/12 bg-white/80 px-4 py-4">
        <p className="text-sm font-semibold text-ink">Visible now</p>
        <p className="mt-2 text-sm leading-6 text-ink-soft">
          {visibleCampaignCount} campaign{visibleCampaignCount === 1 ? '' : 's'} in this view. {inbox.waitingOnClientCount} waiting on client, {inbox.urgentCount} urgent, {inbox.unassignedCount} unassigned.
        </p>
      </div>

      <div className="mt-5 space-y-3">
        {[
          {
            title: 'Urgent',
            body: 'Handle urgent, over-budget, and review-blocked work first so the queue does not stall.',
          },
          {
            title: 'Ownership',
            body: 'Assign or return ownership from the queue when possible so another operator can safely pick it up.',
          },
          {
            title: 'Escalate',
            body: 'Open the full campaign only when you need deeper context, planning detail, or a client-facing action.',
          },
        ].map((item) => (
          <div key={item.title} className="rounded-[18px] border border-line bg-white px-4 py-4">
            <p className="text-sm font-semibold text-ink">{item.title}</p>
            <p className="mt-2 text-sm leading-6 text-ink-soft">{item.body}</p>
          </div>
        ))}
      </div>
    </AgentInsightCard>
  );
}

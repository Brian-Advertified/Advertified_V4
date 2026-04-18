import type { AgentInbox } from '../../../types/domain';
import { AgentDetailStack, AgentInsightCard } from '../../../pages/agent/agentSectionShared';

export function AgentCampaignQueueSidebar({
  inbox,
  visibleCampaignCount,
}: {
  inbox: AgentInbox;
  visibleCampaignCount: number;
}) {
  return (
    <AgentInsightCard
      eyebrow="Queue support"
      title="Keep the queue moving without extra screens."
      description="Use this page to handle ownership, urgency, and the next action before opening the full campaign when you need deeper context."
      tone="soft"
    >
      <AgentDetailStack
        items={[
          { label: 'Visible now', value: visibleCampaignCount },
          { label: 'Urgent', value: inbox.urgentCount },
          { label: 'Unassigned', value: inbox.unassignedCount },
          { label: 'Waiting on client', value: inbox.waitingOnClientCount },
          { label: 'Stale', value: inbox.staleCount },
        ]}
      />

      <div className="mt-5 space-y-3">
        <div className="space-y-3">
          {[
            'Handle urgent, over-budget, and review-blocked items first.',
            'Take the next queue action before opening the full campaign workspace.',
            'Assign or return ownership from the queue so another operator can safely pick it up.',
          ].map((line, index) => (
            <div key={line} className="rounded-[18px] border border-line bg-white px-4 py-4">
              <p className="text-sm font-semibold text-ink">Step {index + 1}</p>
              <p className="mt-2 text-sm leading-6 text-ink-soft">{line}</p>
            </div>
          ))}
        </div>
      </div>
    </AgentInsightCard>
  );
}

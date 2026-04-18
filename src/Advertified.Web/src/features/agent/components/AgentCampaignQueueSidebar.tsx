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
    <div className="space-y-4">
      <AgentInsightCard
        eyebrow="Queue health"
        title="Keep the shared queue safe and moving."
        description="Use the queue first, then open deeper workflow screens only when the campaign needs extra context."
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
      </AgentInsightCard>

      <AgentInsightCard
        eyebrow="Operating rhythm"
        title="Minimal queue handling guide"
        description="A clean front door helps avoid missed ownership, stalled work, and duplicate follow-up."
      >
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
      </AgentInsightCard>
    </div>
  );
}

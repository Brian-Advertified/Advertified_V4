import type { ReactNode } from 'react';
import type { CampaignTimelineStep } from '../../../types/domain';
import { AgentCampaignProgressStrip } from './AgentCampaignProgressStrip';

type AgentCampaignWorkspaceOverviewProps = {
  campaignName: string;
  timeline?: CampaignTimelineStep[];
  actions?: ReactNode;
};

export function AgentCampaignWorkspaceOverview({
  campaignName,
  timeline,
  actions,
}: AgentCampaignWorkspaceOverviewProps) {
  return (
    <section className="panel border-brand/10 bg-white/90 px-6 py-6 sm:px-8">
      <div className="flex flex-col gap-5">
        <div className="flex flex-col gap-3">
          <div className="hero-kicker">Agent campaign workspace</div>
          <h1 className="text-3xl font-semibold tracking-tight text-ink sm:text-[2.2rem]">{campaignName}</h1>
        </div>

        <AgentCampaignProgressStrip timeline={timeline} />

        {actions ? <div className="flex flex-wrap gap-3">{actions}</div> : null}
      </div>
    </section>
  );
}

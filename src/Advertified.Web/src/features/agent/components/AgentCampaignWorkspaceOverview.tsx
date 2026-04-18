import type { ReactNode } from 'react';
import type { CampaignTimelineStep } from '../../../types/domain';
import { AgentCampaignProgressStrip } from './AgentCampaignProgressStrip';

type OverviewValue = {
  label: string;
  value: string;
};

type ProposalSummary = {
  title: string;
  optionLabel?: string;
  statusLabel: string;
  packageName: string;
  value: string;
  budgetUsed: string;
  channels: string;
};

type AuditSummary = {
  request: string;
  selected: string;
  rejected: string;
  policy: string;
  budget?: string;
  fallback?: string;
};

type AgentCampaignWorkspaceOverviewProps = {
  campaignName: string;
  ownershipLabel: string;
  whatToDo: string;
  primaryActions?: ReactNode;
  proposal: ProposalSummary;
  timeline?: CampaignTimelineStep[];
  clientSummary: ReactNode;
  aiSummary: ReactNode;
  campaignDetailsExtra?: ReactNode;
  audit?: AuditSummary | null;
  footerActions?: ReactNode;
};

function SummaryRow({ label, value }: OverviewValue) {
  return (
    <div className="flex items-start justify-between gap-4 border-b border-line/80 py-2 text-sm last:border-b-0">
      <span className="text-ink-soft">{label}</span>
      <span className="max-w-[70%] text-right font-semibold text-ink">{value}</span>
    </div>
  );
}

function WorkspaceCard({
  eyebrow,
  title,
  children,
  action,
}: {
  eyebrow: string;
  title: string;
  children: ReactNode;
  action?: ReactNode;
}) {
  return (
    <section className="rounded-[24px] border border-line bg-white px-5 py-5 shadow-[0_12px_30px_rgba(17,24,39,0.04)]">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">{eyebrow}</p>
          <h3 className="mt-2 text-lg font-semibold text-ink">{title}</h3>
        </div>
        {action ? <div className="shrink-0">{action}</div> : null}
      </div>
      <div className="mt-4">{children}</div>
    </section>
  );
}

function AuditItem({ title, body }: { title: string; body: string }) {
  return (
    <div className="rounded-[18px] border border-line bg-slate-50/75 px-4 py-4">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-ink-soft">{title}</p>
      <p className="mt-2 text-sm leading-6 text-ink-soft">{body}</p>
    </div>
  );
}

export function AgentCampaignWorkspaceOverview({
  campaignName,
  ownershipLabel,
  whatToDo,
  primaryActions,
  proposal,
  timeline,
  clientSummary,
  aiSummary,
  campaignDetailsExtra,
  audit,
  footerActions,
}: AgentCampaignWorkspaceOverviewProps) {
  return (
    <div className="space-y-5">
      <section className="panel border-brand/10 bg-white/90 px-6 py-6 sm:px-8">
        <div className="flex flex-col gap-5">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
            <div className="max-w-3xl">
              <div className="hero-kicker">Agent campaign workspace</div>
              <h1 className="mt-3 text-3xl font-semibold tracking-tight text-ink sm:text-[2.2rem]">{campaignName}</h1>
              <p className="mt-3 text-sm leading-7 text-ink-soft">
                Review the live proposal, keep the campaign moving, and only drop into deeper workflow tools when the next action truly needs it.
              </p>
            </div>
            <div className="self-start rounded-full border border-brand/15 bg-brand-soft px-3 py-1 text-xs font-semibold text-brand">
              {ownershipLabel}
            </div>
          </div>

          <AgentCampaignProgressStrip timeline={timeline} />

          <div className="grid gap-4 xl:grid-cols-[minmax(0,1.05fr)_340px]">
            <WorkspaceCard eyebrow="Recommendation" title={proposal.title} action={(
              <span className="rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
                {proposal.statusLabel}
              </span>
            )}>
              <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_220px]">
                <div>
                  {proposal.optionLabel ? (
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">{proposal.optionLabel}</p>
                  ) : null}
                  <p className="text-sm leading-7 text-ink-soft">{whatToDo}</p>
                  {primaryActions ? <div className="mt-4 flex flex-wrap items-center gap-3">{primaryActions}</div> : null}
                </div>
                <div className="rounded-[18px] border border-line bg-slate-50/75 px-4 py-4">
                  <SummaryRow label="Package" value={proposal.packageName} />
                  <SummaryRow label="Value" value={proposal.value} />
                  <SummaryRow label="Budget used" value={proposal.budgetUsed} />
                  <SummaryRow label="Channels" value={proposal.channels} />
                </div>
              </div>
            </WorkspaceCard>

            <WorkspaceCard eyebrow="What to do now" title="Keep this campaign moving">
              <div className="space-y-3">
                {[
                  'Review the queue and take the next action from this page first.',
                  'Use the proposal summary to check commercial fit before opening deeper tools.',
                  'Escalate into recommendation or execution screens only when the next action depends on them.',
                ].map((item, index) => (
                  <div key={item} className="rounded-[18px] border border-line bg-slate-50/75 px-4 py-4">
                    <p className="text-sm font-semibold text-ink">Step {index + 1}</p>
                    <p className="mt-2 text-sm leading-6 text-ink-soft">{item}</p>
                  </div>
                ))}
              </div>
            </WorkspaceCard>
          </div>
        </div>
      </section>

      <WorkspaceCard eyebrow="Campaign details" title="Commercial and planning context">
        <div className="grid gap-4 xl:grid-cols-2">
          <div className="rounded-[20px] border border-line bg-slate-50/75 px-5 py-5">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">Client</p>
            <div className="mt-3">{clientSummary}</div>
          </div>
          <div className="rounded-[20px] border border-line bg-slate-50/75 px-5 py-5">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-ink-soft">AI inputs</p>
            <div className="mt-3">{aiSummary}</div>
          </div>
        </div>
        {campaignDetailsExtra ? <div className="mt-4">{campaignDetailsExtra}</div> : null}
      </WorkspaceCard>

      {audit ? (
        <WorkspaceCard eyebrow="Engine audit" title="How the current proposal was shaped">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            <AuditItem title="Request" body={audit.request} />
            <AuditItem title="Selected" body={audit.selected} />
            <AuditItem title="Rejected" body={audit.rejected} />
            <AuditItem title="Policy" body={audit.policy} />
            {audit.budget ? <AuditItem title="Budget" body={audit.budget} /> : null}
            {audit.fallback ? <AuditItem title="Fallback" body={audit.fallback} /> : null}
          </div>
        </WorkspaceCard>
      ) : null}

      {footerActions ? (
        <div className="flex flex-wrap justify-end gap-3 border-t border-line/80 pt-4">
          {footerActions}
        </div>
      ) : null}
    </div>
  );
}

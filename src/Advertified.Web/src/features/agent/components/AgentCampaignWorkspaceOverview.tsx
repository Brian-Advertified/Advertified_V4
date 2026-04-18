import type { ReactNode } from 'react';
import { AgentCampaignProgressStrip } from './AgentCampaignProgressStrip';
import type { CampaignTimelineStep } from '../../../types/domain';

type OverviewValue = {
  label: string;
  value: string;
};

type ProposalSummary = {
  title: string;
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
    <div className="flex items-baseline justify-between gap-4 border-b border-line/80 py-2 text-sm last:border-b-0">
      <span className="text-ink-soft">{label}</span>
      <span className="text-right font-semibold text-ink">{value}</span>
    </div>
  );
}

function DisclosureSection({
  title,
  children,
  defaultOpen = false,
}: {
  title: string;
  children: ReactNode;
  defaultOpen?: boolean;
}) {
  return (
    <details className="rounded-[20px] border border-line bg-slate-50/70 px-4 py-3" open={defaultOpen}>
      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 text-sm font-semibold text-ink-soft">
        <span>{title}</span>
        <span className="text-[10px] text-ink-soft">v</span>
      </summary>
      <div className="pt-4">{children}</div>
    </details>
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
      <div className="panel border-brand/10 bg-white/85 px-6 py-6 sm:px-8">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <div className="hero-kicker">Agent review workspace</div>
              <h1 className="mt-3 text-3xl font-semibold tracking-tight text-ink">{campaignName}</h1>
            </div>
            <div className="self-start rounded-full border border-brand/15 bg-brand-soft px-3 py-1 text-xs font-semibold text-brand">
              {ownershipLabel}
            </div>
          </div>

          <AgentCampaignProgressStrip timeline={timeline} />

          <div className="grid gap-4 xl:grid-cols-2">
            <div className="rounded-[24px] border border-line bg-white px-5 py-5">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">What to do now</p>
              <p className="mt-3 text-sm leading-7 text-ink-soft">{whatToDo}</p>
              {primaryActions ? <div className="mt-4 flex flex-wrap items-center gap-3">{primaryActions}</div> : null}
            </div>

            <div className="rounded-[24px] border border-line bg-white px-5 py-5">
              <div className="flex items-center justify-between gap-3">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Approved proposal</p>
                <span className="rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700">
                  {proposal.statusLabel}
                </span>
              </div>
              <p className="mt-4 text-lg font-semibold text-ink">{proposal.title}</p>
              <div className="mt-3">
                <SummaryRow label="Package" value={proposal.packageName} />
                <SummaryRow label="Value" value={proposal.value} />
                <SummaryRow label="Budget used" value={proposal.budgetUsed} />
                <SummaryRow label="Channels" value={proposal.channels} />
              </div>
            </div>
          </div>
        </div>
      </div>

      <DisclosureSection title="Campaign details">
        <div className="grid gap-4 xl:grid-cols-2">
          <div className="rounded-[24px] border border-line bg-white px-5 py-5">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Client</p>
            <div className="mt-3">{clientSummary}</div>
          </div>
          <div className="rounded-[24px] border border-line bg-white px-5 py-5">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">AI inputs</p>
            <div className="mt-3">{aiSummary}</div>
          </div>
        </div>
        {campaignDetailsExtra ? <div className="mt-4">{campaignDetailsExtra}</div> : null}
      </DisclosureSection>

      {audit ? (
        <DisclosureSection title="Engine audit">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            <div className="rounded-[18px] bg-slate-50 px-4 py-4">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Request</p>
              <p className="mt-2 text-sm leading-6 text-ink-soft">{audit.request}</p>
            </div>
            <div className="rounded-[18px] bg-slate-50 px-4 py-4">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Selected</p>
              <p className="mt-2 text-sm leading-6 text-ink-soft">{audit.selected}</p>
            </div>
            <div className="rounded-[18px] bg-slate-50 px-4 py-4">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Rejected</p>
              <p className="mt-2 text-sm leading-6 text-ink-soft">{audit.rejected}</p>
            </div>
            <div className="rounded-[18px] bg-slate-50 px-4 py-4">
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Policy</p>
              <p className="mt-2 text-sm leading-6 text-ink-soft">{audit.policy}</p>
            </div>
            {audit.budget ? (
              <div className="rounded-[18px] bg-slate-50 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Budget</p>
                <p className="mt-2 text-sm leading-6 text-ink-soft">{audit.budget}</p>
              </div>
            ) : null}
            {audit.fallback ? (
              <div className="rounded-[18px] bg-slate-50 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-ink-soft">Fallback</p>
                <p className="mt-2 text-sm leading-6 text-ink-soft">{audit.fallback}</p>
              </div>
            ) : null}
          </div>
        </DisclosureSection>
      ) : null}

      {footerActions ? (
        <div className="flex flex-wrap justify-end gap-3 border-t border-line/80 pt-4">
          {footerActions}
        </div>
      ) : null}
    </div>
  );
}

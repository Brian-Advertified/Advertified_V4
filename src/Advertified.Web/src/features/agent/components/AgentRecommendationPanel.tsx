import { ArrowRightLeft, CircleAlert, CircleCheckBig, RefreshCcw, SlidersHorizontal } from 'lucide-react';
import { EmptyState } from '../../../components/ui/EmptyState';
import type { RefObject } from 'react';
import type { CampaignRecommendation, RecommendationItem, SelectedPlanInventoryItem } from '../../../types/domain';

type DisplayPlanItem = SelectedPlanInventoryItem | RecommendationItem;

export function AgentRecommendationPanel({
  activeRecommendation,
  recommendations,
  showRecommendationEditing,
  recommendationWorkflowLocked,
  recommendationTitle,
  lockedNextStep,
  activeProposalLabel,
  mixPanelRef,
  mixBalance,
  setMixBalance,
  targetMix,
  currentMix,
  displayedGroups,
  constraintChecks,
  isOverBudget,
  budgetWarning,
  canModifyPlan,
  selectedPlanItemsCount,
  onSelectRecommendation,
  onRegenerate,
  onAdjustMix,
  onReplace,
  onOpenInventory,
  formatChannelLabel,
  formatCurrency,
  formatMixSummary,
  formatConfidenceLabel,
  formatFallbackFlag,
}: {
  activeRecommendation?: CampaignRecommendation;
  recommendations: CampaignRecommendation[];
  showRecommendationEditing: boolean;
  recommendationWorkflowLocked: boolean;
  recommendationTitle: string;
  lockedNextStep: string;
  activeProposalLabel: string;
  mixPanelRef: RefObject<HTMLDivElement | null>;
  mixBalance: number;
  setMixBalance: (value: number) => void;
  targetMix: { ooh: number; radio: number; tv: number; digital: number };
  currentMix: { ooh: number; radio: number; tv: number; digital: number };
  displayedGroups: Record<string, DisplayPlanItem[]>;
  constraintChecks: Array<{ label: string; ok: boolean; detail: string }>;
  isOverBudget: boolean;
  budgetWarning?: string;
  canModifyPlan: boolean;
  selectedPlanItemsCount: number;
  onSelectRecommendation: (recommendationId: string) => void;
  onRegenerate: () => void;
  onAdjustMix: () => void;
  onReplace: (itemId: string) => void;
  onOpenInventory: () => void;
  formatChannelLabel: (value: string) => string;
  formatCurrency: (value: number) => string;
  formatMixSummary: (mix: { ooh: number; radio: number; tv: number; digital: number }) => string;
  formatConfidenceLabel: (value?: number) => string | null;
  formatFallbackFlag: (value: string) => string;
}) {
  return (
    <>
      {activeRecommendation?.audit ? (
        <div className="panel border-brand/15 bg-white px-6 py-5">
          <p className="text-sm font-semibold text-ink">Engine audit</p>
          <div className="mt-4 grid gap-3 md:grid-cols-2">
            <AuditLine label="Request" value={activeRecommendation.audit.requestSummary} />
            <AuditLine label="Selected" value={activeRecommendation.audit.selectionSummary} />
            <AuditLine label="Rejected" value={activeRecommendation.audit.rejectionSummary} />
            <AuditLine label="Policy" value={activeRecommendation.audit.policySummary} />
            <AuditLine label="Budget" value={activeRecommendation.audit.budgetSummary} />
            {activeRecommendation.audit.fallbackSummary ? (
              <AuditLine label="Fallback" value={activeRecommendation.audit.fallbackSummary} />
            ) : null}
          </div>
        </div>
      ) : null}

      {activeRecommendation?.clientFeedbackNotes ? (
        <div className="panel border-amber-200 bg-amber-50/80 px-6 py-5">
          <p className="text-sm font-semibold text-amber-800">Client feedback</p>
          <p className="mt-2 text-sm leading-7 text-amber-900">{activeRecommendation.clientFeedbackNotes}</p>
        </div>
      ) : null}

      {activeRecommendation?.manualReviewRequired ? (
        <div className="panel border-rose-200 bg-rose-50/80 px-6 py-5">
          <p className="text-sm font-semibold text-rose-800">Manual review required</p>
          <p className="mt-2 text-sm leading-7 text-rose-900">
            The planner could not fully satisfy package policy or inventory requirements for this draft.
          </p>
          {activeRecommendation.fallbackFlags.length > 0 ? (
            <div className="mt-3 flex flex-wrap gap-2">
              {activeRecommendation.fallbackFlags.map((flag) => (
                <span key={flag} className="rounded-full bg-white px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.12em] text-rose-700 ring-1 ring-rose-200">
                  {formatFallbackFlag(flag)}
                </span>
              ))}
            </div>
          ) : null}
        </div>
      ) : null}

      {showRecommendationEditing && recommendations.length > 1 ? (
        <div className="panel px-6 py-5">
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Proposal set</p>
          <div className="mt-4 flex flex-wrap gap-3">
            {recommendations.map((proposal) => {
              const isActive = proposal.id === activeRecommendation?.id;
              return (
                <button
                  key={proposal.id}
                  type="button"
                  onClick={() => onSelectRecommendation(proposal.id)}
                  className={`rounded-[18px] border px-4 py-3 text-left transition ${
                    isActive ? 'border-brand bg-brand-soft' : 'border-line bg-slate-50 hover:border-brand/30'
                  }`}
                >
                  <p className="text-sm font-semibold text-ink">{proposal.proposalLabel ?? 'Proposal'}</p>
                  <p className="mt-1 text-sm text-ink-soft">{proposal.proposalStrategy ?? 'Recommendation option'}</p>
                  <p className="mt-2 text-sm font-semibold text-ink">{formatCurrency(proposal.totalCost)}</p>
                  <p className="mt-1 text-xs uppercase tracking-[0.14em] text-ink-soft">{proposal.items.length} line item(s)</p>
                </button>
              );
            })}
          </div>
        </div>
      ) : null}

      <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <h2 className="text-xl font-semibold text-ink">{showRecommendationEditing ? 'Recommendation' : 'Next steps'}</h2>
            <p className="mt-2 text-sm text-ink-soft">{showRecommendationEditing ? recommendationTitle : lockedNextStep}</p>
          </div>
          {showRecommendationEditing ? (
            <div className="flex flex-wrap gap-2">
              <button type="button" onClick={onRegenerate} className="button-secondary inline-flex items-center gap-2 px-4 py-2">
                <RefreshCcw className="size-4" />
                Regenerate
              </button>
              <button type="button" onClick={onAdjustMix} className="button-secondary inline-flex items-center gap-2 px-4 py-2">
                <SlidersHorizontal className="size-4" />
                Adjust mix
              </button>
            </div>
          ) : null}
        </div>

        <div className="mt-6 space-y-5">
          {recommendationWorkflowLocked ? (
            <div className="grid gap-4 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
              <div className="rounded-[16px] border border-emerald-200 bg-emerald-50 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-emerald-700">Approved proposal</p>
                <p className="mt-3 text-lg font-semibold text-ink">{activeProposalLabel}</p>
                <p className="mt-1 text-sm text-ink-soft">{formatCurrency(activeRecommendation?.totalCost ?? 0)}</p>
                <p className="mt-3 text-sm leading-7 text-emerald-900">
                  Recommendation work is complete. Keep this page focused on production, delivery, and client follow-up.
                </p>
              </div>
              <div className="rounded-[16px] border border-line bg-slate-50 px-4 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand">What to do now</p>
                <p className="mt-3 text-sm leading-7 text-ink-soft">{lockedNextStep}</p>
                <div className="mt-4 rounded-[14px] border border-line bg-white px-4 py-3 text-sm leading-6 text-ink-soft">
                  Creative production is handled in the creative director workspace. Agents track progress here and use messages when they need to coordinate.
                </div>
              </div>
            </div>
          ) : null}

          <div ref={mixPanelRef} className={`rounded-[16px] border border-line bg-slate-50 px-4 py-4 ${showRecommendationEditing ? '' : 'hidden'}`}>
            <h3 className="text-sm font-semibold text-ink">Budget split</h3>
            <input
              type="range"
              min={0}
              max={100}
              value={mixBalance}
              onChange={(event) => setMixBalance(Number(event.target.value))}
              disabled={recommendationWorkflowLocked}
              className="mt-4 w-full accent-brand"
            />
            <p className="mt-3 text-sm text-ink-soft">Target mix: {formatMixSummary(targetMix)}</p>
            <p className="mt-1 text-sm text-ink-soft">Current draft: {formatMixSummary(currentMix)}</p>
          </div>

          {showRecommendationEditing && Object.entries(displayedGroups).length > 0 ? Object.entries(displayedGroups).map(([channel, items]) => (
            <div key={channel}>
              <p className="mb-3 text-sm font-semibold text-ink">{formatChannelLabel(channel)}</p>
              <div className="grid gap-2.5 md:grid-cols-2">
                {items.map((item) => (
                  <div key={item.id} className="group min-w-[210px] rounded-[16px] border border-line bg-slate-50 px-3.5 py-3">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="text-sm font-semibold text-ink">{'station' in item ? item.station : item.title}</p>
                        <p className="mt-1 text-xs text-ink-soft">
                          {'rate' in item
                            ? `${formatCurrency(item.rate * item.quantity)}${item.quantity > 1 ? ` | Qty ${item.quantity}` : ''}`
                            : `${formatCurrency(item.cost)}${item.quantity > 1 ? ` | Qty ${item.quantity}` : ''}`}
                        </p>
                        {!('station' in item) && item.confidenceScore !== undefined ? (
                          <p className="mt-1 text-[11px] font-medium uppercase tracking-[0.12em] text-brand">
                            {formatConfidenceLabel(item.confidenceScore)}
                          </p>
                        ) : null}
                        {!('station' in item) ? (
                          <>
                            <p className="mt-1 text-xs text-ink-soft line-clamp-2">{item.rationale}</p>
                            {item.selectionReasons.length > 0 ? (
                              <div className="mt-2 flex flex-wrap gap-1.5">
                                {item.selectionReasons.slice(0, 3).map((reason) => (
                                  <span key={reason} className="rounded-full bg-white px-2 py-1 text-[11px] text-ink-soft ring-1 ring-line">
                                    {reason}
                                  </span>
                                ))}
                              </div>
                            ) : null}
                          </>
                        ) : null}
                      </div>
                      {'station' in item && canModifyPlan ? (
                        <button
                          type="button"
                          onClick={() => onReplace(item.id)}
                          className="inline-flex items-center gap-1 rounded-full border border-brand/15 bg-white px-2.5 py-1 text-[11px] font-semibold text-brand transition group-hover:border-brand/30"
                        >
                          <ArrowRightLeft className="size-3" />
                          Replace
                        </button>
                      ) : (
                        <span className="inline-flex rounded-full border border-brand/15 bg-white px-2.5 py-1 text-[11px] font-semibold text-brand">
                          {'station' in item ? 'Locked' : 'AI draft'}
                        </span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )) : (
            <EmptyState
              title="No recommendation lines yet"
              description="Generate the recommendation first, or use the inventory table below to add radio, Billboards and Digital Screens, or digital lines manually."
            />
          )}
        </div>
      </div>

      {showRecommendationEditing ? (
        <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
          <h2 className="text-xl font-semibold text-ink">Validation</h2>
          <div className="mt-4 grid gap-3 md:grid-cols-3">
            {constraintChecks.map((check) => (
              <div
                key={check.label}
                className={`rounded-[14px] border px-4 py-3 ${
                  check.ok ? 'border-emerald-200 bg-emerald-50' : 'border-rose-200 bg-rose-50'
                }`}
              >
                <div className="flex items-start gap-3">
                  {check.ok ? (
                    <CircleCheckBig className="mt-0.5 size-4 text-emerald-700" />
                  ) : (
                    <CircleAlert className="mt-0.5 size-4 text-rose-700" />
                  )}
                  <div>
                    <p className={`text-sm font-semibold ${check.ok ? 'text-emerald-800' : 'text-rose-800'}`}>{check.label}</p>
                    <p className={`text-sm ${check.ok ? 'text-emerald-700' : 'text-rose-700'}`}>{check.detail}</p>
                  </div>
                </div>
              </div>
            ))}
          </div>
          {isOverBudget && budgetWarning ? <p className="mt-4 text-sm text-rose-700">{budgetWarning}</p> : null}
        </div>
      ) : null}

      {showRecommendationEditing ? (
        <button
          type="button"
          onClick={onOpenInventory}
          className="panel flex w-full items-center justify-between gap-4 px-6 py-5 text-left transition hover:border-brand/30"
        >
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Matching inventory</p>
            <p className="mt-2 text-sm leading-7 text-ink-soft">Click to open inventory, apply filters, and search supplier rows.</p>
          </div>
          <div className="rounded-full bg-brand-soft px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-brand">
            {selectedPlanItemsCount} selected
          </div>
        </button>
      ) : null}
    </>
  );
}

function AuditLine({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[14px] border border-line bg-slate-50 px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-ink-soft">{label}</p>
      <p className="mt-2 text-sm leading-6 text-ink">{value}</p>
    </div>
  );
}

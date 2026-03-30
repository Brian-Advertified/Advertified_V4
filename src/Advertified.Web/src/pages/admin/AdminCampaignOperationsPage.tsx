import { useState, type Dispatch, type SetStateAction } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PauseCircle, PlayCircle, RotateCcw } from 'lucide-react';
import { LoadingState } from '../../components/ui/LoadingState';
import { invalidateAdminOperationsQueries, queryKeys } from '../../lib/queryKeys';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AdminCampaignOperationsItem } from '../../types/domain';
import { AdminPageShell, fmtCurrency, fmtDate, titleize } from './adminWorkspace';

type DraftState = {
  amount: string;
  gatewayFee: string;
  refundReason: string;
  pauseReason: string;
  resumeReason: string;
};

export function AdminCampaignOperationsPage() {
  const queryClient = useQueryClient();
  const [selectedCampaignId, setSelectedCampaignId] = useState<string>();
  const [drafts, setDrafts] = useState<Record<string, DraftState>>({});

  const query = useQuery({
    queryKey: queryKeys.admin.campaignOperations,
    queryFn: advertifiedApi.getAdminCampaignOperations,
  });

  const pauseMutation = useMutation({
    mutationFn: ({ campaignId, reason }: { campaignId: string; reason?: string }) => advertifiedApi.pauseAdminCampaign(campaignId, reason),
    onSuccess: () => invalidateAdminOperationsQueries(queryClient),
  });

  const unpauseMutation = useMutation({
    mutationFn: ({ campaignId, reason }: { campaignId: string; reason?: string }) => advertifiedApi.unpauseAdminCampaign(campaignId, reason),
    onSuccess: () => invalidateAdminOperationsQueries(queryClient),
  });

  const refundMutation = useMutation({
    mutationFn: ({ campaignId, amount, gatewayFeeRetainedAmount, reason }: { campaignId: string; amount?: number; gatewayFeeRetainedAmount?: number; reason?: string }) =>
      advertifiedApi.refundAdminCampaign(campaignId, { amount, gatewayFeeRetainedAmount, reason }),
    onSuccess: () => invalidateAdminOperationsQueries(queryClient),
  });

  if (query.isLoading) {
    return (
      <AdminPageShell title="Campaign controls" description="Process refunds, pause campaigns, resume campaigns, and track days left with real operational data.">
        <LoadingState label="Loading campaign controls..." />
      </AdminPageShell>
    );
  }

  if (query.isError) {
    return (
      <AdminPageShell title="Campaign controls" description="Process refunds, pause campaigns, resume campaigns, and track days left with real operational data.">
        <div className="panel p-8">
          <h2 className="text-xl font-semibold text-ink">Campaign controls could not be loaded</h2>
          <p className="mt-3 text-sm leading-6 text-ink-soft">{query.error instanceof Error ? query.error.message : 'The operations controls are unavailable right now.'}</p>
        </div>
      </AdminPageShell>
    );
  }

  const items = query.data ?? [];
  const selected = items.find((item) => item.campaignId === selectedCampaignId) ?? items[0];
  const selectedDraft = selected ? getDraft(selected, drafts) : undefined;

  const pausedCount = items.filter((item) => item.isPaused).length;
  const refundAttentionCount = items.filter((item) => item.canProcessRefund && item.refundPolicyStage === 'post_delivery_or_live').length;
  const scheduledCount = items.filter((item) => item.daysLeft != null).length;

  return (
    <AdminPageShell
      title="Campaign controls"
      description="Process refunds, pause campaigns, resume campaigns, and keep campaign day counts accurate when operations are put on hold."
    >
      <section className="space-y-6">
        <div className="grid gap-4 md:grid-cols-3">
          <MetricCard label="Paused campaigns" value={String(pausedCount)} note="Campaigns currently frozen and not consuming remaining days." />
          <MetricCard label="Manual refund review" value={String(refundAttentionCount)} note="Delivered or live campaigns that need a deliberate refund amount." />
          <MetricCard label="Scheduled campaigns" value={String(scheduledCount)} note="Campaigns with enough timing data to calculate days left." />
        </div>

        <div className="grid gap-6 xl:grid-cols-[1.3fr_0.9fr]">
          <div className="panel overflow-hidden p-0">
            <div className="border-b border-line px-6 py-5">
              <h2 className="text-xl font-semibold text-ink">Operational queue</h2>
              <p className="mt-2 text-sm text-ink-soft">Select a campaign to process a refund, pause it, resume it, or review the current schedule impact.</p>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                  <tr>
                    <th className="px-4 py-4">Campaign</th>
                    <th className="px-4 py-4">Status</th>
                    <th className="px-4 py-4">Refund policy</th>
                    <th className="px-4 py-4">Days left</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr
                      key={item.campaignId}
                      className={`cursor-pointer border-t border-line transition ${selected?.campaignId === item.campaignId ? 'bg-brand-soft/50' : 'hover:bg-slate-50'}`}
                      onClick={() => setSelectedCampaignId(item.campaignId)}
                    >
                      <td className="px-4 py-4 align-top">
                        <p className="font-semibold text-ink">{item.campaignName}</p>
                        <p className="mt-1 text-xs text-ink-soft">{item.clientName} | {item.packageBandName}</p>
                        <p className="mt-1 text-xs text-ink-soft">Charged total {fmtCurrency(item.chargedTotal)}</p>
                      </td>
                      <td className="px-4 py-4 align-top">
                        <div className="space-y-2">
                          <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${item.isPaused ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-emerald-200 bg-emerald-50 text-emerald-700'}`}>
                            {item.isPaused ? 'Paused' : titleize(item.campaignStatus)}
                          </span>
                          <p className="text-xs text-ink-soft">Refund {titleize(item.refundStatus)}</p>
                        </div>
                      </td>
                      <td className="px-4 py-4 align-top">
                        <p className="font-semibold text-ink">{item.refundPolicyLabel}</p>
                        <p className="mt-1 text-xs leading-5 text-ink-soft">{item.refundPolicySummary}</p>
                      </td>
                      <td className="px-4 py-4 align-top text-ink-soft">
                        {item.daysLeft != null ? `${item.daysLeft} day(s)` : 'Not scheduled'}
                      </td>
                    </tr>
                  ))}
                  {items.length === 0 ? (
                    <tr>
                      <td className="px-4 py-8 text-sm text-ink-soft" colSpan={4}>No campaign operations data is available yet.</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          </div>

          <div className="space-y-6">
            {selected ? (
              <>
                <div className="panel p-6">
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Selected campaign</p>
                      <h2 className="mt-3 text-2xl font-semibold text-ink">{selected.campaignName}</h2>
                      <p className="mt-2 text-sm text-ink-soft">{selected.clientName} | {selected.clientEmail}</p>
                    </div>
                    <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${selected.isPaused ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-emerald-200 bg-emerald-50 text-emerald-700'}`}>
                      {selected.isPaused ? 'Paused' : titleize(selected.campaignStatus)}
                    </span>
                  </div>

                  <div className="mt-6 grid gap-3 text-sm text-ink-soft">
                    <InfoRow label="Selected budget" value={fmtCurrency(selected.selectedBudget)} />
                    <InfoRow label="Charged total" value={fmtCurrency(selected.chargedTotal)} />
                    <InfoRow label="Refunded so far" value={fmtCurrency(selected.refundedAmount)} />
                    <InfoRow label="Remaining collected" value={fmtCurrency(selected.remainingCollectedAmount)} />
                    <InfoRow label="Start date" value={selected.startDate ? formatDateOnly(selected.startDate) : 'Not set'} />
                    <InfoRow label="Effective end" value={selected.effectiveEndDate ? formatDateOnly(selected.effectiveEndDate) : 'Not set'} />
                    <InfoRow label="Days left" value={selected.daysLeft != null ? `${selected.daysLeft} day(s)` : 'Not scheduled'} />
                    <InfoRow label="Paused days added" value={`${selected.totalPausedDays} day(s)`} />
                  </div>
                </div>

                <div className="panel p-6">
                  <div className="flex items-center gap-3">
                    <RotateCcw className="size-5 text-brand" />
                    <div>
                      <h3 className="text-lg font-semibold text-ink">Refund controls</h3>
                      <p className="mt-1 text-sm text-ink-soft">{selected.refundPolicyLabel}</p>
                    </div>
                  </div>

                  <p className="mt-4 text-sm leading-6 text-ink-soft">{selected.refundPolicySummary}</p>

                  <div className="mt-5 grid gap-3 text-sm text-ink-soft">
                    <InfoRow label="Suggested refund" value={fmtCurrency(selected.suggestedRefundAmount)} />
                    <InfoRow label="Maximum manual refund" value={fmtCurrency(selected.maxManualRefundAmount)} />
                    <InfoRow label="Gateway fee retained" value={fmtCurrency(selected.gatewayFeeRetainedAmount)} />
                  </div>

                  <div className="mt-5 space-y-3">
                    <label className="block text-sm font-semibold text-ink">
                      Refund amount
                      <input
                        className="input-base mt-2"
                        type="number"
                        min="0"
                        step="0.01"
                        value={selectedDraft?.amount ?? ''}
                        onChange={(event) => updateDraft(selected, drafts, setDrafts, 'amount', event.target.value)}
                      />
                    </label>
                    <label className="block text-sm font-semibold text-ink">
                      Retained gateway fee
                      <input
                        className="input-base mt-2"
                        type="number"
                        min="0"
                        step="0.01"
                        value={selectedDraft?.gatewayFee ?? ''}
                        onChange={(event) => updateDraft(selected, drafts, setDrafts, 'gatewayFee', event.target.value)}
                      />
                    </label>
                    <label className="block text-sm font-semibold text-ink">
                      Refund reason
                      <textarea
                        className="input-base mt-2 min-h-28"
                        value={selectedDraft?.refundReason ?? ''}
                        onChange={(event) => updateDraft(selected, drafts, setDrafts, 'refundReason', event.target.value)}
                      />
                    </label>
                    <button
                      type="button"
                      className="button-primary inline-flex w-full items-center justify-center rounded-full"
                      disabled={!selected.canProcessRefund || refundMutation.isPending}
                      onClick={() => {
                        if (!window.confirm(`Process refund for ${selected.campaignName}?`)) {
                          return;
                        }

                        refundMutation.mutate({
                          campaignId: selected.campaignId,
                          amount: parseOptionalNumber(selectedDraft?.amount),
                          gatewayFeeRetainedAmount: parseOptionalNumber(selectedDraft?.gatewayFee),
                          reason: emptyToUndefined(selectedDraft?.refundReason),
                        });
                      }}
                    >
                      Process refund
                    </button>
                  </div>
                </div>

                <div className="panel p-6">
                  <h3 className="text-lg font-semibold text-ink">Pause controls</h3>
                  <p className="mt-2 text-sm text-ink-soft">Pausing a campaign freezes the remaining day count until the campaign is resumed.</p>

                  <div className="mt-5 space-y-4">
                    <label className="block text-sm font-semibold text-ink">
                      Pause reason
                      <textarea
                        className="input-base mt-2 min-h-24"
                        value={selectedDraft?.pauseReason ?? ''}
                        onChange={(event) => updateDraft(selected, drafts, setDrafts, 'pauseReason', event.target.value)}
                      />
                    </label>
                    <button
                      type="button"
                      className="button-secondary inline-flex w-full items-center justify-center gap-2 rounded-full"
                      disabled={!selected.canPause || pauseMutation.isPending}
                      onClick={() => pauseMutation.mutate({ campaignId: selected.campaignId, reason: emptyToUndefined(selectedDraft?.pauseReason) })}
                    >
                      <PauseCircle className="size-4" />
                      Pause campaign
                    </button>

                    <label className="block text-sm font-semibold text-ink">
                      Resume note
                      <textarea
                        className="input-base mt-2 min-h-24"
                        value={selectedDraft?.resumeReason ?? ''}
                        onChange={(event) => updateDraft(selected, drafts, setDrafts, 'resumeReason', event.target.value)}
                      />
                    </label>
                    <button
                      type="button"
                      className="button-primary inline-flex w-full items-center justify-center gap-2 rounded-full"
                      disabled={!selected.canUnpause || unpauseMutation.isPending}
                      onClick={() => unpauseMutation.mutate({ campaignId: selected.campaignId, reason: emptyToUndefined(selectedDraft?.resumeReason) })}
                    >
                      <PlayCircle className="size-4" />
                      Resume campaign
                    </button>
                  </div>

                  {selected.pauseReason ? <p className="mt-4 text-xs leading-5 text-ink-soft">Latest pause note: {selected.pauseReason}</p> : null}
                  {selected.pausedAt ? <p className="mt-2 text-xs leading-5 text-ink-soft">Paused at {fmtDate(selected.pausedAt)}</p> : null}
                </div>
              </>
            ) : (
              <div className="panel p-8">
                <h2 className="text-xl font-semibold text-ink">No campaigns yet</h2>
                <p className="mt-3 text-sm leading-6 text-ink-soft">Campaign operations will appear here once paid campaigns exist in the system.</p>
              </div>
            )}
          </div>
        </div>
      </section>
    </AdminPageShell>
  );
}

function MetricCard({ label, value, note }: { label: string; value: string; note: string }) {
  return (
    <div className="panel p-6">
      <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{label}</p>
      <p className="mt-4 text-4xl font-semibold text-ink">{value}</p>
      <p className="mt-3 text-sm leading-6 text-ink-soft">{note}</p>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-2xl border border-line px-4 py-3">
      <span>{label}</span>
      <span className="font-semibold text-ink">{value}</span>
    </div>
  );
}

function getDraft(item: AdminCampaignOperationsItem, drafts: Record<string, DraftState>): DraftState {
  return drafts[item.campaignId] ?? {
    amount: item.suggestedRefundAmount > 0 ? String(item.suggestedRefundAmount) : '',
    gatewayFee: item.gatewayFeeRetainedAmount > 0 ? String(item.gatewayFeeRetainedAmount) : '',
    refundReason: item.refundReason ?? '',
    pauseReason: item.pauseReason ?? '',
    resumeReason: '',
  };
}

function updateDraft(
  item: AdminCampaignOperationsItem,
  drafts: Record<string, DraftState>,
  setDrafts: Dispatch<SetStateAction<Record<string, DraftState>>>,
  field: keyof DraftState,
  value: string,
) {
  const current = getDraft(item, drafts);
  setDrafts((existing) => ({
    ...existing,
    [item.campaignId]: {
      ...current,
      [field]: value,
    },
  }));
}

function parseOptionalNumber(value?: string) {
  if (!value || !value.trim()) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function emptyToUndefined(value?: string) {
  if (!value || !value.trim()) {
    return undefined;
  }

  return value.trim();
}

function formatDateOnly(value: string) {
  return new Intl.DateTimeFormat('en-ZA', { dateStyle: 'medium' }).format(new Date(`${value}T00:00:00`));
}

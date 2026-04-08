import { useEffect, useState, type Dispatch, type SetStateAction } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PauseCircle, PlayCircle, RotateCcw } from 'lucide-react';
import { Link } from 'react-router-dom';
import { LoadingState } from '../../components/ui/LoadingState';
import { invalidateAdminOperationsQueries, queryKeys } from '../../lib/queryKeys';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AdminCampaignOperationsItem, AdminCampaignOperationsSort } from '../../types/domain';
import { AdminPageShell, fmtCurrency, fmtDate, titleize } from './adminWorkspace';

type DraftState = {
  amount: string;
  gatewayFee: string;
  refundReason: string;
  pauseReason: string;
  resumeReason: string;
};

export function AdminCampaignOperationsPage() {
  const pageSize = 25;
  const queryClient = useQueryClient();
  const [selectedCampaignId, setSelectedCampaignId] = useState<string>();
  const [editorState, setEditorState] = useState<{ campaignId?: string; isOpen: boolean }>({ isOpen: false });
  const [drafts, setDrafts] = useState<Record<string, DraftState>>({});
  const [page, setPage] = useState(1);
  const [queueSort, setQueueSort] = useState<AdminCampaignOperationsSort>('delivery_risk');
  const [attentionOnly, setAttentionOnly] = useState(false);

  const query = useQuery({
    queryKey: queryKeys.admin.campaignOperations(page, pageSize, queueSort, attentionOnly),
    queryFn: () => advertifiedApi.getAdminCampaignOperations({ page, pageSize, sortBy: queueSort, attentionOnly }),
  });
  const queueItems = query.data?.items ?? [];
  const resolvedSelectedCampaign = queueItems.find((item) => item.campaignId === selectedCampaignId) ?? queueItems[0];
  const selectedCampaignIdForPerformance = resolvedSelectedCampaign?.campaignId;
  const performanceQuery = useQuery({
    queryKey: queryKeys.admin.campaignPerformance(selectedCampaignIdForPerformance ?? 'none'),
    queryFn: () => advertifiedApi.getAdminCampaignPerformance(selectedCampaignIdForPerformance!),
    enabled: Boolean(selectedCampaignIdForPerformance),
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

  const selected = resolvedSelectedCampaign;
  const selectedPerformance = selected && selectedCampaignIdForPerformance === selected.campaignId
    ? performanceQuery.data
    : undefined;
  const isEditorOpen = Boolean(selected?.campaignId && editorState.isOpen && editorState.campaignId === selected.campaignId);
  const selectedDraft = selected ? getDraft(selected, drafts) : undefined;

  useEffect(() => {
    const effectivePage = query.data?.page;
    if (effectivePage && effectivePage !== page) {
      setPage(effectivePage);
    }
  }, [query.data?.page, page]);

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

  const pausedCount = query.data?.totalPausedCount ?? 0;
  const refundAttentionCount = query.data?.totalRefundAttentionCount ?? 0;
  const scheduledCount = query.data?.totalScheduledCount ?? 0;
  const performanceAttentionCount = query.data?.totalPerformanceAttentionCount ?? 0;
  const performanceAttentionThresholdPercent = query.data?.performanceAttentionThresholdPercent ?? 60;

  return (
    <AdminPageShell
      title="Campaign controls"
      description="Process refunds, pause campaigns, resume campaigns, and keep campaign day counts accurate when operations are put on hold."
    >
      <section className="space-y-6">
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <MetricCard label="Paused campaigns" value={String(pausedCount)} note="Campaigns currently frozen and not consuming remaining days." />
          <MetricCard label="Manual refund review" value={String(refundAttentionCount)} note="Delivered or live campaigns that need a deliberate refund amount." />
          <MetricCard label="Scheduled campaigns" value={String(scheduledCount)} note="Campaigns with enough timing data to calculate days left." />
          <MetricCard
            label="Performance attention"
            value={String(performanceAttentionCount)}
            note={`Booked campaigns with no report yet or delivery below ${performanceAttentionThresholdPercent}%.`}
            actionLabel={attentionOnly ? 'Showing filtered queue' : 'Show attention queue'}
            onClick={() => {
              setAttentionOnly(true);
              setPage(1);
            }}
          />
        </div>

        <div className="grid gap-6 xl:grid-cols-[1.3fr_0.9fr]">
          <div className="panel overflow-hidden p-0">
            <div className="border-b border-line px-6 py-5">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h2 className="text-xl font-semibold text-ink">Operational queue</h2>
                  <p className="mt-2 text-sm text-ink-soft">Select a campaign to process a refund, pause it, resume it, or review the current schedule impact.</p>
                </div>
                <div className="flex flex-wrap items-center gap-3">
                  <label className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">
                    Sort
                    <select
                      className="input-base mt-2 min-w-[210px] bg-white py-2 text-sm font-medium normal-case tracking-normal text-ink"
                      value={queueSort}
                      onChange={(event) => {
                        setQueueSort(event.target.value as AdminCampaignOperationsSort);
                        setPage(1);
                      }}
                    >
                      <option value="delivery_risk">Delivery risk (worst first)</option>
                      <option value="highest_spend">Highest spend</option>
                      <option value="latest_update">Latest update</option>
                      <option value="campaign_name">Campaign name</option>
                    </select>
                  </label>
                  <label className="inline-flex items-center gap-2 rounded-full border border-line px-3 py-2 text-xs font-semibold uppercase tracking-[0.12em] text-ink-soft">
                    <input
                      type="checkbox"
                      checked={attentionOnly}
                      onChange={(event) => {
                        setAttentionOnly(event.target.checked);
                        setPage(1);
                      }}
                    />
                    Attention only
                  </label>
                  {attentionOnly ? (
                    <button
                      type="button"
                      className="button-secondary rounded-full px-3 py-1.5 text-xs"
                      onClick={() => {
                        setAttentionOnly(false);
                        setPage(1);
                      }}
                    >
                      Clear filter
                    </button>
                  ) : null}
                </div>
              </div>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                  <tr>
                    <th className="px-4 py-4">Campaign</th>
                    <th className="px-4 py-4">Status</th>
                    <th className="px-4 py-4">Performance</th>
                    <th className="px-4 py-4">Refund policy</th>
                    <th className="px-4 py-4">Days left</th>
                  </tr>
                </thead>
                <tbody>
                  {queueItems.map((item) => (
                    <tr
                      key={item.campaignId}
                      className={`cursor-pointer border-t border-line transition ${selected?.campaignId === item.campaignId ? 'bg-brand-soft/50' : 'hover:bg-slate-50'}`}
                      onClick={() => {
                        setSelectedCampaignId(item.campaignId);
                        setEditorState({ campaignId: item.campaignId, isOpen: false });
                      }}
                    >
                      <td className="px-4 py-4 align-top">
                        <p className="font-semibold text-ink">{item.campaignName}</p>
                        <p className="mt-1 text-xs text-ink-soft">{item.clientName} | {item.packageBandName}</p>
                        <p className="mt-1 text-xs text-ink-soft">Charged total {fmtCurrency(item.chargedTotal)}</p>
                      </td>
                      <td className="px-4 py-4 align-top">
                        <div className="space-y-2">
                          <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${item.isPaused ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-brand/20 bg-brand-soft text-brand'}`}>
                            {item.isPaused ? 'Paused' : titleize(item.campaignStatus)}
                          </span>
                          <p className="text-xs text-ink-soft">Refund {titleize(item.refundStatus)}</p>
                        </div>
                      </td>
                      <td className="px-4 py-4 align-top">
                        <div className="space-y-1 text-xs text-ink-soft">
                          <p>
                            Spend {fmtCurrency(item.performanceDeliveredSpend)} / {fmtCurrency(item.performanceBookedSpend)} ({item.performanceDeliveryPercent}%)
                          </p>
                          <p>
                            Impr {item.performanceImpressions.toLocaleString('en-ZA')} | Clicks/spots {item.performancePlaysOrSpots.toLocaleString('en-ZA')}
                          </p>
                          {item.performanceLatestReportDate ? (
                            <p>Updated {formatDateOnly(item.performanceLatestReportDate)}</p>
                          ) : (
                            <p>No report yet</p>
                          )}
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
                  {queueItems.length === 0 ? (
                    <tr>
                      <td className="px-4 py-8 text-sm text-ink-soft" colSpan={5}>No campaigns match the current queue filter.</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
            <div className="flex items-center justify-between border-t border-line px-6 py-4 text-xs text-ink-soft">
              <span>
                Showing {queueItems.length} of {query.data?.totalCount ?? 0}
              </span>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  className="button-secondary rounded-full px-3 py-1.5"
                  disabled={!(query.data?.hasPreviousPage ?? false)}
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                >
                  Previous
                </button>
                <span>
                  Page {query.data?.page ?? page} of {query.data?.totalPages ?? 1}
                </span>
                <button
                  type="button"
                  className="button-secondary rounded-full px-3 py-1.5"
                  disabled={!(query.data?.hasNextPage ?? false)}
                  onClick={() => setPage((current) => current + 1)}
                >
                  Next
                </button>
              </div>
            </div>
          </div>

          <div className="space-y-6">
            {selected ? (
              <>
                <div className="panel p-6">
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <h2 className="text-2xl font-semibold text-ink">{selected.campaignName}</h2>
                      <p className="mt-2 text-sm text-ink-soft">{selected.clientName} | {selected.clientEmail}</p>
                      <p className="mt-1 text-xs text-ink-soft">
                        Status {selected.isPaused ? 'Paused' : titleize(selected.campaignStatus)} | Refund {titleize(selected.refundStatus)}
                      </p>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <Link to={`/campaigns/${selected.campaignId}`} className="button-secondary rounded-full px-4 py-2">
                        View campaign
                      </Link>
                      <button
                        type="button"
                        className="button-primary rounded-full px-4 py-2"
                        onClick={() => setEditorState((current) => ({
                          campaignId: selected.campaignId,
                          isOpen: current.campaignId === selected.campaignId ? !current.isOpen : true,
                        }))}
                      >
                        {isEditorOpen ? 'Close edit' : 'Edit'}
                      </button>
                    </div>
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
                  <div className="mb-4 flex items-center justify-between gap-3">
                    <h3 className="text-lg font-semibold text-ink">Performance snapshot</h3>
                    <span className="text-xs text-ink-soft">
                      {selectedPerformance?.latestReportDate ? `Updated ${formatDateOnly(selectedPerformance.latestReportDate)}` : 'No updates yet'}
                    </span>
                  </div>
                  {performanceQuery.isLoading ? (
                    <p className="text-sm text-ink-soft">Loading performance...</p>
                  ) : selectedPerformance ? (
                    <div className="grid gap-3 text-sm text-ink-soft">
                      <InfoRow label="Booked spend" value={fmtCurrency(selectedPerformance.totalBookedSpend)} />
                      <InfoRow label="Delivered spend" value={fmtCurrency(selectedPerformance.totalDeliveredSpend)} />
                      <InfoRow label="Delivery %" value={`${selectedPerformance.spendDeliveryPercent}%`} />
                      <InfoRow label="Impressions" value={selectedPerformance.totalImpressions.toLocaleString('en-ZA')} />
                      <InfoRow label="Clicks / spots" value={selectedPerformance.totalPlaysOrSpots.toLocaleString('en-ZA')} />
                      <InfoRow label="Synced clicks" value={selectedPerformance.totalSyncedClicks.toLocaleString('en-ZA')} />
                    </div>
                  ) : (
                    <p className="text-sm text-ink-soft">No performance data for this campaign yet.</p>
                  )}
                </div>

                {isEditorOpen ? (
                  <div className="panel p-6">
                    <div className="flex items-center gap-3">
                      <RotateCcw className="size-5 text-brand" />
                      <div>
                        <h3 className="text-lg font-semibold text-ink">Edit campaign</h3>
                        <p className="mt-1 text-sm text-ink-soft">Use one place to refund, pause, or resume this campaign.</p>
                      </div>
                    </div>

                    <div className="mt-5 grid gap-6 xl:grid-cols-2">
                      <div className="space-y-4 rounded-[24px] border border-line p-5">
                        <div>
                          <p className="text-sm font-semibold text-ink">Refund</p>
                          <p className="mt-1 text-sm leading-6 text-ink-soft">{selected.refundPolicySummary}</p>
                        </div>
                        <div className="grid gap-3 text-sm text-ink-soft">
                          <InfoRow label="Suggested refund" value={fmtCurrency(selected.suggestedRefundAmount)} />
                          <InfoRow label="Maximum manual refund" value={fmtCurrency(selected.maxManualRefundAmount)} />
                          <InfoRow label="Gateway fee retained" value={fmtCurrency(selected.gatewayFeeRetainedAmount)} />
                        </div>
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

                      <div className="space-y-4 rounded-[24px] border border-line p-5">
                        <div>
                          <p className="text-sm font-semibold text-ink">Pause or resume</p>
                          <p className="mt-1 text-sm leading-6 text-ink-soft">Pausing freezes the remaining day count until the campaign is resumed.</p>
                        </div>
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

                        {selected.pauseReason ? <p className="text-xs leading-5 text-ink-soft">Latest pause note: {selected.pauseReason}</p> : null}
                        {selected.pausedAt ? <p className="text-xs leading-5 text-ink-soft">Paused at {fmtDate(selected.pausedAt)}</p> : null}
                      </div>
                    </div>
                  </div>
                ) : null}
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

function MetricCard({
  label,
  value,
  note,
  onClick,
  actionLabel,
}: {
  label: string;
  value: string;
  note: string;
  onClick?: () => void;
  actionLabel?: string;
}) {
  return (
    <div className="panel p-6">
      <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{label}</p>
      <p className="mt-4 text-4xl font-semibold text-ink">{value}</p>
      <p className="mt-3 text-sm leading-6 text-ink-soft">{note}</p>
      {onClick ? (
        <button
          type="button"
          className="button-secondary mt-4 rounded-full px-3 py-1.5 text-xs"
          onClick={onClick}
        >
          {actionLabel ?? 'View'}
        </button>
      ) : null}
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

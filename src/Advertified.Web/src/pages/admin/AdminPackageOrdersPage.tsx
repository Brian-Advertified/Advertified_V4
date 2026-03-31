import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download } from 'lucide-react';
import { LoadingState } from '../../components/ui/LoadingState';
import { useToast } from '../../components/ui/toast';
import { invalidateAdminOperationsQueries, queryKeys } from '../../lib/queryKeys';
import { advertifiedApi } from '../../services/advertifiedApi';
import { AdminPageShell, fmtCurrency, titleize } from './adminWorkspace';
import { AdminPackageOrderEditModal } from './AdminPackageOrderEditModal';

export function AdminPackageOrdersPage() {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [editingOrderId, setEditingOrderId] = useState<string | null>(null);

  const query = useQuery({
    queryKey: queryKeys.admin.packageOrders,
    queryFn: advertifiedApi.getAdminPackageOrders,
  });

  const updateMutation = useMutation({
    mutationFn: (input: {
      orderId: string;
      paymentStatus: 'paid' | 'failed';
      paymentReference?: string;
      notes: string;
      file: File;
    }) => advertifiedApi.updateAdminPackageOrderPaymentStatus(input),
    onSuccess: async (_, variables) => {
      await invalidateAdminOperationsQueries(queryClient);
      const refreshed = await queryClient.fetchQuery({
        queryKey: queryKeys.admin.packageOrders,
        queryFn: advertifiedApi.getAdminPackageOrders,
      });

      const nextPending = refreshed.find((item) => item.canUpdateLulaStatus && item.orderId !== variables.orderId);
      if (nextPending) {
        setEditingOrderId(nextPending.orderId);
        pushToast({
          title: 'Payment status saved.',
          description: 'Opening the next pending Lula order in the queue.',
        });
      } else {
        setEditingOrderId(null);
        pushToast({
          title: 'Payment status saved.',
          description: 'No more pending Lula orders in the queue.',
        });
      }
    },
    onError: (error) => pushToast({
      title: 'Could not update payment status.',
      description: error instanceof Error ? error.message : 'Please try again.',
    }, 'error'),
  });

  const items = query.data ?? [];
  const editingOrder = items.find((item) => item.orderId === editingOrderId) ?? null;

  const pendingLulaCount = items.filter((item) => item.canUpdateLulaStatus).length;
  const paidCount = items.filter((item) => item.paymentStatus === 'paid').length;
  const failedCount = items.filter((item) => item.paymentStatus === 'failed').length;

  if (query.isLoading) {
    return (
      <AdminPageShell title="Payments & Orders" description="Review every package order, invoice, and Lula settlement from one operational queue.">
        <LoadingState label="Loading payments and orders..." />
      </AdminPageShell>
    );
  }

  if (query.isError) {
    return (
      <AdminPageShell title="Payments & Orders" description="Review every package order, invoice, and Lula settlement from one operational queue.">
        <div className="panel p-8">
          <h2 className="text-xl font-semibold text-ink">Payments and orders could not be loaded</h2>
          <p className="mt-3 text-sm leading-6 text-ink-soft">{query.error instanceof Error ? query.error.message : 'The admin payments workspace is unavailable right now.'}</p>
        </div>
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell
      title="Payments & Orders"
      description="Track package orders, review invoice state, and open the Lula settlement editor when a pending order needs manual intervention."
    >
      <section className="space-y-6">
        <div className="grid gap-4 md:grid-cols-4">
          <MetricCard label="Total orders" value={String(items.length)} note="Every package order across all payment providers." />
          <MetricCard label="Pending Lula" value={String(pendingLulaCount)} note="Lula orders waiting for manual review." />
          <MetricCard label="Paid orders" value={String(paidCount)} note="Orders already converted into paid campaigns." />
          <MetricCard label="Declined orders" value={String(failedCount)} note="Orders manually declined or failed." />
        </div>

        <div className="panel overflow-hidden p-0">
          <div className="border-b border-line px-6 py-5">
            <h2 className="text-xl font-semibold text-ink">Order queue</h2>
            <p className="mt-2 text-sm text-ink-soft">Only pending Lula orders can be edited here. VodaPay orders are read-only and handled by the gateway workflow.</p>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-sm">
              <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft">
                <tr>
                  <th className="px-4 py-4">Customer</th>
                  <th className="px-4 py-4">Package</th>
                  <th className="px-4 py-4">Provider</th>
                  <th className="px-4 py-4">Status</th>
                  <th className="px-4 py-4">Invoice</th>
                  <th className="px-4 py-4 text-right">Action</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.orderId} className="border-t border-line hover:bg-slate-50">
                    <td className="px-4 py-4 align-top">
                      <p className="font-semibold text-ink">{item.clientName}</p>
                      <p className="mt-1 text-xs text-ink-soft">{item.clientEmail}</p>
                      <p className="mt-1 text-xs text-ink-soft">{item.clientPhone || 'Phone not captured'}</p>
                    </td>
                    <td className="px-4 py-4 align-top">
                      <p className="font-semibold text-ink">{item.packageBandName}</p>
                      <p className="mt-1 text-xs text-ink-soft">Budget {fmtCurrency(item.selectedBudget)}</p>
                      <p className="mt-1 text-xs text-ink-soft">Charge {fmtCurrency(item.chargedAmount)}</p>
                    </td>
                    <td className="px-4 py-4 align-top text-ink-soft">{titleize(item.paymentProvider)}</td>
                    <td className="px-4 py-4 align-top">
                      <div className="space-y-2">
                        <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${statusTone(item.paymentStatus)}`}>
                          {titleize(item.paymentStatus)}
                        </span>
                        {item.canUpdateLulaStatus ? <p className="text-xs text-ink-soft">Ready for Lula update</p> : null}
                      </div>
                    </td>
                    <td className="px-4 py-4 align-top text-ink-soft">
                      {item.invoiceStatus ? titleize(item.invoiceStatus) : 'No invoice yet'}
                    </td>
                    <td className="px-4 py-4 align-top text-right">
                      {item.canUpdateLulaStatus ? (
                        <button
                          type="button"
                          className="button-secondary rounded-full"
                          onClick={() => setEditingOrderId(item.orderId)}
                        >
                          Edit
                        </button>
                      ) : item.paymentStatus === 'paid' && item.invoicePdfUrl ? (
                        <button
                          type="button"
                          className="button-secondary inline-flex items-center gap-2 rounded-full"
                          onClick={() => advertifiedApi.downloadProtectedFile(item.invoicePdfUrl!, `${item.invoiceId ?? item.orderId}-invoice.pdf`)}
                        >
                          <Download className="size-4" />
                          Paid invoice
                        </button>
                      ) : (
                        <span className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">
                          Read only
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
                {items.length === 0 ? (
                  <tr>
                    <td className="px-4 py-8 text-sm text-ink-soft" colSpan={6}>No package orders are available yet.</td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <AdminPackageOrderEditModal
        order={editingOrder}
        isOpen={editingOrder != null}
        isSaving={updateMutation.isPending}
        onClose={() => setEditingOrderId(null)}
        onSave={(input) => updateMutation.mutate(input)}
      />
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

function statusTone(status: string) {
  if (status === 'paid') {
    return 'border-brand/20 bg-brand-soft text-brand';
  }

  if (status === 'failed') {
    return 'border-rose-200 bg-rose-50 text-rose-700';
  }

  return 'border-amber-200 bg-amber-50 text-amber-700';
}

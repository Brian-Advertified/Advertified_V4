import { useState } from 'react';
import { FileDown, ReceiptText, X } from 'lucide-react';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { AdminPackageOrder } from '../../types/domain';
import { fmtCurrency, fmtDate, titleize } from './adminWorkspace';

type AdminPackageOrderEditModalProps = {
  order: AdminPackageOrder | null;
  isOpen: boolean;
  isSaving: boolean;
  onClose: () => void;
  onSave: (input: {
    orderId: string;
    paymentStatus: 'paid' | 'failed';
    paymentReference?: string;
    notes: string;
    file: File;
  }) => void;
};

export function AdminPackageOrderEditModal({
  order,
  isOpen,
  isSaving,
  onClose,
  onSave,
}: AdminPackageOrderEditModalProps) {
  const [scopedDraft, setScopedDraft] = useState<{
    key: string;
    paymentStatus: 'paid' | 'failed' | '';
    paymentReference: string;
    notes: string;
    file: File | undefined;
  } | null>(null);

  if (!isOpen || !order) {
    return null;
  }

  const modalKey = order.orderId;
  const paymentReference = scopedDraft?.key === modalKey ? scopedDraft.paymentReference : (order.paymentReference ?? '');
  const notes = scopedDraft?.key === modalKey ? scopedDraft.notes : '';
  const file = scopedDraft?.key === modalKey ? scopedDraft.file : undefined;
  const currentPaymentStatus = scopedDraft?.key === modalKey ? scopedDraft.paymentStatus : '';
  const setDraftValue = (
    key: 'paymentStatus' | 'paymentReference' | 'notes' | 'file',
    value: 'paid' | 'failed' | '' | string | File | undefined,
  ) => {
    setScopedDraft((current) => ({
      key: modalKey,
      paymentStatus: key === 'paymentStatus'
        ? value as ('paid' | 'failed' | '')
        : (current?.key === modalKey ? current.paymentStatus : ''),
      paymentReference: key === 'paymentReference'
        ? String(value ?? '')
        : (current?.key === modalKey ? current.paymentReference : (order.paymentReference ?? '')),
      notes: key === 'notes'
        ? String(value ?? '')
        : (current?.key === modalKey ? current.notes : ''),
      file: key === 'file'
        ? value as (File | undefined)
        : (current?.key === modalKey ? current.file : undefined),
    }));
  };

  const fileIsInvalid = Boolean(file && file.type && file.type !== 'application/pdf');
  const canSave = Boolean(order.canUpdateLulaStatus && currentPaymentStatus && notes.trim() && file && !fileIsInvalid);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/45 px-4 py-8">
      <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-[32px] border border-line bg-white shadow-[0_30px_90px_rgba(15,23,42,0.18)]">
        <div className="sticky top-0 z-10 flex items-start justify-between gap-4 border-b border-line bg-white px-6 py-5">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">Order editor</p>
            <h2 className="mt-2 text-2xl font-semibold text-ink">{order.packageBandName}</h2>
            <p className="mt-2 text-sm text-ink-soft">{order.clientName} | {order.clientEmail}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="inline-flex size-10 items-center justify-center rounded-full border border-line text-ink-soft transition hover:border-brand hover:text-brand"
            aria-label="Close order editor"
          >
            <X className="size-4" />
          </button>
        </div>

        <div className="space-y-6 px-6 py-6">
          <div className="grid gap-3 md:grid-cols-2">
            <InfoRow label="Selected budget" value={fmtCurrency(order.selectedBudget)} />
            <InfoRow label="Charged amount" value={fmtCurrency(order.chargedAmount)} />
            <InfoRow label="Provider" value={titleize(order.paymentProvider)} />
            <InfoRow label="Payment status" value={titleize(order.paymentStatus)} />
            <InfoRow label="Reference" value={order.paymentReference ?? 'Awaiting payment reference'} />
            <InfoRow label="Created" value={fmtDate(order.createdAt)} />
            <InfoRow label="Purchased" value={order.purchasedAt ? fmtDate(order.purchasedAt) : 'Not yet paid'} />
            <InfoRow label="Campaign" value={order.campaignName ?? 'Not created yet'} />
            <InfoRow label="Invoice status" value={order.invoiceStatus ? titleize(order.invoiceStatus) : 'No invoice yet'} />
            <InfoRow label="Client phone" value={order.clientPhone || 'Phone not captured'} />
          </div>

          <div className="flex flex-wrap gap-3">
            {order.invoicePdfUrl ? (
              <button
                type="button"
                className="button-secondary inline-flex items-center gap-2 rounded-full"
                onClick={() => advertifiedApi.downloadProtectedFile(order.invoicePdfUrl!, `${order.invoiceId ?? order.orderId}-invoice.pdf`)}
              >
                <ReceiptText className="size-4" />
                Download invoice
              </button>
            ) : null}
            {order.supportingDocumentPdfUrl ? (
              <button
                type="button"
                className="button-secondary inline-flex items-center gap-2 rounded-full"
                onClick={() => advertifiedApi.downloadProtectedFile(order.supportingDocumentPdfUrl!, order.supportingDocumentFileName ?? `lula-supporting-${order.orderId}.pdf`)}
              >
                <FileDown className="size-4" />
                Download supporting PDF
              </button>
            ) : null}
          </div>

          {order.supportingDocumentUploadedAt ? (
            <p className="text-xs leading-5 text-ink-soft">
              Supporting PDF: {order.supportingDocumentFileName ?? 'Document uploaded'} on {fmtDate(order.supportingDocumentUploadedAt)}
            </p>
          ) : null}

          <div className="rounded-[28px] border border-line bg-slate-50/60 p-5">
            <h3 className="text-lg font-semibold text-ink">Finance Partner payment update</h3>
            <p className="mt-2 text-sm leading-6 text-ink-soft">
              Choose the new payment status, add the admin comment, and upload the PDF. Save becomes available once the comment and PDF are added.
            </p>

            {order.canUpdateLulaStatus ? (
              <div className="mt-5 space-y-4">
                <label className="block text-sm font-semibold text-ink">
                  Payment status
                  <select
                    className="input-base mt-2"
                    value={currentPaymentStatus}
                    onChange={(event) => setDraftValue('paymentStatus', event.target.value as 'paid' | 'failed' | '')}
                  >
                    <option value="">Choose status</option>
                    <option value="paid">Successful</option>
                    <option value="failed">Declined</option>
                  </select>
                </label>

                <label className="block text-sm font-semibold text-ink">
                  Payment reference
                  <input
                    className="input-base mt-2"
                    value={paymentReference}
                    onChange={(event) => setDraftValue('paymentReference', event.target.value)}
                    placeholder="Finance Partner settlement reference"
                  />
                </label>

                <label className="block text-sm font-semibold text-ink">
                  Admin comment
                  <textarea
                    className="input-base mt-2 min-h-28"
                    value={notes}
                    onChange={(event) => setDraftValue('notes', event.target.value)}
                    placeholder="Required comment for the audit trail"
                  />
                </label>

                <label className="block text-sm font-semibold text-ink">
                  Supporting PDF
                  <input
                    className="input-base mt-2"
                    type="file"
                    accept="application/pdf,.pdf"
                    onChange={(event) => setDraftValue('file', event.target.files?.[0])}
                  />
                  <span className="mt-2 block text-xs font-normal text-ink-soft">PDF files only. Upload the Finance Partner confirmation or settlement document.</span>
                </label>

                {fileIsInvalid ? <p className="text-sm text-rose-600">Only PDF files are allowed.</p> : null}
              </div>
            ) : (
              <p className="mt-5 text-sm leading-6 text-ink-soft">
                This order is no longer pending with Finance Partner, so the payment status cannot be edited.
              </p>
            )}
          </div>
        </div>

        <div className="sticky bottom-0 flex items-center justify-end gap-3 border-t border-line bg-white px-6 py-5">
          <button type="button" onClick={onClose} className="button-secondary rounded-full">
            Close
          </button>
          <button
            type="button"
            className="button-primary rounded-full"
            disabled={!canSave || isSaving}
            onClick={() => {
              if (!file || !currentPaymentStatus) {
                return;
              }

              onSave({
                orderId: order.orderId,
                paymentStatus: currentPaymentStatus,
                paymentReference: emptyToUndefined(paymentReference),
                notes: notes.trim(),
                file,
              });
            }}
          >
            Save
          </button>
        </div>
      </div>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-2xl border border-line bg-white px-4 py-3">
      <span className="text-sm text-ink-soft">{label}</span>
      <span className="text-right font-semibold text-ink">{value}</span>
    </div>
  );
}

function emptyToUndefined(value?: string) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

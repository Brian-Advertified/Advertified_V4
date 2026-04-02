import { useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eye, Pencil, ShieldCheck, Trash2, Upload, X } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import { Link } from 'react-router-dom';
import {
  AdminPageShell,
  AdminQueryBoundary,
  fmtDate,
  titleize,
  useAdminDashboardQuery,
} from './adminWorkspace';
import {
  ActionButton,
  ReadOnlyNotice,
  hasText,
} from './adminSectionShared';

export function AdminImportsPage() {
  const query = useAdminDashboardQuery();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const rateCardFileInputRef = useRef<HTMLInputElement | null>(null);
  const [importDialog, setImportDialog] = useState<{ mode: 'view' | 'edit'; sourceFile: string } | null>(null);
  const [isUploadFormOpen, setIsUploadFormOpen] = useState(false);
  const [rateCardForm, setRateCardForm] = useState({
    channel: 'radio',
    supplierOrStation: '',
    documentTitle: '',
    notes: '',
    file: null as File | null,
  });
  const [uploadAttempted, setUploadAttempted] = useState(false);
  const [importForm, setImportForm] = useState({
    channel: 'radio',
    supplierOrStation: '',
    documentTitle: '',
    notes: '',
  });

  const uploadRateCardMutation = useMutation({
    mutationFn: () => {
      setUploadAttempted(true);
      if (!rateCardForm.file) {
        throw new Error('Choose a file to upload first.');
      }
      return advertifiedApi.uploadAdminRateCard({
        channel: rateCardForm.channel,
        supplierOrStation: rateCardForm.supplierOrStation || undefined,
        documentTitle: rateCardForm.documentTitle || undefined,
        notes: rateCardForm.notes || undefined,
        file: rateCardForm.file,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setUploadAttempted(false);
      setIsUploadFormOpen(false);
      setRateCardForm({
        channel: 'radio',
        supplierOrStation: '',
        documentTitle: '',
        notes: '',
        file: null,
      });
      pushToast({ title: 'Rate card uploaded.', description: 'The file was stored and added to the live import manifest.' });
    },
    onError: (error) => pushToast({ title: 'Could not upload rate card.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });
  const uploadFormIsValid = hasText(rateCardForm.channel) && !!rateCardForm.file;
  const importFormIsValid = hasText(importForm.channel);

  const updateRateCardMutation = useMutation({
    mutationFn: () => advertifiedApi.updateAdminRateCard(importDialog?.sourceFile ?? '', {
      channel: importForm.channel,
      supplierOrStation: importForm.supplierOrStation || undefined,
      documentTitle: importForm.documentTitle || undefined,
      notes: importForm.notes || undefined,
    }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setImportDialog(null);
      pushToast({ title: 'Rate card updated.', description: 'The import metadata was updated successfully.' });
    },
    onError: (error) => pushToast({ title: 'Could not update rate card.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  const deleteRateCardMutation = useMutation({
    mutationFn: (sourceFile: string) => advertifiedApi.deleteAdminRateCard(sourceFile),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-dashboard'] });
      setImportDialog(null);
      pushToast({ title: 'Rate card deleted.', description: 'The import record and stored file were removed.' });
    },
    onError: (error) => pushToast({ title: 'Could not delete rate card.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error'),
  });

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => {
        const selectedImport = importDialog ? dashboard.recentImports.find((item) => item.sourceFile === importDialog.sourceFile) ?? null : null;
        const openImportDialog = (mode: 'view' | 'edit', item: (typeof dashboard.recentImports)[number]) => {
          setImportForm({
            channel: item.channel,
            supplierOrStation: item.supplierOrStation ?? '',
            documentTitle: item.documentTitle ?? '',
            notes: item.notes ?? '',
          });
          setImportDialog({ mode, sourceFile: item.sourceFile });
        };

        return (
        <AdminPageShell title="Imports and rate cards" description="Upload new source files and review the live import manifest and document metadata already in the system.">
          <section className="space-y-6">
            <div className="panel p-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h3 className="text-lg font-semibold text-ink">Upload rate card</h3>
                  <p className="mt-2 text-sm text-ink-soft">Store a real rate-card file, create the import manifest record, and attach its metadata in the live import tables.</p>
                </div>
                <button
                  type="button"
                  className="pill"
                  onClick={() => setIsUploadFormOpen((current) => !current)}
                >
                  <Upload className="mr-2 inline size-4" />
                  {isUploadFormOpen ? 'Hide upload' : 'Live upload'}
                </button>
              </div>
              {isUploadFormOpen ? (
                <>
                  <div className="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                    <select className="input-base" value={rateCardForm.channel} onChange={(event) => setRateCardForm((current) => ({ ...current, channel: event.target.value }))}>
                      <option value="radio">Radio</option>
                      <option value="tv">TV</option>
                      <option value="ooh">Billboards and Digital Screens</option>
                      <option value="digital">Digital</option>
                    </select>
                    <input className="input-base" placeholder="Supplier or station" value={rateCardForm.supplierOrStation} onChange={(event) => setRateCardForm((current) => ({ ...current, supplierOrStation: event.target.value }))} />
                    <input className="input-base" placeholder="Document title" value={rateCardForm.documentTitle} onChange={(event) => setRateCardForm((current) => ({ ...current, documentTitle: event.target.value }))} />
                    <div className="flex min-h-[60px] items-center justify-between gap-3 rounded-[20px] border border-line bg-white px-4 py-3">
                      <div className="min-w-0">
                        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Rate-card file</p>
                        <p className="mt-1 truncate text-sm text-ink">{rateCardForm.file?.name ?? 'No file selected yet'}</p>
                      </div>
                      <input
                        ref={rateCardFileInputRef}
                        type="file"
                        className="hidden"
                        onChange={(event) => setRateCardForm((current) => ({ ...current, file: event.target.files?.[0] ?? null }))}
                      />
                      <button type="button" className="button-secondary shrink-0 px-4 py-2" onClick={() => rateCardFileInputRef.current?.click()}>
                        {rateCardForm.file ? 'Change file' : 'Choose file'}
                      </button>
                    </div>
                  </div>
                  <textarea className="input-base mt-4 min-h-[100px]" placeholder="Notes" value={rateCardForm.notes} onChange={(event) => setRateCardForm((current) => ({ ...current, notes: event.target.value }))} />
                  {uploadAttempted && !uploadFormIsValid ? <p className="mt-3 text-sm text-rose-600">Choose a channel and file before uploading a rate card.</p> : <p className="mt-3 text-sm text-ink-soft">Upload a source file once the channel and file are ready.</p>}
                  <div className="mt-5 flex justify-end">
                    <button type="button" className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60" onClick={() => uploadRateCardMutation.mutate()} disabled={uploadRateCardMutation.isPending || !uploadFormIsValid}>
                      <Upload className="size-4" />
                      Upload rate card
                    </button>
                  </div>
                </>
              ) : (
                <p className="mt-4 text-sm text-ink-soft">Click live upload when you want to add a new rate card.</p>
              )}
            </div>
            <div className="overflow-hidden rounded-[28px] border border-line">
              <table className="w-full border-collapse text-sm">
                <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Document</th><th className="px-4 py-4">Channel</th><th className="px-4 py-4">Supplier / Station</th><th className="px-4 py-4">Pages</th><th className="px-4 py-4">Imported</th><th className="px-4 py-4 text-right">Actions</th></tr></thead>
                <tbody>
                  {dashboard.recentImports.map((item) => <tr key={`${item.sourceFile}-${item.importedAt}`} className="border-t border-line"><td className="px-4 py-4"><p className="font-semibold text-ink">{item.documentTitle ?? item.sourceFile}</p><p className="text-xs text-ink-soft">{item.sourceFile}</p></td><td className="px-4 py-4 text-ink-soft">{item.channel.toLowerCase() === 'ooh' ? 'Billboards and Digital Screens' : titleize(item.channel)}</td><td className="px-4 py-4 text-ink-soft">{item.supplierOrStation ?? 'Not classified yet'}</td><td className="px-4 py-4 text-ink-soft">{item.pageCount ?? 0}</td><td className="px-4 py-4 text-ink-soft">{fmtDate(item.importedAt)}</td><td className="px-4 py-4"><div className="flex justify-end gap-2"><ActionButton label={`View ${item.documentTitle ?? item.sourceFile}`} icon={Eye} onClick={() => openImportDialog('view', item)} /><ActionButton label={`Edit ${item.documentTitle ?? item.sourceFile}`} icon={Pencil} onClick={() => openImportDialog('edit', item)} /><ActionButton label={`Delete ${item.documentTitle ?? item.sourceFile}`} icon={Trash2} variant="danger" disabled={deleteRateCardMutation.isPending} onClick={() => { if (window.confirm(`Delete ${item.documentTitle ?? item.sourceFile}? This removes the stored file and import metadata.`)) { deleteRateCardMutation.mutate(item.sourceFile); } }} /></div></td></tr>)}
                </tbody>
              </table>
            </div>

            {importDialog && selectedImport ? (
              <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/35 p-4">
                <div className="w-full max-w-4xl rounded-[28px] border border-line bg-white p-6 shadow-[0_24px_80px_rgba(15,23,42,0.18)]">
                  <div className="flex items-center justify-between gap-4"><h3 className="text-xl font-semibold text-ink">{importDialog.mode === 'view' ? 'View import' : 'Edit import'}</h3><button type="button" className="button-secondary p-3" onClick={() => setImportDialog(null)}><X className="size-4" /></button></div>
                  {importDialog.mode === 'view' ? <ReadOnlyNotice label="This import is open in view mode. Switch to edit mode to change channel or document metadata." /> : null}
                  <div className="mt-4 flex flex-wrap items-center gap-3 text-sm text-ink-soft"><span className="pill">{selectedImport.sourceFile}</span><span>Imported {fmtDate(selectedImport.importedAt)}</span></div>
                  <div className="mt-6 grid gap-4 md:grid-cols-2">
                    <select disabled={importDialog.mode === 'view'} className="input-base disabled:bg-slate-50" value={importForm.channel} onChange={(event) => setImportForm((current) => ({ ...current, channel: event.target.value }))}>
                      <option value="radio">Radio</option>
                      <option value="tv">TV</option>
                      <option value="ooh">Billboards and Digital Screens</option>
                      <option value="digital">Digital</option>
                    </select>
                    <input disabled={importDialog.mode === 'view'} className="input-base disabled:bg-slate-50" placeholder="Supplier or station" value={importForm.supplierOrStation} onChange={(event) => setImportForm((current) => ({ ...current, supplierOrStation: event.target.value }))} />
                    <input disabled={importDialog.mode === 'view'} className="input-base md:col-span-2 disabled:bg-slate-50" placeholder="Document title" value={importForm.documentTitle} onChange={(event) => setImportForm((current) => ({ ...current, documentTitle: event.target.value }))} />
                  </div>
                  <textarea disabled={importDialog.mode === 'view'} className="input-base mt-4 min-h-[120px] disabled:bg-slate-50" placeholder="Notes" value={importForm.notes} onChange={(event) => setImportForm((current) => ({ ...current, notes: event.target.value }))} />
                    {!importFormIsValid ? <p className="mt-3 text-sm text-rose-600">Channel is required before you can update this import.</p> : null}
                    <div className="mt-6 flex justify-end gap-3">
                      <button type="button" className="button-secondary px-5 py-3" onClick={() => setImportDialog(null)}>Close</button>
                      {importDialog.mode === 'view' ? <button type="button" className="button-secondary px-5 py-3" onClick={() => openImportDialog('edit', selectedImport)}>Edit import</button> : null}
                      {importDialog.mode === 'edit' ? <button type="button" className="rounded-full border border-rose-200 bg-white px-5 py-3 font-semibold text-rose-600 transition hover:bg-rose-50" disabled={deleteRateCardMutation.isPending} onClick={() => { if (window.confirm(`Delete ${selectedImport.documentTitle ?? selectedImport.sourceFile}? This removes the stored file and import metadata.`)) { deleteRateCardMutation.mutate(selectedImport.sourceFile); } }}>Delete import</button> : null}
                    {importDialog.mode === 'edit' ? <button type="button" className="button-primary px-5 py-3" onClick={() => updateRateCardMutation.mutate()} disabled={updateRateCardMutation.isPending || !importFormIsValid}>Update import</button> : null}
                    </div>
                </div>
              </div>
            ) : null}
          </section>
        </AdminPageShell>
      )}}
    </AdminQueryBoundary>
  );
}

export function AdminHealthPage() {
  const query = useAdminDashboardQuery();

  return (
    <AdminQueryBoundary query={query}>
      {(dashboard) => (
        <AdminPageShell title="Data quality and health" description="Track weak outlets, missing pricing, and live catalog issues so the planning engine stays reliable.">
          <section className="space-y-6">
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {[['Strong', dashboard.health.strongCount], ['Mixed', dashboard.health.mixedCount], ['Weak unpriced', dashboard.health.weakUnpricedCount], ['Weak no inventory', dashboard.health.weakNoInventoryCount]].map(([label, value]) => <div key={String(label)} className="panel p-6"><p className="text-xs font-semibold uppercase tracking-[0.24em] text-ink-soft">{label}</p><p className="mt-4 text-4xl font-semibold text-ink">{value}</p></div>)}
            </div>
            <div className="rounded-[28px] border border-line bg-white p-6">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h3 className="text-lg font-semibold text-ink">Fix queue</h3>
                  <p className="mt-2 text-sm text-ink-soft">Live issues surfaced from outlet health signals and import quality checks.</p>
                </div>
                <div className="rounded-2xl bg-brand-soft p-3 text-brand"><ShieldCheck className="size-5" /></div>
              </div>
              <div className="mt-4 overflow-hidden rounded-[24px] border border-line">
                <table className="w-full border-collapse text-sm">
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-[0.18em] text-ink-soft"><tr><th className="px-4 py-4">Outlet</th><th className="px-4 py-4">Issue</th><th className="px-4 py-4">Impact</th><th className="px-4 py-4">Suggested fix</th><th className="px-4 py-4 text-right">Action</th></tr></thead>
                  <tbody>
                    {dashboard.healthIssues.map((item) => {
                      const goesToPricing = item.issue.toLowerCase().includes('pricing') || item.issue.toLowerCase().includes('inventory');
                      const href = goesToPricing ? `/admin/pricing?outlet=${encodeURIComponent(item.outletCode)}` : `/admin/stations?outlet=${encodeURIComponent(item.outletCode)}&mode=edit`;

                      return (
                        <tr key={`${item.outletCode}-${item.issue}`} className="border-t border-line">
                          <td className="px-4 py-4 font-semibold text-ink">{item.outletName}</td>
                          <td className="px-4 py-4 text-ink-soft">{item.issue}</td>
                          <td className="px-4 py-4 text-ink-soft">{item.impact}</td>
                          <td className="px-4 py-4 text-ink-soft">{item.suggestedFix}</td>
                          <td className="px-4 py-4">
                            <div className="flex justify-end">
                              <Link to={href} className="button-secondary inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold">
                                <Pencil className="size-4" />
                                Apply fix
                              </Link>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </section>
        </AdminPageShell>
      )}
    </AdminQueryBoundary>
  );
}



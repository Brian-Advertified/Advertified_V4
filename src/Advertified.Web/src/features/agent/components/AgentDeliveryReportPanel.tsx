import { useState } from 'react';
import type { CampaignAsset, CampaignDeliveryReport, CampaignSupplierBooking } from '../../../types/domain';

type ReportDraft = {
  supplierBookingId: string;
  reportType: string;
  headline: string;
  summary: string;
  impressions: string;
  playsOrSpots: string;
  spendDelivered: string;
  evidenceAssetId: string;
};

const initialReportDraft: ReportDraft = {
  supplierBookingId: '',
  reportType: 'delivery_update',
  headline: '',
  summary: '',
  impressions: '',
  playsOrSpots: '',
  spendDelivered: '',
  evidenceAssetId: '',
};

export function AgentDeliveryReportPanel({
  deliveryReports,
  supplierBookings,
  executionAssets,
  isSaving,
  onSave,
  titleCase,
}: {
  deliveryReports: CampaignDeliveryReport[];
  supplierBookings: CampaignSupplierBooking[];
  executionAssets: CampaignAsset[];
  isSaving: boolean;
  onSave: (draft: ReportDraft) => void;
  titleCase: (value: string) => string;
}) {
  const [reportDraft, setReportDraft] = useState<ReportDraft>(initialReportDraft);

  return (
    <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
      <h2 className="text-xl font-semibold text-ink">Add a campaign update</h2>
      <p className="mt-2 text-sm leading-7 text-ink-soft">
        Save a short delivery or performance update so anyone opening this campaign can see what happened most recently.
      </p>
      <div className="mt-4 grid gap-3 md:grid-cols-2">
        <select className="input-base" value={reportDraft.reportType} onChange={(event) => setReportDraft((current) => ({ ...current, reportType: event.target.value }))}>
          <option value="delivery_update">Delivery update</option>
          <option value="performance_snapshot">Performance update</option>
          <option value="proof_of_flight">Proof campaign is live</option>
        </select>
        <select className="input-base" value={reportDraft.supplierBookingId} onChange={(event) => setReportDraft((current) => ({ ...current, supplierBookingId: event.target.value }))}>
          <option value="">Link to a saved booking (optional)</option>
          {supplierBookings.map((booking) => <option key={booking.id} value={booking.id}>{booking.supplierOrStation}</option>)}
        </select>
        <input className="input-base md:col-span-2" placeholder="Short title for this update" value={reportDraft.headline} onChange={(event) => setReportDraft((current) => ({ ...current, headline: event.target.value }))} />
        <textarea className="input-base md:col-span-2 min-h-[96px]" placeholder="What changed, what has been delivered, and what the team should know next" value={reportDraft.summary} onChange={(event) => setReportDraft((current) => ({ ...current, summary: event.target.value }))} />
        <input className="input-base" type="number" min="0" step="1" placeholder="Impressions" value={reportDraft.impressions} onChange={(event) => setReportDraft((current) => ({ ...current, impressions: event.target.value }))} />
        <input className="input-base" type="number" min="0" step="1" placeholder="Plays / spots" value={reportDraft.playsOrSpots} onChange={(event) => setReportDraft((current) => ({ ...current, playsOrSpots: event.target.value }))} />
        <input className="input-base" type="number" min="0" step="0.01" placeholder="Spend delivered" value={reportDraft.spendDelivered} onChange={(event) => setReportDraft((current) => ({ ...current, spendDelivered: event.target.value }))} />
        <select className="input-base" value={reportDraft.evidenceAssetId} onChange={(event) => setReportDraft((current) => ({ ...current, evidenceAssetId: event.target.value }))}>
          <option value="">Attach saved evidence file (optional)</option>
          {executionAssets.map((asset) => <option key={asset.id} value={asset.id}>{asset.displayName}</option>)}
        </select>
      </div>
      <button
        type="button"
        disabled={!reportDraft.headline.trim() || isSaving}
        onClick={() => {
          onSave(reportDraft);
          setReportDraft(initialReportDraft);
        }}
        className="button-primary mt-4 inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
      >
        Save update
      </button>
      <div className="mt-4 space-y-3">
        {deliveryReports.length > 0 ? deliveryReports.map((report) => (
          <div key={report.id} className="rounded-[16px] border border-line bg-slate-50 px-4 py-3">
            <p className="text-sm font-semibold text-ink">{report.headline}</p>
            <p className="mt-1 text-xs text-ink-soft">{titleCase(report.reportType)}{report.impressions ? ` | ${report.impressions.toLocaleString()} impressions` : ''}{report.playsOrSpots ? ` | ${report.playsOrSpots} plays/spots` : ''}</p>
            <p className="mt-1 text-xs text-ink-soft">{report.summary ?? 'No summary captured yet.'}</p>
          </div>
        )) : null}
      </div>
    </div>
  );
}

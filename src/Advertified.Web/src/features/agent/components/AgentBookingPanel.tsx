import { useState } from 'react';
import type { CampaignAsset, CampaignSupplierBooking } from '../../../types/domain';

type BookingDraft = {
  supplierOrStation: string;
  channel: string;
  bookingStatus: string;
  committedAmount: string;
  liveFrom: string;
  liveTo: string;
  notes: string;
  proofAssetId: string;
};

const initialBookingDraft: BookingDraft = {
  supplierOrStation: '',
  channel: 'radio',
  bookingStatus: 'planned',
  committedAmount: '',
  liveFrom: '',
  liveTo: '',
  notes: '',
  proofAssetId: '',
};

export function AgentBookingPanel({
  supplierBookings,
  executionAssets,
  isSaving,
  onSave,
  formatChannelLabel,
  formatCurrency,
  titleCase,
}: {
  supplierBookings: CampaignSupplierBooking[];
  executionAssets: CampaignAsset[];
  isSaving: boolean;
  onSave: (draft: BookingDraft) => void;
  formatChannelLabel: (value: string) => string;
  formatCurrency: (value: number) => string;
  titleCase: (value: string) => string;
}) {
  const [bookingDraft, setBookingDraft] = useState<BookingDraft>(initialBookingDraft);

  return (
    <div className="panel border-line/80 bg-white px-6 py-6 shadow-[0_10px_26px_rgba(17,24,39,0.05)]">
      <h2 className="text-xl font-semibold text-ink">Record a supplier booking</h2>
      <p className="mt-2 text-sm leading-7 text-ink-soft">
        Add the booking details once space has been planned, confirmed, or gone live with a supplier or station.
      </p>
      <div className="mt-4 grid gap-3 md:grid-cols-2">
        <input className="input-base" placeholder="Supplier or station name" value={bookingDraft.supplierOrStation} onChange={(event) => setBookingDraft((current) => ({ ...current, supplierOrStation: event.target.value }))} />
        <select className="input-base" value={bookingDraft.channel} onChange={(event) => setBookingDraft((current) => ({ ...current, channel: event.target.value }))}>
          <option value="radio">Radio</option>
          <option value="ooh">Billboards and Digital Screens</option>
          <option value="tv">TV</option>
          <option value="digital">Digital</option>
        </select>
        <select className="input-base" value={bookingDraft.bookingStatus} onChange={(event) => setBookingDraft((current) => ({ ...current, bookingStatus: event.target.value }))}>
          <option value="planned">Planned, not confirmed</option>
          <option value="booked">Booked and confirmed</option>
          <option value="live">Live now</option>
          <option value="completed">Finished</option>
        </select>
        <input className="input-base" type="number" min="0" step="0.01" placeholder="Booked amount" value={bookingDraft.committedAmount} onChange={(event) => setBookingDraft((current) => ({ ...current, committedAmount: event.target.value }))} />
        <input className="input-base" type="date" value={bookingDraft.liveFrom} onChange={(event) => setBookingDraft((current) => ({ ...current, liveFrom: event.target.value }))} />
        <input className="input-base" type="date" value={bookingDraft.liveTo} onChange={(event) => setBookingDraft((current) => ({ ...current, liveTo: event.target.value }))} />
        <select className="input-base md:col-span-2" value={bookingDraft.proofAssetId} onChange={(event) => setBookingDraft((current) => ({ ...current, proofAssetId: event.target.value }))}>
          <option value="">Attach saved proof file (optional)</option>
          {executionAssets.map((asset) => <option key={asset.id} value={asset.id}>{asset.displayName}</option>)}
        </select>
        <textarea className="input-base md:col-span-2 min-h-[96px]" placeholder="Notes for the team, for example supplier contacts, placement details, or anything still outstanding" value={bookingDraft.notes} onChange={(event) => setBookingDraft((current) => ({ ...current, notes: event.target.value }))} />
      </div>
      <button
        type="button"
        disabled={!bookingDraft.supplierOrStation.trim() || isSaving}
        onClick={() => {
          onSave(bookingDraft);
          setBookingDraft(initialBookingDraft);
        }}
        className="button-primary mt-4 inline-flex items-center gap-2 px-5 py-3 disabled:opacity-60"
      >
        Save booking
      </button>
      <div className="mt-4 space-y-3">
        {supplierBookings.length > 0 ? supplierBookings.map((booking) => (
          <div key={booking.id} className="rounded-[16px] border border-line bg-slate-50 px-4 py-3">
            <p className="text-sm font-semibold text-ink">{booking.supplierOrStation}</p>
            <p className="mt-1 text-xs text-ink-soft">{formatChannelLabel(booking.channel)} | {titleCase(booking.bookingStatus)} | {formatCurrency(booking.committedAmount)}</p>
            <p className="mt-1 text-xs text-ink-soft">{booking.liveFrom ?? 'Start TBC'} to {booking.liveTo ?? 'End TBC'}</p>
          </div>
        )) : null}
      </div>
    </div>
  );
}

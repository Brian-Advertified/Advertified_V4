import { formatCurrency } from '../../../lib/utils';
import type { Campaign, CampaignSupplierBooking } from '../../../types/domain';
import type { CreativeBookingDraft } from '../creativeStudioTypes';
import { formatChannelLabel } from '../creativeStudioUtils';

interface CreativeStudioBookingPanelProps {
  campaign: Campaign;
  isPreview: boolean;
  bookingDraft: CreativeBookingDraft;
  onBookingDraftChange: (value: CreativeBookingDraft) => void;
  onSaveBooking: () => void;
  isSavingBooking: boolean;
  canSaveBooking: boolean;
  canMarkLive: boolean;
  onMarkLive: () => void;
  isMarkingLive: boolean;
  supplierBookings: CampaignSupplierBooking[];
}

export function CreativeStudioBookingPanel({
  campaign,
  isPreview,
  bookingDraft,
  onBookingDraftChange,
  onSaveBooking,
  isSavingBooking,
  canSaveBooking,
  canMarkLive,
  onMarkLive,
  isMarkingLive,
  supplierBookings,
}: CreativeStudioBookingPanelProps) {
  const isLocked = isPreview || campaign.status === 'launched';

  return (
    <div className="user-card">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <h3>Supplier booking and client updates</h3>
          <p className="mt-2 text-sm leading-7 text-slate-600">
            Contact the supplier or station, save the confirmed booking here, and the client workspace will reflect that the campaign is now in booking.
          </p>
        </div>
        <div className="rounded-[20px] border border-brand/20 bg-brand-soft/60 px-4 py-3 text-sm text-slate-700">
          {campaign.status === 'launched'
            ? 'Campaign is already live.'
            : campaign.status === 'booking_in_progress'
              ? 'Booking is in progress.'
              : 'Creative approval is complete. First booking will move the campaign into booking.'}
        </div>
      </div>

      <div className="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <input
          className="input-base"
          placeholder="Supplier or station name"
          value={bookingDraft.supplierOrStation}
          disabled={isLocked}
          onChange={(event) => onBookingDraftChange({ ...bookingDraft, supplierOrStation: event.target.value })}
        />
        <select
          className="input-base"
          value={bookingDraft.channel}
          disabled={isLocked}
          onChange={(event) => onBookingDraftChange({ ...bookingDraft, channel: event.target.value })}
        >
          <option value="radio">Radio</option>
          <option value="ooh">Billboards and Digital Screens</option>
          <option value="tv">TV</option>
          <option value="digital">Digital</option>
        </select>
        <select
          className="input-base"
          value={bookingDraft.bookingStatus}
          disabled={isLocked}
          onChange={(event) => onBookingDraftChange({ ...bookingDraft, bookingStatus: event.target.value })}
        >
          <option value="planned">Planned</option>
          <option value="booked">Booked and confirmed</option>
          <option value="live">Live now</option>
          <option value="completed">Completed</option>
        </select>
        <input
          className="input-base"
          type="number"
          min="0"
          step="0.01"
          placeholder="Booked amount"
          value={bookingDraft.committedAmount}
          disabled={isLocked}
          onChange={(event) => onBookingDraftChange({ ...bookingDraft, committedAmount: event.target.value })}
        />
        <input
          className="input-base"
          type="date"
          value={bookingDraft.liveFrom}
          disabled={isLocked}
          onChange={(event) => onBookingDraftChange({ ...bookingDraft, liveFrom: event.target.value })}
        />
        <input
          className="input-base"
          type="date"
          value={bookingDraft.liveTo}
          disabled={isLocked}
          onChange={(event) => onBookingDraftChange({ ...bookingDraft, liveTo: event.target.value })}
        />
        <textarea
          className="input-base min-h-[110px] md:col-span-2 xl:col-span-3"
          placeholder="Notes for the team or the client, for example who confirmed the booking, what is still pending, or any launch timing details."
          value={bookingDraft.notes}
          disabled={isLocked}
          onChange={(event) => onBookingDraftChange({ ...bookingDraft, notes: event.target.value })}
        />
      </div>

      <div className="mt-4 flex flex-wrap gap-3">
        <button
          type="button"
          onClick={onSaveBooking}
          disabled={isLocked || !canSaveBooking || isSavingBooking}
          className="user-btn-primary disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isSavingBooking ? 'Saving booking...' : 'Save supplier booking'}
        </button>
        {canMarkLive ? (
          <button
            type="button"
            onClick={onMarkLive}
            disabled={isMarkingLive}
            className="user-btn-secondary disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isMarkingLive ? 'Marking live...' : 'Mark campaign live'}
          </button>
        ) : null}
      </div>

      <div className="mt-5 grid gap-3">
        {supplierBookings.length > 0 ? supplierBookings.map((booking) => (
          <div key={booking.id} className="user-wire">
            <strong>{booking.supplierOrStation}</strong>
            <div>{formatChannelLabel(booking.channel)} | {booking.bookingStatus.replace(/_/g, ' ')}</div>
            <div>{booking.liveFrom || booking.liveTo ? `${booking.liveFrom ?? 'Start TBC'} to ${booking.liveTo ?? 'End TBC'}` : 'Dates still being confirmed'}</div>
            <div>{formatCurrency(booking.committedAmount)}</div>
          </div>
        )) : (
          <div className="user-wire">
            No supplier bookings have been saved yet. Once you log one here, the client workspace will show that booking is underway.
          </div>
        )}
      </div>
    </div>
  );
}

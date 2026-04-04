import { cn } from '../../lib/utils';

const toneMap: Record<string, string> = {
  pending: 'bg-amber-100 text-amber-700',
  paid: 'bg-brand-soft text-brand',
  failed: 'bg-rose-100 text-rose-700',
  paid_campaign: 'bg-blue-100 text-blue-700',
  brief_in_progress: 'bg-orange-100 text-orange-700',
  brief_submitted: 'bg-indigo-100 text-indigo-700',
  planning_in_progress: 'bg-brand-soft text-brand',
  review_ready: 'bg-brand-soft text-brand',
  sent_to_client: 'bg-cyan-100 text-cyan-700',
  approved: 'bg-brand-soft text-brand',
  creative_sent_to_client_for_approval: 'bg-brand-soft text-brand',
  creative_changes_requested: 'bg-amber-100 text-amber-700',
  creative_approved: 'bg-brand-soft text-brand',
  booking_in_progress: 'bg-blue-100 text-blue-700',
  launched: 'bg-brand-soft text-brand',
};

export function StatusBadge({ status }: { status: string }) {
  return (
    <span
      className={cn(
        'inline-flex rounded-full px-3 py-1 text-xs font-semibold capitalize tracking-wide',
        toneMap[status] ?? 'bg-slate-100 text-slate-700',
      )}
    >
      {status.replaceAll('_', ' ')}
    </span>
  );
}

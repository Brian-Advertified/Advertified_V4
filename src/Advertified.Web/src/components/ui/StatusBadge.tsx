import { cn } from '../../lib/utils';

const toneMap: Record<string, string> = {
  pending: 'bg-amber-100 text-amber-700',
  paid: 'bg-emerald-100 text-emerald-700',
  failed: 'bg-rose-100 text-rose-700',
  paid_campaign: 'bg-blue-100 text-blue-700',
  brief_in_progress: 'bg-orange-100 text-orange-700',
  brief_submitted: 'bg-indigo-100 text-indigo-700',
  planning_in_progress: 'bg-sky-100 text-sky-700',
  review_ready: 'bg-violet-100 text-violet-700',
  sent_to_client: 'bg-cyan-100 text-cyan-700',
  approved: 'bg-emerald-100 text-emerald-700',
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

export function LoadingState({ label = 'Loading your workspace...' }: { label?: string }) {
  return (
    <div className="panel page-shell flex min-h-[280px] flex-col items-center justify-center gap-4 px-8 py-14 text-center">
      <div className="size-10 animate-spin rounded-full border-4 border-brand/20 border-t-brand" />
      <p className="text-sm font-medium text-ink-soft">{label}</p>
    </div>
  );
}

import type { ReactNode } from 'react';
import type { UseQueryResult } from '@tanstack/react-query';
import { LoadingState } from './LoadingState';

type QueryStateBoundaryProps<TData> = {
  query: UseQueryResult<TData>;
  loadingLabel: string;
  errorTitle: string;
  errorDescription: string;
  emptyTitle?: string;
  emptyDescription?: string;
  children: (data: NonNullable<TData>) => ReactNode;
};

export function QueryStateBoundary<TData>({
  query,
  loadingLabel,
  errorTitle,
  errorDescription,
  emptyTitle = errorTitle,
  emptyDescription = errorDescription,
  children,
}: QueryStateBoundaryProps<TData>) {
  if (query.isLoading) {
    return <LoadingState label={loadingLabel} />;
  }

  if (query.isError) {
    return (
      <section className="page-shell">
        <div className="panel mx-auto max-w-3xl p-8">
          <h1 className="text-2xl font-semibold text-ink">{errorTitle}</h1>
          <p className="mt-3 text-sm leading-6 text-ink-soft">{query.error instanceof Error ? query.error.message : errorDescription}</p>
        </div>
      </section>
    );
  }

  if (query.data == null) {
    return (
      <section className="page-shell">
        <div className="panel mx-auto max-w-3xl p-8">
          <h1 className="text-2xl font-semibold text-ink">{emptyTitle}</h1>
          <p className="mt-3 text-sm leading-6 text-ink-soft">{emptyDescription}</p>
        </div>
      </section>
    );
  }

  return <>{children(query.data as NonNullable<TData>)}</>;
}

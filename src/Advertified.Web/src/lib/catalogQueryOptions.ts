export const CATALOG_STALE_TIME_MS = 10 * 60 * 1000;
export const CATALOG_GC_TIME_MS = 30 * 60 * 1000;

export const catalogQueryOptions = {
  staleTime: CATALOG_STALE_TIME_MS,
  gcTime: CATALOG_GC_TIME_MS,
  refetchOnWindowFocus: false as const,
};

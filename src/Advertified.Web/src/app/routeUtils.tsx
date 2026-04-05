import { lazy, type ComponentType, type ReactNode } from 'react';

const CHUNK_RELOAD_GUARD_KEY = 'advertified:chunk-reload-once';

function isChunkLoadError(error: unknown) {
  if (!(error instanceof Error)) {
    return false;
  }

  const message = error.message.toLowerCase();
  return (
    message.includes('failed to fetch dynamically imported module')
    || message.includes('importing a module script failed')
    || message.includes('module script')
  );
}

function reloadAfterChunkFailure() {
  if (typeof window === 'undefined') {
    return;
  }

  const alreadyReloaded = window.sessionStorage.getItem(CHUNK_RELOAD_GUARD_KEY) === '1';
  if (alreadyReloaded) {
    return;
  }

  window.sessionStorage.setItem(CHUNK_RELOAD_GUARD_KEY, '1');
  const url = new URL(window.location.href);
  url.searchParams.set('_app_reload', String(Date.now()));
  window.location.replace(url.toString());
}

export function lazyPage<TModule extends Record<string, unknown>, TExport extends keyof TModule>(
  load: () => Promise<TModule>,
  exportName: TExport,
) {
  return lazy(async () => {
    try {
      const module = await load();

      if (typeof window !== 'undefined') {
        window.sessionStorage.removeItem(CHUNK_RELOAD_GUARD_KEY);
      }

      return {
        default: module[exportName] as ComponentType,
      };
    } catch (error) {
      if (isChunkLoadError(error)) {
        reloadAfterChunkFailure();
      }

      throw error;
    }
  });
}

export type AppRoute = {
  path: string;
  element: ReactNode;
};

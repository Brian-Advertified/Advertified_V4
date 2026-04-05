import { useSyncExternalStore } from 'react';

type IdleCallbackHandle = number;
type IdleDeadlineLike = {
  didTimeout: boolean;
  timeRemaining: () => number;
};

type WindowWithIdleCallback = Window & typeof globalThis & {
  requestIdleCallback?: (
    callback: (deadline: IdleDeadlineLike) => void,
    options?: { timeout?: number },
  ) => IdleCallbackHandle;
  cancelIdleCallback?: (handle: IdleCallbackHandle) => void;
};

type IdleTriggerStore = {
  isTriggered: boolean;
  started: boolean;
  listeners: Set<() => void>;
  cleanup: (() => void) | null;
};

const idleTriggerStores = new Map<number, IdleTriggerStore>();

export function useIdleTrigger(timeout = 900) {
  const store = getIdleTriggerStore(timeout);

  return useSyncExternalStore(
    (listener) => subscribeToIdleTrigger(store, timeout, listener),
    () => store.isTriggered,
    () => false,
  );
}

function getIdleTriggerStore(timeout: number) {
  let store = idleTriggerStores.get(timeout);
  if (!store) {
    store = {
      isTriggered: false,
      started: false,
      listeners: new Set(),
      cleanup: null,
    };
    idleTriggerStores.set(timeout, store);
  }

  return store;
}

function subscribeToIdleTrigger(store: IdleTriggerStore, timeout: number, listener: () => void) {
  store.listeners.add(listener);
  ensureIdleTriggerStarted(store, timeout);

  return () => {
    store.listeners.delete(listener);
  };
}

function ensureIdleTriggerStarted(store: IdleTriggerStore, timeout: number) {
  if (store.started || store.isTriggered || typeof window === 'undefined') {
    return;
  }

  store.started = true;
  const windowWithIdle = window as WindowWithIdleCallback;

  const trigger = () => {
    if (store.isTriggered) {
      return;
    }

    store.isTriggered = true;
    store.cleanup = null;
    for (const listener of store.listeners) {
      listener();
    }
  };

  if (windowWithIdle.requestIdleCallback) {
    const handle = windowWithIdle.requestIdleCallback(() => {
      trigger();
    }, { timeout });
    store.cleanup = () => windowWithIdle.cancelIdleCallback?.(handle);
    return;
  }

  const timeoutId = window.setTimeout(() => {
    trigger();
  }, timeout);
  store.cleanup = () => window.clearTimeout(timeoutId);
}

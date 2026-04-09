import type { SessionUser } from '../types/domain';

const SESSION_STORAGE_KEY = 'advertified-session-user';

function getStorage() {
  if (typeof window === 'undefined') {
    return null;
  }

  return window.sessionStorage;
}

export function readStoredSession(): SessionUser | null {
  const storage = getStorage();
  const raw = storage?.getItem(SESSION_STORAGE_KEY);

  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as SessionUser;
  } catch {
    storage?.removeItem(SESSION_STORAGE_KEY);
    return null;
  }
}

export function writeStoredSession(session: SessionUser | null) {
  const storage = getStorage();

  if (!storage) {
    return;
  }

  if (session) {
    storage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
  } else {
    storage.removeItem(SESSION_STORAGE_KEY);
  }
}

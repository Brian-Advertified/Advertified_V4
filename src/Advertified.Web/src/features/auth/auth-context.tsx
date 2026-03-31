import {
  createContext,
  useEffect,
  useContext,
  useMemo,
  useSyncExternalStore,
  type PropsWithChildren,
} from 'react';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';
import type { LoginInput, RegistrationInput, RegistrationResult, SessionUser } from '../../types/domain';

type AuthContextValue = {
  user: SessionUser | null;
  isAuthenticated: boolean;
  login: (input: LoginInput) => Promise<SessionUser>;
  register: (input: RegistrationInput) => Promise<RegistrationResult>;
  logout: (reason?: 'manual' | 'expired') => void;
  verifyEmail: (token: string) => Promise<SessionUser>;
};

const STORAGE_KEY = 'advertified-session-user';
const INACTIVITY_TIMEOUT_MS = 5 * 60 * 1000;
const ACTIVITY_EVENTS: Array<keyof WindowEventMap> = ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'];
const AuthContext = createContext<AuthContextValue | undefined>(undefined);

type SessionListener = () => void;

let currentUser = readStoredUser();
let inactivityTimeoutId: number | null = null;
let listenersAttached = false;
const sessionListeners = new Set<SessionListener>();
let notifySessionExpired: (() => void) | null = null;

function readStoredUser(): SessionUser | null {
  if (typeof window === 'undefined') {
    return null;
  }

  const raw = window.localStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as SessionUser;
  } catch {
    window.localStorage.removeItem(STORAGE_KEY);
    return null;
  }
}

function emitSessionChange() {
  for (const listener of sessionListeners) {
    listener();
  }
}

function clearInactivityTimer() {
  if (inactivityTimeoutId !== null) {
    window.clearTimeout(inactivityTimeoutId);
    inactivityTimeoutId = null;
  }
}

function persistUser(nextUser: SessionUser | null) {
  currentUser = nextUser;

  if (typeof window !== 'undefined') {
    if (nextUser) {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(nextUser));
    } else {
      window.localStorage.removeItem(STORAGE_KEY);
    }
  }

  syncInactivityTracking();
  emitSessionChange();
}

function expireSession() {
  if (!currentUser) {
    return;
  }

  persistUser(null);
  notifySessionExpired?.();
}

function resetInactivityTimer() {
  if (typeof window === 'undefined' || !currentUser) {
    clearInactivityTimer();
    return;
  }

  clearInactivityTimer();
  inactivityTimeoutId = window.setTimeout(() => {
    expireSession();
  }, INACTIVITY_TIMEOUT_MS);
}

function handleActivity() {
  if (!currentUser) {
    return;
  }

  resetInactivityTimer();
}

function attachActivityListeners() {
  if (typeof window === 'undefined' || listenersAttached) {
    return;
  }

  for (const eventName of ACTIVITY_EVENTS) {
    window.addEventListener(eventName, handleActivity, { passive: true });
  }

  listenersAttached = true;
}

function syncInactivityTracking() {
  if (!currentUser) {
    clearInactivityTimer();
    return;
  }

  attachActivityListeners();
  resetInactivityTimer();
}

function subscribeToSession(listener: SessionListener) {
  sessionListeners.add(listener);

  return () => {
    sessionListeners.delete(listener);
  };
}

function getSessionSnapshot() {
  return currentUser;
}

function getServerSnapshot() {
  return null;
}

function hasSessionProfileChanged(previous: SessionUser, next: SessionUser) {
  return (
    previous.id !== next.id
    || previous.fullName !== next.fullName
    || previous.email !== next.email
    || previous.phone !== next.phone
    || previous.role !== next.role
    || previous.emailVerified !== next.emailVerified
    || previous.identityComplete !== next.identityComplete
    || previous.businessName !== next.businessName
    || previous.city !== next.city
    || previous.province !== next.province
  );
}

export function AuthProvider({ children }: PropsWithChildren) {
  const { pushToast } = useToast();
  const user = useSyncExternalStore(subscribeToSession, getSessionSnapshot, getServerSnapshot);

  useEffect(() => {
    if (!user?.sessionToken) {
      return;
    }

    let cancelled = false;
    advertifiedApi.getMe()
      .then((freshUser) => {
        if (cancelled) {
          return;
        }

        const nextUser: SessionUser = {
          ...user,
          ...freshUser,
          sessionToken: user.sessionToken,
        };

        if (hasSessionProfileChanged(user, nextUser)) {
          persistUser(nextUser);
        }
      })
      .catch(() => {
      });

    return () => {
      cancelled = true;
    };
  }, [user]);

  notifySessionExpired = () => {
    pushToast(
      {
        title: 'Session expired due to no activity.',
        description: 'Please sign in again to continue.',
      },
      'info',
    );
  };

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: Boolean(user),
      async login(input) {
        const nextUser = await advertifiedApi.login(input);
        persistUser(nextUser);
        return nextUser;
      },
      async register(input) {
        return advertifiedApi.register(input);
      },
      logout(reason = 'manual') {
        persistUser(null);
        pushToast(
          reason === 'expired'
            ? {
                title: 'Session expired due to no activity.',
                description: 'Please sign in again to continue.',
              }
            : {
                title: 'Logged out successfully.',
                description: 'You have been signed out of your account.',
              },
          reason === 'expired' ? 'info' : 'success',
        );
      },
      async verifyEmail(token) {
        return advertifiedApi.verifyEmail(token);
      },
    }),
    [pushToast, user],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }

  return context;
}

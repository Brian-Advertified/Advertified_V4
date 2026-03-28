import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
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
const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<SessionUser | null>(null);
  const inactivityTimeoutRef = useRef<number | null>(null);
  const { pushToast } = useToast();

  useEffect(() => {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      setUser(JSON.parse(raw) as SessionUser);
    }
  }, []);

  function persist(nextUser: SessionUser | null) {
    setUser(nextUser);
    if (nextUser) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(nextUser));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }

  useEffect(() => {
    if (!user) {
      if (inactivityTimeoutRef.current) {
        window.clearTimeout(inactivityTimeoutRef.current);
        inactivityTimeoutRef.current = null;
      }
      return;
    }

    const resetTimer = () => {
      if (inactivityTimeoutRef.current) {
        window.clearTimeout(inactivityTimeoutRef.current);
      }

      inactivityTimeoutRef.current = window.setTimeout(() => {
        persist(null);
        pushToast({
          title: 'Session expired due to no activity.',
          description: 'Please sign in again to continue.',
        }, 'info');
      }, INACTIVITY_TIMEOUT_MS);
    };

    const events: Array<keyof WindowEventMap> = ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'];
    resetTimer();
    for (const eventName of events) {
      window.addEventListener(eventName, resetTimer, { passive: true });
    }

    return () => {
      if (inactivityTimeoutRef.current) {
        window.clearTimeout(inactivityTimeoutRef.current);
        inactivityTimeoutRef.current = null;
      }

      for (const eventName of events) {
        window.removeEventListener(eventName, resetTimer);
      }
    };
  }, [pushToast, user]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: Boolean(user),
      async login(input) {
        const nextUser = await advertifiedApi.login(input);
        persist(nextUser);
        return nextUser;
      },
      async register(input) {
        return advertifiedApi.register(input);
      },
      logout(reason = 'manual') {
        persist(null);
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

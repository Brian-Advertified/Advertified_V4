import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ShieldCheck } from 'lucide-react';
import { useState, useSyncExternalStore } from 'react';
import { Link } from 'react-router-dom';
import { useIdleTrigger } from '../../lib/useIdleTrigger';
import { advertifiedApi } from '../../services/advertifiedApi';
import { OPEN_CONSENT_PREFERENCES_EVENT } from './consentPreferences';
import { useToast } from './toast';

const BROWSER_ID_STORAGE_KEY = 'advertified-browser-id';
const consentPreferenceListeners = new Set<() => void>();
let openConsentPreferencesTick = 0;
let consentPreferencesListenerAttached = false;

function notifyConsentPreferenceListeners() {
  openConsentPreferencesTick += 1;
  for (const listener of consentPreferenceListeners) {
    listener();
  }
}

function handleOpenConsentPreferencesEvent() {
  notifyConsentPreferenceListeners();
}

function subscribeToOpenConsentPreferences(listener: () => void) {
  consentPreferenceListeners.add(listener);

  if (typeof window !== 'undefined' && !consentPreferencesListenerAttached) {
    window.addEventListener(OPEN_CONSENT_PREFERENCES_EVENT, handleOpenConsentPreferencesEvent);
    consentPreferencesListenerAttached = true;
  }

  return () => {
    consentPreferenceListeners.delete(listener);

    if (typeof window !== 'undefined' && consentPreferencesListenerAttached && consentPreferenceListeners.size === 0) {
      window.removeEventListener(OPEN_CONSENT_PREFERENCES_EVENT, handleOpenConsentPreferencesEvent);
      consentPreferencesListenerAttached = false;
    }
  };
}

function getOpenConsentPreferencesSnapshot() {
  return openConsentPreferencesTick;
}

function getBrowserId() {
  const existing = localStorage.getItem(BROWSER_ID_STORAGE_KEY);
  if (existing) {
    return existing;
  }

  const next = `browser-${crypto.randomUUID()}`;
  localStorage.setItem(BROWSER_ID_STORAGE_KEY, next);
  return next;
}

export function ConsentBanner() {
  const { pushToast } = useToast();
  const queryClient = useQueryClient();
  const consentBootstrapReady = useIdleTrigger();
  const openPreferencesEventTick = useSyncExternalStore(
    subscribeToOpenConsentPreferences,
    getOpenConsentPreferencesSnapshot,
    getOpenConsentPreferencesSnapshot,
  );
  const [bannerUiState, setBannerUiState] = useState<{ ackTick: number; forceOpen: boolean; manageOpen: boolean }>({
    ackTick: 0,
    forceOpen: false,
    manageOpen: false,
  });
  const [preferenceDraftState, setPreferenceDraftState] = useState<{ key: string; analyticsCookies: boolean; marketingCookies: boolean } | null>(null);
  const browserId = getBrowserId();

  const consentQuery = useQuery({
    queryKey: ['consent-preferences', browserId],
    queryFn: () => advertifiedApi.getConsentPreferences(browserId),
    enabled: consentBootstrapReady,
  });

  const saveMutation = useMutation({
    mutationFn: (input: { analyticsCookies: boolean; marketingCookies: boolean }) => advertifiedApi.saveConsentPreferences({
      browserId,
      analyticsCookies: input.analyticsCookies,
      marketingCookies: input.marketingCookies,
      privacyAccepted: true,
    }),
    onSuccess: (result) => {
      const resultKey = `${result.analyticsCookies ? '1' : '0'}:${result.marketingCookies ? '1' : '0'}:${result.hasSavedPreferences ? '1' : '0'}`;
      setPreferenceDraftState({
        key: resultKey,
        analyticsCookies: result.analyticsCookies,
        marketingCookies: result.marketingCookies,
      });
      queryClient.setQueryData(['consent-preferences', browserId], result);
      setBannerUiState({ ackTick: openPreferencesEventTick, forceOpen: false, manageOpen: false });
      pushToast({
        title: 'Preferences saved.',
        description: 'Your cookie and privacy choices have been saved.',
      });
    },
    onError: (error) => {
      pushToast({
        title: 'Could not save preferences.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    },
  });

  if (!consentBootstrapReady || consentQuery.isLoading || !consentQuery.data) {
    return null;
  }

  const preferenceKey = `${consentQuery.data.analyticsCookies ? '1' : '0'}:${consentQuery.data.marketingCookies ? '1' : '0'}:${consentQuery.data.hasSavedPreferences ? '1' : '0'}`;
  const analyticsCookies = preferenceDraftState?.key === preferenceKey
    ? preferenceDraftState.analyticsCookies
    : consentQuery.data.analyticsCookies;
  const marketingCookies = preferenceDraftState?.key === preferenceKey
    ? preferenceDraftState.marketingCookies
    : consentQuery.data.marketingCookies;
  const eventForcedOpen = openPreferencesEventTick > bannerUiState.ackTick;
  const forceOpen = eventForcedOpen ? true : bannerUiState.forceOpen;
  const manageOpen = eventForcedOpen ? true : bannerUiState.manageOpen;
  const shouldShowBanner = forceOpen || !consentQuery.data.hasSavedPreferences;

  if (!shouldShowBanner) {
    return null;
  }

  const saveAll = () => saveMutation.mutate({ analyticsCookies: true, marketingCookies: true });
  const saveNecessaryOnly = () => saveMutation.mutate({ analyticsCookies: false, marketingCookies: false });
  const saveCustom = () => saveMutation.mutate({ analyticsCookies, marketingCookies });

  return (
    <div className="fixed bottom-0 left-0 right-0 z-[70] border-t border-line bg-white/95 backdrop-blur">
      <div className="mx-auto flex max-w-7xl flex-col gap-3 px-4 py-3 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div className="min-w-0">
            <div className="flex items-center gap-2 text-sm font-semibold text-ink">
              <ShieldCheck className="size-4 text-brand" />
              Cookies and privacy
            </div>
            <p className="mt-1 text-sm leading-6 text-ink-soft">
              We use necessary cookies to run the platform and optional cookies for analytics and marketing-related support. Optional trackers should only be activated after your consent. Read our <Link to="/privacy" className="font-semibold text-brand underline">Privacy Policy</Link> and <Link to="/cookie-policy" className="font-semibold text-brand underline">Cookie Policy</Link>.
            </p>
          </div>

          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={saveNecessaryOnly}
              disabled={saveMutation.isPending}
              className="button-secondary px-4 py-2 disabled:cursor-not-allowed disabled:opacity-60"
            >
              Necessary only
            </button>
            <button
              type="button"
              onClick={saveAll}
              disabled={saveMutation.isPending}
              className="button-primary px-4 py-2 disabled:cursor-not-allowed disabled:opacity-60"
            >
              Accept all
            </button>
            <button
              type="button"
              onClick={() => setBannerUiState((current) => ({
                ackTick: openPreferencesEventTick,
                forceOpen: current.forceOpen,
                manageOpen: !manageOpen,
              }))}
              className="user-btn px-4 py-2"
            >
              {manageOpen ? 'Hide options' : 'Manage'}
            </button>
            {forceOpen ? (
              <button
                type="button"
                onClick={() => {
                  setBannerUiState({ ackTick: openPreferencesEventTick, forceOpen: false, manageOpen: false });
                }}
                className="user-btn px-4 py-2"
              >
                Close
              </button>
            ) : null}
          </div>
        </div>

        {manageOpen ? (
          <div className="grid gap-3 border-t border-line pt-3 md:grid-cols-3">
            <label className="rounded-[18px] border border-line bg-slate-50/70 p-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="text-sm font-semibold text-ink">Necessary cookies</div>
                  <div className="mt-1 text-sm leading-6 text-ink-soft">Required for sign-in, checkout, security, and core platform behavior.</div>
                </div>
                <input type="checkbox" checked readOnly className="mt-1 size-4 accent-brand" />
              </div>
            </label>

            <label className="rounded-[18px] border border-line bg-slate-50/70 p-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="text-sm font-semibold text-ink">Analytics cookies</div>
                  <div className="mt-1 text-sm leading-6 text-ink-soft">Help us understand product usage and improve the platform experience. This category is intended for tools such as Google Analytics when enabled.</div>
                </div>
                <input
                  type="checkbox"
                  checked={analyticsCookies}
                  onChange={(event) => setPreferenceDraftState({
                    key: preferenceKey,
                    analyticsCookies: event.target.checked,
                    marketingCookies,
                  })}
                  className="mt-1 size-4 accent-brand"
                />
              </div>
            </label>

            <label className="rounded-[18px] border border-line bg-slate-50/70 p-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="text-sm font-semibold text-ink">Support and marketing cookies</div>
                  <div className="mt-1 text-sm leading-6 text-ink-soft">Let us remember support preferences and enable advertising-related measurement when approved, for example Meta Pixel or Google Ads tags if introduced later.</div>
                </div>
                <input
                  type="checkbox"
                  checked={marketingCookies}
                  onChange={(event) => setPreferenceDraftState({
                    key: preferenceKey,
                    analyticsCookies,
                    marketingCookies: event.target.checked,
                  })}
                  className="mt-1 size-4 accent-brand"
                />
              </div>
            </label>

            <div className="md:col-span-3 flex flex-wrap gap-2">
              <button
                type="button"
                onClick={saveCustom}
                disabled={saveMutation.isPending}
                className="button-primary px-4 py-2 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {saveMutation.isPending ? 'Saving...' : 'Save preferences'}
              </button>
              <Link to="/cookie-policy" className="button-secondary px-4 py-2">
                Read cookie policy
              </Link>
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}

export const OPEN_CONSENT_PREFERENCES_EVENT = 'advertified:open-consent-preferences';

export function openConsentPreferences() {
  if (typeof window === 'undefined') {
    return;
  }

  window.dispatchEvent(new CustomEvent(OPEN_CONSENT_PREFERENCES_EVENT));
}

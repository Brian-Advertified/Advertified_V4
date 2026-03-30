import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { advertifiedApi } from '../../services/advertifiedApi';
import { useToast } from './toast';

const BROWSER_ID_STORAGE_KEY = 'advertified-browser-id';

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
  const [manageOpen, setManageOpen] = useState(false);
  const [analyticsCookies, setAnalyticsCookies] = useState(false);
  const [marketingCookies, setMarketingCookies] = useState(false);
  const browserId = getBrowserId();

  const consentQuery = useQuery({
    queryKey: ['consent-preferences', browserId],
    queryFn: () => advertifiedApi.getConsentPreferences(browserId),
  });

  const saveMutation = useMutation({
    mutationFn: (input: { analyticsCookies: boolean; marketingCookies: boolean }) => advertifiedApi.saveConsentPreferences({
      browserId,
      analyticsCookies: input.analyticsCookies,
      marketingCookies: input.marketingCookies,
      privacyAccepted: true,
    }),
    onSuccess: (result) => {
      setAnalyticsCookies(result.analyticsCookies);
      setMarketingCookies(result.marketingCookies);
      queryClient.setQueryData(['consent-preferences', browserId], result);
      setManageOpen(false);
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

  if (consentQuery.isLoading || !consentQuery.data || consentQuery.data.hasSavedPreferences) {
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
              We use necessary cookies to run the platform and optional cookies to improve analytics and campaign support. Save your preferences to continue.
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
              onClick={() => setManageOpen((current) => !current)}
              className="user-btn px-4 py-2"
            >
              {manageOpen ? 'Hide options' : 'Manage'}
            </button>
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
                  <div className="mt-1 text-sm leading-6 text-ink-soft">Help us understand product usage and improve the platform experience.</div>
                </div>
                <input
                  type="checkbox"
                  checked={analyticsCookies}
                  onChange={(event) => setAnalyticsCookies(event.target.checked)}
                  className="mt-1 size-4 accent-brand"
                />
              </div>
            </label>

            <label className="rounded-[18px] border border-line bg-slate-50/70 p-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="text-sm font-semibold text-ink">Support and marketing cookies</div>
                  <div className="mt-1 text-sm leading-6 text-ink-soft">Let us remember support preferences and relevant communication settings.</div>
                </div>
                <input
                  type="checkbox"
                  checked={marketingCookies}
                  onChange={(event) => setMarketingCookies(event.target.checked)}
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
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}

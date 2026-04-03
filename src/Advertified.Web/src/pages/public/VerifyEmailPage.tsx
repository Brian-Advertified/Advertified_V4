import { useQuery } from '@tanstack/react-query';
import { MailCheck, RefreshCw } from 'lucide-react';
import { useMemo, useRef, useState } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router-dom';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
import { useAuth } from '../../features/auth/auth-context';
import { advertifiedApi } from '../../services/advertifiedApi';

type VerificationState = 'waiting' | 'verifying' | 'success' | 'error';

export function VerifyEmailPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token')?.trim() ?? '';
  const email = searchParams.get('email')?.trim() ?? '';
  const nextPath = (() => {
    const candidate = searchParams.get('next')?.trim() ?? '';
    return candidate.startsWith('/') ? candidate : '';
  })();
  const [resending, setResending] = useState(false);
  const successToastSentRef = useRef(false);
  const { verifyEmail } = useAuth();
  const { pushToast } = useToast();
  const verificationQuery = useQuery({
    queryKey: ['verify-email', token],
    queryFn: async () => verifyEmail(token),
    enabled: Boolean(token),
    retry: false,
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: Number.POSITIVE_INFINITY,
    refetchOnWindowFocus: false,
  });
  const state: VerificationState = !token
    ? 'waiting'
    : verificationQuery.isPending
      ? 'verifying'
      : verificationQuery.isSuccess
        ? 'success'
        : 'error';
  const requiresPasswordSetup = verificationQuery.data?.requiresPasswordSetup ?? false;
  const errorMessage = verificationQuery.error instanceof Error
    ? verificationQuery.error.message
    : 'We could not activate your account.';

  const title = useMemo(() => {
    if (state === 'success') {
      return 'Your account is active';
    }

    if (state === 'error') {
      return 'That activation link could not be used';
    }

    if (state === 'verifying') {
      return 'Activating your account';
    }

    return 'Check your email to activate';
  }, [state]);

  const redirectPath = requiresPasswordSetup
    ? (nextPath ? `/set-password?next=${encodeURIComponent(nextPath)}` : '/set-password')
    : (nextPath || '/dashboard');

  if (state === 'success' && !successToastSentRef.current) {
    successToastSentRef.current = true;
    pushToast({
      title: 'Your email has been verified.',
      description: requiresPasswordSetup
        ? 'Set your password to finish account setup.'
        : 'Your account is active. Redirecting you now.',
    });
  }

  if (state === 'success') {
    return <Navigate to={redirectPath} replace />;
  }

  async function handleResend() {
    if (!email) {
      pushToast({
        title: 'Add your email address first.',
        description: 'We need your email address before we can send another activation link.',
      }, 'error');
      return;
    }

    try {
      setResending(true);
      await advertifiedApi.resendVerification(email);
      pushToast({
        title: 'A fresh activation email is on its way.',
        description: 'Check your inbox in a moment for the new secure link.',
      });
    } catch (error) {
      pushToast({
        title: 'We could not resend the activation email.',
        description: error instanceof Error ? error.message : 'Please try again in a moment.',
      }, 'error');
    } finally {
      setResending(false);
    }
  }

  return (
    <section className="page-shell py-8 sm:py-12">
      {state === 'verifying' || resending ? (
        <ProcessingOverlay label={state === 'verifying' ? 'Verifying your email...' : 'Sending a fresh activation link...'} />
      ) : null}
      <div className="mx-auto max-w-3xl">
        <div className="panel overflow-hidden">
          <div className="hero-mint px-6 py-8 sm:px-10 sm:py-10">
            <div className="hero-kicker">Account activation</div>
            <h1 className="mt-5 text-3xl font-semibold tracking-tight text-ink sm:text-4xl">{title}</h1>
            <p className="mt-4 max-w-2xl text-sm leading-7 text-ink-soft sm:text-base">
              Activate your Advertified account before signing in, buying packages, or unlocking campaign planning.
            </p>
          </div>

          <div className="px-6 py-8 sm:px-10">
            <div className="hero-glass-card rounded-[26px] px-5 py-6 sm:px-6">
              {state === 'waiting' ? (
                <div className="space-y-4 text-sm text-ink-soft">
                  <div className="flex items-start gap-3">
                    <MailCheck className="mt-1 size-5 text-brand" />
                    <div>
                      <p className="font-semibold text-ink">Open the activation email we just sent{email ? ` to ${email}` : ''}.</p>
                      <p className="mt-2 leading-7">
                        Click the activation link in that email and we&apos;ll bring you straight back here to finish the account setup.
                      </p>
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-3 pt-2">
                    <button
                      type="button"
                      onClick={handleResend}
                      disabled={resending || !email}
                      className="hero-primary-button disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      <RefreshCw className="size-4" />
                      {resending ? 'Resending...' : 'Resend activation email'}
                    </button>
                    <Link to={nextPath ? `/login?next=${encodeURIComponent(nextPath)}` : '/login'} className="hero-secondary-button rounded-full font-semibold">
                      Go to login
                    </Link>
                  </div>
                </div>
              ) : null}

              {state === 'verifying' ? (
                <div className="flex items-start gap-3 text-sm text-ink-soft">
                  <RefreshCw className="mt-1 size-5 animate-spin text-brand" />
                  <div>
                    <p className="font-semibold text-ink">We&apos;re verifying your email now.</p>
                    <p className="mt-2 leading-7">This should only take a moment.</p>
                  </div>
                </div>
              ) : null}

              {state === 'error' ? (
                <div className="space-y-4 text-sm text-ink-soft">
                  <div>
                    <p className="font-semibold text-ink">The activation link is invalid or has expired.</p>
                    <p className="mt-2 leading-7">{errorMessage || 'Request another activation email and we will send a fresh secure link.'}</p>
                  </div>
                  <div className="flex flex-wrap gap-3">
                    <button
                      type="button"
                      onClick={handleResend}
                      disabled={resending || !email}
                      className="hero-primary-button disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      <RefreshCw className="size-4" />
                      {resending ? 'Resending...' : 'Send a new activation link'}
                    </button>
                    <Link
                      to={`/verify-email${email ? `?email=${encodeURIComponent(email)}${nextPath ? `&next=${encodeURIComponent(nextPath)}` : ''}` : ''}`}
                      className="hero-secondary-button rounded-full font-semibold"
                    >
                      Back to activation help
                    </Link>
                  </div>
                </div>
              ) : null}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

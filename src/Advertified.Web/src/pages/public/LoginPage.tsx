import { useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { LoginForm } from '../../features/auth/components/LoginForm';
import { useAuth } from '../../features/auth/auth-context';
import { useToast } from '../../components/ui/toast';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import type { LoginSchema } from '../../features/auth/schemas';

export function LoginPage() {
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const { pushToast } = useToast();
  const navigate = useNavigate();
  const location = useLocation();
  const activated = useMemo(() => new URLSearchParams(location.search).get('activated') === '1', [location.search]);
  const nextPath = useMemo(() => {
    const candidate = new URLSearchParams(location.search).get('next')?.trim() ?? '';
    return candidate.startsWith('/') ? candidate : null;
  }, [location.search]);
  const initialEmail = useMemo(() => new URLSearchParams(location.search).get('email')?.trim() ?? '', [location.search]);
  const existingAccount = useMemo(() => new URLSearchParams(location.search).get('existing') === '1', [location.search]);
  const isContinuingJourney = Boolean(nextPath);

  async function handleSubmit(values: LoginSchema) {
    try {
      setLoading(true);
      const user = await login(values);
      pushToast({
        title: 'Logged in successfully.',
        description: `Welcome back, ${user.fullName.split(' ')[0]}.`,
      });
      const redirectPath =
        (location.state as { from?: string } | null)?.from
        || nextPath
        || (user.role === 'admin' ? '/admin' : user.role === 'creative_director' ? '/creative/studio-demo' : user.role === 'agent' ? '/agent' : '/dashboard');

      navigate(redirectPath, { replace: true });
    } catch (error) {
      pushToast({
        title: 'We could not sign you in.',
        description: error instanceof Error ? error.message : 'Please check your details and try again.',
      }, 'error');
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="page-shell">
      {loading ? <ProcessingOverlay label="Signing you in..." /> : null}
      <div className="mx-auto grid max-w-5xl gap-8 lg:grid-cols-[0.9fr_1.1fr] lg:items-start">
        <div className="hero-mint overflow-hidden rounded-[32px] px-6 py-8 sm:px-8 sm:py-10">
          <div className="hero-kicker">Client, agent, and creative access</div>
          <h1 className="mt-5 text-4xl font-semibold tracking-tight text-ink sm:text-5xl">
            {isContinuingJourney ? 'Sign in to continue where you left off.' : 'Sign in to continue your package or planning journey.'}
          </h1>
          <p className="mt-4 text-base leading-8 text-ink-soft">
            {isContinuingJourney
              ? 'You are returning to an in-progress Advertified step. Sign in and we will take you straight back to it.'
              : 'Clients can continue from package purchase into campaign briefing. Agents can move into campaign operations, while creative directors move straight into the production studio.'}
          </p>
          {activated ? (
            <div className="mt-6 rounded-[22px] border border-brand/20 bg-brand-soft px-4 py-4 text-sm text-brand">
              Your email has been verified. Sign in to continue.
            </div>
          ) : null}
          {existingAccount ? (
            <div className="mt-6 rounded-[22px] border border-brand/20 bg-white/70 px-4 py-4 text-sm text-ink-soft">
              This email already has an Advertified account. Sign in to continue and we will reconnect you to the same journey.
            </div>
          ) : null}
          {isContinuingJourney ? (
            <div className="mt-6 rounded-[22px] border border-brand/20 bg-white/70 px-4 py-4 text-sm text-ink-soft">
              Your progress will continue after sign-in. You do not need to restart the journey.
            </div>
          ) : null}
        </div>
        <LoginForm onSubmit={handleSubmit} loading={loading} initialEmail={initialEmail} />
      </div>
    </section>
  );
}

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

  async function handleSubmit(values: LoginSchema) {
    try {
      setLoading(true);
      const user = await login(values);
      pushToast({
        title: 'Logged in successfully.',
        description: `Welcome back, ${user.fullName.split(' ')[0]}.`,
      });
      navigate(
        (location.state as { from?: string } | null)?.from
          ?? (user.role === 'admin' ? '/admin' : user.role === 'creative_director' ? '/creative' : user.role === 'agent' ? '/agent' : '/dashboard'),
      );
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
        <div className="panel hero-glow px-6 py-8 text-white sm:px-8">
          <div className="pill border-white/10 bg-white/10 text-white/75">Client, agent, and creative access</div>
          <h1 className="mt-5 text-4xl font-semibold tracking-tight sm:text-5xl">Sign in to continue your package or planning journey.</h1>
          <p className="mt-4 text-base leading-8 text-white/75">
            Clients can continue from package purchase into campaign briefing. Agents can move into campaign operations, while creative directors move straight into the production studio.
          </p>
          {activated ? (
            <div className="mt-6 rounded-[22px] border border-emerald-200/40 bg-emerald-400/10 px-4 py-4 text-sm text-white/85">
              Your email has been verified. Sign in to continue.
            </div>
          ) : null}
        </div>
        <LoginForm onSubmit={handleSubmit} loading={loading} />
      </div>
    </section>
  );
}

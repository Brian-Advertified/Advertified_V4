import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../features/auth/auth-context';
import { useToast } from '../../components/ui/toast';

export function SetPasswordPage() {
  const { user, setPassword } = useAuth();
  const { pushToast } = useToast();
  const navigate = useNavigate();
  const [password, setPasswordValue] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [saving, setSaving] = useState(false);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!password.trim() || !confirmPassword.trim()) {
      pushToast({
        title: 'Password is required.',
        description: 'Enter and confirm your new password.',
      }, 'error');
      return;
    }

    if (password.trim() !== confirmPassword.trim()) {
      pushToast({
        title: 'Passwords do not match.',
        description: 'Please make sure both password fields are the same.',
      }, 'error');
      return;
    }

    setSaving(true);
    try {
      const nextUser = await setPassword({ password, confirmPassword });
      pushToast({
        title: 'Password set successfully.',
        description: 'Your account setup is complete.',
      });

      const targetPath = nextUser.role === 'admin'
        ? '/admin'
        : nextUser.role === 'creative_director'
          ? '/creative/studio-demo'
          : nextUser.role === 'agent'
            ? '/agent'
            : '/dashboard';
      navigate(targetPath, { replace: true });
    } catch (error) {
      pushToast({
        title: 'Could not set password.',
        description: error instanceof Error ? error.message : 'Please try again.',
      }, 'error');
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="page-shell">
      <div className="mx-auto max-w-xl">
        <div className="panel px-6 py-7 sm:px-8">
          <div className="hero-kicker">Account setup</div>
          <h1 className="mt-3 text-3xl font-semibold tracking-tight text-ink">Set your password</h1>
          <p className="mt-3 text-sm leading-7 text-ink-soft">
            {user?.email ? `Welcome ${user.email}. ` : ''}
            Before continuing, set your password for future sign-ins.
          </p>

          <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
            <label className="block">
              <span className="text-sm font-semibold text-ink">New password</span>
              <input
                type="password"
                value={password}
                onChange={(event) => setPasswordValue(event.target.value)}
                className="input-base mt-2"
                placeholder="At least 12 characters"
                autoComplete="new-password"
              />
            </label>

            <label className="block">
              <span className="text-sm font-semibold text-ink">Confirm password</span>
              <input
                type="password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                className="input-base mt-2"
                placeholder="Re-enter password"
                autoComplete="new-password"
              />
            </label>

            <p className="text-xs text-ink-soft">
              Use at least 12 characters with uppercase, lowercase, number, and symbol.
            </p>

            <button
              type="submit"
              disabled={saving}
              className="button-primary inline-flex w-full items-center justify-center gap-2 px-5 py-3 disabled:opacity-60"
            >
              {saving ? 'Saving password...' : 'Set password and continue'}
            </button>
          </form>
        </div>
      </div>
    </section>
  );
}

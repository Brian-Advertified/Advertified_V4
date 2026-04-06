import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useToast } from '../../components/ui/toast';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { PageHero } from '../../components/marketing/PageHero';
import { useAuth } from '../../features/auth/auth-context';
import { RegistrationWizard } from '../../features/auth/components/RegistrationWizard';
import type { RegistrationSchema } from '../../features/auth/schemas';

export function RegisterPage() {
  const { register } = useAuth();
  const { pushToast } = useToast();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [loading, setLoading] = useState(false);
  const nextPath = (() => {
    const candidate = searchParams.get('next')?.trim() ?? '';
    return candidate.startsWith('/') ? candidate : '';
  })();
  const isContinuingJourney = nextPath.length > 0;

  async function handleSubmit(values: RegistrationSchema) {
    try {
      setLoading(true);
      const result = await register({
        ...values,
        nextPath: nextPath || undefined,
      });
      pushToast({
        title: 'Your account has been created.',
        description: 'Check your email for the activation link before you sign in.',
      });
      const verifyUrl = nextPath
        ? `/verify-email?email=${encodeURIComponent(result.email)}&next=${encodeURIComponent(nextPath)}`
        : `/verify-email?email=${encodeURIComponent(result.email)}`;
      navigate(verifyUrl);
    } catch (error) {
      pushToast({
        title: 'We could not create your account.',
        description: error instanceof Error ? error.message : 'Please review the form and try again.',
      }, 'error');
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="page-shell py-6 sm:py-10">
      {loading ? <ProcessingOverlay label="Creating your account..." /> : null}
      <div className="register-layout space-y-8">
        <PageHero
          kicker="Create account"
          title={isContinuingJourney ? 'Create your account to continue where you left off.' : 'Register once, then unlock packages, payment, and planning.'}
          description={isContinuingJourney
            ? 'You are continuing an in-progress Advertified journey. Create your account once and we will take you back to the next step after activation.'
            : 'Set up your Advertified account so you can buy a package, complete your campaign brief, and move through planning with confidence.'}
        />
        {isContinuingJourney ? (
          <div className="rounded-[22px] border border-brand/20 bg-brand-soft px-5 py-4 text-sm leading-6 text-brand">
            Your progress is not being reset. Once your email is activated, you will continue from the step you came from.
          </div>
        ) : null}
        <RegistrationWizard onSubmit={handleSubmit} loading={loading} />
      </div>
    </section>
  );
}

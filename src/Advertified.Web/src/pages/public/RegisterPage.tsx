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
          title="Register once, then unlock packages, payment, and planning."
          description="Set up your Advertified account so you can buy a package, complete your campaign brief, and move through planning with confidence."
        />
        <RegistrationWizard onSubmit={handleSubmit} loading={loading} />
      </div>
    </section>
  );
}

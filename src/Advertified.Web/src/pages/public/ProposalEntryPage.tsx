import { ArrowRight, FileText } from 'lucide-react';
import { Link, Navigate, useParams, useSearchParams } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';
import { useAuth } from '../../features/auth/auth-context';

function getSafeNextPath(raw: string | null) {
  if (!raw) {
    return null;
  }

  const trimmed = raw.trim();
  return trimmed.startsWith('/') ? trimmed : null;
}

export function ProposalEntryPage() {
  const { id = '' } = useParams();
  const [searchParams] = useSearchParams();
  const { isAuthenticated } = useAuth();
  const recommendationId = searchParams.get('recommendationId')?.trim() ?? '';
  const requestedAction = searchParams.get('action')?.trim() ?? '';
  const action = requestedAction === 'reject_all' ? 'reject_all' : '';

  if (!id) {
    return <Navigate to="/" replace />;
  }

  const queryParams = new URLSearchParams();
  if (recommendationId) {
    queryParams.set('recommendationId', recommendationId);
  }
  if (action) {
    queryParams.set('action', action);
  }
  const query = queryParams.toString();
  const approvalsPath = query
    ? `/campaigns/${encodeURIComponent(id)}/approvals?${query}`
    : `/campaigns/${encodeURIComponent(id)}/approvals`;
  const safeNextPath = getSafeNextPath(approvalsPath) ?? `/campaigns/${encodeURIComponent(id)}/approvals`;

  if (isAuthenticated) {
    return <Navigate to={safeNextPath} replace />;
  }

  const loginPath = `/login?next=${encodeURIComponent(safeNextPath)}`;
  const registerPath = `/register?next=${encodeURIComponent(safeNextPath)}`;

  return (
    <section className="page-shell space-y-8 pb-20">
      <PageHero
        kicker="Proposal review"
        title="Your Advertified proposal is ready."
        description="Sign in or create your account to open this proposal and continue to approval and payment."
      />

      <div className="mx-auto grid max-w-4xl gap-6 lg:grid-cols-[1.15fr_0.85fr]">
        <div className="rounded-[24px] border border-line bg-white p-6 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
          <div className="inline-flex items-center gap-2 rounded-full border border-brand/20 bg-brand-soft px-3 py-1 text-xs font-semibold uppercase tracking-[0.14em] text-brand">
            <FileText className="size-4" />
            Proposal access
          </div>
          <p className="mt-4 text-sm leading-7 text-ink-soft">
            This secure link opens your campaign proposal workspace where you can review Proposal A/B/C, accept one option, or request changes.
          </p>
          {action === 'reject_all' ? (
            <div className="mt-4 rounded-[14px] border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
              We&apos;ll take you straight to the feedback form so you can request a new proposal set.
            </div>
          ) : null}
          {recommendationId ? (
            <div className="mt-4 rounded-[14px] border border-brand/20 bg-brand/[0.06] px-4 py-3 text-sm text-ink">
              A specific proposal was preselected from your email or PDF link.
            </div>
          ) : null}
        </div>

        <aside className="rounded-[24px] border border-line bg-white p-6 shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
          <p className="text-sm font-semibold text-ink">Continue securely</p>
          <div className="mt-4 space-y-3">
            <Link to={loginPath} className="button-primary flex w-full items-center justify-center gap-2 px-5 py-3">
              Sign in to review
              <ArrowRight className="size-4" />
            </Link>
            <Link to={registerPath} className="button-secondary flex w-full items-center justify-center gap-2 px-5 py-3">
              Create account and continue
            </Link>
          </div>
        </aside>
      </div>
    </section>
  );
}

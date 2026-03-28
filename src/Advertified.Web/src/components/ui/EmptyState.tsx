import { ArrowRight } from 'lucide-react';
import { Link } from 'react-router-dom';

export function EmptyState({
  title,
  description,
  ctaLabel,
  ctaHref,
}: {
  title: string;
  description: string;
  ctaLabel?: string;
  ctaHref?: string;
}) {
  return (
    <div className="panel flex flex-col items-start gap-5 px-6 py-8 sm:px-8">
      <div className="pill bg-highlight-soft text-highlight">No items yet</div>
      <div>
        <h3 className="text-xl font-semibold text-ink">{title}</h3>
        <p className="mt-3 max-w-xl text-sm leading-7 text-ink-soft">{description}</p>
      </div>
      {ctaLabel && ctaHref ? (
        <Link
          to={ctaHref}
          className="inline-flex items-center gap-2 rounded-full bg-ink px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand"
        >
          {ctaLabel}
          <ArrowRight className="size-4" />
        </Link>
      ) : null}
    </div>
  );
}

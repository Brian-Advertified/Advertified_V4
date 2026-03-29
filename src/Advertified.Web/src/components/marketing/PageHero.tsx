import type { ReactNode } from 'react';

export function PageHero({
  kicker,
  title,
  description,
  actions,
  aside,
}: {
  kicker: string;
  title: string;
  description?: string;
  actions?: ReactNode;
  aside?: ReactNode;
}) {
  return (
    <div className="hero-mint overflow-hidden rounded-[30px] px-6 py-7 sm:px-8 sm:py-8 lg:px-10">
      <div className={`grid gap-6 ${aside ? 'lg:grid-cols-[minmax(0,1fr)_minmax(220px,0.72fr)] lg:items-end' : ''}`}>
        <div className="max-w-4xl">
          <div className="hero-kicker">{kicker}</div>
          <h1 className="mt-4 text-3xl font-semibold tracking-tight text-ink sm:text-4xl lg:text-[2.6rem]">
            {title}
          </h1>
          {description ? (
            <p className="mt-4 max-w-3xl text-sm leading-7 text-ink-soft sm:text-base">
              {description}
            </p>
          ) : null}
          {actions ? <div className="mt-6 flex flex-wrap gap-3">{actions}</div> : null}
        </div>
        {aside ? <div className="hero-glass-card rounded-[24px] px-5 py-5 text-ink">{aside}</div> : null}
      </div>
    </div>
  );
}

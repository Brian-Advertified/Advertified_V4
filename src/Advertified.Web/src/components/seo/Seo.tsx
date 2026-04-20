import { useEffect } from 'react';
import type { SeoMeta } from '../../lib/seo';
import { applySeo } from '../../lib/seo';

type JsonLd = Record<string, unknown> | Array<Record<string, unknown>>;

type SeoProps = SeoMeta & {
  jsonLd?: JsonLd;
};

export function Seo({ jsonLd, ...meta }: SeoProps) {
  useEffect(() => {
    applySeo(meta);
  }, [meta.description, meta.noindex, meta.path, meta.title, meta.type]);

  useEffect(() => {
    if (!jsonLd) {
      return undefined;
    }

    const script = document.createElement('script');
    script.type = 'application/ld+json';
    script.dataset.managedSeoJsonld = 'true';
    script.text = JSON.stringify(jsonLd);
    document.head.appendChild(script);

    return () => {
      script.remove();
    };
  }, [jsonLd]);

  return null;
}

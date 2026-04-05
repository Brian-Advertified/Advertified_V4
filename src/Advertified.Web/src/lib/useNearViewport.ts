import { useEffect, useState, type RefObject } from 'react';

export function useNearViewport<TElement extends Element>(
  ref: RefObject<TElement | null>,
  rootMargin = '280px',
) {
  const [isNearViewport, setIsNearViewport] = useState(false);

  useEffect(() => {
    if (isNearViewport) {
      return;
    }

    const node = ref.current;
    if (!node) {
      return;
    }

    if (typeof IntersectionObserver === 'undefined') {
      setIsNearViewport(true);
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          setIsNearViewport(true);
          observer.disconnect();
        }
      },
      {
        rootMargin,
      },
    );

    observer.observe(node);
    return () => observer.disconnect();
  }, [isNearViewport, ref, rootMargin]);

  return isNearViewport;
}

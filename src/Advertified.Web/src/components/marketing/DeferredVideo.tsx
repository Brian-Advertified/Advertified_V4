import { CirclePlay } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

export function DeferredVideo({
  src,
  loadSrc,
  posterSrc,
  className,
  title,
}: {
  src?: string;
  loadSrc?: () => Promise<string>;
  posterSrc?: string;
  className?: string;
  title: string;
}) {
  const [resolvedSrc, setResolvedSrc] = useState(src);
  const [isLoaded, setIsLoaded] = useState(false);
  const [isPreparing, setIsPreparing] = useState(false);
  const videoRef = useRef<HTMLVideoElement | null>(null);

  useEffect(() => {
    if (!isLoaded || !videoRef.current) {
      return;
    }

      void videoRef.current.play().catch(() => {
      // Keep controls available even if autoplay is blocked.
    });
  }, [isLoaded]);

  async function handleLoad() {
    if (isLoaded || isPreparing) {
      return;
    }

    if (resolvedSrc) {
      setIsLoaded(true);
      return;
    }

    if (!loadSrc) {
      return;
    }

    setIsPreparing(true);
    try {
      const nextSrc = await loadSrc();
      setResolvedSrc(nextSrc);
      setIsLoaded(true);
    } finally {
      setIsPreparing(false);
    }
  }

  if (!isLoaded) {
    return (
      <button
        type="button"
        onClick={() => void handleLoad()}
        className={[
          'group relative flex w-full items-center justify-center overflow-hidden bg-slate-950 text-white',
          className ?? '',
        ].join(' ')}
        aria-label={`Load ${title} video`}
        disabled={isPreparing}
      >
        {posterSrc ? (
          <div
            className="absolute inset-0 bg-cover bg-center"
            style={{ backgroundImage: `url("${posterSrc}")` }}
          />
        ) : (
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_top,#1e293b_0%,#0f172a_55%,#020617_100%)]" />
        )}
        <div className="absolute inset-0 bg-slate-950/35" />
        <div className="absolute inset-0 opacity-40 [background-image:linear-gradient(rgba(255,255,255,0.08)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.08)_1px,transparent_1px)] [background-size:24px_24px]" />
        <div className="relative flex flex-col items-center gap-3 px-6 text-center">
          <span className="flex size-14 items-center justify-center rounded-full bg-white/12 transition group-hover:bg-white/18">
            <CirclePlay className="size-8" />
          </span>
          <span className="text-sm font-semibold tracking-wide">
            {isPreparing ? 'Preparing video...' : 'Load intro video'}
          </span>
          <span className="max-w-[15rem] text-xs leading-6 text-white/70">
            Video loads only when you choose to play it, keeping the page lighter on first load.
          </span>
        </div>
      </button>
    );
  }

  return (
    <video
      ref={videoRef}
      className={className}
      controls
      preload="metadata"
      playsInline
    >
      <source src={resolvedSrc} type="video/mp4" />
      Your browser does not support the video tag.
    </video>
  );
}

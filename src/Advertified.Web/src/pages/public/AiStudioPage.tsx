import { useEffect, useMemo, useRef, useState } from 'react';
import billboardImage from '../../assets/Channels/optimized/billboard-sa-optimized.jpg';
import radioImage from '../../assets/Channels/optimized/radio-sa-optimized.jpg';
import tvImage from '../../assets/Channels/optimized/tv-sa-optimized.jpg';
import socialImage from '../../assets/Channels/optimized/social-platforms-optimized.jpg';

type ChannelKey = 'billboard' | 'radio' | 'tv' | 'social';
type FilterKey = 'all' | ChannelKey;

const filters: Array<{ key: FilterKey; label: string }> = [
  { key: 'all', label: 'All' },
  { key: 'billboard', label: 'Billboards' },
  { key: 'radio', label: 'Radio' },
  { key: 'tv', label: 'TV & Video' },
  { key: 'social', label: 'Social' },
];

const tiles = [
  {
    key: 'billboard' as const,
    channel: 'Billboards, Digital Screens',
    title: ['Outdoor headlines', '& visual direction.'],
    subtitle: 'Campaign-specific copy, format notes, and visual briefs for real placements.',
  },
  {
    key: 'radio' as const,
    channel: 'Radio & TV Audio',
    title: ['Scripts, voice routes', '& broadcast lines.'],
    subtitle: '30-second spots with CTA, voice direction, and language-ready adaptations.',
  },
  {
    key: 'tv' as const,
    channel: 'TV & Video',
    title: ['Shot guides &', 'scene structure.'],
    subtitle: 'Visual direction, VO scripts, and production notes for campaign video.',
  },
  {
    key: 'social' as const,
    channel: 'Social & Digital',
    title: ['Channel-native cutdowns', '& platform variants.'],
    subtitle: 'Platform-ready captions, creative direction, and mobile-first formats from one idea.',
  },
];

const stripFacts = [
  'Start with one approved brief and one clear objective.',
  'Studio builds channel-specific assets across every format.',
  'Review, approve, and move straight to booking and launch.',
];

const flowSteps = [
  ['Package & Campaign', 'Select your package and set the campaign objective.', false],
  ['Brief & Recommendation', 'Campaign brief is built and a media recommendation generated.', false],
  ['Client Approval', 'You review and sign off before production starts.', true],
  ['Studio Creates', 'Scripts, shot guides, headlines, and asset packs are built.', true],
  ['Bookings & Launch', 'Approved content moves directly to booking and goes live.', false],
] as const;

const radioHeights = [6, 11, 18, 26, 33, 38, 35, 28, 38, 34, 26, 18, 10, 5, 9, 22, 36, 32, 20, 8, 15, 29, 37, 25, 13];

function TileVisual({ channel }: { channel: ChannelKey }) {
  if (channel === 'billboard') {
    return (
      <div className="studio-v1 studio-tile-visual">
        <img src={billboardImage} alt="Billboards, Digital Screens campaign mockup" className="studio-tile-photo" />
        <div className="studio-tile-photo-wash" />
        <div className="studio-v1-sky" />
        <div className="studio-v1-stars" />
        <div className="studio-v1-city">
          {[55, 78, 44, 92, 62, 38, 82, 50, 70, 41, 60, 85].map((height, index) => (
            <div key={`${channel}-${height}-${index}`} className="studio-v1-building" style={{ height: `${height}%` }} />
          ))}
        </div>
        <div className="studio-v1-road" />
        <div className="studio-v1-board">
          <div className="studio-v1-face">
            <div className="studio-v1-copy">
              <small>Billboards, Digital Screens</small>
              Make them
              <br />
              stop & look.
            </div>
          </div>
          <div className="studio-v1-poles"><i /><i /></div>
        </div>
      </div>
    );
  }

  if (channel === 'radio') {
    return (
      <div className="studio-v2 studio-tile-visual">
        <img src={radioImage} alt="Radio campaign mockup" className="studio-tile-photo" />
        <div className="studio-tile-photo-wash" />
        <div className="studio-v2-rings">{[0, 1, 2, 3].map((ring) => <div key={`${channel}-${ring}`} className="studio-v2-ring" />)}</div>
        <div className="studio-v2-core">
          <div className="studio-v2-dial">
            <div className="studio-v2-dial-inner" />
            <div className="studio-v2-needle" />
            <div className="studio-v2-center-dot" />
          </div>
          <div className="studio-v2-wave">
            {radioHeights.map((height, index) => (
              <div key={`${channel}-${height}-${index}`} className="studio-v2-bar" style={{ height: `${height}px`, animationDelay: `${(index * 0.065).toFixed(3)}s` }} />
            ))}
          </div>
          <div className="studio-v2-label">On air / 30-sec spot</div>
        </div>
      </div>
    );
  }

  if (channel === 'tv') {
    return (
      <div className="studio-v3 studio-tile-visual">
        <img src={tvImage} alt="TV storyboard campaign mockup" className="studio-tile-photo" />
        <div className="studio-tile-photo-wash" />
        <div className="studio-v3-band-top" />
        <div className="studio-v3-band-bottom" />
        <div className="studio-v3-scan" />
        <div className="studio-v3-top-copy"><span>CAM A / SCENE 03</span><span className="studio-tc">00:00:14:22</span></div>
        <div className="studio-v3-content">
          <div className="studio-v3-play"><div className="studio-v3-arrow" /></div>
          <div className="studio-v3-scene">Scene 3 - Product reveal<br />close-up, warm light.</div>
          <div className="studio-v3-shot">Shot guide / 30 sec TVC</div>
        </div>
        <div className="studio-v3-progress">
          <div className="studio-v3-pause-marker" aria-hidden="true">
            <span />
            <span />
          </div>
          <div className="studio-v3-fill" />
        </div>
      </div>
    );
  }

  return (
    <div className="studio-v4 studio-tile-visual">
      <img src={socialImage} alt="Social campaign mockup" className="studio-tile-photo" />
      <div className="studio-tile-photo-wash" />
      <div className="studio-v4-stack">
        <div className="studio-v4-card studio-v4-card-back">
          <div className="studio-v4-card-label">Story set</div>
          <div className="studio-v4-card-metrics">
            <span />
            <span />
            <span />
          </div>
        </div>
        <div className="studio-v4-phone">
          <div className="studio-v4-status-bar"><div className="studio-v4-notch" /></div>
          <div className="studio-v4-app-bar"><div className="studio-v4-dot" /><div className="studio-v4-icons"><div className="studio-v4-icon" /><div className="studio-v4-icon" /></div></div>
          <div className="studio-v4-feed">
            <div className="studio-v4-post">
              <div className="studio-v4-image studio-bb studio-v4-image-tall">Reel</div>
              <div className="studio-v4-post-body">
                <div className="studio-v4-line" />
                <div className="studio-v4-line studio-v4-line-short" />
                <div className="studio-v4-actions"><div /><div /><div /></div>
              </div>
            </div>
            <div className="studio-v4-post">
              <div className="studio-v4-image studio-ba">Feed</div>
              <div className="studio-v4-post-body">
                <div className="studio-v4-line" />
                <div className="studio-v4-line studio-v4-line-short" />
              </div>
            </div>
          </div>
        </div>
        <div className="studio-v4-card studio-v4-card-front">
          <div className="studio-v4-card-label">Paid social</div>
          <div className="studio-v4-card-preview studio-bc">Ad cutdown</div>
        </div>
      </div>
    </div>
  );
}

export function AiStudioPage() {
  const pageRef = useRef<HTMLDivElement | null>(null);
  const [activeFilter, setActiveFilter] = useState<FilterKey>('all');
  const [heroReady, setHeroReady] = useState(false);
  const [loaderStage, setLoaderStage] = useState<'loading' | 'revealing' | 'done'>('loading');
  const [loaderCount, setLoaderCount] = useState(0);
  const [cursor, setCursor] = useState({ x: 0, y: 0, visible: false, large: false });

  const visibleTiles = useMemo(() => tiles.filter((tile) => activeFilter === 'all' || tile.key === activeFilter), [activeFilter]);

  useEffect(() => {
    const body = document.body;
    const previousOverflow = body.style.overflow;
    body.style.overflow = 'hidden';
    const timers: number[] = [];

    timers.push(window.setTimeout(() => {
      let count = 0;
      const tick = () => {
        count += 1;
        setLoaderCount(count);
        if (count >= 100) {
          setLoaderStage('revealing');
          timers.push(window.setTimeout(() => {
            setLoaderStage('done');
            setHeroReady(true);
            body.style.overflow = previousOverflow;
          }, 950));
          return;
        }

        const nextDelay = count < 25 ? 28 : count < 60 ? 18 : count < 85 ? 12 : 8;
        timers.push(window.setTimeout(tick, nextDelay));
      };
      tick();
    }, 920));

    timers.push(window.setTimeout(() => setHeroReady(true), 1700));

    return () => {
      timers.forEach((timer) => window.clearTimeout(timer));
      body.style.overflow = previousOverflow;
    };
  }, []);

  useEffect(() => {
    const mediaQuery = window.matchMedia('(pointer: fine)');
    if (!mediaQuery.matches) {
      return undefined;
    }

    let frameId = 0;
    let targetX = 0;
    let targetY = 0;
    let currentX = 0;
    let currentY = 0;

    const animate = () => {
      currentX += (targetX - currentX) * 0.12;
      currentY += (targetY - currentY) * 0.12;
      setCursor((current) => ({ ...current, x: currentX, y: currentY }));
      frameId = window.requestAnimationFrame(animate);
    };

    const handleMove = (event: MouseEvent) => {
      targetX = event.clientX;
      targetY = event.clientY;
      setCursor((current) => ({ ...current, visible: true }));
    };

    const handleLeave = () => setCursor((current) => ({ ...current, visible: false }));
    const handlePointerOver = (event: Event) => {
      const growTarget = event.target instanceof Element ? event.target.closest('[data-cursor-grow="true"]') : null;
      setCursor((current) => ({ ...current, large: Boolean(growTarget) }));
    };

    frameId = window.requestAnimationFrame(animate);
    window.addEventListener('mousemove', handleMove);
    window.addEventListener('mouseleave', handleLeave);
    document.addEventListener('mouseover', handlePointerOver);

    return () => {
      window.cancelAnimationFrame(frameId);
      window.removeEventListener('mousemove', handleMove);
      window.removeEventListener('mouseleave', handleLeave);
      document.removeEventListener('mouseover', handlePointerOver);
    };
  }, []);

  useEffect(() => {
    const root = pageRef.current;
    if (!root) {
      return undefined;
    }

    const nodes = Array.from(root.querySelectorAll<HTMLElement>('[data-reveal], [data-scroll-reveal]'));
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('in');
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.1 });

    nodes.forEach((node) => observer.observe(node));
    return () => observer.disconnect();
  }, [activeFilter]);

  useEffect(() => {
    const pad = (value: number) => String(value).padStart(2, '0');
    let frame = 22;
    let second = 14;
    let minute = 0;

    const interval = window.setInterval(() => {
      frame = (frame + 1) % 25;
      if (frame === 0) {
        second = (second + 1) % 60;
        if (second === 0) {
          minute = (minute + 1) % 60;
        }
      }

      const value = `00:${pad(minute)}:${pad(second)}:${pad(frame)}`;
      document.querySelectorAll<HTMLElement>('.studio-tc').forEach((node) => {
        node.textContent = value;
      });
    }, 40);

    return () => window.clearInterval(interval);
  }, [activeFilter]);

  return (
    <div ref={pageRef} className="studio-page">
      <div className={`studio-cursor ${cursor.large ? 'big' : ''} ${cursor.visible ? '' : 'hidden'} hidden md:block`} style={{ transform: `translate(${cursor.x}px, ${cursor.y}px) translate(-50%, -50%)` }} />

      {loaderStage !== 'done' ? (
        <>
          <div className={`studio-loader ${loaderStage}`}>
            <div className="studio-loader-panel studio-loader-top" />
            <div className="studio-loader-panel studio-loader-bottom" />
          </div>
          <div className={`studio-loader-center ${loaderStage === 'revealing' ? 'fade' : ''}`}>
            <div className="studio-loader-eyebrow">Advertified Studio</div>
            <div className="studio-loader-word-wrap"><div className="studio-loader-word">Advertified</div></div>
            <div className="studio-loader-line" />
            <div className="studio-loader-counter"><div className="studio-loader-dot" /><span>{loaderCount}</span></div>
          </div>
        </>
      ) : null}

      <section className="studio-hero">
        <div className="studio-hero-bg">
          <div className={`studio-hero-grid ${heroReady ? 'in' : ''}`} />
          <div className={`studio-hero-ghost ${heroReady ? 'in' : ''}`}>Studio</div>
        </div>
        <div className="studio-hero-content">
          <p className="studio-hero-eyebrow"><span className={heroReady ? 'in' : ''}>Advertified Studio - Creative Production for Every Channel</span></p>
          <div className="studio-hero-title">
            <div className="studio-title-line"><span className={`studio-title-inner ${heroReady ? 'in' : ''}`}>From the brief</span></div>
            <div className="studio-title-line"><span className={`studio-title-inner ${heroReady ? 'in' : ''}`} style={{ transitionDelay: '160ms' }}>to <em>every channel.</em></span></div>
            <div className="studio-title-line"><span className={`studio-title-inner ${heroReady ? 'in' : ''}`} style={{ transitionDelay: '320ms' }}>In-house.</span></div>
          </div>
          <div className={`studio-hero-bottom ${heroReady ? 'in' : ''}`}>
            <div className="studio-filter-row">
              {filters.map((filter) => (
                <button key={filter.key} type="button" data-cursor-grow="true" className={`studio-filter ${activeFilter === filter.key ? 'active' : ''}`} onClick={() => setActiveFilter(filter.key)}>
                  {filter.label}
                </button>
              ))}
            </div>
            <div className="studio-scroll">Scroll</div>
          </div>
        </div>
      </section>

      <div className="studio-tiles">
        {visibleTiles.map((tile) => (
          <div key={tile.key} className="studio-tile" data-reveal>
            <TileVisual channel={tile.key} />
            <div className="studio-tile-overlay" />
            <div className="studio-tile-info">
              <div>
                <div className="studio-tile-channel">{tile.channel}</div>
                <div className="studio-tile-title">{tile.title[0]}<br />{tile.title[1]}</div>
                <div className="studio-tile-subtitle">{tile.subtitle}</div>
              </div>
            </div>
            <div className="studio-tile-cta-wrap">
              <div className="studio-tile-cta">View outputs</div>
            </div>
          </div>
        ))}
      </div>

      <section className="studio-strip">
        <div data-scroll-reveal>
          <p className="studio-strip-tag"><em>Advertified Studio</em> is where your approved campaign becomes <em>real content.</em></p>
        </div>
        <div className="studio-strip-facts">
          {stripFacts.map((fact, index) => (
            <div key={fact} className={`studio-fact studio-delay-${index + 1}`} data-scroll-reveal>
              <div className="studio-fact-number">{`0${index + 1}`}</div>
              <div className="studio-fact-detail">{fact}</div>
            </div>
          ))}
        </div>
      </section>

      <section className="studio-how">
        <div className="studio-how-head">
          <div className="studio-how-title" data-scroll-reveal>The flow.</div>
          <div className="studio-how-note studio-delay-1" data-scroll-reveal>One continuous process - no re-formatting, no lost briefs between teams.</div>
        </div>
        <div className="studio-how-steps">
          {flowSteps.map(([title, text, highlight], index) => (
            <div key={title} className={`studio-step${highlight ? ' highlight' : ''} studio-delay-${Math.min(index + 1, 4)}`} data-scroll-reveal>
              <div className="studio-step-number">{`0${index + 1}`}</div>
              <h4>{title}</h4>
              <p>{text}</p>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

export const AiStudioConsolePage = AiStudioPage;

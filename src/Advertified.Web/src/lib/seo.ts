export type SeoMeta = {
  title: string;
  description: string;
  path?: string;
  noindex?: boolean;
  type?: 'website' | 'article';
};

export const SITE_NAME = 'Advertified';
export const SITE_URL = 'https://www.advertified.com';
export const DEFAULT_TITLE = 'Advertified | Advertising Packages and Media Buying in South Africa';
export const DEFAULT_DESCRIPTION = 'Advertified helps businesses in South Africa start advertising with clear packages, guided campaign planning, and multi-channel media buying support.';

const routeSeo: Record<string, SeoMeta> = {
  '/': {
    title: DEFAULT_TITLE,
    description: DEFAULT_DESCRIPTION,
    path: '/',
    type: 'website',
  },
  '/packages': {
    title: 'Advertising Packages in South Africa | Advertified',
    description: 'Browse Advertified package bands, choose the budget that fits your campaign, and move into guided advertising planning with clearer pricing.',
    path: '/packages/',
    type: 'website',
  },
  '/how-it-works': {
    title: 'How Advertified Works | Guided Advertising Planning',
    description: 'See how Advertified takes businesses from package purchase to campaign recommendation, approval, creative production, and launch.',
    path: '/how-it-works/',
    type: 'website',
  },
  '/about': {
    title: 'About Advertified | Advertising Platform for South African Businesses',
    description: 'Learn how Advertified helps businesses buy advertising with more clarity through package-led planning, campaign workflows, and multi-channel execution.',
    path: '/about/',
    type: 'website',
  },
  '/faq': {
    title: 'Advertified FAQ | Packages, Payments, Campaigns, and Launch',
    description: 'Read common questions about Advertified packages, payment, campaign approvals, creative production, launch workflows, and support.',
    path: '/faq/',
    type: 'website',
  },
  '/billboard-advertising-south-africa': {
    title: 'Billboard Advertising in South Africa | Advertified',
    description: 'Explore billboard advertising in South Africa with Advertified, including budget-led planning, package-led campaign setup, and support across Billboards and Digital Screens.',
    path: '/billboard-advertising-south-africa/',
    type: 'website',
  },
  '/radio-advertising-south-africa': {
    title: 'Radio Advertising in South Africa | Advertified',
    description: 'Learn how Advertified helps businesses approach radio advertising in South Africa through budget-led campaign planning, guided recommendations, and structured activation.',
    path: '/radio-advertising-south-africa/',
    type: 'website',
  },
  '/tv-advertising-south-africa': {
    title: 'TV Advertising in South Africa | Advertified',
    description: 'Discover a clearer route into TV advertising in South Africa with Advertified, including package-led campaign planning and structured support across the approval and launch journey.',
    path: '/tv-advertising-south-africa/',
    type: 'website',
  },
  '/digital-advertising-south-africa': {
    title: 'Digital Advertising in South Africa | Advertified',
    description: 'See how Advertified supports digital advertising in South Africa with package-led planning, guided campaign setup, and multi-channel support across social, SMS, and digital touchpoints.',
    path: '/digital-advertising-south-africa/',
    type: 'website',
  },
  '/media-partners': {
    title: 'Media Partners | Advertified',
    description: 'Partner with Advertified to connect premium billboard, radio, TV, press, and venue inventory to structured advertiser demand.',
    path: '/media-partners/',
    type: 'website',
  },
  '/partner-enquiry': {
    title: 'Become a Media Partner | Advertified',
    description: 'Submit a partner enquiry to Advertified and tell us about your media inventory, venue network, or channel footprint.',
    path: '/partner-enquiry/',
    type: 'website',
  },
  '/privacy': {
    title: 'Privacy Policy | Advertified',
    description: 'Read how Advertified collects, uses, stores, and protects personal and business information.',
    path: '/privacy/',
    type: 'website',
  },
  '/cookie-policy': {
    title: 'Cookie Policy | Advertified',
    description: 'Learn how Advertified uses cookies and manages necessary, analytics, and marketing consent preferences.',
    path: '/cookie-policy/',
    type: 'website',
  },
  '/terms-of-service': {
    title: 'Terms and Conditions | Advertified',
    description: 'Review Advertified commercial terms covering proposals, bookings, payments, campaign execution, and related obligations.',
    path: '/terms-of-service/',
    type: 'website',
  },
  '/login': {
    title: 'Log In | Advertified',
    description: 'Log in to your Advertified workspace.',
    path: '/login/',
    noindex: true,
    type: 'website',
  },
  '/register': {
    title: 'Create Your Advertified Account',
    description: 'Create your Advertified account to start your campaign journey.',
    path: '/register/',
    noindex: true,
    type: 'website',
  },
  '/verify-email': {
    title: 'Verify Email | Advertified',
    description: 'Verify your email address for Advertified.',
    path: '/verify-email/',
    noindex: true,
    type: 'website',
  },
  '/set-password': {
    title: 'Set Password | Advertified',
    description: 'Set your Advertified password.',
    path: '/set-password/',
    noindex: true,
    type: 'website',
  },
  '/start-campaign': {
    title: 'Start Your Campaign Brief | Advertified',
    description: 'Start your Advertified campaign questionnaire.',
    path: '/start-campaign/',
    noindex: true,
    type: 'website',
  },
  '/checkout/payment': {
    title: 'Checkout | Advertified',
    description: 'Complete your Advertified payment.',
    path: '/checkout/payment/',
    noindex: true,
    type: 'website',
  },
  '/checkout/confirmation': {
    title: 'Checkout Confirmation | Advertified',
    description: 'Your Advertified order confirmation.',
    path: '/checkout/confirmation/',
    noindex: true,
    type: 'website',
  },
  '/ai-studio': {
    title: 'Advertified Studio',
    description: 'Advertified Studio creative workspace.',
    path: '/ai-studio',
    noindex: true,
    type: 'website',
  },
};

function upsertMeta(name: string, content: string, attr: 'name' | 'property' = 'name') {
  let element = document.head.querySelector<HTMLMetaElement>(`meta[${attr}="${name}"]`);
  if (!element) {
    element = document.createElement('meta');
    element.setAttribute(attr, name);
    document.head.appendChild(element);
  }

  element.setAttribute('content', content);
}

function upsertLink(rel: string, href: string) {
  let element = document.head.querySelector<HTMLLinkElement>(`link[rel="${rel}"]`);
  if (!element) {
    element = document.createElement('link');
    element.setAttribute('rel', rel);
    document.head.appendChild(element);
  }

  element.setAttribute('href', href);
}

function removeMeta(name: string, attr: 'name' | 'property' = 'name') {
  document.head.querySelector(`meta[${attr}="${name}"]`)?.remove();
}

export function buildAbsoluteUrl(path = '/') {
  const normalizedPath = path === '/' ? '/' : `/${path.replace(/^\/+/, '')}`;
  return new URL(normalizedPath, SITE_URL).toString();
}

export function applySeo(meta: SeoMeta) {
  const canonicalUrl = buildAbsoluteUrl(meta.path ?? window.location.pathname);
  document.title = meta.title;

  upsertMeta('description', meta.description);
  upsertMeta('robots', meta.noindex ? 'noindex, nofollow' : 'index, follow');
  upsertLink('canonical', canonicalUrl);

  upsertMeta('og:title', meta.title, 'property');
  upsertMeta('og:description', meta.description, 'property');
  upsertMeta('og:url', canonicalUrl, 'property');
  upsertMeta('og:site_name', SITE_NAME, 'property');
  upsertMeta('og:type', meta.type ?? 'website', 'property');

  upsertMeta('twitter:card', 'summary');
  upsertMeta('twitter:title', meta.title);
  upsertMeta('twitter:description', meta.description);

  removeMeta('twitter:image');
  removeMeta('og:image', 'property');
}

export function clearManagedStructuredData() {
  document.head
    .querySelectorAll('script[data-managed-seo-jsonld="true"]')
    .forEach((element) => element.remove());
}

export function getRouteSeo(pathname: string): SeoMeta {
  if (routeSeo[pathname]) {
    return routeSeo[pathname];
  }

  if (
    pathname.startsWith('/agent')
    || pathname.startsWith('/admin')
    || pathname.startsWith('/dashboard')
    || pathname.startsWith('/proposal/')
    || pathname.startsWith('/lead-proposal/')
    || pathname.startsWith('/creative/')
  ) {
    return {
      title: SITE_NAME,
      description: DEFAULT_DESCRIPTION,
      path: pathname,
      noindex: true,
      type: 'website',
    };
  }

  return {
    title: DEFAULT_TITLE,
    description: DEFAULT_DESCRIPTION,
    path: pathname,
    noindex: false,
    type: 'website',
  };
}

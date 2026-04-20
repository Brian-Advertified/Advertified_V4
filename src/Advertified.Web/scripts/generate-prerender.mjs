import { mkdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const appRoot = path.resolve(__dirname, '..');
const prerenderRoot = path.join(appRoot, '.prerender');

const siteName = 'Advertified';
const siteUrl = 'https://www.advertified.com';
const defaultDescription = 'Advertified helps businesses in South Africa start advertising with clear packages, guided campaign planning, and multi-channel media buying support.';

const routes = [
  {
    slug: 'packages',
    title: 'Advertising Packages in South Africa | Advertified',
    description: 'Browse Advertified package bands, choose the budget that fits your campaign, and move into guided advertising planning with clearer pricing.',
    heading: 'Advertising packages built for clearer campaign buying',
    lead: 'Explore package-led advertising options designed to help businesses move from budget to campaign with more confidence.',
    body: [
      'Advertified helps South African businesses start from a clear budget range, choose the right package band, and move into guided planning across multiple advertising channels.',
      'If you already know your spend, you can browse packages and continue toward payment. If you need help first, Advertified can guide the brief before planning starts.',
    ],
    links: [
      { href: '/', label: 'Home' },
      { href: '/how-it-works', label: 'How it works' },
    ],
  },
  {
    slug: 'how-it-works',
    title: 'How Advertified Works | Guided Advertising Planning',
    description: 'See how Advertified takes businesses from package purchase to campaign recommendation, approval, creative production, and launch.',
    heading: 'How Advertified works',
    lead: 'A guided flow from package purchase to campaign recommendation, approval, and launch.',
    body: [
      'Advertified is designed to keep the buying journey commercial first and then unlock the planning support needed to shape a real campaign.',
      'Businesses can move from signup and package choice into campaign briefing, recommendation review, creative approval, and launch through a structured workflow.',
    ],
    links: [
      { href: '/packages', label: 'Browse packages' },
      { href: '/about', label: 'About Advertified' },
    ],
  },
  {
    slug: 'about',
    title: 'About Advertified | Advertising Platform for South African Businesses',
    description: 'Learn how Advertified helps businesses in South Africa buy advertising with more clarity through package-led planning, campaign workflows, and multi-channel execution.',
    heading: 'About Advertified',
    lead: 'A clearer way for businesses to buy and run advertising.',
    body: [
      'Advertified helps businesses move from budget to campaign through a structured process that starts with what they want to spend and builds toward a campaign they can launch with confidence.',
      'The platform is built around clearer commercial steps, visible campaign progress, and support across billboards, digital screens, radio, TV, social, SMS, and print.',
    ],
    links: [
      { href: '/packages', label: 'Browse packages' },
      { href: '/faq', label: 'Read the FAQ' },
    ],
  },
  {
    slug: 'faq',
    title: 'Advertified FAQ | Packages, Payments, Campaigns, and Launch',
    description: 'Read common questions about Advertified packages, payment timing, campaign approvals, creative production, launch workflows, and platform support.',
    heading: 'Advertified FAQ',
    lead: 'Answers to common questions about packages, payments, approvals, creative production, and launch.',
    body: [
      'The Advertified FAQ explains how package selection, payment, campaign approvals, creative review, launch, and in-platform support work.',
      'It is designed to help businesses understand the customer journey before they commit to a campaign.',
    ],
    links: [
      { href: '/how-it-works', label: 'See how it works' },
      { href: '/partner-enquiry', label: 'Contact our team' },
    ],
  },
  {
    slug: 'billboard-advertising-south-africa',
    title: 'Billboard Advertising in South Africa | Advertified',
    description: 'Explore billboard advertising in South Africa with Advertified, including budget-led planning, package-led campaign setup, and support across OOH and digital screens.',
    heading: 'Billboard advertising in South Africa',
    lead: 'A clearer route into billboard and digital screen campaign planning.',
    body: [
      'Advertified helps businesses approach billboard advertising through package-led planning, campaign guidance, and clearer commercial steps from budget to activation.',
      'The route is useful for businesses that want visible market presence and a more structured way to shape outdoor campaigns alongside broader media support.',
    ],
    links: [
      { href: '/packages', label: 'Browse packages' },
      { href: '/how-it-works', label: 'How it works' },
    ],
  },
  {
    slug: 'radio-advertising-south-africa',
    title: 'Radio Advertising in South Africa | Advertified',
    description: 'Learn how Advertified helps businesses approach radio advertising in South Africa through budget-led campaign planning, guided recommendations, and structured activation.',
    heading: 'Radio advertising in South Africa',
    lead: 'A clearer way to plan radio-led campaigns around budget, brief, and campaign intent.',
    body: [
      'Advertified helps businesses shape radio campaigns through package-led planning and recommendation workflows that make it easier to move from budget to campaign setup.',
      'Radio can support awareness, promotions, repeated message exposure, and multi-channel campaigns when businesses need stronger frequency.',
    ],
    links: [
      { href: '/packages', label: 'Browse packages' },
      { href: '/faq', label: 'Read the FAQ' },
    ],
  },
  {
    slug: 'tv-advertising-south-africa',
    title: 'TV Advertising in South Africa | Advertified',
    description: 'Discover a clearer route into TV advertising in South Africa with Advertified, including package-led campaign planning and structured support across the approval and launch journey.',
    heading: 'TV advertising in South Africa',
    lead: 'A more structured route into broader awareness campaigns and television planning.',
    body: [
      'Advertified helps businesses approach television campaigns with clearer commercial steps, package-led planning, and support across the campaign workflow.',
      'TV can be a strong route when brands need broader visibility, stronger authority, and larger-format creative storytelling.',
    ],
    links: [
      { href: '/media-partners', label: 'Media partners' },
      { href: '/how-it-works', label: 'How it works' },
    ],
  },
  {
    slug: 'digital-advertising-south-africa',
    title: 'Digital Advertising in South Africa | Advertified',
    description: 'See how Advertified supports digital advertising in South Africa with package-led planning, guided campaign setup, and multi-channel support across social, SMS, and digital touchpoints.',
    heading: 'Digital advertising in South Africa',
    lead: 'A clearer path into digital support campaigns, social visibility, and response-led media activity.',
    body: [
      'Advertified supports digital campaigns as part of a broader package-led planning approach, helping businesses shape the right mix around goals, budget, and approvals.',
      'Digital can work well alongside billboard, radio, TV, print, and SMS where brands need reinforcement, targeting flexibility, or response support.',
    ],
    links: [
      { href: '/packages', label: 'Browse packages' },
      { href: '/about', label: 'About Advertified' },
    ],
  },
  {
    slug: 'media-partners',
    title: 'Media Partners | Advertified',
    description: 'Partner with Advertified to connect premium billboard, radio, TV, press, and venue inventory to structured advertiser demand.',
    heading: 'Media partnerships with Advertified',
    lead: 'Connect premium inventory to structured advertiser demand through a clearer operating model.',
    body: [
      'Advertified works with media owners, networks, and venue operators to connect high-intent advertisers to quality inventory through a planning and activation flow designed for reliability.',
      'Partnerships can cover billboards, radio, television, press, and venue-based inventory.',
    ],
    links: [
      { href: '/partner-enquiry', label: 'Become a partner' },
      { href: '/how-it-works', label: 'See advertiser journey' },
    ],
  },
  {
    slug: 'partner-enquiry',
    title: 'Become a Media Partner | Advertified',
    description: 'Submit a partner enquiry to Advertified and tell us about your media inventory, venue network, or channel footprint.',
    heading: 'Partner with Advertified',
    lead: 'Tell us about your inventory, your market coverage, and the partnership you want to explore.',
    body: [
      'Advertified reviews partner enquiries from media owners, networks, venue operators, and publications looking to align supply with structured advertiser demand.',
      'The enquiry process is designed to help our team understand your inventory footprint, operating model, and commercial fit.',
    ],
    links: [
      { href: '/media-partners', label: 'Back to media partners' },
      { href: '/', label: 'Home' },
    ],
  },
  {
    slug: 'privacy',
    title: 'Privacy Policy | Advertified',
    description: 'Read how Advertified collects, uses, stores, and protects personal and business information.',
    heading: 'Privacy Policy',
    lead: 'How Advertified collects, uses, stores, and protects personal and business information.',
    body: [
      'Advertified collects account, business, campaign, payment, and consent information to operate the platform securely and deliver campaign services.',
      'The policy explains what information is collected, why it is processed, and how users can exercise their rights.',
    ],
    links: [
      { href: '/cookie-policy', label: 'Cookie Policy' },
      { href: '/terms-of-service', label: 'Terms and Conditions' },
    ],
  },
  {
    slug: 'cookie-policy',
    title: 'Cookie Policy | Advertified',
    description: 'Learn how Advertified uses cookies and manages necessary, analytics, and marketing consent preferences.',
    heading: 'Cookie Policy',
    lead: 'How Advertified uses necessary cookies and manages optional analytics and marketing preferences.',
    body: [
      'Advertified uses necessary cookies for core platform behaviour and can support optional analytics and marketing preferences where consent is given.',
      'The policy explains cookie categories, consent controls, and how users can update their choices.',
    ],
    links: [
      { href: '/privacy', label: 'Privacy Policy' },
      { href: '/terms-of-service', label: 'Terms and Conditions' },
    ],
  },
  {
    slug: 'terms-of-service',
    title: 'Terms and Conditions | Advertified',
    description: 'Review Advertified commercial terms covering proposals, bookings, payments, campaign execution, and related obligations.',
    heading: 'Terms and Conditions',
    lead: 'Commercial terms governing proposals, bookings, payments, campaign execution, and related obligations.',
    body: [
      'Advertified terms and conditions cover the commercial framework for proposals, bookings, payments, campaign execution, and dispute handling.',
      'The current terms are available through the Advertified platform and are part of the campaign buying journey.',
    ],
    links: [
      { href: '/privacy', label: 'Privacy Policy' },
      { href: '/cookie-policy', label: 'Cookie Policy' },
    ],
  },
  {
    slug: 'register',
    title: 'Create Your Advertified Account',
    description: 'Create your Advertified account to start your campaign journey.',
    heading: 'Create your Advertified account',
    lead: 'Register once, then continue into packages, payment, and campaign planning.',
    body: [
      'This page supports account creation for businesses starting or continuing their Advertified journey.',
      'It should not appear in search results because it is part of the account access flow rather than public marketing content.',
    ],
    links: [
      { href: '/', label: 'Back to home' },
      { href: '/packages', label: 'Browse packages' },
    ],
    noindex: true,
  },
  {
    slug: 'login',
    title: 'Log In | Advertified',
    description: 'Log in to your Advertified workspace.',
    heading: 'Log in to Advertified',
    lead: 'Access your Advertified workspace to manage campaigns, approvals, and account activity.',
    body: [
      'This page is intended for existing users returning to their Advertified workspace.',
      'It should not appear in search results because it is part of the account access flow rather than public discovery content.',
    ],
    links: [
      { href: '/', label: 'Back to home' },
      { href: '/register', label: 'Create account' },
    ],
    noindex: true,
  },
  {
    slug: 'verify-email',
    title: 'Verify Email | Advertified',
    description: 'Verify your email address for Advertified.',
    heading: 'Verify your email address',
    lead: 'Email verification is required before continuing through account setup.',
    body: [
      'This page is part of the Advertified account security and activation flow.',
      'It should not appear in search results because it is not a public destination page.',
    ],
    links: [
      { href: '/login', label: 'Log in' },
      { href: '/register', label: 'Create account' },
    ],
    noindex: true,
  },
  {
    slug: 'set-password',
    title: 'Set Password | Advertified',
    description: 'Set your Advertified password.',
    heading: 'Set your password',
    lead: 'Finish account setup and secure your Advertified workspace.',
    body: [
      'This page is part of the Advertified authentication flow and exists for password setup only.',
      'It should not appear in search results because it is not a public marketing page.',
    ],
    links: [
      { href: '/login', label: 'Log in' },
      { href: '/', label: 'Back to home' },
    ],
    noindex: true,
  },
  {
    slug: 'start-campaign',
    title: 'Start Your Campaign Brief | Advertified',
    description: 'Start your Advertified campaign questionnaire.',
    heading: 'Start your campaign brief',
    lead: 'Begin the guided questionnaire that shapes the right campaign path.',
    body: [
      'This page is part of the in-product campaign flow and is meant for direct user entry, not organic search.',
      'It should not appear in search results because it is a workflow page rather than a public content destination.',
    ],
    links: [
      { href: '/', label: 'Back to home' },
      { href: '/how-it-works/', label: 'How it works' },
    ],
    noindex: true,
  },
  {
    slug: 'checkout/payment',
    title: 'Checkout | Advertified',
    description: 'Complete your Advertified payment.',
    heading: 'Complete your payment',
    lead: 'This page is part of the private checkout flow for active users.',
    body: [
      'The checkout payment page is used during account and campaign transactions inside Advertified.',
      'It should not appear in search results because it is a transactional workflow page, not public marketing content.',
    ],
    links: [
      { href: '/packages/', label: 'Browse packages' },
      { href: '/', label: 'Back to home' },
    ],
    noindex: true,
  },
  {
    slug: 'checkout/confirmation',
    title: 'Checkout Confirmation | Advertified',
    description: 'Your Advertified order confirmation.',
    heading: 'Order confirmation',
    lead: 'This page is part of the private post-checkout workflow.',
    body: [
      'Order confirmation is shown after specific checkout events inside Advertified.',
      'It should not appear in search results because it is tied to user-specific transactions and workflow state.',
    ],
    links: [
      { href: '/packages/', label: 'Browse packages' },
      { href: '/', label: 'Back to home' },
    ],
    noindex: true,
  },
];

function escapeHtml(value) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function buildUrl(slug) {
  return `${siteUrl}/${slug}/`;
}

function renderPage(route) {
  const canonical = buildUrl(route.slug);
  const robots = route.noindex ? 'noindex, nofollow' : 'index, follow';
  const links = route.links
    .map((link) => `<li><a href="${link.href}">${escapeHtml(link.label)}</a></li>`)
    .join('');
  const paragraphs = route.body
    .map((paragraph) => `<p>${escapeHtml(paragraph)}</p>`)
    .join('');

  return `<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>${escapeHtml(route.title)}</title>
    <meta name="description" content="${escapeHtml(route.description || defaultDescription)}" />
    <meta name="robots" content="${robots}" />
    <link rel="canonical" href="${canonical}" />
    <meta property="og:title" content="${escapeHtml(route.title)}" />
    <meta property="og:description" content="${escapeHtml(route.description || defaultDescription)}" />
    <meta property="og:url" content="${canonical}" />
    <meta property="og:site_name" content="${siteName}" />
    <meta property="og:type" content="website" />
    <meta name="twitter:card" content="summary" />
    <meta name="twitter:title" content="${escapeHtml(route.title)}" />
    <meta name="twitter:description" content="${escapeHtml(route.description || defaultDescription)}" />
    <style>
      :root { color-scheme: light; }
      body { margin: 0; font-family: Georgia, "Times New Roman", serif; background: #f6f1e8; color: #1f2937; }
      .shell { max-width: 72rem; margin: 0 auto; padding: 2.5rem 1.25rem 4rem; }
      .hero { background: #fffaf2; border: 1px solid #e9dcc6; border-radius: 1.5rem; padding: 2rem; box-shadow: 0 18px 40px rgba(31, 41, 55, 0.06); }
      .eyebrow { display: inline-block; padding: 0.4rem 0.8rem; border-radius: 999px; background: #d8efe9; color: #0f766e; font: 600 0.75rem/1.2 Arial, sans-serif; letter-spacing: 0.12em; text-transform: uppercase; }
      h1 { margin: 1rem 0 0.75rem; font-size: clamp(2rem, 4vw, 3.25rem); line-height: 1.05; }
      p { font-size: 1rem; line-height: 1.8; margin: 0.9rem 0; max-width: 60ch; }
      .links { margin-top: 1.5rem; padding-left: 1.2rem; }
      .links a { color: #0f766e; text-decoration: none; font-weight: 700; }
    </style>
  </head>
  <body>
    <div id="root">
      <main class="shell">
        <section class="hero">
          <span class="eyebrow">${siteName}</span>
          <h1>${escapeHtml(route.heading)}</h1>
          <p>${escapeHtml(route.lead)}</p>
          ${paragraphs}
          <ul class="links">${links}</ul>
        </section>
      </main>
    </div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>`;
}

await rm(prerenderRoot, { recursive: true, force: true });
await mkdir(prerenderRoot, { recursive: true });

for (const route of routes) {
  const routeDirectory = path.join(appRoot, route.slug);
  await rm(routeDirectory, { recursive: true, force: true });
  await mkdir(routeDirectory, { recursive: true });
  await writeFile(path.join(routeDirectory, 'index.html'), renderPage(route), 'utf8');
  await mkdir(path.join(prerenderRoot, route.slug), { recursive: true });
}

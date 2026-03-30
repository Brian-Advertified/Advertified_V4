import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';

const legalLinks = [
  { to: '/privacy', label: 'Privacy Policy' },
  { to: '/cookie-policy', label: 'Cookie Policy' },
  { to: '/terms-of-service', label: 'Terms of Service' },
];

function LegalLayout({
  kicker,
  title,
  description,
  children,
}: {
  kicker: string;
  title: string;
  description: string;
  children: ReactNode;
}) {
  return (
    <div className="page-shell space-y-8 pb-16">
      <PageHero
        kicker={kicker}
        title={title}
        description={description}
        actions={(
          <>
            {legalLinks.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) => [
                  'hero-secondary-button rounded-full font-semibold',
                  isActive ? 'pointer-events-none border-brand bg-brand text-white shadow-[0_18px_40px_rgba(15,118,110,0.22)]' : '',
                ].filter(Boolean).join(' ')}
              >
                {item.label}
              </NavLink>
            ))}
          </>
        )}
      />

      <section className="panel max-w-4xl px-6 py-8 sm:px-8">
        <div className="space-y-8 text-sm leading-7 text-ink-soft sm:text-base">
          {children}
        </div>
      </section>
    </div>
  );
}

function LegalSection({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <section className="space-y-3">
      <h2 className="text-xl font-semibold tracking-tight text-ink">{title}</h2>
      <div className="space-y-3">{children}</div>
    </section>
  );
}

export function PrivacyPolicyPage() {
  return (
    <LegalLayout
      kicker="Privacy"
      title="Privacy Policy"
      description="How Advertified collects, uses, stores, and protects personal and business information in a POPIA-aligned operating model."
    >
      <LegalSection title="Who we are">
        <p>Advertified is operated in South Africa and uses the holding-company issuer profile Black Space PSG (Pty) Ltd t/a Black Space VSBLT for invoicing and billing records where applicable.</p>
        <p>Information Officer: Nkonzo Mabetha.</p>
      </LegalSection>

      <LegalSection title="Who this service is for">
        <p>Advertified is intended for users who are 18 years or older and who are acting on behalf of a business, brand, or organisation.</p>
      </LegalSection>

      <LegalSection title="What information we collect">
        <p>We collect account details such as full name, email address, phone number, password, and verification details supplied during onboarding.</p>
        <p>We also collect business details such as registered business name, trading name, registration number, VAT number, industry, revenue band, street address, city, and province.</p>
        <p>When you use the platform, we may store campaign briefs, planning preferences, recommendations, messages, invoices, payment references, and consent preferences.</p>
      </LegalSection>

      <LegalSection title="Why we process information">
        <p>We use this information to create and secure accounts, verify users, process payments, issue invoices, support campaign planning, provide dashboards and campaign workspaces, respond to support requests, and maintain platform security and records.</p>
      </LegalSection>

      <LegalSection title="Payments and service providers">
        <p>Advertified supports payment and billing workflows using providers such as VodaPay and Lula. Payment status, invoice details, and related transaction references may be stored to fulfil orders and maintain accounting records.</p>
      </LegalSection>

      <LegalSection title="Cookies and optional tracking">
        <p>We use necessary cookies to keep the platform working, including sign-in, security, checkout continuity, and consent storage.</p>
        <p>Optional analytics and marketing cookies are controlled through the Advertified consent banner. These optional categories should only be activated after you choose to allow them.</p>
        <p>At the time of this update, the codebase includes consent controls for analytics and marketing cookies, but no live Google Analytics, Meta Pixel, Google Ads, Hotjar, Mixpanel, PostHog, or similar third-party tracking scripts are currently installed by default.</p>
      </LegalSection>

      <LegalSection title="How long we keep information">
        <p>We keep information for as long as reasonably necessary to provide the service, maintain campaign and payment records, meet operational needs, and comply with legal, tax, fraud-prevention, and audit obligations.</p>
      </LegalSection>

      <LegalSection title="Your rights">
        <p>Subject to applicable law, you may request access to your personal information, ask for corrections, object to certain processing, or request deletion where retention is no longer required.</p>
      </LegalSection>

      <LegalSection title="Contact">
        <p>General enquiries: info@advertified.com</p>
        <p>Support: support@advertified.com</p>
        <p>Phone: +27 11 040 1195</p>
      </LegalSection>
    </LegalLayout>
  );
}

export function CookiePolicyPage() {
  return (
    <LegalLayout
      kicker="Cookies"
      title="Cookie Policy"
      description="How Advertified uses necessary cookies and manages optional analytics and marketing preferences."
    >
      <LegalSection title="Cookie categories">
        <p>Necessary cookies are always used for essential platform behaviour such as security, sign-in, navigation continuity, checkout support, and saving your consent choices.</p>
        <p>Analytics cookies are optional and are intended to help us understand product usage and improve the platform experience.</p>
        <p>Marketing and support cookies are optional and are intended to support communication preferences, campaign follow-up, and advertising-related measurement where enabled.</p>
      </LegalSection>

      <LegalSection title="Consent controls">
        <p>You can choose between necessary-only cookies, accepting all optional cookies, or managing preferences individually in the cookie banner.</p>
        <p>Your selections are stored as consent preferences linked to a browser identifier and, where applicable, your signed-in account.</p>
      </LegalSection>

      <LegalSection title="Current implementation status">
        <p>The platform already records consent choices for necessary, analytics, and marketing categories.</p>
        <p>Optional third-party trackers should only be loaded after consent. At present, no Google Analytics, Meta Pixel, Google Ads, Hotjar, Mixpanel, Segment, or PostHog scripts are installed by default in this codebase.</p>
      </LegalSection>

      <LegalSection title="How to change your choices">
        <p>You can update your cookie preferences through the cookie banner when it is shown, or at any time through the cookie settings entry point in the site footer.</p>
      </LegalSection>
    </LegalLayout>
  );
}

export function TermsPage() {
  return (
    <LegalLayout
      kicker="Terms"
      title="Terms and Conditions"
      description="Core commercial and platform terms for using Advertified."
    >
      <LegalSection title="Eligibility">
        <p>You must be at least 18 years old to use Advertified and should only use the platform on behalf of yourself, your business, or another entity you are authorised to represent.</p>
      </LegalSection>

      <LegalSection title="Platform service">
        <p>Advertified helps businesses buy advertising packages, submit campaign briefs, receive planning support, review recommendations, and progress campaigns toward activation.</p>
      </LegalSection>

      <LegalSection title="Payments">
        <p>Package purchases are processed as once-off transactions. Access to campaign workflows, invoices, and planning steps may depend on successful payment and account verification.</p>
      </LegalSection>

      <LegalSection title="Refunds">
        <p>Refund outcomes depend on the stage of work already completed.</p>
        <p>Before work starts, a full refund may be available, less any non-recoverable payment gateway fee.</p>
        <p>Once briefing or strategy work is in progress, a partial refund may apply and planning or strategy value may be retained.</p>
        <p>After recommendation delivery, creative work, or campaign go-live, only unused or uncommitted value may be refundable following manual review.</p>
      </LegalSection>

      <LegalSection title="Acceptable use">
        <p>You may not use Advertified for unlawful activity, fraudulent payment behaviour, unauthorised access attempts, or submission of campaign content that you do not have the right to use.</p>
      </LegalSection>

      <LegalSection title="Contact and legal administration">
        <p>Information Officer: Nkonzo Mabetha.</p>
        <p>General contact: info@advertified.com</p>
        <p>Support contact: support@advertified.com</p>
      </LegalSection>
    </LegalLayout>
  );
}

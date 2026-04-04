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
        <p>General enquiries: ad@advertified.com</p>
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
      description="Commercial terms governing proposals, bookings, payments, campaign execution, liability, and dispute handling for Advertified services."
    >
      <LegalSection title="1. Agreement Formation">
        <p>These Terms and Conditions constitute a binding agreement between Advertified and the Client upon the earliest of written acceptance of a quotation or proposal, issuance of a purchase order or instruction, or payment of any invoice.</p>
        <p>In the event of conflict, the following order of precedence applies: signed agreement, if any; approved proposal or insertion order; then these Terms and Conditions.</p>
      </LegalSection>

      <LegalSection title="2. Payment Terms">
        <p>Payment is due within 7 (seven) days from invoice date unless otherwise agreed in writing.</p>
        <p>Late payments incur interest at 2% per month, calculated daily.</p>
        <p>Advertified reserves the right to suspend campaigns for accounts overdue by more than 7 days and cancel campaigns for accounts overdue by more than 14 days.</p>
        <p>The Client is liable for all reasonable legal and collection costs incurred in recovering overdue amounts.</p>
      </LegalSection>

      <LegalSection title="3. Booking and Media Placement">
        <p>All media placements are subject to availability and supplier confirmation.</p>
        <p>No booking is secured until payment or valid proof of payment is received.</p>
        <p>Advertified reserves the right to substitute equivalent media placements where necessary.</p>
      </LegalSection>

      <LegalSection title="4. Cancellations and Amendments">
        <p>All cancellations must be submitted in writing.</p>
        <p>Cancellation fees may be up to 50% more than 14 days before campaign start, and up to 100% less than 7 days before campaign start.</p>
        <p>Post-confirmation changes may incur additional costs and remain subject to supplier approval.</p>
      </LegalSection>

      <LegalSection title="5. Third-Party Media Suppliers">
        <p>Advertified acts solely as an intermediary. All media inventory is owned and operated by third-party suppliers.</p>
        <p>The Client agrees that supplier terms apply in addition to these Terms, and that Advertified is not liable for supplier delays, errors, or non-performance.</p>
        <p>In the event of supplier failure, Advertified&apos;s obligation is limited to rebooking equivalent media or issuing credit where applicable.</p>
      </LegalSection>

      <LegalSection title="6. Campaign Execution">
        <p>Campaign timelines depend on receipt of payment, final creative approval, and supplier scheduling.</p>
        <p>Delays caused by the Client do not entitle the Client to refunds.</p>
      </LegalSection>

      <LegalSection title="7. Creative Content and Compliance">
        <p>The Client warrants that all content complies with South African law and meets Advertising Regulatory Board standards.</p>
        <p>Advertified reserves the right to reject non-compliant material.</p>
      </LegalSection>

      <LegalSection title="8. Intellectual Property">
        <p>The Client retains ownership of all creative assets supplied.</p>
        <p>The Client grants Advertified a non-exclusive license to use campaign materials for execution and marketing purposes.</p>
        <p>The Client indemnifies Advertified against any intellectual property infringement claims.</p>
      </LegalSection>

      <LegalSection title="9. Data Protection (POPIA)">
        <p>Advertified processes personal information in accordance with the Protection of Personal Information Act.</p>
        <p>The Client consents to the processing of data necessary for campaign execution and communication.</p>
      </LegalSection>

      <LegalSection title="10. Proof of Performance">
        <p>Proof of campaign execution may include photos, logs, or supplier reports, depending on supplier capability.</p>
        <p>Such proof constitutes sufficient evidence of delivery.</p>
      </LegalSection>

      <LegalSection title="11. No Performance Guarantee">
        <p>Advertified does not guarantee sales outcomes, audience engagement, or return on investment.</p>
        <p>Advertising inherently carries commercial risk.</p>
      </LegalSection>

      <LegalSection title="12. Refund Policy">
        <p>Refunds are not standard and remain subject to supplier approval.</p>
        <p>Where applicable, refunds will be issued as account credit by default, or a partial monetary refund at Advertified&apos;s discretion.</p>
      </LegalSection>

      <LegalSection title="13. Limitation of Liability">
        <p>Advertified&apos;s total liability is limited to the value of fees paid by the Client.</p>
        <p>Advertified is not liable for indirect or consequential losses, including loss of profit, revenue, or business opportunity.</p>
      </LegalSection>

      <LegalSection title="14. Indemnity">
        <p>The Client indemnifies Advertified against all claims arising from illegal or non-compliant advertising content, intellectual property infringement, defamation, or regulatory breaches.</p>
      </LegalSection>

      <LegalSection title="15. Force Majeure">
        <p>Advertified is not liable for delays or failures caused by events beyond its control, including natural disasters, government actions, or supplier disruptions.</p>
      </LegalSection>

      <LegalSection title="16. Dispute Resolution">
        <p>Disputes must be submitted in writing within 5 business days.</p>
        <p>The parties agree to attempt resolution in good faith before litigation.</p>
      </LegalSection>

      <LegalSection title="17. Governing Law">
        <p>This agreement is governed by the laws of the Republic of South Africa.</p>
        <p>Jurisdiction is the Gauteng High Court.</p>
      </LegalSection>

      <LegalSection title="18. Non-Assignment">
        <p>The Client may not assign or transfer rights or obligations without prior written consent.</p>
      </LegalSection>

      <LegalSection title="19. Entire Agreement">
        <p>These Terms constitute the entire agreement and supersede all prior discussions or representations.</p>
      </LegalSection>

      <LegalSection title="20. Acceptance">
        <p>Payment or written confirmation constitutes full acceptance of these Terms and Conditions.</p>
      </LegalSection>
    </LegalLayout>
  );
}

import { ArrowRight, Check, Megaphone, Radio, Smartphone, Tv } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { Link } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';
import { Seo } from '../../components/seo/Seo';

type ChannelLandingPageProps = {
  path: string;
  title: string;
  description: string;
  heroTitle: string;
  heroDescription: string;
  kicker: string;
  icon: LucideIcon;
  overview: string[];
  benefits: readonly string[];
  useCases: readonly string[];
  relatedLinks: ReadonlyArray<{ href: string; label: string }>;
};

function ChannelLandingPage({
  path,
  title,
  description,
  heroTitle,
  heroDescription,
  kicker,
  icon: Icon,
  overview,
  benefits,
  useCases,
  relatedLinks,
}: ChannelLandingPageProps) {
  return (
    <div className="page-shell space-y-8 pb-10">
      <Seo
        title={title}
        description={description}
        path={path}
        type="website"
      />
      <PageHero
        kicker={kicker}
        title={heroTitle}
        description={heroDescription}
        actions={(
          <>
            <Link to="/packages" className="hero-primary-button">
              Browse packages
              <ArrowRight className="size-4" />
            </Link>
            <Link to="/start-campaign" className="hero-secondary-button rounded-full font-semibold">
              Start questionnaire
            </Link>
          </>
        )}
        aside={(
          <div className="space-y-4">
            <div className="flex size-12 items-center justify-center rounded-2xl bg-white text-brand shadow-[0_10px_24px_rgba(15,118,110,0.12)]">
              <Icon className="size-6" />
            </div>
            <p className="text-sm leading-7 text-ink-soft">
              Advertified helps businesses move from budget to channel planning with a clearer commercial route, guided recommendations, and multi-channel campaign support.
            </p>
            <div className="flex flex-wrap gap-2">
              {relatedLinks.map((item) => (
                <Link key={item.href} to={item.href} className="rounded-full border border-brand/15 bg-white/80 px-3 py-1.5 text-xs font-semibold text-brand">
                  {item.label}
                </Link>
              ))}
            </div>
          </div>
        )}
      />

      <section className="grid gap-5 lg:grid-cols-[1.05fr_0.95fr]">
        <div className="panel px-6 py-7 sm:px-8 sm:py-8">
          <div className="pill self-start bg-brand-soft text-brand">Why this channel</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">What this route is good at</h2>
          <div className="mt-5 space-y-4">
            {overview.map((paragraph) => (
              <p key={paragraph} className="text-sm leading-7 text-ink-soft sm:text-base">
                {paragraph}
              </p>
            ))}
          </div>
        </div>

        <div className="panel px-6 py-7 sm:px-8 sm:py-8">
          <div className="pill self-start bg-highlight-soft text-highlight">Best fit</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">Common campaign benefits</h2>
          <div className="mt-6 space-y-3">
            {benefits.map((item) => (
              <div key={item} className="flex items-start gap-3 rounded-[20px] border border-line bg-white px-4 py-4 text-sm leading-7 text-ink-soft shadow-[0_8px_24px_rgba(17,24,39,0.04)]">
                <Check className="mt-1 size-4 shrink-0 text-brand" />
                <span>{item}</span>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="panel px-6 py-7 sm:px-8 sm:py-8">
        <div className="max-w-3xl">
          <div className="pill self-start bg-brand-soft text-brand">Use cases</div>
          <h2 className="mt-4 text-3xl font-semibold tracking-tight text-ink">Where businesses usually use this channel</h2>
        </div>
        <div className="mt-6 grid gap-4 md:grid-cols-2">
          {useCases.map((item) => (
            <article key={item} className="rounded-[22px] border border-line bg-white px-5 py-5 shadow-[0_10px_30px_rgba(17,24,39,0.04)]">
              <p className="text-base font-semibold text-ink">{item}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="panel px-6 py-7 text-center sm:px-8 sm:py-8">
        <h2 className="text-3xl font-semibold tracking-tight text-ink">Need help choosing the right mix?</h2>
        <p className="mx-auto mt-3 max-w-2xl text-sm leading-7 text-ink-soft sm:text-base">
          Start with your budget if you know your spend, or let Advertified guide the brief first and help shape the right campaign route.
        </p>
        <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
          <Link to="/packages" className="hero-primary-button">
            Browse packages
            <ArrowRight className="size-4" />
          </Link>
          <Link to="/faq" className="hero-secondary-button rounded-full font-semibold">
            Read the FAQ
          </Link>
        </div>
      </section>
    </div>
  );
}

export function BillboardAdvertisingPage() {
  return (
    <ChannelLandingPage
      path="/billboard-advertising-south-africa"
      title="Billboard Advertising in South Africa | Advertified"
      description="Explore billboard advertising in South Africa with Advertified, including budget-led planning, package-led campaign setup, and support across Billboards and Digital Screens."
      kicker="Billboard advertising"
      heroTitle="Billboard advertising in South Africa with a clearer path from budget to campaign."
      heroDescription="Advertified helps businesses approach Billboards and Digital Screens through package-led planning, guided recommendations, and a more structured activation journey."
      icon={Megaphone}
      overview={[
        'Billboards and Digital Screens are often strongest when a business needs visible market presence, repeated daily reach, and formats that keep the brand in front of commuters, shoppers, and local audiences.',
        'Advertified helps businesses start with the budget band first, then shape the campaign around the right planning route, geography, and channel mix instead of beginning with an open-ended quoting process.',
      ]}
      benefits={[
        'Strong visual presence across high-traffic environments',
        'Useful for market visibility, local awareness, and launch support',
        'Can be combined with radio, TV, digital, SMS, and print',
      ]}
      useCases={[
        'Retail campaigns that need repeated local visibility',
        'Brand launches that need large-format awareness quickly',
        'Regional campaigns that need visible roadside or commuter presence',
        'Businesses that want Billboards and Digital Screens supported by other channels',
      ]}
      relatedLinks={[
        { href: '/packages', label: 'Packages' },
        { href: '/how-it-works', label: 'How it works' },
      ]}
    />
  );
}

export function RadioAdvertisingPage() {
  return (
    <ChannelLandingPage
      path="/radio-advertising-south-africa"
      title="Radio Advertising in South Africa | Advertified"
      description="Learn how Advertified helps businesses approach radio advertising in South Africa through budget-led campaign planning, guided recommendations, and structured activation."
      kicker="Radio advertising"
      heroTitle="Radio advertising in South Africa that starts with budget clarity and campaign intent."
      heroDescription="Advertified helps businesses shape radio-led campaigns through package-led planning, campaign guidance, and multi-channel support where reach, frequency, and message timing matter."
      icon={Radio}
      overview={[
        'Radio advertising is often effective when a business needs repeated exposure, local or regional frequency, and a channel that can support offers, launches, and brand awareness with strong message repetition.',
        'Advertified supports a clearer planning route by helping businesses start from their spend, define campaign intent, and move into a recommendation process that can include radio alongside other media channels.',
      ]}
      benefits={[
        'Strong frequency and repeated message exposure',
        'Useful for regional, metro, and broader awareness support',
        'Can support launch campaigns, promotions, and call-to-action activity',
      ]}
      useCases={[
        'Retail and service businesses running short-term promotions',
        'Brands needing repeated awareness over a concentrated period',
        'Campaigns that work better with audio repetition than pure visual media',
        'Businesses that want radio combined with Billboards and Digital Screens, TV, social, or SMS',
      ]}
      relatedLinks={[
        { href: '/packages', label: 'Packages' },
        { href: '/faq', label: 'FAQ' },
      ]}
    />
  );
}

export function TelevisionAdvertisingPage() {
  return (
    <ChannelLandingPage
      path="/tv-advertising-south-africa"
      title="TV Advertising in South Africa | Advertified"
      description="Discover a clearer route into TV advertising in South Africa with Advertified, including package-led campaign planning and structured support across the approval and launch journey."
      kicker="TV advertising"
      heroTitle="TV advertising in South Africa with a more structured route into planning and launch."
      heroDescription="Advertified helps businesses approach television campaigns through budget-led planning, recommendation workflows, and support that makes complex media buying easier to navigate."
      icon={Tv}
      overview={[
        'Television advertising can be powerful when a campaign needs broader awareness, stronger perceived authority, and more immersive creative storytelling across a wider audience.',
        'Advertified helps reduce the friction around planning by connecting budget choice, campaign briefing, approvals, and launch into a clearer workflow before activation begins.',
      ]}
      benefits={[
        'Useful for high-visibility brand awareness campaigns',
        'Stronger authority and perceived scale for the advertiser',
        'Can sit inside a broader multi-channel plan with Billboards and Digital Screens, radio, digital, and print',
      ]}
      useCases={[
        'Brand awareness campaigns that need broad market visibility',
        'Businesses launching into larger regions or multiple markets',
        'Campaigns that rely on richer creative storytelling',
        'Advertisers combining television with radio, outdoor, and digital support',
      ]}
      relatedLinks={[
        { href: '/media-partners', label: 'Media partners' },
        { href: '/how-it-works', label: 'How it works' },
      ]}
    />
  );
}

export function DigitalAdvertisingPage() {
  return (
    <ChannelLandingPage
      path="/digital-advertising-south-africa"
      title="Digital Advertising in South Africa | Advertified"
      description="See how Advertified supports digital advertising in South Africa with package-led planning, guided campaign setup, and multi-channel support across social, SMS, and digital touchpoints."
      kicker="Digital advertising"
      heroTitle="Digital advertising in South Africa with clearer commercial steps and guided campaign planning."
      heroDescription="Advertified helps businesses use package-led planning to shape digital support campaigns across social platforms, SMS, and broader channel mixes that support awareness and conversion goals."
      icon={Smartphone}
      overview={[
        'Digital advertising is often best when businesses need flexible support around targeting, remarketing, social visibility, message reinforcement, or response-driven campaign activity.',
        'Advertified treats digital as part of a wider campaign journey, helping businesses begin with a clear package or brief and then shape the right mix around goals, budget, and approval flow.',
      ]}
      benefits={[
        'Flexible support around social, digital, and response-led activity',
        'Useful alongside Billboards and Digital Screens, radio, TV, and print campaigns',
        'Can help carry campaign momentum between larger-format placements',
      ]}
      useCases={[
        'Brands that need always-on social support around a launch',
        'Campaigns that need response support or message reinforcement',
        'Businesses combining offline visibility with online follow-through',
        'Advertisers looking for a multi-channel mix rather than one isolated channel',
      ]}
      relatedLinks={[
        { href: '/packages', label: 'Packages' },
        { href: '/about', label: 'About Advertified' },
      ]}
    />
  );
}

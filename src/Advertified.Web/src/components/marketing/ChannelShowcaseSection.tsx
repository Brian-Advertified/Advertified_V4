import { Link } from 'react-router-dom';
import billboardImage from '../../assets/Channels/optimized/billboard-sa-optimized.jpg';
import newspaperImage from '../../assets/Channels/optimized/newspaper-sa-optimized.jpg';
import radioImage from '../../assets/Channels/optimized/radio-sa-optimized.jpg';
import smsImage from '../../assets/Channels/optimized/sms-sa-optimized.jpg';
import socialImage from '../../assets/Channels/optimized/social-platforms-optimized.jpg';
import tvImage from '../../assets/Channels/optimized/tv-sa-optimized.jpg';

const channels = [
  {
    title: 'Billboards, Digital Screens',
    description: 'Roadside, retail, commuter, and high-traffic visual advertising environments.',
    image: billboardImage,
    href: '/billboard-advertising-south-africa',
  },
  {
    title: 'Radio',
    description: 'Regional and national station packages, slots, and audio-led reach campaigns.',
    image: radioImage,
    href: '/radio-advertising-south-africa',
  },
  {
    title: 'Television',
    description: 'Broadcast and programme-aligned placements for broader awareness and authority.',
    image: tvImage,
    href: '/tv-advertising-south-africa',
  },
  {
    title: 'Social Platforms',
    description: 'Meta, TikTok, and always-on social support that amplifies campaign momentum.',
    image: socialImage,
    href: '/digital-advertising-south-africa',
  },
  {
    title: 'SMS',
    description: 'Direct-response messaging for retail pushes, reminders, and call-to-action bursts.',
    image: smsImage,
    href: '/packages',
  },
  {
    title: 'Newspaper Advertising',
    description: 'Newspaper and press placements that support trust, offers, and local market presence.',
    image: newspaperImage,
    href: '/newspaper-advertising-south-africa',
  },
];

export function ChannelShowcaseSection() {
  return (
    <section className="page-shell pt-2">
      <div className="panel overflow-hidden px-5 py-6 sm:px-6 sm:py-7">
        <div className="flex flex-col gap-3 sm:max-w-3xl">
          <div className="pill self-start bg-brand-soft px-3 py-1 text-[11px] tracking-[0.16em] text-brand">Channels we activate</div>
          <h2 className="mt-1 max-w-3xl text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
            What Advertified actually helps you buy, plan, and activate.
          </h2>
          <p className="max-w-2xl text-sm leading-7 text-ink-soft">
            From Billboards, Digital Screens to radio, TV, newspaper advertising, digital support, SMS, and print, these are the channels our package and planning workflow is designed to bring together.
          </p>
        </div>

        <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {channels.map((channel) => (
            <Link key={channel.title} to={channel.href} className="block overflow-hidden rounded-[22px] border border-line bg-white shadow-[0_8px_18px_rgba(17,24,39,0.04)] transition hover:-translate-y-0.5 hover:border-brand/30 hover:shadow-[0_14px_28px_rgba(17,24,39,0.08)]">
              <div className="flex h-32 items-center justify-center overflow-hidden bg-white sm:h-36">
                <img
                  src={channel.image}
                  alt={channel.title}
                  loading="lazy"
                  decoding="async"
                  className="max-h-full w-full object-contain"
                />
              </div>
              <div className="space-y-2 px-4 py-4">
                <h3 className="text-base font-semibold tracking-tight text-ink">{channel.title}</h3>
                <p className="text-xs leading-6 text-ink-soft">{channel.description}</p>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}

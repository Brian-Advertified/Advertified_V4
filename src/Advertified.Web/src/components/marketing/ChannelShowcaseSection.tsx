import billboardImage from '../../assets/Channels/optimized/billboard-sa-optimized.jpg';
import newspaperImage from '../../assets/Channels/optimized/newspaper-sa-optimized.jpg';
import radioImage from '../../assets/Channels/optimized/radio-sa-optimized.jpg';
import smsImage from '../../assets/Channels/optimized/sms-sa-optimized.jpg';
import socialImage from '../../assets/Channels/optimized/social-platforms-optimized.jpg';
import tvImage from '../../assets/Channels/optimized/tv-sa-optimized.jpg';

const channels = [
  {
    title: 'Billboards and Digital Screens',
    description: 'Premium roadside, retail, and commuter visibility across high-traffic environments.',
    image: billboardImage,
  },
  {
    title: 'Radio',
    description: 'Regional and national station packages, slots, and audio-led reach campaigns.',
    image: radioImage,
  },
  {
    title: 'Television',
    description: 'Broadcast and programme-aligned placements for broader awareness and authority.',
    image: tvImage,
  },
  {
    title: 'Social Platforms',
    description: 'Meta, TikTok, and always-on social support that amplifies campaign momentum.',
    image: socialImage,
  },
  {
    title: 'SMS',
    description: 'Direct-response messaging for retail pushes, reminders, and call-to-action bursts.',
    image: smsImage,
  },
  {
    title: 'Press and Print',
    description: 'Newspaper placements that support trust, offers, and local market presence.',
    image: newspaperImage,
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
            From Billboards and Digital Screens to radio, TV, digital support, SMS, and print, these are the channels our package and planning workflow is designed to bring together.
          </p>
        </div>

        <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {channels.map((channel) => (
            <article key={channel.title} className="overflow-hidden rounded-[22px] border border-line bg-white shadow-[0_8px_18px_rgba(17,24,39,0.04)]">
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
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

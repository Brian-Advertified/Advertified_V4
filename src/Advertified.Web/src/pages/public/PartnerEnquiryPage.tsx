import { useState } from 'react';
import { ArrowRight, Building2, Handshake, Mail, Phone } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { PageHero } from '../../components/marketing/PageHero';
import { Seo } from '../../components/seo/Seo';
import { ProcessingOverlay } from '../../components/ui/ProcessingOverlay';
import { useToast } from '../../components/ui/toast';
import { advertifiedApi } from '../../services/advertifiedApi';

const partnerTypes = [
  'Billboards, Digital Screens Media Owner',
  'Radio Network',
  'TV Channel',
  'Retail Venue Group',
  'Publication',
  'Other',
] as const;

export function PartnerEnquiryPage() {
  const [fullName, setFullName] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [partnerType, setPartnerType] = useState<string>(partnerTypes[0]);
  const [inventorySummary, setInventorySummary] = useState('');
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const { pushToast } = useToast();
  const navigate = useNavigate();

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      setSubmitting(true);
      await advertifiedApi.submitPartnerEnquiry({
        fullName,
        companyName,
        email,
        phone,
        partnerType,
        inventorySummary,
        message,
      });

      pushToast({
        title: 'Partner enquiry sent.',
        description: 'Our team has received your enquiry and will contact you at the details you provided.',
      });

      navigate('/media-partners');
    } catch (error) {
      pushToast({
        title: 'We could not send your enquiry.',
        description: error instanceof Error ? error.message : 'Please try again in a moment.',
      }, 'error');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page-shell space-y-8 pb-10">
      <Seo
        title="Become a Media Partner | Advertified"
        description="Submit a partner enquiry to Advertified and tell us about your media inventory, venue network, or channel footprint."
        path="/partner-enquiry"
        type="website"
      />
      {submitting ? <ProcessingOverlay label="Sending your partner enquiry..." /> : null}

      <PageHero
        kicker="Partner enquiry"
        title="Tell us about your inventory and how you would like to work with Advertified."
        description="Use this form to introduce your media supply, venue footprint, or channel network. Our team will review the enquiry and respond directly at ad@advertified.com."
        aside={(
          <div className="space-y-4">
            <div className="rounded-[20px] border border-line bg-white/80 px-4 py-4">
              <Handshake className="size-5 text-brand" />
              <p className="mt-3 text-sm font-semibold text-ink">What to include</p>
              <p className="mt-2 text-sm leading-7 text-ink-soft">Partner type, market coverage, inventory footprint, and any commercial or operational notes that matter.</p>
            </div>
            <div className="rounded-[20px] border border-line bg-white/80 px-4 py-4 text-sm leading-7 text-ink-soft">
              Your enquiry will be sent to <span className="font-semibold text-ink">ad@advertified.com</span>.
            </div>
          </div>
        )}
      />

      <section className="grid gap-6 lg:grid-cols-[minmax(0,1.1fr)_minmax(280px,0.9fr)]">
        <form onSubmit={handleSubmit} className="panel space-y-5 px-6 py-7 sm:px-8 sm:py-8">
          <div className="grid gap-5 sm:grid-cols-2">
            <label>
              <span className="label-base">Full name</span>
              <input className="input-base" value={fullName} onChange={(event) => setFullName(event.target.value)} required />
            </label>
            <label>
              <span className="label-base">Company name</span>
              <input className="input-base" value={companyName} onChange={(event) => setCompanyName(event.target.value)} required />
            </label>
          </div>

          <div className="grid gap-5 sm:grid-cols-2">
            <label>
              <span className="label-base">Email</span>
              <input type="email" className="input-base" value={email} onChange={(event) => setEmail(event.target.value)} required />
            </label>
            <label>
              <span className="label-base">Phone</span>
              <input className="input-base" value={phone} onChange={(event) => setPhone(event.target.value)} />
            </label>
          </div>

          <label>
            <span className="label-base">Partner type</span>
            <select className="input-base" value={partnerType} onChange={(event) => setPartnerType(event.target.value)} required>
              {partnerTypes.map((item) => (
                <option key={item} value={item}>{item}</option>
              ))}
            </select>
          </label>

          <label>
            <span className="label-base">Inventory summary</span>
            <textarea
              className="input-base min-h-[120px]"
              value={inventorySummary}
              onChange={(event) => setInventorySummary(event.target.value)}
              placeholder="Describe the inventory, markets, channel footprint, or venue network you want to discuss."
            />
          </label>

          <label>
            <span className="label-base">Message</span>
            <textarea
              className="input-base min-h-[160px]"
              value={message}
              onChange={(event) => setMessage(event.target.value)}
              placeholder="Tell us what kind of partnership you are exploring, what inventory you represent, and what a successful relationship would look like."
              required
            />
          </label>

          <div className="flex flex-wrap gap-3 pt-2">
            <button type="submit" className="hero-primary-button" disabled={submitting}>
              Send partner enquiry
              <ArrowRight className="size-4" />
            </button>
            <Link to="/media-partners" className="hero-secondary-button rounded-full font-semibold">
              Back to media partners
            </Link>
          </div>
        </form>

        <aside className="space-y-4">
          <div className="panel px-6 py-6">
            <div className="flex items-start gap-3">
              <Building2 className="mt-1 size-5 text-brand" />
              <div>
                <p className="text-base font-semibold text-ink">Structured partner onboarding</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">
                  We use this information to understand your inventory footprint, commercial model, and the right way to align demand.
                </p>
              </div>
            </div>
          </div>

          <div className="panel px-6 py-6">
            <div className="flex items-start gap-3">
              <Mail className="mt-1 size-5 text-brand" />
              <div>
                <p className="text-base font-semibold text-ink">Email destination</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">
                  Every enquiry is delivered to <span className="font-semibold text-ink">ad@advertified.com</span> for review and follow-up.
                </p>
              </div>
            </div>
          </div>

          <div className="panel px-6 py-6">
            <div className="flex items-start gap-3">
              <Phone className="mt-1 size-5 text-brand" />
              <div>
                <p className="text-base font-semibold text-ink">What happens next</p>
                <p className="mt-2 text-sm leading-7 text-ink-soft">
                  After review, the Advertified team can contact you directly to discuss inventory alignment, operating model, and commercial fit.
                </p>
              </div>
            </div>
          </div>
        </aside>
      </section>
    </div>
  );
}

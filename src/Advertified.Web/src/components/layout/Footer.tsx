import { Link } from 'react-router-dom';
import advertifiedLogo from '../../assets/advertified-logo-v3.png';
import { openConsentPreferences } from '../ui/consentPreferences';

const currentYear = new Date().getFullYear();

export function Footer() {
  return (
    <footer className="border-t border-line bg-[linear-gradient(180deg,rgba(236,250,246,0.78)_0%,rgba(255,255,255,0.98)_65%,rgba(255,255,255,1)_100%)]">
      <div className="page-shell py-10 sm:py-12">
        <div className="grid gap-10 lg:grid-cols-[1.45fr_0.9fr_0.9fr_1fr]">
          <div className="max-w-xs">
            <img
              src={advertifiedLogo}
              alt="Advertified"
              className="h-8 w-auto"
            />
            <p className="mt-5 text-sm leading-7 text-ink-soft">
              Empowering businesses with flexible advertising solutions that align with your cash flow.
            </p>
          </div>

          <div>
            <h3 className="text-sm font-semibold text-ink">Product</h3>
            <div className="mt-4 flex flex-col gap-2.5 text-sm text-ink-soft">
              <Link to="/packages" className="transition hover:text-brand">Packages</Link>
              <Link to="/how-it-works" className="transition hover:text-brand">How It Works</Link>
              <Link id="footer-faq" to="/faq" className="transition hover:text-brand">FAQ</Link>
            </div>
          </div>

          <div>
            <h3 className="text-sm font-semibold text-ink">Company</h3>
            <div className="mt-4 flex flex-col gap-2.5 text-sm text-ink-soft">
              <Link to="/about" className="transition hover:text-brand">About Us</Link>
              <Link id="footer-media-partners" to="/media-partners" className="transition hover:text-brand">Media Partners</Link>
            </div>
          </div>

          <div>
            <h3 className="text-sm font-semibold text-ink">Contact</h3>
            <div className="mt-4 space-y-2.5 text-sm text-ink-soft">
              <p>Office 301, Elkestad Mall</p>
              <p>43 Andringa Stellenbosch</p>
              <p>Stellenbosch</p>
              <p>Cape Town, 7559</p>
              <p>
                <a href="mailto:ad@advertified.com" className="transition hover:text-brand">ad@advertified.com</a>
              </p>
            </div>
          </div>
        </div>

        <div className="mt-10 flex flex-col gap-4 border-t border-line/80 pt-5 text-xs text-ink-soft sm:flex-row sm:items-center sm:justify-between">
          <p>&copy; {currentYear} Advertified. All rights reserved.</p>
          <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
            <Link to="/privacy" className="transition hover:text-brand">Privacy Policy</Link>
            <Link to="/terms-of-service" className="transition hover:text-brand">Terms of Service</Link>
            <Link to="/cookie-policy" className="transition hover:text-brand">Cookie Policy</Link>
            <button type="button" onClick={openConsentPreferences} className="cursor-pointer border-0 bg-transparent p-0 text-left transition hover:text-brand">
              Cookie settings
            </button>
          </div>
        </div>
      </div>
    </footer>
  );
}

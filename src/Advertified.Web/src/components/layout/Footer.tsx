import { Link } from 'react-router-dom';
import advertifiedLogo from '../../assets/advertified-logo-v3.png';

export function Footer() {
  return (
    <footer className="footer-shell border-t border-line">
      <div className="page-shell grid gap-10 py-12 md:grid-cols-[1.3fr_1fr_1fr]">
        <div>
          <img
            src={advertifiedLogo}
            alt="Advertified"
            className="h-9 w-auto sm:h-10"
          />
          <p className="mt-4 max-w-md text-sm leading-7 text-ink-soft">
            Unlock Premium Advertising Without Breaking Your Cash Flow. Choose your package, pay, then unlock AI or agent-assisted campaign planning.
          </p>
        </div>
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Product</p>
          <div className="mt-4 flex flex-col gap-3 text-sm text-ink">
            <Link to="/packages">Packages</Link>
            <Link to="/register">Register</Link>
            <Link to="/login">Log in</Link>
          </div>
        </div>
        <div>
          <p className="text-sm font-semibold uppercase tracking-[0.18em] text-brand">Contact</p>
          <div className="mt-4 space-y-2 text-sm text-ink">
            <p>Stellenbosch, South Africa</p>
            <p>info@advertified.com</p>
            <p>+27 11 040 1195</p>
          </div>
        </div>
      </div>
    </footer>
  );
}

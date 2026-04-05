import { Menu, X } from 'lucide-react';
import { useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../features/auth/auth-context';
import { NotificationCenter } from '../../features/notifications/NotificationCenter';
import { publicAiStudioEnabled } from '../../lib/featureFlags';
import advertifiedLogo from '../../assets/advertified-logo-v3.png';
import type { UserRole } from '../../types/domain';

const publicLinks = [
  { label: 'Advertified', href: '/about' },
  { label: 'Packages', href: '/packages' },
] as const;

const workspaceLinksByRole: Record<UserRole, { label: string; href: string }> = {
  client: { label: 'Dashboard', href: '/dashboard' },
  agent: { label: 'Agent Workspace', href: '/agent' },
  creative_director: { label: 'Creative Studio', href: '/creative/studio-demo' },
  admin: { label: 'Admin Workspace', href: '/admin' },
};

export function Navbar() {
  const [open, setOpen] = useState(false);
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const workspaceLink = user ? workspaceLinksByRole[user.role] : null;
  const isAiStudioActive = location.pathname === '/ai-studio' || location.pathname.startsWith('/ai-studio/');
  const isWorkspaceLinkActive = workspaceLink
    ? location.pathname === workspaceLink.href || location.pathname.startsWith(`${workspaceLink.href}/`)
    : false;

  return (
    <header className="site-header sticky top-0 z-40">
      <div className="page-shell flex items-center justify-between gap-4 py-4">
        <Link to="/" className="flex items-center gap-3">
          <img
            src={advertifiedLogo}
            alt="Advertified"
            className="h-8 w-auto sm:h-9"
          />
        </Link>

        <nav className="hidden items-center gap-7 text-sm font-medium lg:flex">
          {publicLinks.map((item) => (
            <NavLink key={item.href} to={item.href} className="nav-link">
              {item.label}
            </NavLink>
          ))}
          {publicAiStudioEnabled ? (
            <Link
              to="/ai-studio"
              className={`relative inline-flex items-center gap-2 rounded-full border px-4 py-2 text-xs font-semibold uppercase tracking-[0.22em] transition ${
                isAiStudioActive
                  ? 'border-brand/35 bg-gradient-to-r from-brand via-highlight to-brand text-white shadow-[0_14px_32px_rgba(15,118,110,0.28)]'
                  : 'border-brand/25 bg-gradient-to-r from-brand-soft via-white to-brand-soft text-brand shadow-[0_10px_24px_rgba(15,118,110,0.2)] hover:border-brand/35 hover:shadow-[0_14px_28px_rgba(15,118,110,0.24)]'
              }`}
            >
              Advertified Studio
              <span className="rounded-full bg-black/25 px-2 py-0.5 text-[10px] tracking-[0.14em] text-white">DEV</span>
            </Link>
          ) : null}
        </nav>

        <div className="hidden items-center gap-3 lg:flex">
          {user ? (
            <>
              {workspaceLink ? (
                <Link
                  to={workspaceLink.href}
                  className={isWorkspaceLinkActive ? 'button-secondary px-4 py-2' : 'button-primary px-5 py-3'}
                >
                  {workspaceLink.label}
                </Link>
              ) : null}
              <NotificationCenter />
              <span className="text-sm font-medium text-ink-soft">{user.fullName}</span>
              <button
                type="button"
                className="button-secondary px-4 py-2"
                onClick={() => {
                  logout('manual');
                  navigate('/');
                }}
              >
                Log out
              </button>
            </>
          ) : (
            <>
              <Link className="px-4 py-2 text-sm font-semibold text-ink transition hover:text-brand" to="/login">
                Log in
              </Link>
              <Link className="button-primary px-5 py-3" to="/register">
                Get started
              </Link>
            </>
          )}
        </div>

        <button type="button" className="button-secondary p-3 lg:hidden" onClick={() => setOpen((current) => !current)}>
          {open ? <X className="size-5" /> : <Menu className="size-5" />}
        </button>
      </div>
      {open ? (
        <div className="border-t border-line bg-white lg:hidden">
          <div className="page-shell flex flex-col gap-4 py-4 text-sm font-medium text-ink">
            {publicLinks.map((item) => (
              <NavLink key={item.href} to={item.href} className="nav-link" onClick={() => setOpen(false)}>
                {item.label}
              </NavLink>
            ))}
            {publicAiStudioEnabled ? (
              <Link
                to="/ai-studio"
                className={`inline-flex items-center justify-center gap-2 rounded-full border px-4 py-3 text-xs font-semibold uppercase tracking-[0.22em] ${
                  isAiStudioActive
                    ? 'border-brand/35 bg-gradient-to-r from-brand via-highlight to-brand text-white'
                    : 'border-brand/25 bg-gradient-to-r from-brand-soft via-white to-brand-soft text-brand'
                }`}
                onClick={() => setOpen(false)}
              >
                Advertified Studio
                <span className="rounded-full bg-black/25 px-2 py-0.5 text-[10px] tracking-[0.14em] text-white">DEV</span>
              </Link>
            ) : null}
            {workspaceLink ? (
              <Link
                to={workspaceLink.href}
                className={isWorkspaceLinkActive ? 'button-secondary px-4 py-3 text-center' : 'button-primary px-4 py-3 text-center'}
                onClick={() => setOpen(false)}
              >
                {workspaceLink.label}
              </Link>
            ) : null}
            {user ? (
              <button
                type="button"
                className="button-secondary px-4 py-3 text-left"
                onClick={() => {
                  logout('manual');
                  setOpen(false);
                  navigate('/');
                }}
              >
                Log out
              </button>
            ) : (
              <div className="flex gap-3">
                <Link className="button-secondary px-4 py-3" to="/login" onClick={() => setOpen(false)}>
                  Log in
                </Link>
                <Link className="button-primary px-4 py-3" to="/register" onClick={() => setOpen(false)}>
                  Get started
                </Link>
              </div>
            )}
          </div>
        </div>
      ) : null}
    </header>
  );
}

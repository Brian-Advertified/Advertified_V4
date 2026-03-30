import { Menu, X } from 'lucide-react';
import { useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../features/auth/auth-context';
import { NotificationCenter } from '../../features/notifications/NotificationCenter';
import advertifiedLogo from '../../assets/advertified-logo-v3.png';
import type { UserRole } from '../../types/domain';

const publicLinks = [
  { label: 'About', href: '/about' },
  { label: 'How It Works', href: '/how-it-works' },
  { label: 'Packages', href: '/packages' },
  { label: 'Media Partners', href: '/media-partners' },
];

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

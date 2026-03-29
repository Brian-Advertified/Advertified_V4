import { Menu, X } from 'lucide-react';
import { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../../features/auth/auth-context';
import { NotificationCenter } from '../../features/notifications/NotificationCenter';
import advertifiedLogo from '../../assets/advertified-logo-v3.png';

const publicLinks = [
  { label: 'How It Works', href: '/how-it-works' },
  { label: 'Packages', href: '/packages' },
  { label: 'Media Partners', href: '/media-partners' },
];

export function Navbar() {
  const [open, setOpen] = useState(false);
  const { user, logout } = useAuth();
  const navigate = useNavigate();

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
          {user?.role === 'client' ? (
            <NavLink to="/dashboard" className="nav-link">
              Dashboard
            </NavLink>
          ) : null}
          {user?.role === 'agent' ? (
            <NavLink to="/agent" className="nav-link">
              Agent
            </NavLink>
          ) : null}
          {user?.role === 'admin' ? (
            <NavLink to="/admin" className="nav-link">
              Admin
            </NavLink>
          ) : null}
        </nav>

        <div className="hidden items-center gap-3 lg:flex">
          {user ? (
            <>
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
            {user?.role === 'client' ? (
              <NavLink to="/dashboard" className="nav-link" onClick={() => setOpen(false)}>
                Dashboard
              </NavLink>
            ) : null}
            {user?.role === 'agent' ? (
              <NavLink to="/agent" className="nav-link" onClick={() => setOpen(false)}>
                Agent
              </NavLink>
            ) : null}
            {user?.role === 'admin' ? (
              <NavLink to="/admin" className="nav-link" onClick={() => setOpen(false)}>
                Admin
              </NavLink>
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

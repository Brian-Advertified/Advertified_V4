import { Suspense, useEffect, useLayoutEffect } from 'react';
import { Route, Routes, useLocation } from 'react-router-dom';
import { Footer } from '../components/layout/Footer';
import { Navbar } from '../components/layout/Navbar';
import { LoadingState } from '../components/ui/LoadingState';
import { clearManagedStructuredData, applySeo, getRouteSeo } from '../lib/seo';
import { appRoutes } from './routeRegistry';

export function App() {
  const location = useLocation();
  const isAgentRoute = location.pathname.startsWith('/agent') || location.pathname.startsWith('/admin');
  const isAiStudioRoute = location.pathname === '/ai-studio' || location.pathname.startsWith('/ai-studio/');

  useLayoutEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    window.scrollTo({ top: 0, left: 0, behavior: 'auto' });
  }, [location.pathname, location.search]);

  useEffect(() => {
    clearManagedStructuredData();
    applySeo(getRouteSeo(location.pathname));
  }, [location.pathname]);

  return (
    <div className="min-h-screen">
      <Navbar />
      <main className={isAiStudioRoute ? 'pb-0 pt-0' : isAgentRoute ? 'pb-20 pt-8' : 'pb-20 pt-6 sm:pt-8'}>
        <Suspense fallback={<LoadingState label="Loading your workspace..." />}>
          <Routes>
            {appRoutes.map((route) => (
              <Route key={route.path} path={route.path} element={route.element} />
            ))}
          </Routes>
        </Suspense>
      </main>
      <Footer />
    </div>
  );
}

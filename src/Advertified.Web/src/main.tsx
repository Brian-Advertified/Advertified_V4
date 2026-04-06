import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import { App } from './app/App';
import { AppErrorBoundary } from './components/ui/AppErrorBoundary';
import { AuthProvider } from './features/auth/auth-context';
import { ConsentBanner } from './components/ui/ConsentBanner';
import { ToastProvider } from './components/ui/toast';
import './index.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => {
        const message = error instanceof Error ? error.message : String(error ?? '');
        if (/failed to fetch|networkerror|load failed/i.test(message)) {
          return false;
        }

        return failureCount < 2;
      },
    },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppErrorBoundary>
          <ToastProvider>
            <AuthProvider>
              <App />
              <ConsentBanner />
            </AuthProvider>
          </ToastProvider>
        </AppErrorBoundary>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);

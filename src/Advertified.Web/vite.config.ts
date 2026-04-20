import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'node:path';

function manualChunks(id: string) {
  if (!id.includes('node_modules')) {
    return undefined;
  }

  if (id.includes('@tanstack/react-query')) {
    return 'vendor-query';
  }

  if (
    id.includes('react-hook-form')
    || id.includes('@hookform/resolvers')
    || id.includes('/zod/')
    || id.includes('\\zod\\')
  ) {
    return 'vendor-forms';
  }

  if (id.includes('@mapbox/search-js-core') || id.includes('@mapbox/search-js-react')) {
    return 'vendor-mapbox-search';
  }

  if (id.includes('react-router') || id.includes('@remix-run')) {
    return 'vendor-router';
  }

  if (id.includes('lucide-react')) {
    return 'vendor-icons';
  }

  if (id.includes('react') || id.includes('scheduler')) {
    return 'vendor-react';
  }

  return 'vendor';
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    rollupOptions: {
      input: {
        index: path.resolve(__dirname, 'index.html'),
        'packages/index': path.resolve(__dirname, 'packages/index.html'),
        'how-it-works/index': path.resolve(__dirname, 'how-it-works/index.html'),
        'about/index': path.resolve(__dirname, 'about/index.html'),
        'faq/index': path.resolve(__dirname, 'faq/index.html'),
        'billboard-advertising-south-africa/index': path.resolve(__dirname, 'billboard-advertising-south-africa/index.html'),
        'radio-advertising-south-africa/index': path.resolve(__dirname, 'radio-advertising-south-africa/index.html'),
        'tv-advertising-south-africa/index': path.resolve(__dirname, 'tv-advertising-south-africa/index.html'),
        'digital-advertising-south-africa/index': path.resolve(__dirname, 'digital-advertising-south-africa/index.html'),
        'media-partners/index': path.resolve(__dirname, 'media-partners/index.html'),
        'partner-enquiry/index': path.resolve(__dirname, 'partner-enquiry/index.html'),
        'privacy/index': path.resolve(__dirname, 'privacy/index.html'),
        'cookie-policy/index': path.resolve(__dirname, 'cookie-policy/index.html'),
        'terms-of-service/index': path.resolve(__dirname, 'terms-of-service/index.html'),
        'register/index': path.resolve(__dirname, 'register/index.html'),
        'login/index': path.resolve(__dirname, 'login/index.html'),
        'verify-email/index': path.resolve(__dirname, 'verify-email/index.html'),
        'set-password/index': path.resolve(__dirname, 'set-password/index.html'),
        'start-campaign/index': path.resolve(__dirname, 'start-campaign/index.html'),
        'checkout/payment/index': path.resolve(__dirname, 'checkout/payment/index.html'),
        'checkout/confirmation/index': path.resolve(__dirname, 'checkout/confirmation/index.html'),
      },
      output: {
        manualChunks,
      },
    },
  },
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    hmr: {
      host: 'localhost',
      port: 5173,
      protocol: 'ws',
    },
  },
});

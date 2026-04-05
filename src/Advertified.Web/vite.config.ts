import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

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

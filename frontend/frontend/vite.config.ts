import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  const environment = loadEnv(mode, '.', 'VITE_');
  const target = environment.VITE_API_PROXY_TARGET || 'http://localhost:5000';
  return {
    plugins: [react()],
    build: {
      rollupOptions: {
        output: {
          manualChunks(id) {
            if (!id.includes('/node_modules/')) return;
            if (id.includes('/@mui/') || id.includes('/@emotion/')) return 'ui';
            if (id.includes('/react/') || id.includes('/react-dom/') || id.includes('/react-router')) return 'react';
          },
        },
      },
    },
    server: {
      proxy: {
        '/api': target,
        '/uploads': target,
      },
    },
  };
});

import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// Proxy /api và /auth sang backend để khỏi dính CORS khi dev.
// Backend đang bật UseHttpsRedirection nên mặc định trỏ vào https://localhost:7173
// (chạy backend bằng: dotnet run --launch-profile https).
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const target = env.VITE_PROXY_TARGET || 'https://localhost:7173';
  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        '/api': { target, changeOrigin: true, secure: false },
        '/auth': { target, changeOrigin: true, secure: false },
      },
    },
  };
});

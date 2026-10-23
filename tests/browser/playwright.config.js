import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.js',
  workers: 1,
  retries: 0,
  timeout: 45_000,
  reporter: 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL,
    viewport: { width: 1440, height: 900 },
    // Do not record traces containing sign-in requests or credentials.
    trace: 'off',
    screenshot: 'off',
    launchOptions: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ? {
      executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE,
    } : {},
  },
});

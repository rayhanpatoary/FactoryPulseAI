import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: 'e2e',
  testMatch: process.env['FACTORYPULSE_WEB_LIVE'] ? 'live.spec.ts' : 'dashboard.spec.ts',
  fullyParallel: true,
  workers: process.env['CI'] ? 2 : undefined,
  reporter: 'list',
  use: { baseURL: 'http://localhost:4200', trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run start -- --prebundle=false',
    url: 'http://localhost:4200',
    reuseExistingServer: !process.env['CI'],
    timeout: 120_000,
  },
});

import { expect, test } from '@playwright/test';

test('connects to the real monitoring API through the development proxy', async ({ page }) => {
  const mode = process.env['FACTORYPULSE_WEB_LIVE'];
  const errors: Error[] = [];
  page.on('pageerror', (error) => errors.push(error));
  if (mode === 'development') {
    const socket = page.waitForEvent('websocket', (connection) =>
      connection.url().includes('/hubs/monitoring?'),
    );
    await page.goto('/');
    await expect(page.getByText('Live connection', { exact: true })).toBeVisible();
    const machine = page.getByRole('link', { name: /CNC-001/ });
    const timestamp = machine.locator('time');
    await expect(timestamp).toBeVisible();
    const initial = await timestamp.getAttribute('datetime');
    const transport = await socket;
    await transport.waitForEvent('framereceived', (event) => {
      const payload = String(event.payload);
      return payload.includes('MachineReadingUpdated') && payload.includes('CNC-001');
    });
    await expect(timestamp).not.toHaveAttribute('datetime', initial ?? '');
    await machine.click();
    await expect(page.getByRole('img', { name: 'Temperature' })).toBeVisible();
  } else {
    await page.goto('/');
    await expect(page.getByText('No machines are registered.')).toBeVisible();
    await expect(page.getByText('Live updates unavailable')).toBeVisible();
  }
  expect(errors).toEqual([]);
});

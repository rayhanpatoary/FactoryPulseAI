import { expect, Page, test } from '@playwright/test';
import { randomUUID } from 'node:crypto';

const machine = {
  id: '33f27d0e-36a8-4e14-874a-a93352ea5082',
  machineCode: 'CNC-001',
  name: 'Mill A',
  status: 'Running',
};
const timestampUtc = '2026-01-05T11:30:00Z';
const reading = {
  machineId: machine.id,
  timestampUtc,
  temperature: 51.2,
  vibration: 1.2,
  rpm: 2300,
  torque: 15,
  toolWear: 0.142,
};
const alarm = {
  id: 3,
  machineId: machine.id,
  alarmCode: 'TEMPERATURE_WARNING',
  severity: 'Warning',
  state: 'ActiveUnacknowledged',
  createdAtUtc: timestampUtc,
  acknowledgedAtUtc: null,
  resolvedAtUtc: null,
};

async function fixture(
  page: Page,
  options: { empty?: boolean; allowAcknowledgement?: boolean; anonymous?: boolean } = {},
) {
  let authenticated = !options.anonymous;
  const user = {
    userName: 'test-user',
    roles: [options.allowAcknowledgement ? 'Operator' : 'Viewer'],
    canAcknowledge: options.allowAcknowledgement === true,
    expiresAtUtc: null,
  };
  const password = randomUUID();
  await page.route('**/hubs/monitoring/negotiate?*', (route) => route.fulfill({ status: 404 }));
  await page.route('**/api/**', (route) => {
    const url = new URL(route.request().url());
    const pageNo = Number(url.searchParams.get('page') ?? 1);
    const paged = (items: unknown[], hasMore = false) => ({
      page: pageNo,
      pageSize: 25,
      hasMore,
      items,
    });
    switch (url.pathname) {
      case '/api/auth/me':
        return authenticated ? route.fulfill({ json: user }) : route.fulfill({ status: 401 });
      case '/api/auth/csrf':
        return route.fulfill({ status: 204 });
      case '/api/auth/login':
        authenticated = route.request().postDataJSON()?.password === password;
        return authenticated ? route.fulfill({ json: user }) : route.fulfill({ status: 401 });
      case '/api/auth/logout':
        authenticated = false;
        return route.fulfill({ status: 204 });
      case '/api/status':
        return route.fulfill({ json: { operatingMode: 'simulation' } });
      case '/api/machines':
        return route.fulfill({ json: options.empty ? [] : [machine] });
      case `/api/machines/${machine.id}`:
        return route.fulfill({
          json: { ...machine, location: 'Line A', machineType: 'CNC', description: null },
        });
      case `/api/machines/${machine.id}/readings/latest`:
        return route.fulfill({ json: reading });
      case `/api/machines/${machine.id}/readings`:
        return route.fulfill({ json: paged([reading], pageNo === 1) });
      case `/api/alarms/${alarm.id}/acknowledge`:
        return route.fulfill({ status: 403, json: { status: 403 } });
      case '/api/alarms':
      case '/api/alarms/active':
      case `/api/machines/${machine.id}/alarms`:
        return route.fulfill({ json: paged(options.empty ? [] : [alarm]) });
      default:
        return route.fulfill({ status: 404 });
    }
  });
  return password;
}

test('opens the dashboard, machine catalogue and telemetry charts', async ({ page }) => {
  await fixture(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible();
  await expect(page.getByText('51.2')).toBeVisible();
  await expect(page.getByText('Live updates unavailable')).toBeVisible();
  await page.getByRole('navigation').getByRole('link', { name: 'Machines' }).click();
  await expect(page.getByRole('heading', { name: 'Machines', exact: true })).toBeVisible();
  await page.getByRole('link', { name: /CNC-001 Mill A/ }).click();
  await expect(page.getByRole('heading', { name: 'CNC-001' })).toBeVisible();
  await expect(page.getByRole('img', { name: 'Temperature' })).toBeVisible();
  await expect(page.getByRole('img', { name: 'Vibration' })).toBeVisible();
  await expect(page.getByRole('img', { name: 'Speed' })).toBeVisible();
});

test('switches languages live, preserves codes and saves only the language', async ({ page }) => {
  await fixture(page);
  await page.goto('/');
  await page.getByRole('combobox', { name: 'Language' }).selectOption('de');
  await expect(page.getByRole('heading', { name: 'Übersicht', exact: true })).toBeVisible();
  await expect(page.getByText('51,2')).toBeVisible();
  await page.getByRole('navigation').getByRole('link', { name: 'Alarme' }).click();
  await expect(page.getByRole('heading', { name: 'Alarme' })).toBeVisible();
  await expect(page.getByText('Hohe Temperatur')).toBeVisible();
  await expect(page.getByText('TEMPERATURE_WARNING')).toBeVisible();
  expect(await page.evaluate(() => Object.keys(localStorage))).toEqual(['factorypulse.language']);
  await expect(page.getByRole('button', { name: 'Quittieren' })).toHaveCount(0);
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('lang', 'de');
  await page.getByRole('combobox', { name: 'Sprache' }).selectOption('en');
  await expect(page.getByRole('heading', { name: 'Alarms' })).toBeVisible();
  await expect(page.getByText('High temperature')).toBeVisible();
});

test('supports alarm filtering and handles forbidden acknowledgement', async ({ page }) => {
  await fixture(page, { allowAcknowledgement: true });
  await page.goto('/alarms');
  await page.getByLabel('Severity').selectOption('Warning');
  await page.getByLabel('Alarm state').selectOption('true');
  const request = page.waitForRequest(
    (request) => new URL(request.url()).searchParams.get('severity') === 'Warning',
  );
  await page.getByRole('button', { name: 'Apply filters' }).click();
  expect(new URL((await request).url()).searchParams.get('active')).toBe('true');
  await page.getByRole('button', { name: 'Acknowledge' }).click();
  await expect(page.getByRole('alert')).toHaveText(
    'This deployment does not allow alarm acknowledgement.',
  );
  await expect(page.getByRole('button', { name: 'Acknowledge' })).toHaveCount(0);
});

test('validates history dates and requests the next persisted page', async ({ page }) => {
  await fixture(page);
  await page.goto('/history');
  await page.getByRole('combobox', { name: 'Machine', exact: true }).selectOption(machine.id);
  await page.getByLabel('From (local time)').fill('2026-01-07T12:00');
  await page.getByLabel('Before (local time)').fill('2026-01-06T12:00');
  await page.getByRole('button', { name: 'Apply filters' }).click();
  await expect(page.getByRole('alert')).toHaveText('The start time must be before the end time.');
  await page.getByLabel('From (local time)').fill('2026-01-05T09:00');
  await page.getByRole('button', { name: 'Apply filters' }).click();
  await expect(page.getByRole('table')).toBeVisible();
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByText('Page 2')).toBeVisible();
});

test('keeps offline and empty states usable on a narrow viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await fixture(page, { empty: true });
  await page.goto('/');
  await expect(page.getByText('No machines are registered.')).toBeVisible();
  await expect(page.getByText('Live updates unavailable')).toBeVisible();
  const nav = page.getByRole('navigation', { name: 'Main navigation' });
  await nav.getByRole('link', { name: 'Settings' }).click();
  await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
  await nav.getByRole('link', { name: 'Dashboard' }).click();
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});

test('guards monitoring, validates sign-in, then stops SignalR and clears navigation on logout', async ({
  page,
}) => {
  const password = await fixture(page, { anonymous: true });
  let negotiateCount = 0;
  let notifyClosed!: () => void;
  const closed = new Promise<void>((resolve) => {
    notifyClosed = resolve;
  });
  await page.route('**/hubs/monitoring/negotiate?*', (route) => {
    negotiateCount++;
    return route.fulfill({
      json: {
        negotiateVersion: 1,
        connectionId: 'fixture',
        connectionToken: 'fixture',
        availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text'] }],
      },
    });
  });
  await page.routeWebSocket('**/hubs/monitoring?*', (socket) => {
    socket.onMessage((message) => {
      if (String(message).includes('"protocol":"json"')) socket.send('{}\x1e');
    });
    socket.onClose(notifyClosed);
  });

  await page.goto('/alarms');
  await expect(page).toHaveURL(/\/login$/);
  expect(negotiateCount).toBe(0);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByText('This field is required.').first()).toBeVisible();
  await page.getByLabel('Username').fill('test-user');
  await page.getByLabel('Password').fill(randomUUID());
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('alert')).toContainText('Sign-in was declined.');
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible();
  await expect(page.getByText('Live connection', { exact: true })).toBeVisible();
  expect(negotiateCount).toBe(1);
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await closed;
  await expect(page.getByRole('navigation')).toHaveCount(0);
});

test('returns an expired session to the localized login screen', async ({ page }) => {
  await fixture(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible();
  await page.getByRole('combobox', { name: 'Language' }).selectOption('de');
  await page.route('**/api/status', (route) => route.fulfill({ status: 401 }));
  await page.getByRole('button', { name: 'Aktualisieren' }).click();
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole('button', { name: 'Anmelden' })).toBeVisible();
  await expect(page.getByRole('status')).toContainText('Ihre Sitzung ist abgelaufen.');
});

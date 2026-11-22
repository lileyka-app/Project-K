import { expect, test, type APIRequestContext } from '@playwright/test';
import { fakePush, standalone } from './support/fake-push';

/**
 * Web Push against the real API (the e2e stack, as real-login.pw.ts), whose env carries a throwaway
 * VAPID pair: the key is served, a device registers and unregisters, an endpoint that is not a push
 * service is refused, and the «Меню» switch goes through the same calls from the app.
 */
const realApi = process.env['PW_REAL_API'];
const resetToken = process.env['E2E_RESET_TOKEN'] ?? 'local-e2e-reset-token';
const member = {
  email: process.env['E2E_MEMBER_EMAIL'] ?? 'g1member1@projectk.com',
  password: process.env['E2E_MEMBER_PASSWORD'] ?? '',
};
// A browser's PushSubscription.toJSON() keys (RFC 8291's example device); the endpoint varies per test.
const keys = {
  p256dh: 'BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4',
  auth: 'BTBZMqHH6r4Tts7J_aSIgg',
};

test.skip(!realApi || !member.password, 'needs the e2e stack (PW_REAL_API, E2E_MEMBER_PASSWORD)');

test.beforeAll(async ({ request }) => {
  const reset = await request.post(`${realApi}/test/e2e/reset`, {
    headers: { 'X-E2E-Reset-Token': resetToken },
  });
  expect(reset.ok(), `E2E reset failed with ${reset.status()}`).toBe(true);
});

async function token(request: APIRequestContext): Promise<string> {
  const login = await request.post(`${realApi}/auth/login`, { data: { email: member.email, password: member.password } });
  expect(login.ok(), `sign-in failed with ${login.status()}`).toBe(true);
  const body = await login.json();
  expect(body.tokens?.accessToken, 'the e2e member has no second factor after the reset').toBeTruthy();
  return body.tokens.accessToken as string;
}

test('the API serves its key and keeps a device until it asks to stop', async ({ request }, info) => {
  const headers = { Authorization: `Bearer ${await token(request)}` };
  const endpoint = `https://fcm.googleapis.com/fcm/send/e2e-api-${info.project.name}-${Date.now()}`;

  const key = await request.get(`${realApi}/notifications/push/public-key`, { headers });
  expect(key.ok()).toBe(true);
  expect((await key.json()).publicKey).toMatch(/^B[A-Za-z0-9_-]{86}$/);

  const subscribe = await request.post(`${realApi}/notifications/push/subscriptions`, {
    headers,
    data: { endpoint, expirationTime: null, keys },
  });
  expect(subscribe.status()).toBe(204);
  // Again, as on every app start: the same device is refreshed, not doubled.
  const again = await request.post(`${realApi}/notifications/push/subscriptions`, { headers, data: { endpoint, keys } });
  expect(again.status()).toBe(204);

  const elsewhere = await request.post(`${realApi}/notifications/push/subscriptions`, {
    headers,
    data: { endpoint: 'https://example.com/not-a-push-service', keys },
  });
  expect(elsewhere.status()).toBe(400);

  const unsubscribe = await request.delete(`${realApi}/notifications/push/subscriptions`, {
    headers,
    params: { endpoint },
  });
  expect(unsubscribe.status()).toBe(200);
  expect(await unsubscribe.json()).toBe(true);
  // Already forgotten: still fine, nothing left to remove.
  const twice = await request.delete(`${realApi}/notifications/push/subscriptions`, { headers, params: { endpoint } });
  expect(await twice.json()).toBe(false);
});

test('the «Меню» switch registers this device with the real API', async ({ page }, info) => {
  await fakePush(page, { permission: 'default', answer: 'granted' });
  await standalone(page);
  const calls: string[] = [];
  page.on('response', (r) => {
    if (r.url().includes('/notifications/push/')) calls.push(`${r.request().method()} ${r.status()}`);
  });

  await page.goto('./');
  await expect(page).toHaveURL(/\/m\/login$/);
  await page.locator('[data-testid=email] input').fill(member.email);
  await page.locator('[data-testid=password] input').fill(member.password);
  await page.getByRole('button', { name: 'Увійти' }).click();
  await expect(page).toHaveURL(/\/m\/tabs\/home$/);

  await page.locator('#tab-button-more').click();
  const row = page.locator('app-more').getByTestId('push-row');
  await expect(row).toHaveAttribute('data-state', 'off');
  await row.evaluate((el) => el.scrollIntoView({ block: 'center' }));
  await row.getByTestId('push-toggle').click();
  await expect(row).toHaveAttribute('data-state', 'on');
  expect(calls).toContain('POST 204');
  await page.screenshot({ path: `test-results/pwa-screens/${info.project.name}-real-push-on.png`, scale: 'css' });

  await row.getByTestId('push-toggle').click();
  await expect(row).toHaveAttribute('data-state', 'off');
  await expect.poll(() => calls).toContain('DELETE 200');
});

import { expect, test } from '@playwright/test';
import { admin, api, fillCredentials, leader, member, mockApi, shot, signIn } from './support/mock-api';

test('serves an installable manifest', async ({ request }) => {
  const manifest = await (await request.get('manifest.webmanifest')).json();
  expect(manifest.name).toBe('Лілейка');
  expect(manifest.display).toBe('standalone');
  expect(manifest.start_url).toBe('./');
  expect(manifest.scope).toBe('./');
  const sizes = manifest.icons.map((icon: { sizes: string }) => icon.sizes);
  expect(sizes).toEqual(expect.arrayContaining(['192x192', '512x512']));
  expect(manifest.icons.some((icon: { purpose: string }) => icon.purpose === 'maskable')).toBe(true);
  const responses = await Promise.all(
    manifest.icons.map((icon: { src: string }) => request.get(icon.src)),
  );
  for (const response of responses) {
    expect(response.ok(), response.url()).toBe(true);
  }
});

test('shows the youth dashboard in the platform look', async ({ page }, info) => {
  const project = info.project.name;
  await signIn(page);
  await expect(page.locator('html')).toHaveAttribute('mode', project === 'iphone' ? 'ios' : 'md');
  await expect(page.getByText(/^Остап · /)).toBeVisible();
  await expect(page.getByText('Мандрівка на Говерлу')).toBeVisible();
  await expect(page.getByText('Вивчити вузли')).toBeVisible();
  await expect(page.getByText('прострочено', { exact: false })).toBeVisible();
  await expect(page.getByText('Підписано 12 з 30')).toBeVisible();
  await expect(page.getByText('42', { exact: true })).toBeVisible();
  await expect(page.getByText('Борг 150 ₴')).toBeVisible();
  if (project === 'iphone') {
    await expect(page.getByText('Додай Лілейку на екран')).toBeVisible();
  }
  await shot(page, project, '01-home-light');
  await page.evaluate(() => document.querySelector<HTMLIonContentElement>('app-home ion-content')?.scrollToBottom(0));
  await expect(page.getByText('Борг 150 ₴')).toBeInViewport();
  await shot(page, project, '01-home-light-scrolled');

  await page.emulateMedia({ colorScheme: 'dark' });
  await page.evaluate(() => document.querySelector<HTMLIonContentElement>('app-home ion-content')?.scrollToTop(0));
  await shot(page, project, '03-home-dark');
  await page.emulateMedia({ colorScheme: 'light' });
});

test('answers an event and moves a task from the dashboard', async ({ page }) => {
  const log = await signIn(page);

  const event = page.getByTestId('event').filter({ hasText: 'Сходини гуртка' });
  await event.locator('ion-segment-button').filter({ hasText: /^Іду$/ }).click();
  await expect.poll(() => log.writes.find((w) => w.path === 'me/events/e1/response')?.body).toMatchObject({
    status: 'Going',
  });
  // A series is answered per occurrence.
  expect((log.writes.find((w) => w.path === 'me/events/e1/response')?.body as { occurrenceStartUtc: string })
    .occurrenceStartUtc).toBeTruthy();

  const task = page.getByTestId('task').filter({ hasText: 'Вивчити вузли' });
  await task.getByRole('button', { name: 'Почати' }).click();
  await expect(task.getByRole('button', { name: 'Зроблено' })).toBeVisible();
  expect(log.writes.find((w) => w.path === 'agenda/t1/status')?.body).toEqual({ status: 'InProgress' });

  await task.getByRole('button', { name: 'Зроблено' }).click();
  await expect(page.getByTestId('task').filter({ hasText: 'Вивчити вузли' })).toHaveCount(0);
});

test('shows the profile and the app details from Меню', async ({ page }, info) => {
  const project = info.project.name;
  await signIn(page);
  await page.locator('#tab-button-more').click();
  await expect(page.locator('app-more').getByText('Налаштування акаунта')).toBeVisible();
  await shot(page, project, '02-more-light');

  // The account row on top, as in iOS Settings: the person's name, opening the profile.
  await expect(page.getByTestId('account')).toContainText('Остап Коваль');
  await page.getByTestId('account').click();
  await expect(page.locator('app-profile').getByText('Остап Коваль')).toBeVisible();
  await expect(page.locator('app-profile').getByText('Петрович', { exact: true })).toBeVisible();
  await expect(page.getByText('14 березня 2012')).toBeVisible();
  await shot(page, project, '07-profile');

  await page.goto('./tabs/more/about');
  await expect(page.getByText('вкладка браузера')).toBeVisible();
  // The API comes from the server's /env.js, like on a self-hosted install.
  await expect(page.getByText(api)).toBeVisible();
  await page.getByRole('button', { name: 'Натиснути' }).click();
  await page.getByRole('button', { name: 'Натиснути' }).click();
  await expect(page.getByText('Натиснуто: 2')).toBeVisible();
});

test('looks like an app when opened from the home screen', async ({ page }, info) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.addInitScript(() => {
    // What iOS and Chrome report for a home-screen launch.
    Object.defineProperty(navigator, 'standalone', { value: true });
    const original = window.matchMedia.bind(window);
    window.matchMedia = (query: string) =>
      query.includes('display-mode: standalone')
        ? ({ ...original(query), matches: true, media: query } as MediaQueryList)
        : original(query);
  });
  await signIn(page);
  await expect(page.getByText('Додай Лілейку на екран')).toHaveCount(0);
  await shot(page, info.project.name, '04-home-standalone');
  await page.goto('./tabs/more/about');
  await expect(page.getByText('застосунок', { exact: true })).toBeVisible();
});

test.describe('with the service worker', () => {
  test.use({ serviceWorkers: 'allow' });

  test('keeps working offline after the first visit', async ({ page, context }, info) => {
    test.skip(info.project.name !== 'android', 'Playwright drives service workers in Chromium only');

    await signIn(page);
    // Wait until ngsw has activated and prefetched the app shell into its cache.
    await expect
      .poll(
        () =>
          page.evaluate(async () => {
            await navigator.serviceWorker.ready;
            const key = (await caches.keys()).find((name) => name.endsWith(':assets:app:cache'));
            const shell = new URL('index.html', document.baseURI).pathname;
            return key !== undefined && (await (await caches.open(key)).match(shell)) !== undefined;
          }),
        { timeout: 45_000 },
      )
      .toBe(true);

    await context.setOffline(true);
    await page.reload();
    await expect(page.getByText('Найближче', { exact: true })).toBeVisible();
    await page.locator('#tab-button-more').click();
    await expect(page.locator('app-more').getByText('Про Лілейку')).toBeVisible();
    await shot(page, info.project.name, '05-offline');
  });
});

test('the page does not zoom like a website', async ({ page }) => {
  await page.goto('./');
  await expect(page.getByRole('button', { name: 'Увійти' })).toBeVisible();
  const blocked = await page.evaluate(() => {
    const pinch = new Event('gesturestart', { cancelable: true });
    document.dispatchEvent(pinch);
    return {
      pinch: pinch.defaultPrevented,
      touchAction: getComputedStyle(document.documentElement).touchAction,
    };
  });
  expect(blocked.pinch).toBe(true);
  expect(blocked.touchAction).toBe('pan-x pan-y');
});

test('signs in with email and password, and keeps the session across a reload', async ({ page }, info) => {
  await mockApi(page.context());
  await page.goto('./tabs/more');
  await expect(page).toHaveURL(/\/m\/login$/);
  await shot(page, info.project.name, '00-login');

  await fillCredentials(page, { ...member, password: 'wrong' });
  await expect(page.getByRole('alert')).toHaveText('Неправильний email або пароль.');
  await expect(page).toHaveURL(/\/m\/login$/);
  await shot(page, info.project.name, '00-login-error');

  await page.locator('[data-testid=password] input').fill(member.password);
  await page.getByRole('button', { name: 'Увійти' }).click();
  await expect(page.getByText('Сходини гуртка')).toBeVisible();
  const stored = await page.evaluate(() => JSON.parse(localStorage.getItem('authState') ?? '{}'));
  expect(stored).toMatchObject({ email: member.email, accessToken: null });

  // The access token lives in memory only; after a reload it comes back from the refresh cookie.
  const refreshed = page.waitForRequest((r) => r.url().endsWith('/auth/refresh') && r.method() === 'POST');
  await page.reload();
  await refreshed;
  await expect(page.getByText('Сходини гуртка')).toBeVisible();

  await page.goto('./login');
  await expect(page).toHaveURL(/\/m\/tabs\/home$/);
});

test('asks for the code when the account has two-factor sign-in', async ({ page }, info) => {
  await mockApi(page.context());
  await page.goto('./');
  await fillCredentials(page, leader);
  await expect(page.getByText('Підтвердження входу')).toBeVisible();
  await shot(page, info.project.name, '00-login-code');

  await page.locator('[data-testid=code] input').fill('000000');
  await page.getByRole('button', { name: 'Підтвердити' }).click();
  await expect(page.getByRole('alert')).toHaveText('Неправильний код підтвердження.');

  await page.locator('[data-testid=code] input').fill('123456');
  await page.getByRole('button', { name: 'Підтвердити' }).click();
  await expect(page.getByText('Сходини гуртка')).toBeVisible();
});

test('makes провід turn on two-factor sign-in before anything else', async ({ page }, info) => {
  const project = info.project.name;
  await mockApi(page.context());
  await page.goto('./');
  await fillCredentials(page, admin);
  await expect(page).toHaveURL(/\/m\/mfa$/);
  await expect(page.getByText('двофакторний вхід обовʼязковий', { exact: false })).toBeVisible();
  await expect(page.getByTestId('shared-key')).toHaveText('JBSW Y3DP EHPK 3PXP');
  await shot(page, project, '08-mfa-setup');

  // No way around it: the tabs send the person back.
  await page.goto('./tabs/home');
  await expect(page).toHaveURL(/\/m\/mfa$/);

  await page.locator('[data-testid=mfa-code] input').filter({ visible: true }).fill('000000');
  await page.getByRole('button', { name: 'Увімкнути' }).click();
  await expect(page.getByRole('alert')).toHaveText('Код не підійшов. Перевір застосунок і спробуй ще раз.');

  await page.locator('[data-testid=mfa-code] input').filter({ visible: true }).fill('123456');
  await page.getByRole('button', { name: 'Увімкнути' }).click();
  await expect(page.getByTestId('recovery-codes')).toContainText('aaaa-1111');
  await shot(page, project, '09-mfa-codes');

  await page.getByRole('button', { name: 'Я зберіг коди' }).click();
  await expect(page).toHaveURL(/\/m\/tabs\/home$/);
  await expect(page.getByText('Сходини гуртка')).toBeVisible();
});

test('signs out from the Меню tab', async ({ page }, info) => {
  await signIn(page);
  await page.locator('#tab-button-more').click();
  await expect(page.getByTestId('account')).toContainText(member.email);

  // A sheet asks first; backing out keeps the session.
  await page.locator('app-more').getByText('Вийти', { exact: true }).click();
  await page.getByRole('button', { name: 'Скасувати' }).click();
  await expect(page.locator('ion-action-sheet')).toHaveCount(0);
  expect(await page.evaluate(() => localStorage.getItem('authState'))).not.toBeNull();

  const loggedOut = page.waitForResponse((r) => r.url().endsWith('/auth/logout'));
  await page.locator('app-more').getByText('Вийти', { exact: true }).click();
  await shot(page, info.project.name, '11-sign-out-sheet');
  await page.locator('ion-action-sheet').getByRole('button', { name: 'Вийти' }).click();
  expect((await loggedOut).ok()).toBe(true);
  await expect(page).toHaveURL(/\/m\/login$/);
  expect(await page.evaluate(() => localStorage.getItem('authState'))).toBeNull();

  await page.goto('./tabs/home');
  await expect(page).toHaveURL(/\/m\/login$/);
});


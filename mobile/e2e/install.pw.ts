import { expect, test, type Page } from '@playwright/test';
import { standalone } from './support/fake-push';
import { mockApi, shot, signIn } from './support/mock-api';

/**
 * /install (open without signing in) and the hints that lead to it: the login screen and Головна.
 * Chromium's install dialog is faked in the page: a `beforeinstallprompt` with prompt()/userChoice,
 * as Chrome hands it over. The real event is held back, so headless Chromium deciding the page is
 * installable on its own does not change what a test sees.
 */

const INSTAGRAM_IOS =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148 Instagram 337.0.3.23.54 (iPhone14,5; iOS 17_5; uk_UA; uk; scale=3.00; 1170x2532; 614410279)';
const TELEGRAM_ANDROID =
  'Mozilla/5.0 (Linux; Android 14; Pixel 7 Build/AP2A.240705.005; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/126.0.6478.71 Mobile Safari/537.36 Telegram-Android/11.2.3 (Google Pixel 7; Android 14; SDK 34; HIGH)';
/** The messenger each phone project opens the page in. */
const MESSENGER_UA: Record<string, string> = { iphone: INSTAGRAM_IOS, android: TELEGRAM_ANDROID };

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    window.addEventListener(
      'beforeinstallprompt',
      (event) => {
        if (!(event as Event & { fake?: boolean }).fake) event.stopImmediatePropagation();
      },
      true,
    );
  });
});

/** What Chrome fires when the page may be installed; `answer` is the person's choice in its dialog. */
async function offerInstall(
  page: Page,
  answer: 'accepted' | 'dismissed' = 'accepted',
): Promise<void> {
  await page.evaluate((outcome) => {
    const event = new Event('beforeinstallprompt', { cancelable: true }) as Event &
      Record<string, unknown>;
    event['fake'] = true;
    event['prompt'] = () => {
      (window as unknown as { prompted: number }).prompted =
        ((window as unknown as { prompted?: number }).prompted ?? 0) + 1;
      return Promise.resolve();
    };
    event['userChoice'] = Promise.resolve({ outcome });
    window.dispatchEvent(event);
  }, answer);
}

/** The step pictures are files: wait until each has loaded, so a screenshot shows them. */
async function picturesLoaded(page: Page, count: number): Promise<void> {
  const pictures = page.locator('app-install-art img');
  await expect(pictures).toHaveCount(count);
  await expect
    .poll(() =>
      pictures.evaluateAll((imgs) =>
        imgs.every(
          (img) => (img as HTMLImageElement).complete && (img as HTMLImageElement).naturalWidth > 0,
        ),
      ),
    )
    .toBe(true);
}

test('shows this phone the steps by hand, with pictures', async ({ page }, info) => {
  const project = info.project.name;
  await mockApi(page.context());
  await page.goto('./install');
  const steps = page.getByTestId('install-step');
  if (project === 'iphone') {
    await expect(page.getByRole('heading', { name: 'Додай Лілейку на iPhone' })).toBeVisible();
    await expect(steps).toHaveCount(3);
    await expect(steps.nth(0)).toContainText('Поділитися');
    await expect(steps.nth(1)).toContainText('На початковий екран');
    await expect(steps.nth(2)).toContainText('Додати');
    await expect(page.getByText('увійди ще раз')).toBeVisible();
    await picturesLoaded(page, 3);
  } else {
    // No install dialog came (Chrome had nothing to offer yet): after a moment, the way by the menu.
    await expect(page.getByText('Готуємо встановлення…')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Додай Лілейку на телефон' })).toBeVisible({
      timeout: 8_000,
    });
    await expect(steps).toHaveCount(2);
    await expect(steps.nth(0)).toContainText('Встановити застосунок');
    await picturesLoaded(page, 2);
  }
  await expect(page.getByTestId('install-help')).toHaveAttribute(
    'href',
    /\/user\/howto\/install-app\/$/,
  );
  await shot(page, project, '40-install-steps');

  await page.emulateMedia({ colorScheme: 'dark' });
  await shot(page, project, '40-install-steps-dark');
  await page.emulateMedia({ colorScheme: 'light' });
  await page
    .locator('app-install ion-content')
    .evaluate((el) => (el as HTMLIonContentElement).scrollToBottom(0));
  await shot(page, project, '40-install-steps-end');
});

test('installs in one tap where Chrome offers its dialog, then says it is done', async ({
  page,
}, info) => {
  test.skip(info.project.name !== 'android', 'beforeinstallprompt is Chromium’s');
  await mockApi(page.context());
  await page.goto('./install');
  await expect(page.locator('app-install')).toBeVisible();
  await offerInstall(page);

  const button = page.getByTestId('install-now');
  await expect(button).toBeVisible();
  await expect(page.getByTestId('install-step')).toHaveCount(0);
  await shot(page, info.project.name, '41-install-one-tap');

  await button.click();
  await expect
    .poll(() => page.evaluate(() => (window as unknown as { prompted?: number }).prompted))
    .toBe(1);
  await expect(page.getByTestId('install-done')).toContainText('Готово!');
  await shot(page, info.project.name, '42-install-done');
});

test('falls back to the menu when the person says no in Chrome’s dialog', async ({
  page,
}, info) => {
  test.skip(info.project.name !== 'android', 'beforeinstallprompt is Chromium’s');
  await mockApi(page.context());
  await page.goto('./install');
  await expect(page.locator('app-install')).toBeVisible();
  await offerInstall(page, 'dismissed');
  await page.getByTestId('install-now').click();
  await expect(page.getByTestId('install-manual')).toContainText('Додай Лілейку на телефон');
  await expect(page.getByTestId('install-now')).toHaveCount(0);
});

test.describe('inside a messenger’s own browser', () => {
  test.use({
    userAgent: ({ userAgent }, use, info) => use(MESSENGER_UA[info.project.name] ?? userAgent),
  });

  test('asks to open the page in Safari or Chrome and offers the link', async ({ page }, info) => {
    const project = info.project.name;
    await mockApi(page.context());
    await page.goto('./install');
    const manual = page.getByTestId('install-manual');
    if (project === 'iphone') {
      await expect(manual).toContainText('Відкрий у Safari');
      await expect(manual).toContainText('Instagram');
      await expect(page.getByTestId('install-open-chrome')).toHaveCount(0);
    } else {
      await expect(manual).toContainText('Відкрий у Chrome');
      await expect(manual).toContainText('Telegram');
      await expect(page.getByTestId('install-open-chrome').getByRole('link')).toHaveAttribute(
        'href',
        /^intent:\/\/127\.0\.0\.1:\d+\/m\/install#Intent;scheme=https;package=com\.android\.chrome;end$/,
      );
    }
    await picturesLoaded(page, 1);
    await shot(page, project, '43-install-in-app');

    await page.getByTestId('install-copy').click();
    await expect(page.locator('ion-toast')).toContainText(
      /Посилання скопійовано|Не вдалося скопіювати/,
    );
  });
});

test('says so when Лілейка already runs from the home screen', async ({ page }, info) => {
  await standalone(page);
  await mockApi(page.context());
  await page.goto('./install');
  await expect(page.getByTestId('install-installed')).toContainText('Лілейку вже встановлено');
  await shot(page, info.project.name, '44-install-installed');
  await page.getByTestId('install-open').click();
  await expect(page).toHaveURL(/\/m\/login$/);
  // From the home screen the login screen has nothing to offer.
  await expect(page.getByTestId('login-install-help')).toHaveCount(0);
});

test('the login screen leads to the install', async ({ page }, info) => {
  const project = info.project.name;
  await mockApi(page.context());
  await page.goto('./');
  await expect(page).toHaveURL(/\/m\/login$/);
  await expect(page.getByTestId('login-install-help')).toContainText('Як встановити на телефон');
  await shot(page, project, '45-login-install-link');

  if (project === 'android') {
    // Chrome offering its dialog: one tap right on the login screen.
    await offerInstall(page);
    await expect(page.getByTestId('login-install')).toBeVisible();
    await shot(page, project, '45-login-install-button');
    await page.getByTestId('login-install').click();
    await expect
      .poll(() => page.evaluate(() => (window as unknown as { prompted?: number }).prompted))
      .toBe(1);
    await expect(page.getByTestId('login-install')).toHaveCount(0);
    return;
  }
  await page.getByTestId('login-install-help').click();
  await expect(page).toHaveURL(/\/m\/install$/);
  await expect(page.getByTestId('install-step')).toHaveCount(3);
});

test('Головна’s iPhone card shows how, and «Не зараз» stays dismissed', async ({ page }, info) => {
  test.skip(info.project.name !== 'iphone', 'the Share-sheet card is the iPhone’s');
  await signIn(page);
  const card = page.getByTestId('install-card');
  await expect(card).toContainText('Додай Лілейку на екран');
  await card.getByRole('link', { name: 'Показати як' }).click();
  await expect(page).toHaveURL(/\/m\/install$/);
  await expect(page.getByTestId('install-step')).toHaveCount(3);

  await page.goto('./tabs/home');
  await page.getByTestId('install-card').getByRole('button', { name: 'Не зараз' }).click();
  await expect(page.getByTestId('install-card')).toHaveCount(0);
  await page.reload();
  await expect(page.getByText('Сходини гуртка')).toBeVisible();
  await expect(page.getByTestId('install-card')).toHaveCount(0);
});

/** The web lives next to the app on the same origin; this server has only the app, so a stand-in. */
async function standInWeb(page: Page): Promise<void> {
  await page.route(/\/\?full=1$/, (route) =>
    route.fulfill({
      contentType: 'text/html; charset=utf-8',
      body: '<meta charset="utf-8"><h1>Лілейка (веб)</h1>',
    }),
  );
}

test('«Повна версія» hands over to the web and remembers it', async ({ page }, info) => {
  await signIn(page);
  await standInWeb(page);
  await page.locator('#tab-button-more').click();
  const row = page.locator('app-more').getByTestId('full-version');
  await row.evaluate((el) => el.scrollIntoView({ block: 'center' }));
  await expect(row).toContainText('Повна версія');
  await shot(page, info.project.name, '46-more-full-version');
  await row.click();
  await expect(page).toHaveURL(/\/\?full=1$/);
  await expect(page.getByRole('heading', { name: 'Лілейка (веб)' })).toBeVisible();
  expect(await page.evaluate(() => localStorage.getItem('lk.preferWeb'))).toBe('1');
});

test('/install offers the full version too', async ({ page }) => {
  await mockApi(page.context());
  await standInWeb(page);
  await page.goto('./install');
  await page.getByTestId('install-full-version').click();
  await expect(page).toHaveURL(/\/\?full=1$/);
});

import { expect, test, type Page } from '@playwright/test';

/**
 * A phone on the web goes to the phone app (/m/, the PWA) before the web renders, on the matching
 * screen; «Повна версія» (in the app, arriving as `?full=1`) keeps it on the web until «Мобільна
 * версія». Needs no API, so it runs against a bare `ng serve` too (`--no-deps`, skipping auth-setup).
 * `ng serve` has no phone app, so /m/ is a stand-in page that shows the browser really went there.
 */

const app = (page: Page) => page.getByRole('heading', { name: 'Лілейка (PWA)' });

test.beforeEach(async ({ page }) => {
  await page.route('**/m/**', route =>
    route.fulfill({ contentType: 'text/html; charset=utf-8', body: '<h1>Лілейка (PWA)</h1>' })
  );
});

test.describe('on a phone', () => {
  test.skip(({ isMobile }) => !isMobile, 'the redirect is for phones');
  // A phone as it comes, without the «Повна версія» the project sets for the rest of the suite.
  test.use({ storageState: { cookies: [], origins: [] } });

  test('goes to the matching screen of the phone app', async ({ page }) => {
    await page.goto('/login');
    await expect(page).toHaveURL(/\/m\/login$/);
    await expect(app(page)).toBeVisible();

    // A link from an email keeps its query.
    await page.goto('/member/m1?tab=skills');
    await expect(page).toHaveURL(/\/m\/tabs\/kurin\/member\/m1\?tab=skills$/);

    // No screen for it in the app: the app's start.
    await page.goto('/users');
    await expect(page).toHaveURL(/\/m\/$/);
  });

  test('keeps the pages email links and the app land on', async ({ page }) => {
    await page.goto('/reset-password?token=t&email=a%40b.c');
    // The router may write the address back with the @ decoded; it is the same link either way.
    await expect(page).toHaveURL(/\/reset-password\?token=t&email=a(%40|@)b\.c$/);
    await page.goto('/join');
    await expect(page).toHaveURL(/\/join$/);
    await expect(app(page)).toHaveCount(0);
  });

  test('stays for one page the app opened on the web', async ({ page }) => {
    await page.goto('/login?web=1');
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.locator('#email')).toBeVisible();
    // Nothing remembered: the next visit goes to the app.
    await page.goto('/login');
    await expect(page).toHaveURL(/\/m\/login$/);
  });

  test('remembers «Повна версія» until «Мобільна версія»', async ({ page }) => {
    await page.goto('/login?full=1');
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.locator('#email')).toBeVisible();
    await expect(page.getByTestId('install-app-banner')).toHaveCount(0);

    await page.reload();
    await expect(page.locator('#email')).toBeVisible();
    const back = page.getByTestId('mobile-version-banner');
    await expect(back).toContainText('Це повна версія сайту.');

    await back.getByRole('button', { name: 'Мобільна версія' }).click();
    await expect(page).toHaveURL(/\/m\/login$/);
    await page.goto('/login');
    await expect(page).toHaveURL(/\/m\/login$/);
  });
});

test('leaves a computer on the web, with no phone banners', async ({ page, isMobile }) => {
  test.skip(isMobile, 'the computer’s case');
  await page.goto('/login');
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.locator('#email')).toBeVisible();
  await expect(page.getByTestId('install-app-banner')).toHaveCount(0);
  await expect(page.getByTestId('mobile-version-banner')).toHaveCount(0);
});

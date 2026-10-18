import { expect, test, type Page } from '@playwright/test';
import {
  admin,
  api,
  fillCredentials,
  member,
  mockApi,
  ok,
  refuse,
  shot,
  signIn,
  type ExtraApi,
} from './support/mock-api';

/**
 * «Меню» and the app shell (the web's sidebar, the ☰ drawer, the header's kurin switcher): account settings, appearance, kurins, privacy, report a problem, about,
 * the cold-start notice and the sign-in links to the web.
 */

const PNG_1PX = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
  'base64',
);
const blobHost = 'https://blob.example.test';
// `/health` sits beside `/api`.
const healthUrl = `${new URL(api).origin}/health`;

const k1 = { kurinKey: 'k1', kurinNumber: 7, branch: 'UPYu', namedAfter: null, kind: 'Youth' };
const k2 = { kurinKey: 'k2', kurinNumber: 42, branch: 'USP', namedAfter: 'Сірого Лева', kind: 'Staff' };

interface Fixture {
  extra: ExtraApi;
  /** Auth writes the shared log leaves out. */
  authWrites: { path: string; body: any }[];
  account: { email: string; phoneNumber: string | null; pendingEmail: string | null; twoFactorEnabled: boolean };
  current: { kurinKey: string };
}

/** The account endpoints as the API answers them, with the second factor as the test sets it. */
function accountApi(options: { password: string; mfa: boolean; kurins?: Record<string, unknown>[] }): Fixture {
  const authWrites: Fixture['authWrites'] = [];
  const account = { email: '', phoneNumber: '+380670000000', pendingEmail: null as string | null, twoFactorEnabled: options.mfa };
  const current = { kurinKey: 'k1' };
  const kurins = options.kurins ?? [k1, k2];
  let shots = 0;
  const extra: ExtraApi = ({ method, path, body, state }) => {
    const email = state.session ?? '';
    account.email ||= email;
    if (account.twoFactorEnabled) state.mfaEnabled.add(email);
    else state.mfaEnabled.delete(email);
    if (method !== 'GET' && path.startsWith('auth/')) authWrites.push({ path, body });
    switch (`${method} ${path}`) {
      case 'GET user/me':
        return ok({ userKey: `u-${email}`, memberKey: 'm1', firstName: 'Остап', lastName: 'Коваль', role: 'Member', ...account });
      case 'PUT user/me': {
        const changed = body.email.toLowerCase() !== account.email.toLowerCase();
        if (changed && body.currentPassword !== options.password) return refuse(401, 'InvalidCredentials');
        account.phoneNumber = body.phoneNumber;
        if (changed) account.pendingEmail = body.email;
        return ok({ userKey: `u-${email}`, memberKey: 'm1', firstName: 'Остап', lastName: 'Коваль', role: 'Member', ...account });
      }
      case 'POST user/me/password':
        return body.currentPassword === options.password ? ok(true) : refuse(400, 'PasswordChangeFailed');
      case 'POST user/me/mfa/disable':
      case 'POST user/me/mfa/reset':
        if (body.currentPassword !== options.password) return refuse(401, 'InvalidCredentials');
        account.twoFactorEnabled = false;
        state.mfaEnabled.delete(email);
        return ok(true);
      case 'POST auth/mfa/recovery-codes':
        return body.currentPassword === options.password
          ? ok({ recoveryCodes: ['new1-aaaa', 'new2-bbbb', 'new3-cccc', 'new4-dddd'] })
          : refuse(401, 'InvalidCredentials');
      case 'GET auth/kurin-scope/options':
        return ok(kurins);
      case 'POST auth/kurin-scope':
        current.kurinKey = body.kurinKey;
        return ok({
          userKey: `u-${email}`, memberKey: 'm1', email, isAdmin: false, permissions: [], roles: [],
          kurinKey: body.kurinKey, requiresMfa: false, tokens: { accessToken: 'access-9' },
        });
      case 'GET me/groups':
        return ok([
          { groupKey: 'g1', kurin: { kurinKey: 'k1', kurinNumber: 7, namedAfter: null, isCurrent: current.kurinKey === 'k1' },
            name: 'Соколи', silhouetteUrl: null, isOwn: true, isLed: false },
          { groupKey: 'g7', kurin: { kurinKey: 'k2', kurinNumber: 42, namedAfter: 'Сірого Лева', isCurrent: current.kurinKey === 'k2' },
            name: 'Орли', silhouetteUrl: null, isOwn: false, isLed: true },
        ]);
      case 'POST feedback/screenshots':
        shots += 1;
        return ok({ url: `${blobHost}/photos/feedback-screenshots/shot-${shots}.png` });
      case 'POST feedback/problems':
        return ok({ issueUrl: 'https://github.com/lileyka-app/Project-K/issues/321', issueNumber: 321 });
      default:
        return null;
    }
  };
  return { extra, authWrites, account, current };
}

/** The stored session's kurin: what the next start of the app reads. */
async function storedKurin(page: Page): Promise<string | null> {
  return page.evaluate(() => JSON.parse(localStorage.getItem('authState') ?? '{}').kurinKey ?? null);
}

async function openMore(page: Page): Promise<void> {
  await page.locator('#tab-button-more').click();
  await expect(page.locator('app-more').getByText('Вигляд')).toBeVisible();
}

/** `window.open` as the app calls it for pages outside: the URL is kept instead of a tab opened. */
async function catchOpened(page: Page): Promise<() => Promise<string | null>> {
  await page.evaluate(() => {
    (window as any).__opened = null;
    window.open = (url?: string | URL) => {
      (window as any).__opened = String(url);
      return null;
    };
  });
  return () => page.evaluate(() => (window as any).__opened as string | null);
}

test('Меню repeats the web sidebar, then the look, the kurins and privacy', async ({ page }, info) => {
  const fixture = accountApi({ password: member.password, mfa: false });
  await signIn(page, member, fixture.extra);
  await openMore(page);
  const more = page.locator('app-more');
  await expect(more.getByTestId('web-menu').locator('ion-label')).toHaveText([
    'Головна',
    'Курінь',
    'Календар',
    'Задачі',
    'Планування',
    'Точкування',
    'Налаштування акаунта',
    'Довідка',
    'Повідомити про проблему',
    'Про Лілейку',
  ]);
  // More than one kurin: the row opens the switcher and names the one acted in.
  await expect(more.getByTestId('kurins-row')).toContainText('Мої курені');
  await expect(more.getByTestId('kurins-row')).toContainText('к. ч. 7');
  await expect(more.locator('ion-item').filter({ hasText: 'Вигляд' })).toContainText('Системна');
  await shot(page, info.project.name, 'account-01-more');

  // Help is the web's docs site, opened outside the app.
  const opened = await catchOpened(page);
  await more.getByTestId('menu-help').click();
  expect(await opened()).toBe('https://docs.lileyka.app/user/start/what-is/');

  // An item the app has a screen for opens it: Планування under Календар, Точкування under Курінь.
  await more.getByTestId('menu-planning').click();
  await expect(page).toHaveURL(/\/tabs\/calendar\/planning$/);
  expect(await opened()).toBe('https://docs.lileyka.app/user/start/what-is/');
  await openMore(page);
  await more.getByTestId('menu-score').click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/score$/);
  await openMore(page);
  await page.evaluate(() => document.querySelector<HTMLIonContentElement>('app-more ion-content')?.scrollToBottom(0));
  await shot(page, info.project.name, 'account-01-more-scrolled');
});

test('one kurin is not offered, as the web hides its switcher', async ({ page }) => {
  const fixture = accountApi({ password: member.password, mfa: false, kurins: [k1] });
  await signIn(page, member, fixture.extra);
  await openMore(page);
  await expect(page.locator('app-more').getByTestId('menu-kurin')).toBeVisible();
  await expect(page.locator('app-more').getByTestId('kurins-row')).toHaveCount(0);
  await expect(page.getByTestId('kurin-switcher')).toHaveCount(0);
});

test('switches the kurin from the header, as the web does', async ({ page }, info) => {
  const fixture = accountApi({ password: member.password, mfa: false });
  await signIn(page, member, fixture.extra);
  const switcher = page.locator('app-home').getByTestId('kurin-switcher');
  await expect(switcher).toContainText('к. ч. 7');
  await switcher.click();
  const sheet = page.locator('ion-action-sheet');
  await expect(sheet.getByRole('button', { name: 'к. ч. 7 ✓' })).toBeVisible();
  await shot(page, info.project.name, 'nav-03-kurin-switcher');
  await sheet.getByRole('button', { name: 'к. ч. 42 ім. Сірого Лева' }).click();
  await expect(page).toHaveURL(/\/m\/tabs\/home$/);
  expect(fixture.authWrites.find((w) => w.path === 'auth/kurin-scope')?.body).toEqual({ kurinKey: 'k2' });
  await expect(page.locator('app-home').getByTestId('kurin-switcher')).toContainText('к. ч. 42');
});

test('«Як у вебі» swaps the tabs for the web’s ☰ drawer', async ({ page }, info) => {
  const fixture = accountApi({ password: member.password, mfa: false });
  await signIn(page, member, fixture.extra);
  await page.goto('./tabs/more/appearance');
  await page.getByTestId('nav-web').click();
  await expect(page.locator('ion-tab-bar')).toBeHidden();

  await page.goto('./tabs/home');
  const burger = page.locator('app-home ion-menu-button');
  await expect(burger).toBeVisible();
  await burger.click();
  const drawer = page.getByTestId('drawer');
  await expect(drawer.getByTestId('menu-home')).toHaveClass(/current/);
  await shot(page, info.project.name, 'nav-02-drawer');

  await drawer.getByTestId('menu-calendar').click();
  await expect(page).toHaveURL(/\/tabs\/calendar$/);
  await expect(drawer).not.toHaveClass(/show-menu/);
  await expect(page.locator('app-calendar ion-menu-button')).toBeVisible();
  await shot(page, info.project.name, 'nav-01-web-calendar');

  // Remembered on the device; back to the tabs.
  await page.reload();
  await expect(page.locator('ion-tab-bar')).toBeHidden();
  await page.goto('./tabs/more/appearance');
  await page.getByTestId('nav-tabs').click();
  await expect(page.locator('ion-tab-bar')).toBeVisible();
  await expect(page.locator('app-more ion-menu-button, app-home ion-menu-button').first()).toBeHidden();
});

test('changes contacts and the password from Акаунт', async ({ page }, info) => {
  const fixture = accountApi({ password: member.password, mfa: false });
  const log = await signIn(page, member, fixture.extra);
  await page.goto('./tabs/more/account');
  const account = page.locator('app-account');
  await expect(account.getByTestId('email-row')).toContainText(member.email);
  await expect(account.getByText('+380 67 000 0000')).toBeVisible();
  await expect(account.getByTestId('mfa-status')).toHaveText('Вимкнено');
  await shot(page, info.project.name, 'account-02-account');

  // Phone only: no password asked.
  await account.getByTestId('email-row').click();
  const contacts = page.locator('ion-modal');
  await expect(contacts.getByText('Контакти', { exact: true })).toBeVisible();
  await contacts.locator('[data-testid=contact-phone] input').filter({ visible: true }).fill('+380 50 123 45 67');
  await expect(contacts.getByTestId('contact-password')).toHaveCount(0);
  await contacts.getByTestId('save-contacts').click();
  await expect(page.getByText('Контакти оновлено.')).toBeVisible();
  expect(log.writes.find((w) => w.path === 'user/me')?.body).toEqual({
    email: member.email,
    phoneNumber: '+380501234567',
    currentPassword: null,
  });
  await expect(account.getByText('+380 50 123 4567')).toBeVisible();

  // A new email asks for the password and waits for the letter.
  await account.getByTestId('email-row').click();
  await contacts.locator('[data-testid=contact-email] input').filter({ visible: true }).fill('new@example.com');
  await contacts.locator('[data-testid=contact-password] input').filter({ visible: true }).fill('wrong');
  await contacts.getByTestId('save-contacts').click();
  await expect(contacts.getByRole('alert')).toHaveText('Неправильний поточний пароль.');
  await contacts.locator('[data-testid=contact-password] input').filter({ visible: true }).fill(member.password);
  await shot(page, info.project.name, 'account-03-contacts');
  await contacts.getByTestId('save-contacts').click();
  await expect(page.getByText('Ми надіслали лист на new@example.com', { exact: false })).toBeVisible();
  await expect(account.getByText('Очікує підтвердження', { exact: false })).toContainText('new@example.com');
  // The session keeps the old address until it is confirmed.
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem('authState') ?? '{}').email)).toBe(member.email);

  // Password: the web's rules, then the match.
  await account.getByText('Змінити пароль').click();
  const sheet = page.locator('ion-modal').filter({ hasText: 'Новий пароль' });
  await sheet.locator('[data-testid=password-current] input').filter({ visible: true }).fill(member.password);
  await sheet.locator('[data-testid=password-new] input').filter({ visible: true }).fill('weak');
  await expect(sheet.getByTestId('save-password')).toHaveAttribute('disabled', /.*/);
  await sheet.locator('[data-testid=password-new] input').filter({ visible: true }).clear();
  await sheet.locator('[data-testid=password-new] input').filter({ visible: true }).fill('Strong#Pass1');
  await sheet.locator('[data-testid=password-confirm] input').filter({ visible: true }).fill('Strong#Pass2');
  await expect(sheet.getByText('Паролі не збігаються.')).toBeVisible();
  await sheet.locator('[data-testid=password-confirm] input').filter({ visible: true }).clear();
  await sheet.locator('[data-testid=password-confirm] input').filter({ visible: true }).fill('Strong#Pass1');
  await shot(page, info.project.name, 'account-04-password');
  await sheet.getByTestId('save-password').click();
  await expect(page.getByText('Пароль змінено.')).toBeVisible();
  expect(log.writes.find((w) => w.path === 'user/me/password')?.body).toEqual({
    currentPassword: member.password,
    newPassword: 'Strong#Pass1',
  });
});

test('renews recovery codes and turns the second factor off', async ({ page }, info) => {
  const fixture = accountApi({ password: member.password, mfa: true });
  const log = await signIn(page, member, fixture.extra);
  await openMore(page);
  await page.locator('app-more').getByTestId('menu-account').click();
  const account = page.locator('app-account');
  await expect(account.getByTestId('mfa-status')).toHaveText('Увімкнено');

  await account.getByText('Оновити резервні коди').click();
  const alert = page.locator('ion-alert');
  await alert.locator('input').fill(member.password);
  await shot(page, info.project.name, 'account-05-password-alert');
  await alert.getByRole('button', { name: 'Оновити' }).click();
  await expect(page.getByTestId('recovery-codes')).toContainText('new1-aaaa');
  expect(fixture.authWrites.find((w) => w.path === 'auth/mfa/recovery-codes')?.body).toEqual({
    currentPassword: member.password,
  });
  await shot(page, info.project.name, 'account-06-codes');
  await page.locator('ion-modal').getByRole('button', { name: 'Готово' }).click();
  await expect(page.getByTestId('recovery-codes')).toHaveCount(0);

  // Not провід: «Вимкнути», confirmed with the password.
  await expect(account.getByText('Скинути двофакторний вхід')).toHaveCount(0);
  await account.getByText('Вимкнути двофакторний вхід').click();
  await alert.locator('input').fill('wrong');
  await alert.getByRole('button', { name: 'Вимкнути' }).click();
  await expect(page.getByText('Неправильний поточний пароль.')).toBeVisible();
  await expect(account.getByTestId('mfa-status')).toHaveText('Увімкнено');

  await account.getByText('Вимкнути двофакторний вхід').click();
  await alert.locator('input').fill(member.password);
  await alert.getByRole('button', { name: 'Вимкнути' }).click();
  await expect(account.getByTestId('mfa-status')).toHaveText('Вимкнено');
  expect(log.writes.filter((w) => w.path === 'user/me/mfa/disable').at(-1)?.body).toEqual({
    currentPassword: member.password,
  });
  await expect(account.getByText('Увімкнути', { exact: true })).toBeVisible();
});

test('провід resets the second factor and sets it up again', async ({ page }) => {
  const fixture = accountApi({ password: admin.password, mfa: false });
  await mockApi(page.context(), fixture.extra);
  await page.goto('./');
  await fillCredentials(page, admin);
  // Mandatory first: turn it on.
  await expect(page).toHaveURL(/\/m\/mfa$/);
  fixture.account.twoFactorEnabled = true;
  await page.locator('[data-testid=mfa-code] input').filter({ visible: true }).fill('123456');
  await page.getByRole('button', { name: 'Увімкнути' }).click();
  await expect(page.getByTestId('recovery-codes')).toBeVisible();
  await page.getByRole('button', { name: 'Я зберіг коди' }).click();
  // The first way into the tabs since sign-in (the tabs chunk loads cold): WebKit on a busy CI
  // runner once stayed on /mfa past the 5s default.
  await expect(page).toHaveURL(/\/m\/tabs\/home$/, { timeout: 15_000 });

  await page.goto('./tabs/more/account');
  const account = page.locator('app-account');
  await expect(account.getByText('Вимкнути двофакторний вхід')).toHaveCount(0);
  await account.getByText('Скинути двофакторний вхід').click();
  await page.locator('ion-alert input').filter({ visible: true }).fill(admin.password);
  await page.locator('ion-alert').getByRole('button', { name: 'Скинути' }).click();
  // Straight into the setup again, with no way past it.
  await expect(page).toHaveURL(/\/m\/mfa$/);
  await expect(page.getByText('Для проводу і адміністраторів', { exact: false })).toBeVisible();
});

test('switches the theme at once and remembers it', async ({ page }, info) => {
  await page.emulateMedia({ colorScheme: 'light' });
  const fixture = accountApi({ password: member.password, mfa: false });
  await signIn(page, member, fixture.extra);
  await page.goto('./tabs/more/appearance');
  const html = page.locator('html');
  await expect(html).not.toHaveClass(/ion-palette-dark/);

  await page.getByTestId('theme-dark').click();
  await expect(html).toHaveClass(/ion-palette-dark/);
  const surface = () =>
    page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--lk-surface').trim());
  expect(await surface()).toBe('#0d1110');
  await shot(page, info.project.name, 'account-07-appearance-dark');

  // Remembered on the device.
  await page.reload();
  await expect(html).toHaveClass(/ion-palette-dark/);
  await openMore(page);
  await expect(page.locator('app-more ion-item').filter({ hasText: 'Вигляд' })).toContainText('Темна');
  await shot(page, info.project.name, 'account-08-more-dark');

  // Light on a dark phone: the brand stays light.
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto('./tabs/more/appearance');
  await page.getByTestId('theme-light').click();
  await expect(html).not.toHaveClass(/ion-palette-dark/);
  await expect(html).toHaveClass(/lk-light/);
  expect(await surface()).toBe('#f7f9f8');

  // Системна follows the phone again.
  await page.getByTestId('theme-system').click();
  await expect(html).toHaveClass(/ion-palette-dark/);
  await page.emulateMedia({ colorScheme: 'light' });
  await expect(html).not.toHaveClass(/ion-palette-dark/);
});

test('switches the kurin and opens Головна in it', async ({ page }, info) => {
  const fixture = accountApi({ password: member.password, mfa: false });
  await signIn(page, member, fixture.extra);
  await openMore(page);
  await page.locator('app-more').getByTestId('kurins-row').click();
  const kurins = page.locator('app-kurins');
  await expect(kurins.getByTestId('kurin-7')).toContainText('УПЮ · Юнацтво');
  await expect(kurins.getByTestId('kurin-7').getByLabel('Поточний курінь')).toBeVisible();
  await expect(kurins.getByTestId('kurin-42')).toContainText('к. ч. 42 ім. Сірого Лева');
  await expect(kurins.getByTestId('group-g7')).toContainText('впорядник · к. ч. 42');
  await shot(page, info.project.name, 'account-09-kurins');

  await kurins.getByTestId('kurin-42').click();
  await expect(page).toHaveURL(/\/m\/tabs\/home$/);
  await expect(page.getByText('Сходини гуртка')).toBeVisible();
  expect(fixture.authWrites.find((w) => w.path === 'auth/kurin-scope')?.body).toEqual({ kurinKey: 'k2' });
  expect(await storedKurin(page)).toBe('k2');
  await openMore(page);
  await expect(page.locator('app-more').getByTestId('kurins-row')).toContainText('к. ч. 42');
});

test('names a підготовчий курінь without a number, last in the list', async ({ page }) => {
  const k3 = { kurinKey: 'k3', kurinNumber: null, branch: 'UPYu', namedAfter: 'Ольги Басараб', kind: 'Staff' };
  const fixture = accountApi({ password: member.password, mfa: false, kurins: [k3, k1, k2] });
  await signIn(page, member, fixture.extra);
  await openMore(page);
  await page.locator('app-more').getByTestId('kurins-row').click();
  const rows = page.locator('app-kurins [data-testid^=kurin-]');
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(2)).toContainText('підг. курінь ім. Ольги Басараб');
  await expect(rows.nth(2).locator('app-kurin-plaque')).toHaveText('');
});

test('reports a problem with a screenshot', async ({ page }, info) => {
  const fixture = accountApi({ password: member.password, mfa: false });
  await page.context().route(`${blobHost}/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'image/png', body: PNG_1PX }),
  );
  const log = await signIn(page, member, fixture.extra);
  const uploads: string[] = [];
  page.on('request', (request) => {
    if (request.url().endsWith('/feedback/screenshots')) uploads.push(request.postDataBuffer()?.toString('latin1') ?? '');
  });
  await openMore(page);
  await page.locator('app-more').getByText('Повідомити про проблему').click();
  const report = page.locator('app-report');
  await expect(report.getByText('публічний', { exact: false })).toBeVisible();
  await expect(report.getByTestId('send')).toHaveAttribute('disabled', /.*/);

  await report.locator('[data-testid=report-title] input').filter({ visible: true }).fill('Не зберігається телефон');
  await report.getByTestId('report-description').fill('Змінив телефон, а після оновлення старий.');
  await report.getByTestId('report-steps').fill('1. Ще → Акаунт\n2. Змінити телефон');
  await report.getByTestId('report-expected').fill('Новий телефон лишається');
  await report.getByTestId('screenshot-input').setInputFiles({ name: 'shot.png', mimeType: 'image/png', buffer: PNG_1PX });
  await expect(report.getByTestId('thumbs').locator('img')).toHaveCount(1);

  // A photo bigger than a screen goes up drawn down, as JPEG.
  await report.getByTestId('screenshot-input').evaluate(async (input: HTMLInputElement) => {
    const canvas = document.createElement('canvas');
    canvas.width = 3000;
    canvas.height = 2000;
    canvas.getContext('2d')!.fillRect(0, 0, 3000, 2000);
    const blob = await new Promise<Blob>((resolve) => canvas.toBlob((b) => resolve(b!), 'image/png'));
    const files = new DataTransfer();
    files.items.add(new File([blob], 'photo.png', { type: 'image/png' }));
    input.files = files.files;
    input.dispatchEvent(new Event('change'));
  });
  await expect(report.getByTestId('thumbs').locator('img')).toHaveCount(2);
  expect(uploads[1]).toContain('filename="photo.jpg"');
  expect(uploads[1]).toContain('image/jpeg');
  await shot(page, info.project.name, 'account-10-report');

  await report.getByTestId('send').click();
  await expect(report.getByTestId('report-done')).toContainText('issue #321');
  expect(log.writes.find((w) => w.path === 'feedback/problems')?.body).toMatchObject({
    title: 'Не зберігається телефон',
    description: 'Змінив телефон, а після оновлення старий.',
    steps: '1. Ще → Акаунт\n2. Змінити телефон',
    expected: 'Новий телефон лишається',
    screenshotUrls: [
      `${blobHost}/photos/feedback-screenshots/shot-1.png`,
      `${blobHost}/photos/feedback-screenshots/shot-2.png`,
    ],
  });
  await shot(page, info.project.name, 'account-11-report-done');
  await report.getByRole('button', { name: 'Готово' }).click();
  await expect(page).toHaveURL(/\/m\/tabs\/more$/);
});

test('shows the privacy policy and the story of Лілейка', async ({ page }, info) => {
  await page.context().route(healthUrl, (route) =>
    route.fulfill({ status: 200, json: { version: 'v0.20.0-beta', codeName: 'Honeypot Ant' } }),
  );
  const fixture = accountApi({ password: member.password, mfa: false });
  await signIn(page, member, fixture.extra);
  await openMore(page);
  await page.locator('app-more').getByText('Конфіденційність').click();
  const privacy = page.locator('app-privacy');
  await expect(privacy.getByText('Що Лілейка зберігає про людей і хто це бачить')).toBeVisible();
  await expect(privacy.getByText('Сесії: пристрій, час входу, IP-адреса')).toBeVisible();
  await shot(page, info.project.name, 'account-12-privacy');

  await page.goto('./tabs/more/about');
  const about = page.locator('app-about');
  await expect(about.getByText('Лілейка: щоб курінь був під рукою', { exact: false })).toBeVisible();
  await expect(about.getByText('Ростислав Муха')).toBeVisible();
  await expect(about.getByTestId('milestones')).toContainText('v0.20.0-beta «Honeypot Ant»');
  await expect(about.getByTestId('api-version')).toHaveText('v0.20.0-beta «Honeypot Ant»');
  await shot(page, info.project.name, 'account-13-about');
});

test('tells that the server is waking up and goes when it answers', async ({ page }, info) => {
  let pings = 0;
  await page.context().route(healthUrl, (route) => {
    pings += 1;
    return pings === 1 ? route.fulfill({ status: 503, body: 'starting' }) : route.fulfill({ status: 200, body: 'Healthy' });
  });
  await mockApi(page.context());
  await page.goto('./');
  const banner = page.getByTestId('cold-start');
  await expect(banner).toContainText('Сервер запускається');
  await shot(page, info.project.name, 'account-14-cold-start');
  await expect(banner).toHaveCount(0, { timeout: 8000 });
  expect(pings).toBe(2);
});

test('opens the password reset and the join form on the web', async ({ page }) => {
  await mockApi(page.context());
  await page.goto('./');
  const origin = new URL(page.url()).origin;
  await expect(page.getByTestId('forgot')).toHaveAttribute('href', `${origin}/forgot-password`);
  await expect(page.getByTestId('join')).toHaveAttribute('href', `${origin}/join`);
  // In a browser tab they open in place.
  await expect(page.getByTestId('forgot')).not.toHaveAttribute('target', /.*/);
});

test.describe('from the home screen', () => {
  test('the web pages open over the app', async ({ page }) => {
    await page.addInitScript(() => Object.defineProperty(navigator, 'standalone', { value: true }));
    await mockApi(page.context());
    await page.goto('./');
    await expect(page.getByTestId('forgot')).toHaveAttribute('target', '_blank');
    await expect(page.getByTestId('join')).toHaveAttribute('target', '_blank');
  });
});

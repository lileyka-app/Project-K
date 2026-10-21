import { expect, test, type Page } from '@playwright/test';
import { member, ok, refuse, shot, signIn, type Answer, type ApiLog, type ExtraApi } from './support/mock-api';

/**
 * «Заявки» for a system admin (web adminModule waitlist-management): the applications to join,
 * approved with an invitation, rejected with an optional reason, or invited again. The row sits in
 * «Меню» with a dot while something waits, and a `WaitlistEntrySubmitted` notification opens it.
 * Nobody but an admin gets in.
 */

type Entry = Record<string, unknown> & { waitlistEntryKey: string; verificationStatus: string };

const entries = (): Entry[] => [
  {
    waitlistEntryKey: 'w1', firstName: 'Ярема', lastName: 'Гнатюк', email: 'yarema@example.com', phoneNumber: '+380671234567',
    dateOfBirth: '2010-02-20T00:00:00', stanytsia: 'Львів', regionOrCountry: 'Україна', isKurinLeaderCandidate: false,
    claimedKurinNameOrNumber: null, verificationStatus: 'Submitted', requestedAtUtc: '2026-10-10T09:00:00',
  },
  {
    waitlistEntryKey: 'w2', firstName: 'Олена', lastName: 'Ковальчук', email: 'olena@example.com', phoneNumber: '+380501112233',
    dateOfBirth: '1990-05-01T00:00:00', stanytsia: 'Київ', regionOrCountry: 'Україна', isKurinLeaderCandidate: true,
    claimedKurinNameOrNumber: '42', verificationStatus: 'Submitted', requestedAtUtc: '2026-10-11T07:30:00',
  },
  {
    waitlistEntryKey: 'w3', firstName: 'Тарас', lastName: 'Бондар', email: 'taras@example.com', phoneNumber: '+380631112233',
    dateOfBirth: '2011-01-01T00:00:00', stanytsia: null, regionOrCountry: 'Канада', isKurinLeaderCandidate: false,
    claimedKurinNameOrNumber: null, verificationStatus: 'ApprovedForInvitation', requestedAtUtc: '2026-10-01T10:00:00',
    approvedAtUtc: '2026-10-02T10:00:00', invitationSentAtUtc: '2026-10-02T10:00:00',
  },
  {
    waitlistEntryKey: 'w4', firstName: 'Ігор', lastName: 'Сидоренко', email: 'igor@example.com', phoneNumber: '+380631112299',
    dateOfBirth: '2011-01-01T00:00:00', stanytsia: 'Одеса', regionOrCountry: null, isKurinLeaderCandidate: false,
    claimedKurinNameOrNumber: null, verificationStatus: 'Rejected', requestedAtUtc: '2026-09-20T10:00:00',
  },
];

const notification: Record<string, unknown> = {
  notificationKey: 'n1', type: 'WaitlistEntrySubmitted', severity: 'Info', title: 'Нова заявка на розгляд',
  body: 'Олена Ковальчук · курінь 42', entityType: 'WaitlistEntry', entityKey: 'w2', route: '/waitlist',
  isRead: false, createdAtUtc: new Date().toISOString(), readAtUtc: null,
};

/** The admin's writes, by path and body (the shared log leaves out everything under auth/). */
type Calls = { path: string; body: unknown }[];

/** The admin's endpoints; a non-admin is refused by the API too. */
function waitlistApi(options: { admin: boolean }, calls: Calls = []): ExtraApi {
  const list = entries();
  const decide = (key: string, verb: string, path: string, body: unknown): Answer => {
    calls.push({ path, body });
    const entry = list.find((e) => e.waitlistEntryKey === key)!;
    if (verb === 'approve') entry.verificationStatus = 'ApprovedForInvitation';
    if (verb === 'reject') Object.assign(entry, { verificationStatus: 'Rejected', verificationNote: body });
    return ok(entry.waitlistEntryKey);
  };
  return ({ method, path, body }): Answer | null => {
    if (path.startsWith('auth/onboarding/waitlist') && !options.admin) return refuse(403);
    if (method === 'GET' && path === 'auth/onboarding/waitlist') return ok(list);
    if (method === 'GET' && path === 'notifications') return ok([notification]);
    if (method === 'GET' && path === 'notifications/unread-count') return ok(notification.isRead ? 0 : 1);
    if (method === 'PUT' && path === 'notifications/n1/read') return ok(Object.assign(notification, { isRead: true }));
    const action = /^auth\/onboarding\/waitlist\/(w\d)\/(approve|reject|resend-invitation)$/.exec(path);
    if (method === 'POST' && action) return decide(action[1], action[2], path, body);
    return null;
  };
}

/** Signs in as a system admin outside any kurin, the way the web's sidebar shows them «Адміністрація». */
async function signInAdmin(page: Page, extra: ExtraApi): Promise<ApiLog> {
  const log = await signIn(page, member, extra, [], { isAdmin: true, kurinKey: null });
  await expect(page.getByText('Сходини гуртка')).toBeVisible();
  return log;
}

const sheetButton = (page: Page, name: string) =>
  page.locator('ion-action-sheet:not(.overlay-hidden)').last().getByRole('button', { name, exact: true });

const entryRow = (page: Page, name: RegExp) => page.getByTestId('waitlist-entry').filter({ hasText: name });

async function openWaitlist(page: Page): Promise<void> {
  await page.locator('#tab-button-more').click();
  await expect(page.getByTestId('waitlist-dot')).toBeVisible();
  await page.getByTestId('menu-waitlist').click();
  await expect(page.getByTestId('waitlist-waiting')).toContainText('Чекають рішення · 2');
}

test('lets an admin approve, reject and invite again', async ({ page }, info) => {
  const project = info.project.name;
  const calls: Calls = [];
  await signInAdmin(page, waitlistApi({ admin: true }, calls));
  await page.locator('#tab-button-more').click();
  await expect(page.getByTestId('menu-waitlist')).toBeVisible();
  await shot(page, project, 'menu-waitlist');
  await openWaitlist(page);
  // Newest first; the claim to a kurin is said.
  await expect(page.getByTestId('waitlist-waiting').getByTestId('waitlist-entry').first()).toContainText(/Олена.*Ковальчук/);
  await expect(entryRow(page, /Олена.*Ковальчук/)).toContainText('претендує на курінь 42');
  await expect(page.getByTestId('waitlist-approved')).toContainText(/Тарас.*Бондар/);
  await expect(page.getByTestId('waitlist-done')).toContainText('Відхилено');
  await shot(page, project, 'waitlist');

  // Approve: the sheet says where the invitation goes.
  await entryRow(page, /Ярема.*Гнатюк/).click();
  await expect(page.locator('ion-action-sheet:not(.overlay-hidden)')).toContainText('yarema@example.com');
  await shot(page, project, 'waitlist-actions');
  await sheetButton(page, 'Схвалити й надіслати запрошення').click();
  await expect(page.locator('ion-toast').last()).toContainText('Схвалено. Запрошення надіслано.');
  expect(calls.map((c) => c.path)).toContain('auth/onboarding/waitlist/w1/approve');
  await expect(page.getByTestId('waitlist-approved')).toContainText(/Ярема.*Гнатюк/);
  await expect(page.getByTestId('waitlist-waiting')).toContainText('Чекають рішення · 1');

  // Reject with a reason: it goes as a bare JSON string.
  await entryRow(page, /Олена.*Ковальчук/).click();
  await sheetButton(page, 'Відхилити').click();
  const alert = page.locator('ion-alert');
  await expect(alert).toContainText(/Відхилити заявку Олена.*Ковальчук/);
  await alert.locator('textarea').fill('  Курінь 42 уже має Звʼязкового.  ');
  await shot(page, project, 'waitlist-reject');
  await alert.getByRole('button', { name: 'Відхилити' }).click();
  await expect(page.locator('ion-toast').last()).toContainText('Заявку відхилено.');
  expect(calls.find((c) => c.path === 'auth/onboarding/waitlist/w2/reject')?.body).toBe('Курінь 42 уже має Звʼязкового.');
  await expect(page.getByTestId('waitlist-waiting')).toHaveCount(0);

  // Invite again: an approved one.
  await entryRow(page, /Тарас.*Бондар/).click();
  await expect(page.locator('ion-action-sheet:not(.overlay-hidden)')).toContainText('попереднє посилання перестане працювати');
  await sheetButton(page, 'Надіслати запрошення ще раз').click();
  await expect(page.locator('ion-toast').last()).toContainText('Запрошення надіслано ще раз.');
  expect(calls.map((c) => c.path)).toContain('auth/onboarding/waitlist/w3/resend-invitation');

  // A rejected one has nothing to do.
  await expect(entryRow(page, /Ігор.*Сидоренко/)).not.toHaveClass(/item-button|ion-activatable/);

  // Nothing waits any more: the menu's dot goes out.
  await page.locator('ion-back-button').filter({ visible: true }).click();
  await expect(page.getByTestId('menu-waitlist')).toBeVisible();
  await expect(page.getByTestId('waitlist-dot')).toHaveCount(0);
});

test('opens «Заявки» from the notification about a new application', async ({ page }) => {
  await signInAdmin(page, waitlistApi({ admin: true }));
  await page.getByTestId('bell').click();
  await page.getByTestId('notification').filter({ hasText: 'Нова заявка на розгляд' }).click();
  await expect(page).toHaveURL(/\/m\/tabs\/more\/waitlist$/);
  await expect(page.getByTestId('waitlist-waiting')).toContainText(/Олена.*Ковальчук/);
});

test('keeps «Заявки» from anyone but an admin', async ({ page }) => {
  await signIn(page, member, waitlistApi({ admin: false }), ['Group:Manage:KurinWide']);
  await page.locator('#tab-button-more').click();
  await expect(page.getByTestId('menu-kurin')).toBeVisible();
  await expect(page.getByTestId('menu-waitlist')).toHaveCount(0);
  await page.goto('./tabs/more/waitlist');
  await expect(page.locator('app-waitlist app-forbidden')).toContainText('Немає доступу');
  await expect(page.getByTestId('waitlist-entry')).toHaveCount(0);
});

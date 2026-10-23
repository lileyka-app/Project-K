import { expect, test, type Locator, type Page } from '@playwright/test';
import { day, member, ok, refuse, shot, signIn, type Answer, type ExtraApi } from './support/mock-api';
import { png, pngDataUrl } from './support/png';

/**
 * The member edit (web upsert-member) on the phone: photo with the square crop, email where the
 * web shows it, ступені, profile check and перестороги for the провід, and what a youth sees on
 * their own card. Fixtures are shaped like the API's MemberResponse.
 */

const future = new Date(Date.now() + 40 * day).toISOString();
const me = `u-${member.email}`;

const kurin = { kurinKey: 'k1', number: 7, branch: 'UPYu', namedAfter: 'Івана Франка', profileVerificationEnabled: true };

const card = (memberKey: string, firstName: string, lastName: string, extra: Record<string, unknown> = {}) => ({
  memberKey, userKey: `u-${memberKey}`, accountStatus: 'Active', userRole: 'Kurin.Member', firstName, middleName: 'Петрович', lastName,
  email: `${memberKey}@example.com`, phoneNumber: '+380670000000', dateOfBirth: '2012-03-14', kurinKey: 'k1', groupKey: 'g1',
  address: 'Львів, вул. Франка, 1', school: null, profilePhotoUrl: null, latestPlastLevel: 'Uchasnyk',
  plastLevelHistories: [{ plastLevelHistoryKey: `pl-${memberKey}`, memberKey, plastLevel: 'Uchasnyk', dateAchieved: '2022-04-23' }],
  profileVerificationStatus: 'Unverified', profileVerifiedAtUtc: null, warnings: [], awards: [], leadershipHistories: [], mentoredGroupNames: [],
  ...extra,
});

const warning = (key: string, level: string, issuedByUserKey: string) =>
  ({ memberWarningKey: key, memberKey: 'x', level, issuedAtUtc: '2026-09-01T10:00:00Z', expiresAtUtc: future, issuedByUserKey, revokedAtUtc: null });

/** Марта waits for her invitation; Андрій has a claimed account, a checked profile and a пересторога from this viewer. */
function cards(): Record<string, Record<string, unknown>> {
  return {
    m1: card('m1', 'Остап', 'Коваль', {
      email: member.email, userKey: me, profilePhotoUrl: pngDataUrl(48, 48), profileVerificationStatus: 'VerifiedCurrent',
      profileVerifiedAtUtc: '2026-09-15T09:00:00Z',
    }),
    m2: card('m2', 'Марта', 'Шевчук', { accountStatus: 'PendingActivation', warnings: [warning('w1', 'Level1', 'u-other')] }),
    m3: card('m3', 'Андрій', 'Мельник', {
      cardExtras: { visaType: 'D' },
      profileVerificationStatus: 'VerifiedCurrent', profileVerifiedAtUtc: '2026-09-15T09:00:00Z', warnings: [warning('w9', 'Level1', me)],
    }),
  };
}

/** The край's card fields as the server resolves them: without a profile, both fields asked and nothing more. */
const DEFAULT_CARD = { fields: { middleName: 'required', phoneNumber: 'required' }, extra: [] };

/** `leads`: whether the kurin's policy gives the viewer a leader's перестороги over the member. */
function memberApi(
  canUpdate: (memberKey: string) => boolean,
  cardFields: unknown = DEFAULT_CARD,
  leads: (memberKey: string) => boolean = () => false,
): ExtraApi {
  const all = cards();
  const read = (path: string, match: RegExpExecArray | null): Answer | null => {
    if (path === 'kurin/k1') return ok(kurin);
    if (path === 'kurin/k1/profile') return ok({ cardFields });
    if (path === 'catalog/badges' || path === 'catalog/probes') return ok([]);
    if (!match) return null;
    const [, key, rest] = match;
    switch (rest ?? '') {
      case '': return all[key] ? ok(all[key]) : refuse(404);
      case '/dossier/memberships':
        return ok([{ membershipKey: `ms-${key}`, kurinKey: 'k1', kurinNumber: 7, branch: 'UPYu', kurinNamedAfter: 'Івана Франка', groupKey: 'g1',
          groupName: 'Соколи', kind: 'Youth', joinedAtUtc: '2020-09-01T00:00:00Z', leftAtUtc: null, isCurrent: true }]);
      case '/badges/progress': return ok([]);
      case '/dues': return refuse(403);
      case '/abilities':
        return ok({
          warnings: leads(key) ? { give: [1, 2, 3], cancel: 'OwnIssued' } : { give: [], cancel: 'None' },
          awards: { confirm: [], remove: false },
          probes: { sign: false, unsign: false },
          badges: { review: false },
          journal: false,
        });
    }
    return null;
  };
  const verify = (method: string, key: string): Answer => {
    all[key] = method === 'PUT'
      ? { ...all[key], profileVerificationStatus: 'VerifiedCurrent', profileVerifiedAtUtc: '2026-10-10T09:00:00Z' }
      : { ...all[key], profileVerificationStatus: 'Unverified', profileVerifiedAtUtc: null };
    return ok(all[key]);
  };
  return ({ method, path, body }): Answer | null => {
    const match = /^member\/(m\d)(\/.*)?$/.exec(path);
    if (method === 'GET') return read(path, match);
    if (path === 'auth/check-access') return ok(body?.entityType === 'member' && canUpdate(body.entityKey));
    if (!match) return null;
    const [, key, rest] = match;
    if (rest === '/profile-verification') return verify(method, key);
    if (rest?.startsWith('/warnings')) return ok({});
    if (!rest && method === 'PUT') return ok(all[key]);
    if (!rest && method === 'DELETE') return { status: 204, empty: true };
    return null;
  };
}

const ZVYAZKOVYI = ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Leadership:Manage:KurinWide'];
const VYKHOVNYK = ['Group:Update:OwnGroups', 'BadgeProgress:Update:OwnGroups'];

const sheetOf = (page: Page) => page.locator('ion-modal app-profile-edit-sheet');
// Not the copy Ionic's scroll assist puts in for a moment while a field takes the focus.
const input = (scope: Locator, testId: string) => scope.locator(`[data-testid=${testId}] input:not(.cloned-input)`);
const sentBody = (log: { writes: { method: string; path: string; body: any }[] }, method: string, path: string) =>
  String(log.writes.find((w) => w.method === method && w.path === path)?.body ?? '');

/** Brings a part of the sheet to the top of its scroll, for a screenshot of it. */
const scrollTo = (part: Locator) => part.evaluate((el) => el.scrollIntoView({ block: 'start' }));
/**
 * Leaves the field typed into and scrolls the sheet back up, once the scroll that typing set going
 * (Ionic keeps a focused field in view) has run out.
 */
async function toTop(page: Page, sheet: Locator): Promise<void> {
  await sheet.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await page.waitForTimeout(1000);
  await sheet.locator('ion-content').evaluate((content: HTMLIonContentElement) => content.scrollToTop(0));
}

async function openEdit(page: Page, memberKey: string, firstName: string): Promise<Locator> {
  await page.goto(`./tabs/kurin/member/${memberKey}`);
  await expect(page.getByTestId('member-head')).toContainText(firstName);
  await page.getByTestId('edit-profile').click();
  const sheet = sheetOf(page);
  await expect(input(sheet, 'profile-firstName')).toHaveValue(new RegExp(`^${firstName}`));
  return sheet;
}

/** Picks a picture in the sheet's file input and takes the crop as it opens. */
async function cropPicture(page: Page, fileInput: Locator, name: string, project: string, screenshot?: string): Promise<void> {
  await fileInput.setInputFiles({ name, mimeType: 'image/png', buffer: png(240, 160) });
  const crop = page.locator('app-image-crop-sheet');
  await expect(crop.getByTestId('crop-stage').locator('img')).toBeVisible();
  // Zoom in a step with the slider, as a thumb would.
  await crop.getByTestId('crop-zoom').evaluate((el: HTMLInputElement) => {
    el.value = '1.5';
    el.dispatchEvent(new Event('input', { bubbles: true }));
  });
  if (screenshot) await shot(page, project, screenshot);
  await crop.getByTestId('crop-done').click();
  await expect(crop).toHaveCount(0);
}

// ── Провід ───────────────────────────────────────────────────────────────────────────────────────

test('lets the Звʼязковий change a card in full: photo, email, ступені, check and перестороги', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, member, memberApi(() => true, DEFAULT_CARD, () => true), ZVYAZKOVYI);
  const sheet = await openEdit(page, 'm2', 'Марта');

  // An account nobody has claimed: its address is still the record's to fix.
  await expect(input(sheet, 'profile-email')).toHaveValue('m2@example.com');
  await expect(sheet.getByTestId('pending-account')).toContainText('Акаунт ще не активовано');
  await expect(sheet.getByTestId('resend-invitation')).toBeVisible();

  await cropPicture(page, sheet.getByTestId('photo-input'), 'marta.png', project, 'member-photo-crop');
  await expect(sheet.getByTestId('profile-photo').locator('img')).toHaveAttribute('src', /^blob:/);
  await expect(sheet.getByTestId('clear-photo')).toBeVisible();
  await input(sheet, 'profile-email').fill('marta.new@example.com');
  await expect(sheet.getByTestId('pending-account')).toContainText('лист із новим посиланням');
  await toTop(page, sheet);
  await shot(page, project, 'member-edit');

  // Ступені: the УПЮ switch opens its dates.
  const ranks = sheet.getByTestId('profile-ranks');
  await expect(input(ranks, 'rank-Uchasnyk')).toHaveValue('2022-04-23');
  await expect(ranks.getByTestId('rank-Rozviduvach')).toHaveCount(0);
  await ranks.getByTestId('rank-branch-UPYu').click();
  await input(ranks, 'rank-Rozviduvach').fill('2025-05-01');
  await scrollTo(ranks);
  await shot(page, project, 'member-ranks');

  // The profile check is done at once.
  const check = sheet.getByTestId('profile-verification');
  await expect(check).toContainText('Ще не верифіковано');
  await check.getByTestId('verification-action').click();
  await expect.poll(() => log.writes.some((w) => w.method === 'PUT' && w.path === 'member/m2/profile-verification')).toBe(true);
  await expect(check).toContainText('Верифіковано 10.10.2026');
  await expect(check.getByTestId('verification-action')).toContainText('Зняти верифікацію профілю');

  // Перестороги: somebody else's first stays; the second follows it, and only then the third.
  const warnings = sheet.getByTestId('profile-warnings');
  await expect(warnings.getByTestId('warning-Level1')).toHaveClass(/checkbox-checked/);
  await expect(warnings.getByTestId('warning-Level1')).toHaveClass(/checkbox-disabled/);
  await expect(warnings).toContainText('Скасувати може автор перестороги або той, кому курінь це дозволив.');
  await expect(warnings.getByTestId('warning-Level3')).toHaveClass(/checkbox-disabled/);
  await warnings.getByTestId('warning-Level2').click();
  await expect(warnings.getByTestId('warning-Level2')).toHaveClass(/checkbox-checked/);
  await expect(warnings.getByTestId('warning-Level3')).not.toHaveClass(/checkbox-disabled/);
  await expect(sheet.getByTestId('delete-member')).toBeVisible();
  await scrollTo(warnings);
  await expect(page.locator('ion-toast')).toHaveCount(0, { timeout: 8_000 });
  await shot(page, project, 'member-warnings');

  await sheet.getByTestId('save-profile').click();
  await expect.poll(() => sentBody(log, 'PUT', 'member/m2')).toContain('marta.new@example.com');
  const sent = sentBody(log, 'PUT', 'member/m2');
  expect(sent).toMatch(/name="blob"; filename="marta\.jpg"/);
  expect(sent).toContain('Content-Type: image/jpeg');
  expect(sent).toMatch(/name="plastLevelHistories\[1\]\.plastLevel"\r\n\r\nRozviduvach/);
  expect(sent).toMatch(/name="plastLevelHistories\[0\]\.plastLevelHistoryKey"\r\n\r\npl-m2/);
  expect(sent).toMatch(/name="removeProfilePhoto"\r\n\r\nfalse/);
  // The address was not touched, so it is left out and the server keeps it.
  expect(sent).not.toMatch(/name="address"/);
  await expect.poll(() => log.writes.find((w) => w.method === 'POST' && w.path === 'member/m2/warnings')?.body).toEqual({ level: 'Level2' });
  // Somebody else's пересторога is not touched.
  expect(log.writes.some((w) => w.method === 'DELETE' && w.path.startsWith('member/m2/warnings'))).toBe(false);
  await expect(page.locator('ion-modal.show-modal')).toHaveCount(0);
});

test('lets a Виховник check and warn but not touch an account’s email or delete', async ({ page }, info) => {
  const log = await signIn(page, member, memberApi(() => true, DEFAULT_CARD, () => true), VYKHOVNYK);
  const sheet = await openEdit(page, 'm3', 'Андрій');
  // A claimed account's address is its owner's.
  await expect(sheet.getByTestId('profile-email')).toHaveCount(0);
  await expect(sheet).toContainText('Email належить акаунту учасника');
  await expect(sheet.getByTestId('delete-member')).toHaveCount(0);
  await expect(sheet.getByTestId('profile-ranks')).toBeVisible();

  // Taking a check back asks first.
  const check = sheet.getByTestId('profile-verification');
  await expect(check).toContainText('Верифіковано 15.09.2026');
  await check.getByTestId('verification-action').click();
  await page.locator('ion-action-sheet').getByRole('button', { name: 'Зняти', exact: true }).click();
  await expect.poll(() => log.writes.some((w) => w.method === 'DELETE' && w.path === 'member/m3/profile-verification')).toBe(true);
  await expect(check).toContainText('Ще не верифіковано');

  // One's own пересторога can be lifted.
  const warnings = sheet.getByTestId('profile-warnings');
  await expect(warnings.getByTestId('warning-Level1')).not.toHaveClass(/checkbox-disabled/);
  await warnings.getByTestId('warning-Level1').click();
  await expect(warnings.getByTestId('warning-Level1')).not.toHaveClass(/checkbox-checked/);
  await sheet.getByTestId('save-profile').click();
  await expect.poll(() => log.writes.some((w) => w.method === 'DELETE' && w.path === 'member/m3/warnings/w9')).toBe(true);
  // The email goes back as it was.
  expect(sentBody(log, 'PUT', 'member/m3')).toMatch(/name="email"\r\n\r\nm3@example\.com/);
  await shot(page, info.project.name, 'member-after-edit');
});

test('asks what the край asks: no middle name, an optional phone and a visa', async ({ page }) => {
  const visaCard = {
    fields: { middleName: 'hidden', phoneNumber: 'optional' },
    extra: [
      { key: 'visaType', label: 'Тип візи', type: 'text', required: true },
      { key: 'visaUntil', label: 'Віза до', type: 'date', required: false },
      { key: 'visaNote', label: 'Примітка', type: 'text', required: false },
    ],
  };
  const log = await signIn(page, member, memberApi(() => true, visaCard, () => true), VYKHOVNYK);
  await page.goto('./tabs/kurin/member/m3');
  await expect(page.getByTestId('card-extra-visaType')).toContainText('Тип візи');
  await expect(page.getByTestId('card-extra-visaType')).toContainText('D');
  await expect(page.getByTestId('card-extra-visaUntil')).toHaveCount(0);

  await page.getByTestId('edit-profile').click();
  const sheet = sheetOf(page);
  await expect(input(sheet, 'profile-extra-visaType')).toHaveValue('D');
  await expect(sheet.getByTestId('profile-middleName')).toHaveCount(0);
  await input(sheet, 'profile-phoneNumber').fill('');
  await input(sheet, 'profile-extra-visaType').fill('');
  await expect(sheet.getByTestId('save-profile')).toHaveAttribute('disabled', '');
  await input(sheet, 'profile-extra-visaType').fill('C');
  await input(sheet, 'profile-extra-visaUntil').fill('2027-03-01');
  await sheet.getByTestId('save-profile').click();
  await expect.poll(() => sentBody(log, 'PUT', 'member/m3')).toContain('cardExtras[visaType]');
  const sent = sentBody(log, 'PUT', 'member/m3');
  expect(sent).toMatch(/name="cardExtras\[visaType\]"\r\n\r\nC\r\n/);
  expect(sent).toMatch(/name="cardExtras\[visaUntil\]"\r\n\r\n2027-03-01/);
  // Only what changed goes: a value this viewer was not shown is not wiped.
  expect(sent).not.toContain('cardExtras[visaNote]');
  // The hidden middle name goes back as it was read; the server keeps it either way.
  expect(sent).toMatch(/name="middleName"\r\n\r\nПетрович/);
  expect(sent).toMatch(/name="phoneNumber"\r\n\r\n\r\n/);
});

test('lets the Звʼязковий delete a profile after asking', async ({ page }) => {
  const log = await signIn(page, member, memberApi(() => true, DEFAULT_CARD, () => true), ZVYAZKOVYI);
  const sheet = await openEdit(page, 'm2', 'Марта');
  await sheet.getByTestId('delete-member').click();
  await page.locator('ion-action-sheet').getByRole('button', { name: 'Видалити', exact: true }).click();
  await expect.poll(() => log.writes.some((w) => w.method === 'DELETE' && w.path === 'member/m2')).toBe(true);
  await expect(page).toHaveURL(/\/tabs\/kurin$/);
});

// ── Youth ────────────────────────────────────────────────────────────────────────────────────────

test('gives a youth on their own card the photo, the basic fields and their ступені', async ({ page }, info) => {
  const log = await signIn(page, member, memberApi((key) => key === 'm1'));
  const sheet = await openEdit(page, 'm1', 'Остап');
  await expect(sheet.getByTestId('profile-email')).toHaveCount(0);
  await expect(sheet).toContainText('Email змінюється в налаштуваннях акаунта.');
  await Promise.all(
    ['profile-verification', 'profile-warnings', 'delete-member', 'pending-account'].map((part) =>
      expect(sheet.getByTestId(part)).toHaveCount(0),
    ),
  );
  // Their own ступені are theirs to record; a checked profile then waits for the провід again.
  await expect(sheet.getByTestId('profile-ranks')).toBeVisible();
  await expect(sheet.getByTestId('ranks-stale-note')).toContainText('зніме верифікацію');
  const ranks = sheet.getByTestId('profile-ranks');
  await ranks.getByTestId('rank-branch-UPYu').click();
  await input(ranks, 'rank-Rozviduvach').fill('2024-05-01');
  // The photo is theirs to change, as on the web.
  await expect(sheet.getByTestId('profile-photo').locator('img')).toBeVisible();
  await sheet.getByTestId('clear-photo').click();
  await expect(sheet.getByTestId('profile-photo').locator('img')).toHaveCount(0);
  await expect(sheet.getByTestId('pick-photo')).toContainText('Завантажити фото');
  await shot(page, info.project.name, 'member-edit-own');
  await sheet.getByTestId('save-profile').click();
  await expect.poll(() => sentBody(log, 'PUT', 'member/m1')).toMatch(/name="removeProfilePhoto"\r\n\r\ntrue/);
  const sent = sentBody(log, 'PUT', 'member/m1');
  expect(sent).toContain(member.email);
  expect(sent).toMatch(/name="plastLevelHistories\[0\]\.plastLevelHistoryKey"\r\n\r\npl-m1/);
  expect(sent).toMatch(/name="plastLevelHistories\[1\]\.plastLevel"\r\n\r\nRozviduvach/);
  expect(sent).not.toContain('name="blob"');
  // Nobody else's card opens for change.
  await page.goto('./tabs/kurin/member/m2');
  await expect(page.getByTestId('member-head')).toContainText('Марта');
  await expect(page.getByTestId('edit-profile')).toHaveCount(0);
});

test('lets a youth put up a new photo of their own', async ({ page }, info) => {
  const log = await signIn(page, member, memberApi((key) => key === 'm1'));
  const sheet = await openEdit(page, 'm1', 'Остап');
  await cropPicture(page, sheet.getByTestId('photo-input'), 'IMG_0042.PNG', info.project.name);
  await sheet.getByTestId('save-profile').click();
  await expect.poll(() => sentBody(log, 'PUT', 'member/m1')).toMatch(/name="blob"; filename="IMG_0042\.jpg"/);
});

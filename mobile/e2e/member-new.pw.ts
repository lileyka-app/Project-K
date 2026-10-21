import { expect, test, type Locator, type Page } from '@playwright/test';
import { member, ok, refuse, shot, signIn, type Answer, type ExtraApi } from './support/mock-api';
import { png } from './support/png';

/**
 * «Новий учасник» on the phone (web upsert-member, create): the Звʼязковий adds someone to the
 * kurin from its «Учасники», whoever the server lets create in a гурток adds them there, and a
 * youth sees neither. The new card opens once the record is made, as on the web.
 */

const kurin = { kurinKey: 'k1', number: 7, branch: 'UPYu', namedAfter: 'Івана Франка', profileVerificationEnabled: false };
const group = { groupKey: 'g1', kurinKey: 'k1', name: 'Соколи', description: null, silhouetteUrl: null, kurinNumber: 7 };

const row = (memberKey: string, firstName: string, lastName: string) => ({
  memberKey, firstName, middleName: 'Петрович', lastName, groupKey: 'g1', groupName: 'Соколи', latestPlastLevel: 'Uchasnyk', warnings: [],
});

/** Whatever the form sent becomes the new card, so the page that opens shows what was typed. */
function newMemberApi(opts: { canCreateInGroup: boolean; emailTaken?: boolean }): ExtraApi {
  let created: Record<string, unknown> | null = null;
  const field = (body: string, name: string) => {
    const escaped = name.replace(/[\\[\].]/g, String.raw`\$&`);
    return new RegExp(String.raw`name="${escaped}"\r\n\r\n([^\r]*)`).exec(body)?.[1] ?? null;
  };
  return ({ method, path, body }): Answer | null => {
    if (method === 'GET') {
      switch (path) {
        case 'kurin/k1': return ok(kurin);
        case 'group/groups': return ok([group]);
        case 'group/g1': return ok(group);
        case 'member/kurins/k1/members': return ok([row('m2', 'Марта', 'Шевчук'), row('m3', 'Андрій', 'Мельник')]);
        case 'member/groups/g1/members': return ok([row('m2', 'Марта', 'Шевчук')]);
        case 'member/members/kv/k1':
        case 'group/groups/k1/mentor-assignments':
        case 'kurin/k1/dues/groups':
        case 'group/g1/mentors':
        case 'catalog/badges':
        case 'catalog/probes':
        case 'member/m9/badges/progress':
          return ok([]);
        case 'leadership/type/group/g1': return ok(null);
        case 'member/m9': return created ? ok(created) : refuse(404);
        case 'member/m9/dossier/memberships':
          return ok([{ membershipKey: 'ms9', kurinKey: 'k1', kurinNumber: 7, branch: 'UPYu', kurinNamedAfter: 'Івана Франка',
            groupKey: created?.['groupKey'] ?? null, groupName: created?.['groupKey'] ? 'Соколи' : null, kind: 'Youth',
            joinedAtUtc: '2026-10-11T00:00:00Z', leftAtUtc: null, isCurrent: true }]);
        case 'member/m9/dues': return refuse(403);
      }
      return null;
    }
    if (path === 'auth/check-access') return ok(body?.entityType === 'group' && body?.action === 'Create' && opts.canCreateInGroup);
    if (method === 'POST' && (path === 'member' || path === 'member/kurins/k1/members')) {
      if (opts.emailTaken) return refuse(409);
      const sent = String(body);
      created = {
        memberKey: 'm9', userKey: 'u-m9', accountStatus: 'PendingActivation', invitationSent: true,
        firstName: field(sent, 'firstName'), middleName: field(sent, 'middleName'), lastName: field(sent, 'lastName'),
        email: field(sent, 'email'), phoneNumber: field(sent, 'phoneNumber'), dateOfBirth: field(sent, 'dateOfBirth'),
        kurinKey: 'k1', groupKey: field(sent, 'groupKey'), profilePhotoUrl: null, latestPlastLevel: 'Prykhylnyk',
        plastLevelHistories: [{ plastLevelHistoryKey: 'pl9', plastLevel: 'Prykhylnyk', dateAchieved: field(sent, 'plastLevelHistories[0].dateAchieved') }],
        warnings: [], awards: [], leadershipHistories: [], mentoredGroupNames: [], profileVerificationStatus: 'Unverified',
      };
      return { status: 201, json: created };
    }
    return null;
  };
}

const ZVYAZKOVYI = ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Leadership:Manage:KurinWide'];
const VYKHOVNYK = ['Group:Update:OwnGroups', 'BadgeProgress:Update:OwnGroups', 'Leadership:Update:OwnGroups'];

const sheetOf = (page: Page) => page.locator('ion-modal app-profile-edit-sheet');
const input = (scope: Locator, testId: string) => scope.locator(`[data-testid=${testId}] input`);
const sentBody = (log: { writes: { method: string; path: string; body: any }[] }, method: string, path: string) =>
  String(log.writes.find((w) => w.method === method && w.path === path)?.body ?? '');

async function openKurinMembers(page: Page): Promise<void> {
  await page.locator('ion-tab-button[tab=kurin]').click();
  await expect(page.getByTestId('kurin-head')).toContainText('7 курінь');
  await page.locator('ion-segment-button[value=members]').click();
  await expect(page.getByTestId('member')).toHaveCount(2);
}

async function fillBasics(sheet: Locator): Promise<void> {
  await input(sheet, 'profile-firstName').fill('Ярема');
  await input(sheet, 'profile-middleName').fill('Олегович');
  await input(sheet, 'profile-lastName').fill('Гнатюк');
  await input(sheet, 'profile-email').fill('yarema@example.com');
  await input(sheet, 'profile-phoneNumber').fill('+380 67 123 45 67');
  await input(sheet, 'profile-dateOfBirth').fill('2014-02-20');
}

/** Leaves the field typed into and scrolls the sheet back up once Ionic's keep-in-view scroll has run out. */
async function toTop(page: Page, sheet: Locator): Promise<void> {
  await sheet.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await page.waitForTimeout(1000);
  await sheet.locator('ion-content').evaluate((content: HTMLIonContentElement) => content.scrollToTop(0));
}

test('lets the Звʼязковий add a member to the kurin and opens the new card', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, member, newMemberApi({ canCreateInGroup: true }), ZVYAZKOVYI);
  await openKurinMembers(page);
  await page.getByTestId('new-member').click();
  const sheet = sheetOf(page);
  await expect(sheet.locator('ion-title')).toContainText('Новий учасник');
  // A new record: the email is asked for, as the invitation's address; nothing to verify, warn or delete yet.
  await expect(sheet.getByTestId('invite-hint')).toContainText('лист із посиланням для активації акаунта');
  await Promise.all(
    ['profile-verification', 'profile-warnings', 'delete-member', 'pending-account', 'clear-photo'].map((part) =>
      expect(sheet.getByTestId(part)).toHaveCount(0),
    ),
  );
  await expect(sheet.getByTestId('save-profile')).toHaveAttribute('disabled', '');

  await fillBasics(sheet);
  // Too young for Пласт (web minAge): the form says so and will not save.
  await input(sheet, 'profile-dateOfBirth').fill('2022-01-01');
  await expect(sheet.getByTestId('too-young')).toContainText('щонайменше 6 років');
  await expect(sheet.getByTestId('save-profile')).toHaveAttribute('disabled', '');
  await input(sheet, 'profile-dateOfBirth').fill('2014-02-20');
  await expect(sheet.getByTestId('too-young')).toHaveCount(0);

  // A photo, cropped square as in the edit.
  await sheet.getByTestId('photo-input').setInputFiles({ name: 'yarema.png', mimeType: 'image/png', buffer: png(240, 160) });
  const crop = page.locator('app-image-crop-sheet');
  await expect(crop.getByTestId('crop-stage').locator('img')).toBeVisible();
  await crop.getByTestId('crop-done').click();
  await expect(crop).toHaveCount(0);
  await expect(sheet.getByTestId('profile-photo').locator('img')).toHaveAttribute('src', /^blob:/);

  // The провід who start the record give the ступені too.
  await input(sheet.getByTestId('profile-ranks'), 'rank-Prykhylnyk').fill('2024-01-10');
  await toTop(page, sheet);
  await shot(page, project, 'member-new');

  await sheet.getByTestId('save-profile').click();
  await expect.poll(() => sentBody(log, 'POST', 'member/kurins/k1/members')).toContain('yarema@example.com');
  const sent = sentBody(log, 'POST', 'member/kurins/k1/members');
  expect(sent).toMatch(/name="kurinKey"\r\n\r\nk1/);
  expect(sent).not.toContain('name="groupKey"');
  expect(sent).toMatch(/name="createUserAccount"\r\n\r\ntrue/);
  expect(sent).toMatch(/name="dateOfBirth"\r\n\r\n2014-02-20/);
  expect(sent).toMatch(/name="plastLevelHistories\[0\]\.plastLevel"\r\n\r\nPrykhylnyk/);
  expect(sent).toMatch(/name="blob"; filename="yarema\.jpg"/);
  expect(sent).not.toContain('removeProfilePhoto');

  // The new card opens.
  await expect(page).toHaveURL(/\/tabs\/kurin\/member\/m9$/);
  await expect(page.getByTestId('member-head')).toContainText('Ярема');
  await expect(page.locator('ion-toast')).toContainText('Запрошення пішло на yarema@example.com');
});

test('adds a member straight into a гурток for whoever the server lets', async ({ page }) => {
  const log = await signIn(page, member, newMemberApi({ canCreateInGroup: true }), VYKHOVNYK);
  await page.goto('./tabs/kurin/group/g1');
  await expect(page.getByTestId('group-head')).toContainText('Соколи');
  await page.getByTestId('group-new-member').click();
  const sheet = sheetOf(page);
  await fillBasics(sheet);
  await sheet.getByTestId('save-profile').click();
  await expect.poll(() => sentBody(log, 'POST', 'member')).toMatch(/name="groupKey"\r\n\r\ng1/);
  expect(sentBody(log, 'POST', 'member')).not.toContain('name="kurinKey"');
  await expect(page).toHaveURL(/\/tabs\/kurin\/member\/m9$/);
  await expect(page.getByTestId('member-head')).toContainText('Гнатюк');
});

test('says when the address already belongs to someone and keeps the form', async ({ page }) => {
  await signIn(page, member, newMemberApi({ canCreateInGroup: true, emailTaken: true }), ZVYAZKOVYI);
  await openKurinMembers(page);
  await page.getByTestId('new-member').click();
  const sheet = sheetOf(page);
  await fillBasics(sheet);
  await sheet.getByTestId('save-profile').click();
  await expect(page.locator('ion-toast')).toContainText('Ця адреса вже належить іншому акаунту або заявці');
  await expect(sheet).toBeVisible();
  // Android's scroll assist clones a focused input for a moment; the field is the first.
  await expect(input(sheet, 'profile-email').first()).toHaveValue('yarema@example.com');
});

test('gives a youth no way to add a member', async ({ page }) => {
  await signIn(page, member, newMemberApi({ canCreateInGroup: false }));
  await openKurinMembers(page);
  await expect(page.getByTestId('new-member')).toHaveCount(0);
  await page.goto('./tabs/kurin/group/g1');
  await expect(page.getByTestId('group-members')).toContainText('Марта');
  await expect(page.getByTestId('group-new-member')).toHaveCount(0);
});

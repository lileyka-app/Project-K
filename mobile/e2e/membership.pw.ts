import { expect, test, type Page } from '@playwright/test';
import { member, ok, refuse, shot, signIn, type Answer, type ExtraApi } from './support/mock-api';

/**
 * Membership in the kurin, the Звʼязковий's (member-card's memberships tile «Гурток» / «Вивести»):
 * a member's гурток changes, and a membership is closed with a confirmation. Nobody else sees any
 * of it.
 */

const kurin = { kurinKey: 'k1', number: 7, branch: 'UPYu', namedAfter: 'Івана Франка', profileVerificationEnabled: false };
const groups = [
  { groupKey: 'g1', kurinKey: 'k1', name: 'Соколи', description: null, silhouetteUrl: null, kurinNumber: 7 },
  { groupKey: 'g2', kurinKey: 'k1', name: 'Орли', description: null, silhouetteUrl: null, kurinNumber: 7 },
];

const row = (memberKey: string, firstName: string, lastName: string) => ({
  memberKey, firstName, middleName: null, lastName, groupKey: 'g1', groupName: 'Соколи', latestPlastLevel: 'Uchasnyk', warnings: [],
});

const marta = {
  ...row('m2', 'Марта', 'Шевчук'), userKey: 'u-m2', email: 'marta@example.com', kurinKey: 'k1', profilePhotoUrl: null,
  plastLevelHistories: [], awards: [], leadershipHistories: [], mentoredGroupNames: [], profileVerificationStatus: 'Unverified',
};

interface World {
  kurinMembers: ReturnType<typeof row>[];
  /** Marta's memberships: here (with her гурток) and another kurin she is also in. */
  martaGroup: { groupKey: string | null; groupName: string | null };
  martaLeft: boolean;
  /** What Marta owes the kurin, if anything: debt does not leave with her. */
  martaOwes?: number;
}

function membershipApi(world: World): ExtraApi {
  return ({ method, path, body }): Answer | null => {
    if (method === 'GET') {
      switch (path) {
        case 'kurin/k1': return ok(kurin);
        case 'group/groups': return ok(groups);
        case 'member/kurins/k1/members': return ok(world.kurinMembers);
        case 'member/members/kv/k1':
        case 'group/groups/k1/mentor-assignments':
        case 'kurin/k1/dues/groups':
        case 'catalog/badges':
        case 'catalog/probes':
        case 'member/m2/badges/progress':
          return ok([]);
        case 'leadership/type/kurin/k1': return ok(null);
        case 'member/m2': return ok({ ...marta, groupKey: world.martaGroup.groupKey, groupName: world.martaGroup.groupName });
        case 'member/m2/dossier/memberships':
          return ok([
            { membershipKey: 'ms2', kurinKey: 'k1', kurinNumber: 7, branch: 'UPYu', kurinNamedAfter: 'Івана Франка', ...world.martaGroup,
              kind: 'Youth', joinedAtUtc: '2021-09-01T00:00:00Z', leftAtUtc: world.martaLeft ? '2026-10-11T08:00:00Z' : null, isCurrent: !world.martaLeft },
            { membershipKey: 'ms3', kurinKey: 'k5', kurinNumber: 12, branch: 'UPYu', kurinNamedAfter: null, groupKey: 'g9', groupName: 'Лелеки',
              kind: 'Youth', joinedAtUtc: '2024-09-01T00:00:00Z', leftAtUtc: null, isCurrent: true },
          ]);
        case 'member/m2/dues':
          return world.martaOwes
            ? ok({ hasAccount: true, kurinKey: 'k1', currency: 'UAH', numberLocale: 'uk-UA', currentQuarter: { year: 2026, number: 4 },
              balance: -world.martaOwes, owedToLeave: world.martaOwes, quarterRate: null, isConcessionNow: false, currentGroupKey: 'g1', currentGroupName: 'Соколи',
              canOpenGroupDues: false, accounts: [], entries: [] })
            : refuse(403);
      }
      return null;
    }
    if (path === 'auth/check-access') return ok(false);
    if (method === 'PUT' && path === 'kurin/k1/memberships/m2/group') {
      const target = groups.find((g) => g.groupKey === body?.groupKey);
      world.martaGroup = { groupKey: target?.groupKey ?? null, groupName: target?.name ?? null };
      return ok({});
    }
    if (method === 'DELETE' && path === 'kurin/k1/memberships/m2') {
      world.martaLeft = true;
      return { status: 200, empty: true };
    }
    return null;
  };
}

const newWorld = (): World => ({
  kurinMembers: [row('m2', 'Марта', 'Шевчук'), row('m3', 'Андрій', 'Мельник')],
  martaGroup: { groupKey: 'g1', groupName: 'Соколи' },
  martaLeft: false,
});

const ZVYAZKOVYI = ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Leadership:Manage:KurinWide'];
const VYKHOVNYK = ['Group:Update:OwnGroups', 'BadgeProgress:Update:OwnGroups', 'Leadership:Update:OwnGroups'];

/** A button of the action sheet that is open now (a dismissed one can linger while it animates out). */
const sheetButton = (page: Page, name: string | RegExp) =>
  page.locator('ion-action-sheet:not(.overlay-hidden)').last().getByRole('button', { name, exact: true });

async function openKurinMembers(page: Page, count = 2): Promise<void> {
  await page.locator('ion-tab-button[tab=kurin]').click();
  await expect(page.getByTestId('kurin-head')).toContainText('7 курінь');
  await page.locator('ion-segment-button[value=members]').click();
  await expect(page.getByTestId('member')).toHaveCount(count);
}

async function openMarta(page: Page): Promise<void> {
  await openKurinMembers(page);
  await page.getByTestId('member').filter({ hasText: /Шевчук.*Марта/ }).click();
  await expect(page.locator('app-member h1')).toContainText('Марта');
  await expect(page.getByTestId('memberships')).toContainText('к. ч. 7');
}

test('lets the Звʼязковий change a member’s гурток and take them out of the kurin', async ({ page }, info) => {
  const project = info.project.name;
  const world = newWorld();
  const log = await signIn(page, member, membershipApi(world), ZVYAZKOVYI);
  await openMarta(page);
  const memberships = page.getByTestId('memberships');
  // Only the membership in the kurin one acts in; another kurin's is its own провід's.
  await expect(page.getByTestId('membership-group')).toHaveCount(1);
  await expect(page.getByTestId('membership-leave')).toHaveCount(1);
  await page.getByTestId('membership-leave').scrollIntoViewIfNeeded();
  await shot(page, project, 'member-memberships-actions');

  await page.getByTestId('membership-group').click();
  await expect(sheetButton(page, 'Поза гуртком')).toBeVisible();
  // The гурток one is in is not offered again.
  await expect(sheetButton(page, /^Соколи/)).toHaveCount(0);
  await sheetButton(page, /^Орли/).click();
  await expect(page.locator('ion-toast').last()).toContainText(/Тепер у гуртку «Орли/);
  expect(log.writes.find((w) => w.method === 'PUT' && w.path === 'kurin/k1/memberships/m2/group')?.body).toEqual({ groupKey: 'g2' });
  await expect(memberships).toContainText('Орли');

  await page.getByTestId('membership-leave').click();
  await expect(sheetButton(page, 'Вивести')).toBeVisible();
  await expect(page.locator('ion-action-sheet:not(.overlay-hidden)')).toContainText('лишаються — вони належать їй, а не куреню');
  await shot(page, project, 'member-leave-confirm');
  await sheetButton(page, 'Вивести').click();
  await expect(page.locator('ion-toast').last()).toContainText('Членство в курені ч. 7 закрито.');
  expect(log.writes.some((w) => w.method === 'DELETE' && w.path === 'kurin/k1/memberships/m2')).toBe(true);
  // The membership goes to «Раніше»; nothing is left to act on.
  await expect(page.getByTestId('membership-leave')).toHaveCount(0);
  await expect(page.getByTestId('member-memberships-past')).toContainText('к. ч. 7');
});

test('does not offer to take out someone who owes the kurin', async ({ page }, info) => {
  const world = { ...newWorld(), martaOwes: 300 };
  const log = await signIn(page, member, membershipApi(world), ZVYAZKOVYI);
  await openMarta(page);
  await page.getByTestId('membership-leave').scrollIntoViewIfNeeded();
  await page.getByTestId('membership-leave').click();
  await expect(page.locator('ion-toast').last()).toContainText(/винна куреню 300.*сплачено повністю/);
  await shot(page, info.project.name, 'member-leave-owes');
  await expect(page.locator('ion-action-sheet:not(.overlay-hidden)')).toHaveCount(0);
  expect(log.writes.some((w) => w.method === 'DELETE')).toBe(false);
});

test('keeps the Звʼязковий’s membership controls from a Виховник', async ({ page }) => {
  await signIn(page, member, membershipApi(newWorld()), VYKHOVNYK);
  await openMarta(page);
  await expect(page.getByTestId('membership-group')).toHaveCount(0);
  await expect(page.getByTestId('membership-leave')).toHaveCount(0);
  // Back to the kurin's list: the active tab, tapped again, goes to its root.
  await page.locator('ion-tab-button[tab=kurin]').click();
  await expect(page.getByTestId('member')).toHaveCount(2);
  await expect(page.getByTestId('new-member')).toHaveCount(0);
});

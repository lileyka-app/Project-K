import { expect, test, type Page } from '@playwright/test';
import { member, ok, refuse, shot, signIn, type Answer, type ExtraApi } from './support/mock-api';

/**
 * People on a гурток (web group-panel, its «Впорядники» dialog, the leadership form for a гурток
 * and member-card's «Гурток»): the Звʼязковий adds people from the kurin, moves them to another
 * гурток or out of this one, and picks the виховники; whoever seats a гурток's провід does so
 * here; a youth sees none of it.
 */

const NONE = '00000000-0000-0000-0000-000000000000';
const kurin = { kurinKey: 'k1', number: 7, branch: 'UPYu', namedAfter: 'Івана Франка' };
const groups = [
  { groupKey: 'g1', kurinKey: 'k1', name: 'Соколи', description: null, silhouetteUrl: null, kurinNumber: 7 },
  { groupKey: 'g2', kurinKey: 'k1', name: 'Орли', description: null, silhouetteUrl: null, kurinNumber: 7 },
];

const person = (memberKey: string, firstName: string, lastName: string, groupKey: string) => ({
  memberKey, userKey: `u-${memberKey}`, firstName, middleName: null, lastName, groupKey,
  groupName: groups.find((g) => g.groupKey === groupKey)?.name ?? null, latestPlastLevel: 'Uchasnyk', warnings: [],
});

/** The kurin's people and where each is placed, the гурток's виховники and its провід, as the server keeps them between calls. */
function peopleApi(opts: { canCreate: boolean; withLeadership: boolean }): ExtraApi {
  const people = [
    person('m1', 'Марта', 'Шевчук', 'g1'),
    person('m2', 'Остап', 'Коваль', 'g1'),
    person('m3', 'Іван', 'Бойко', 'g2'),
    person('m4', 'Ярема', 'Гнатюк', NONE),
    person('m5', 'Софія', 'Дмитренко', NONE),
  ];
  const kv = [person('m7', 'Олена', 'Петренко', NONE), person('m8', 'Андрій', 'Мельник', NONE)];
  let mentors = [kv[0]];
  let leadership: Record<string, unknown> | null = opts.withLeadership
    ? { leadershipKey: 'l1', startDate: '2025-09-01', endDate: null, leadershipHistories: [
        { leadershipHistoryKey: 'h1', leadershipKey: 'l1', role: 'Hurtkoviy', startDate: '2025-09-01', endDate: null, member: people[0] },
      ] }
    : null;
  const historiesFrom = (body: any) => body.leadershipHistories.map((h: any, i: number) => ({
    leadershipHistoryKey: `n${i}`, leadershipKey: 'l1', role: h.role, startDate: h.startDate, endDate: null,
    member: people.find((p) => p.memberKey === h.member.memberKey),
  }));
  const read = (path: string): Answer | null => {
    switch (path) {
      case 'kurin/k1': return ok(kurin);
      case 'group/groups': return ok(groups);
      case 'group/g1': return ok(groups[0]);
      case 'member/kurins/k1/members': return ok(people);
      case 'member/groups/g1/members': return ok(people.filter((p) => p.groupKey === 'g1'));
      case 'member/members/mentor-candidates/k1': return ok(kv);
      case 'group/g1/mentors': return ok(mentors);
      case 'leadership/type/group/g1': return ok(leadership);
      case 'member/members/kv/k1':
      case 'group/groups/k1/mentor-assignments':
      case 'kurin/k1/dues/groups':
        return ok([]);
    }
    return null;
  };
  return ({ method, path, body }): Answer | null => {
    if (method === 'GET') return read(path);
    if (path === 'auth/check-access') return ok(body?.entityType === 'group' && body?.action === 'Create' && opts.canCreate);
    const placement = /^kurin\/k1\/memberships\/(m\d)\/group$/.exec(path);
    if (placement && method === 'PUT') {
      const who = people.find((p) => p.memberKey === placement[1]);
      if (!who) return refuse(404, 'NotAMember');
      who.groupKey = body.groupKey ?? NONE;
      who.groupName = groups.find((g) => g.groupKey === body.groupKey)?.name ?? null;
      return { status: 200, empty: true };
    }
    const mentor = /^group\/g1\/mentors\/(u-m\d)$/.exec(path);
    if (mentor) {
      const who = kv.find((p) => p.userKey === mentor[1])!;
      mentors = method === 'POST' ? [...mentors, who] : mentors.filter((m) => m !== who);
      return { status: 200, empty: true };
    }
    if (path === 'leadership' && method === 'POST') {
      leadership = { leadershipKey: 'l1', startDate: body.startDate, endDate: null, leadershipHistories: historiesFrom(body) };
      return { status: 201, json: leadership };
    }
    if (path === 'leadership/l1' && method === 'PUT') {
      leadership = { ...leadership, leadershipHistories: historiesFrom(body) };
      return ok(leadership);
    }
    return null;
  };
}

const ZVYAZKOVYI = ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Leadership:Manage:KurinWide'];
const VYKHOVNYK = ['Group:Update:OwnGroups', 'Leadership:Update:OwnGroups'];

const writes = (log: { writes: { method: string; path: string; body: any }[] }, method: string, path: string | RegExp) =>
  log.writes.filter((w) => w.method === method && (typeof path === 'string' ? w.path === path : path.test(w.path)));

async function openGroup(page: Page): Promise<void> {
  await page.goto('./tabs/kurin/group/g1');
  await expect(page.getByTestId('group-head')).toContainText('Соколи');
  await expect(page.getByTestId('group-member')).toHaveCount(2);
}

const picker = (page: Page) => page.locator('ion-modal app-people-pick-sheet');
// The one opened last: a sheet that leads to another is still leaving when the next one shows.
const actionSheet = (page: Page) => page.locator('ion-action-sheet').last();

test('lets the Звʼязковий add people from the kurin, move one on and take one out', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, member, peopleApi({ canCreate: true, withLeadership: true }), ZVYAZKOVYI);
  await openGroup(page);
  await expect(page.getByTestId('group-new-member')).toBeVisible();
  await expect(page.getByTestId('member-actions')).toHaveCount(2);

  // «Додати з куреня»: those in no гурток first, then those elsewhere, with where they are now.
  await page.getByTestId('add-from-kurin').click();
  const pick = picker(page);
  await expect(pick.locator('ion-title')).toContainText('Додати з куреня');
  const rows = pick.getByTestId('pick-person');
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toContainText('Гнатюк');
  await expect(rows.nth(0)).toContainText('Без гуртка');
  await expect(rows.nth(2)).toContainText('Зараз: Орли');
  await expect(pick.getByTestId('pick-done')).toHaveAttribute('disabled', '');
  await rows.filter({ hasText: 'Гнатюк' }).click();
  await rows.filter({ hasText: 'Бойко' }).click();
  await expect(pick.getByTestId('pick-done')).toContainText('Додати (2)');
  await shot(page, project, 'group-add-people');
  await pick.getByTestId('pick-done').click();
  await expect.poll(() => writes(log, 'PUT', /^kurin\/k1\/memberships\/m\d\/group$/).map((w) => [w.path, w.body.groupKey])).toEqual([
    ['kurin/k1/memberships/m4/group', 'g1'],
    ['kurin/k1/memberships/m3/group', 'g1'],
  ]);
  await expect(page.locator('ion-toast')).toContainText('Додано до гуртка: 2.');
  await expect(page.getByTestId('group-member')).toHaveCount(4);
  await expect(page.getByTestId('group-members')).toContainText('Гнатюк');
  await expect(page.locator('ion-toast')).toHaveCount(0, { timeout: 8_000 });
  await shot(page, project, 'group-people');

  // Moving one on: to another гурток of the kurin.
  await page.getByTestId('group-member-row').filter({ hasText: 'Коваль' }).getByTestId('member-actions').click();
  await actionSheet(page).getByRole('button', { name: 'Перевести до іншого гуртка' }).click();
  await expect(actionSheet(page)).toContainText('До якого гуртка перевести?');
  await actionSheet(page).getByRole('button', { name: 'Орли' }).click();
  await expect.poll(() => writes(log, 'PUT', 'kurin/k1/memberships/m2/group').map((w) => w.body)).toEqual([{ groupKey: 'g2' }]);
  await expect(page.getByTestId('group-members')).not.toContainText('Коваль');

  // Taking one out asks first; they stay in the kurin.
  await page.getByTestId('group-member-row').filter({ hasText: 'Бойко' }).getByTestId('member-actions').click();
  await actionSheet(page).getByRole('button', { name: 'Прибрати з гуртка' }).click();
  await expect(actionSheet(page)).toContainText('Учасник лишається в курені.');
  await actionSheet(page).getByRole('button', { name: 'Прибрати', exact: true }).click();
  await expect.poll(() => writes(log, 'PUT', 'kurin/k1/memberships/m3/group').map((w) => w.body)).toEqual([{ groupKey: 'g1' }, { groupKey: null }]);
  await expect(page.getByTestId('group-member')).toHaveCount(2);
});

test('lets the Звʼязковий pick the виховники and seat the провід гуртка', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, member, peopleApi({ canCreate: true, withLeadership: true }), ZVYAZKOVYI);
  await openGroup(page);

  // Впорядники: the КВ, with the ones there now ticked.
  await expect(page.getByTestId('group-mentors')).toContainText('Петренко');
  await page.getByTestId('choose-mentors').click();
  const pick = picker(page);
  await expect(pick.locator('ion-title')).toContainText('Впорядники');
  await expect(pick.getByTestId('pick-person').filter({ hasText: 'Петренко' })).toHaveAttribute('aria-pressed', 'true');
  await pick.getByTestId('pick-person').filter({ hasText: 'Петренко' }).click();
  await pick.getByTestId('pick-person').filter({ hasText: 'Мельник' }).click();
  await pick.getByTestId('pick-done').click();
  await expect.poll(() => log.writes.filter((w) => w.path.startsWith('group/g1/mentors/')).map((w) => `${w.method} ${w.path}`).sort((a, b) => a.localeCompare(b))).toEqual([
    'DELETE group/g1/mentors/u-m7',
    'POST group/g1/mentors/u-m8',
  ]);
  await expect(page.getByTestId('group-mentors')).toContainText('Мельник');
  await expect(page.getByTestId('group-mentors')).not.toContainText('Петренко');

  // Провід гуртка: one person in each office, from the гурток.
  await expect(page.getByTestId('group-leadership')).toContainText('Гуртковий');
  await page.getByTestId('setup-leadership').click();
  const form = page.locator('ion-modal app-leadership-sheet');
  await expect(form.getByTestId('seat-Hurtkoviy')).toContainText('Шевчук');
  await expect(form.getByTestId('seat-Pysar')).toContainText('Не обсаджено');
  await expect(form.getByTestId('save-leadership')).toHaveAttribute('disabled', '');
  await form.getByTestId('seat-Pysar').click();
  const seat = picker(page);
  await expect(seat.locator('ion-title')).toContainText('Писар');
  await seat.getByTestId('pick-person').filter({ hasText: 'Коваль' }).click();
  await expect(seat).toHaveCount(0);
  await expect(form.getByTestId('seat-Pysar')).toContainText('Коваль');
  await expect(page.locator('ion-toast')).toHaveCount(0, { timeout: 8_000 });
  await shot(page, project, 'group-leaders');
  await form.getByTestId('save-leadership').click();
  await expect.poll(() => writes(log, 'PUT', 'leadership/l1')[0]?.body?.leadershipHistories?.map((h: any) => `${h.role}:${h.member.memberKey}`)).toEqual([
    'Hurtkoviy:m1',
    'Pysar:m2',
  ]);
  expect(writes(log, 'PUT', 'leadership/l1')[0].body).toMatchObject({ leadershipKey: 'l1', type: 'Group', entityKey: 'g1', endDate: null });
  await expect(form).toHaveCount(0);
  await expect(page.getByTestId('group-leadership')).toContainText('Писар');
});

test('lets a Виховник seat a first провід and add a new member, but not place people', async ({ page }) => {
  const log = await signIn(page, member, peopleApi({ canCreate: true, withLeadership: false }), VYKHOVNYK);
  await openGroup(page);
  await expect(page.getByTestId('group-new-member')).toBeVisible();
  await Promise.all(['add-from-kurin', 'group-mentors', 'member-actions'].map((part) => expect(page.getByTestId(part)).toHaveCount(0)));

  await page.getByTestId('setup-leadership').click();
  const form = page.locator('ion-modal app-leadership-sheet');
  await form.getByTestId('seat-Hurtkoviy').click();
  // «Не обсаджено» is ticked while nobody holds the office.
  await expect(picker(page).getByTestId('pick-none')).toContainText('Не обсаджено');
  await picker(page).getByTestId('pick-person').filter({ hasText: 'Шевчук' }).click();
  await form.getByTestId('save-leadership').click();
  await expect.poll(() => writes(log, 'POST', 'leadership')[0]?.body).toMatchObject({
    type: 'Group', entityKey: 'g1', leadershipHistories: [{ role: 'Hurtkoviy', member: { memberKey: 'm1' } }],
  });
  await expect(page.getByTestId('group-leadership')).toContainText('Шевчук');
});

test('gives a youth none of the people controls on a гурток', async ({ page }) => {
  await signIn(page, member, peopleApi({ canCreate: false, withLeadership: true }));
  await openGroup(page);
  await expect(page.getByTestId('group-leadership')).toContainText('Гуртковий');
  await Promise.all(
    ['group-new-member', 'add-from-kurin', 'group-mentors', 'member-actions', 'setup-leadership', 'edit-group'].map((part) =>
      expect(page.getByTestId(part)).toHaveCount(0),
    ),
  );
  // No swipe actions behind the rows either.
  await expect(page.locator('ion-item-options')).toHaveCount(0);
});

import { expect, test, type Page } from '@playwright/test';
import { member, ok, shot, signIn, type Answer, type ExtraApi } from './support/mock-api';

/**
 * The kurin's КВ and провід (web kv-panel, leadership-panel and the leadership form): everyone
 * reads who is in them now and the archive; the Звʼязковий adds a впорядник, sets whose гуртки are
 * whose and hands his office over; whoever may seat the kurin's провід does so — the курінний all
 * but himself.
 */

const kurin = { kurinKey: 'k1', number: 7, branch: 'UPYu', namedAfter: 'Івана Франка', stanytsia: 'Львів', regionOrCountry: 'Львівщина' };
const groups = [
  { groupKey: 'g1', kurinKey: 'k1', name: 'Соколи', description: null, silhouetteUrl: null, kurinNumber: 7 },
  { groupKey: 'g2', kurinKey: 'k1', name: 'Орли', description: null, silhouetteUrl: null, kurinNumber: 7 },
];

const person = (memberKey: string, firstName: string, lastName: string, userRole: string | null = null) => ({
  memberKey, userKey: `u-${memberKey}`, userRole, firstName, middleName: null, lastName, profilePhotoUrl: null,
  latestPlastLevel: 'Uchasnyk', leadershipHistories: [], mentoredGroupNames: [], warnings: [],
});
const lookup = (p: ReturnType<typeof person>) => ({ memberKey: p.memberKey, firstName: p.firstName, lastName: p.lastName, middleName: null });
const office = (key: string, role: string, who: ReturnType<typeof person>, startDate: string, endDate: string | null = null, leadershipKey = 'l1') => ({
  leadershipHistoryKey: key, leadershipKey, role, startDate, endDate, member: lookup(who),
});

const olena = person('m1', 'Олена', 'Петренко', 'KV.Zvyazkovyi');
const andrii = person('m5', 'Андрій', 'Мельник', 'KV.Vykhovnyk');
const marta = person('m2', 'Марта', 'Шевчук');
const ivan = person('m3', 'Іван', 'Бойко');
const ostap = person('m4', 'Остап', 'Коваль');
const taras = person('m6', 'Тарас', 'Гнатюк');
const people = [olena, andrii, marta, ivan, ostap, taras];

/** The КВ, its assignments and the kurin's провід, as the server keeps them between calls. */
function kvApi(withLeadership = true): ExtraApi {
  let kvMembers = [olena, andrii];
  let assignments = [
    { mentorAssignmentKey: 'ma1', mentorUserKey: 'u-m5', groupKey: 'g1', groupName: 'Соколи', assignedAtUtc: '2025-09-01T00:00:00Z', revokedAtUtc: null, member: andrii },
    { mentorAssignmentKey: 'ma0', mentorUserKey: 'u-m4', groupKey: 'g2', groupName: 'Орли', assignedAtUtc: '2023-09-01T00:00:00Z', revokedAtUtc: '2025-06-30T00:00:00Z', member: ostap },
  ];
  let kv: any = { leadershipKey: 'kv1', startDate: '2024-09-01', endDate: null, leadershipHistories: [
    office('kh1', 'Zvyazkovyi', olena, '2024-09-01', null, 'kv1'),
    office('kh2', 'Vykhovnyk', andrii, '2025-09-01', null, 'kv1'),
  ] };
  let leadership: any = withLeadership
    ? { leadershipKey: 'l1', startDate: '2023-09-01', endDate: null, leadershipHistories: [
        office('h1', 'Kurinnuy', marta, '2025-09-01'),
        office('h2', 'Pysar', ivan, '2025-09-01'),
        office('h0', 'Kurinnuy', ostap, '2023-09-01', '2025-08-31'),
      ] }
    : null;
  const historiesFrom = (body: any, key: string, before: any[]) => [
    ...before.map((h) => ({ ...h, endDate: body.leadershipHistories.some((n: any) => n.role === h.role && n.member.memberKey === h.member.memberKey) ? h.endDate : (h.endDate ?? '2026-10-11') })),
    ...body.leadershipHistories
      .filter((n: any) => !before.some((h) => !h.endDate && h.role === n.role && h.member.memberKey === n.member.memberKey))
      .map((n: any, i: number) => ({ leadershipHistoryKey: `${key}-n${i}`, leadershipKey: key, role: n.role, startDate: n.startDate, endDate: null, member: n.member })),
  ];
  const read = (path: string): Answer | null => {
    switch (path) {
      case 'kurin/k1': return ok(kurin);
      case 'group/groups': return ok(groups);
      case 'group/g1': return ok(groups[0]);
      case 'member/kurins/k1/members': return ok(people);
      case 'member/groups/g1/members': return ok([marta, ivan]);
      case 'member/members/kv/k1': return ok(kvMembers);
      case 'member/members/mentor-candidates/k1': return ok(people);
      case 'group/groups/k1/mentor-assignments': return ok(assignments);
      case 'leadership/type/kurin/k1': return ok(leadership);
      case 'leadership/type/kv/k1': return ok(kv);
      case 'leadership/type/group/g1':
        return ok({ leadershipKey: 'lg', startDate: '2024-09-01', endDate: null, leadershipHistories: [
          office('g-h1', 'Hurtkoviy', marta, '2025-09-01', null, 'lg'),
          office('g-h0', 'Hurtkoviy', ivan, '2024-09-01', '2025-08-31', 'lg'),
        ] });
      case 'kurin/k1/dues/groups': return ok([]);
    }
    return null;
  };
  const assign = (groupKey: string, userKey: string) => {
    const who = people.find((p) => p.userKey === userKey)!;
    assignments = [...assignments, { mentorAssignmentKey: `ma-${groupKey}-${userKey}`, mentorUserKey: userKey, groupKey, groupName: groups.find((g) => g.groupKey === groupKey)!.name, assignedAtUtc: '2026-10-11T08:00:00Z', revokedAtUtc: null, member: who }];
    if (!kvMembers.includes(who)) kvMembers = [...kvMembers, { ...who, userRole: 'KV.Vykhovnyk' }];
  };
  const revoke = (groupKey: string, userKey: string) => {
    assignments = assignments.map((a) => (a.mentorUserKey === userKey && a.groupKey === groupKey && !a.revokedAtUtc ? { ...a, revokedAtUtc: '2026-10-11T08:00:00Z' } : a));
  };
  return ({ method, path, body }): Answer | null => {
    if (method === 'GET') return read(path);
    if (path === 'auth/check-access') return ok(false);
    if (path === 'auth/kurin-scope') {
      // Rights come from the offices: with the office gone, so are the Звʼязковий's.
      return ok({ userKey: 'u-yunak@example.com', memberKey: 'm1', email: member.email, isAdmin: false, permissions: [], roles: [], kurinKey: 'k1', requiresMfa: false, tokens: { accessToken: 'access-9' } });
    }
    const mentor = /^group\/(g\d)\/mentors\/(u-m\d)$/.exec(path);
    if (mentor) {
      const [, groupKey, userKey] = mentor;
      if (method === 'POST') assign(groupKey, userKey);
      else revoke(groupKey, userKey);
      return { status: 200, empty: true };
    }
    if (path === 'leadership/kv1' && method === 'PUT') {
      kv = { ...kv, leadershipHistories: historiesFrom(body, 'kv1', kv.leadershipHistories) };
      const holder = body.leadershipHistories.find((h: any) => h.role === 'Zvyazkovyi')?.member.memberKey;
      kvMembers = [...people.filter((p) => p.memberKey === holder).map((p) => ({ ...p, userRole: 'KV.Zvyazkovyi' })), andrii];
      return ok(kv);
    }
    if (path === 'leadership/l1' && method === 'PUT') {
      leadership = { ...leadership, leadershipHistories: historiesFrom(body, 'l1', leadership.leadershipHistories) };
      return ok(leadership);
    }
    if (path === 'leadership' && method === 'POST') {
      leadership = { leadershipKey: 'l1', startDate: body.startDate, endDate: null, leadershipHistories: historiesFrom(body, 'l1', []) };
      return { status: 201, json: leadership };
    }
    return null;
  };
}

const ZVYAZKOVYI = ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Leadership:Manage:KurinWide'];
const KURINNYI = ['Leadership:Update:KurinWide'];

const writes = (log: { writes: { method: string; path: string; body: any }[] }, method: string, path: string | RegExp) =>
  log.writes.filter((w) => w.method === method && (typeof path === 'string' ? w.path === path : path.test(w.path)));

const picker = (page: Page) => page.locator('ion-modal app-people-pick-sheet');
const form = (page: Page) => page.locator('ion-modal app-leadership-sheet');
const mentorSheet = (page: Page) => page.locator('ion-modal app-kv-mentor-sheet');

async function openKurinTab(page: Page): Promise<void> {
  await page.locator('ion-tab-button[tab=kurin]').click();
  await expect(page.getByTestId('kurin-head')).toContainText('7 курінь');
  await expect(page.getByTestId('kv')).toContainText('Петренко');
}

test('shows everyone the КВ and the провід with their archives, and nothing to change', async ({ page }, info) => {
  const project = info.project.name;
  await signIn(page, member, kvApi());
  await openKurinTab(page);
  await expect(page.getByTestId('kv-more')).toContainText('Архів впорядників');
  await expect(page.getByTestId('setup-leadership')).toHaveCount(0);

  await page.getByTestId('kv-more').click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/kv$/);
  const rows = page.getByTestId('kv-row');
  await expect(rows).toHaveCount(2);
  await expect(rows.first()).toContainText('Петренко');
  await expect(rows.first().locator('ion-badge')).toHaveText(['Звʼязковий']);
  await expect(rows.first().locator('ion-badge')).toHaveClass(/lk-tag--danger/);
  await expect(rows.nth(1).locator('ion-badge')).toHaveText(['Впорядник', 'Соколи']);
  await expect(page.getByTestId('kv-archived')).toContainText('Коваль');
  await expect(page.getByTestId('kv-archived')).toContainText('01.09.23 – 30.06.25');
  await Promise.all(['add-mentor', 'edit-mentor', 'hand-over'].map((part) => expect(page.getByTestId(part)).toHaveCount(0)));
  await shot(page, project, 'kv-page');

  await page.locator('app-kv ion-back-button').click();
  await page.getByTestId('leadership-more').click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/leadership$/);
  const now = page.getByTestId('office');
  await expect(now).toHaveCount(2);
  await expect(now.first()).toContainText('Шевчук');
  await expect(now.first()).toContainText('з 01.09.25');
  await expect(now.first().locator('ion-badge.lk-tag--danger')).toHaveText('Курінний');
  await expect(page.getByTestId('past-office')).toContainText('Коваль');
  await expect(page.getByTestId('past-office')).toContainText('01.09.23 – 31.08.25');
  // An ended office is grey whatever it was, as on the web.
  await expect(page.getByTestId('past-office').locator('ion-badge.lk-tag--secondary')).toHaveText('Курінний');
  await expect(page.getByTestId('setup-leadership')).toHaveCount(0);
  await shot(page, project, 'leadership-page');

  // A гурток's провід has the same page, from its own.
  await page.goto('./tabs/kurin/group/g1');
  await page.getByTestId('group-leadership-more').click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/group\/g1\/leadership$/);
  await expect(page.locator('app-leadership ion-title')).toContainText('Провід гуртка');
  await expect(page.getByTestId('past-office')).toContainText('Бойко');
});

test('lets the Звʼязковий add a впорядник, change their гуртки and hand his office over', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, member, kvApi(), ZVYAZKOVYI);
  await openKurinTab(page);
  await expect(page.getByTestId('kv-more')).toContainText('Впорядники й архів');
  await page.getByTestId('kv-more').click();
  await expect(page.getByTestId('kv-row')).toHaveCount(2);
  await expect(page.getByTestId('edit-mentor')).toHaveCount(2);
  await shot(page, project, 'kv-manage');

  // «Додати до КВ»: somebody from the kurin, and their гуртки.
  await page.getByTestId('add-mentor').click();
  const sheet = mentorSheet(page);
  await expect(sheet.getByTestId('mentor')).toContainText('Оберіть учасника');
  await expect(sheet.getByTestId('save-mentor')).toHaveAttribute('disabled', '');
  await sheet.getByTestId('mentor').click();
  await picker(page).getByTestId('pick-person').filter({ hasText: 'Гнатюк' }).click();
  await expect(sheet.getByTestId('mentor')).toContainText('Гнатюк');
  await sheet.getByTestId('mentor-group').filter({ hasText: 'Орли' }).click();
  await expect(sheet.getByTestId('mentor-group').filter({ hasText: 'Орли' })).toHaveAttribute('aria-pressed', 'true');
  await shot(page, project, 'kv-mentor');
  await sheet.getByTestId('save-mentor').click();
  await expect.poll(() => writes(log, 'POST', /^group\/g\d\/mentors\//).map((w) => w.path)).toEqual(['group/g2/mentors/u-m6']);
  await expect(sheet).toHaveCount(0);
  await expect(page.getByTestId('kv-row').filter({ hasText: 'Гнатюк' }).locator('ion-badge')).toHaveText(['Впорядник', 'Орли']);

  // Their гуртки from the row: Соколи on, Орли off.
  await page.getByTestId('kv-row').filter({ hasText: 'Гнатюк' }).getByTestId('edit-mentor').click();
  await expect(sheet.getByTestId('mentor-group').filter({ hasText: 'Орли' })).toHaveAttribute('aria-pressed', 'true');
  await sheet.getByTestId('mentor-group').filter({ hasText: 'Орли' }).click();
  await sheet.getByTestId('mentor-group').filter({ hasText: 'Соколи' }).click();
  await sheet.getByTestId('save-mentor').click();
  await expect.poll(() => log.writes.filter((w) => w.path.startsWith('group/') && w.path.endsWith('/u-m6')).map((w) => `${w.method} ${w.path}`).sort((a, b) => a.localeCompare(b))).toEqual([
    'DELETE group/g2/mentors/u-m6',
    'POST group/g1/mentors/u-m6',
    'POST group/g2/mentors/u-m6',
  ]);
  await expect(page.getByTestId('kv-row').filter({ hasText: 'Гнатюк' }).locator('ion-badge')).toHaveText(['Впорядник', 'Соколи']);
  await expect(page.getByTestId('kv-archived').filter({ hasText: 'Гнатюк' })).toContainText('Орли');
  await expect(page.locator('ion-toast')).toHaveCount(0, { timeout: 8_000 });

  // «Передати роль Звʼязкового»: anyone but the one holding it, then a confirmation.
  await page.getByTestId('hand-over').click();
  const pick = picker(page);
  await expect(pick.locator('ion-title')).toContainText('Новий Звʼязковий');
  await expect(pick.getByTestId('pick-person').filter({ hasText: 'Петренко' })).toHaveCount(0);
  await pick.getByTestId('pick-person').filter({ hasText: 'Мельник' }).click();
  const confirm = page.locator('ion-action-sheet').last();
  await expect(confirm).toContainText(/Андрій.* Мельник.* стане Звʼязковим куреня\./);
  await shot(page, project, 'kv-hand-over');
  await confirm.getByRole('button', { name: 'Передати', exact: true }).click();
  await expect.poll(() => writes(log, 'PUT', 'leadership/kv1')[0]?.body?.leadershipHistories?.map((h: any) => `${h.role}:${h.member.memberKey}`)).toEqual([
    'Vykhovnyk:m5',
    'Zvyazkovyi:m5',
  ]);
  expect(writes(log, 'PUT', 'leadership/kv1')[0].body).toMatchObject({ leadershipKey: 'kv1', type: 'KV', entityKey: 'k1' });
  await expect(page.locator('ion-toast')).toContainText(/Роль Звʼязкового передано: Андрій.* Мельник/);
  // His rights went with the office: nothing left to manage here.
  await expect(page.getByTestId('kv-row').first()).toContainText('Мельник');
  await Promise.all(['add-mentor', 'edit-mentor', 'hand-over'].map((part) => expect(page.getByTestId(part)).toHaveCount(0)));
});

test('lets the курінний seat the kurin’s провід, all but himself', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, member, kvApi(), KURINNYI);
  await openKurinTab(page);
  await expect(page.getByTestId('leadership-more')).toContainText('Архів проводу');
  await page.getByTestId('setup-leadership').scrollIntoViewIfNeeded();
  await shot(page, project, 'kurin-kv-leadership');
  await page.getByTestId('setup-leadership').click();
  const sheet = form(page);
  await expect(sheet.locator('ion-title')).toContainText('Провід куреня');
  // His own office is the Звʼязковий's to fill.
  await expect(sheet.getByTestId('seat-Kurinnuy')).toContainText('Курінного призначає Звʼязковий.');
  await expect(sheet.getByTestId('seat-Kurinnuy')).toContainText('Шевчук');
  await expect(sheet.getByTestId('seat-Pysar')).toContainText('Бойко');
  await expect(sheet.getByTestId('save-leadership')).toHaveAttribute('disabled', '');
  await sheet.getByTestId('seat-Skarbnyk').click();
  await expect(picker(page).locator('ion-title')).toContainText('Скарбник');
  await picker(page).getByTestId('pick-person').filter({ hasText: 'Коваль' }).click();
  await expect(sheet.getByTestId('seat-Skarbnyk')).toContainText('Коваль');
  await sheet.getByTestId('seat-Pysar').click();
  await picker(page).getByTestId('pick-none').click();
  await expect(sheet.getByTestId('seat-Pysar')).toContainText('Не обсаджено');
  await sheet.getByTestId('show-archive').click();
  await expect(sheet.getByTestId('archived-office')).toContainText('Коваль');
  await expect(sheet.getByTestId('archived-office')).toContainText('Курінний · 01.09.23 – 31.08.25');
  await shot(page, project, 'leadership-form');
  await sheet.getByTestId('save-leadership').click();
  await expect.poll(() => writes(log, 'PUT', 'leadership/l1')[0]?.body?.leadershipHistories?.map((h: any) => `${h.role}:${h.member.memberKey}`)).toEqual([
    'Kurinnuy:m2',
    'Skarbnyk:m4',
  ]);
  expect(writes(log, 'PUT', 'leadership/l1')[0].body).toMatchObject({ leadershipKey: 'l1', type: 'Kurin', entityKey: 'k1', endDate: null });
  await expect(sheet).toHaveCount(0);
  await expect(page.getByTestId('leadership')).toContainText('Скарбник');
  await expect(page.getByTestId('leadership')).not.toContainText('Писар');
});

test('lets the Звʼязковий seat a first провід куреня from its page', async ({ page }) => {
  const log = await signIn(page, member, kvApi(false), ZVYAZKOVYI);
  await page.goto('./tabs/kurin/leadership');
  await expect(page.getByTestId('current-offices')).toContainText('Проводу ще немає.');
  await expect(page.getByTestId('past-offices')).toContainText('Архів порожній.');
  await page.getByTestId('setup-leadership').click();
  const sheet = form(page);
  await expect(sheet.getByTestId('seat-Kurinnuy')).not.toContainText('Курінного призначає');
  await sheet.getByTestId('seat-Kurinnuy').click();
  await picker(page).getByTestId('pick-person').filter({ hasText: 'Шевчук' }).click();
  await sheet.getByTestId('save-leadership').click();
  await expect.poll(() => writes(log, 'POST', 'leadership')[0]?.body).toMatchObject({
    type: 'Kurin', entityKey: 'k1', leadershipHistories: [{ role: 'Kurinnuy', member: { memberKey: 'm2' } }],
  });
  await expect(page.getByTestId('office')).toContainText('Шевчук');
});

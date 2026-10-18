import { expect, test, type Page } from '@playwright/test';
import { day, member, ok, refuse, shot, signIn, type Answer, type ExtraApi } from './support/mock-api';

// ── Fixtures, shaped like the API's DTOs (kurinModule/models on the web) ─────────────────────────

const pad = (n: number) => String(n).padStart(2, '0');
/** A birth date in 2012 that comes round in `days` days. */
const bornIn = (days: number) => {
  const date = new Date(Date.now() + days * day);
  return `2012-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
};
const future = new Date(Date.now() + 20 * day).toISOString();

const kurin = {
  kurinKey: 'k1', number: 7, branch: 'UPYu', stanytsia: 'Львів', regionOrCountry: 'Львівщина',
  namedAfter: 'Івана Франка', description: 'Курінь юнаків і юначок при станиці Львів.', profileVerificationEnabled: true,
};
const groups = [
  { groupKey: 'g1', kurinKey: 'k1', name: 'Соколи', description: 'Гурток, що любить мандрівки.', silhouetteUrl: null, kurinNumber: 7 },
  { groupKey: 'g2', kurinKey: 'k1', name: 'Орли', description: null, silhouetteUrl: null, kurinNumber: 7 },
];

const person = (memberKey: string, firstName: string, lastName: string, extra: Record<string, unknown> = {}) => ({
  memberKey, userKey: `u-${memberKey}`, userRole: 'Kurin.Member', firstName, lastName, middleName: null,
  profilePhotoUrl: null, latestPlastLevel: 'Uchasnyk', phoneNumber: null, dateOfBirth: null,
  profileVerificationStatus: 'Unverified', leadershipHistories: [], mentoredGroupNames: [], warnings: [], ...extra,
});
const office = (key: string, role: string, who: { memberKey: string; firstName: string; lastName: string }, type = 'Kurin', groupName: string | null = null) => ({
  leadershipHistoryKey: key, leadershipKey: 'l1', role, leadershipType: type, groupName, startDate: '2024-09-01', endDate: null,
  member: { memberKey: who.memberKey, firstName: who.firstName, lastName: who.lastName, middleName: null },
});

const ostap = person('m1', 'Остап', 'Коваль', { dateOfBirth: bornIn(3), profileVerificationStatus: 'VerifiedCurrent' });
const marta = person('m2', 'Марта', 'Шевчук', { dateOfBirth: bornIn(0), latestPlastLevel: 'Rozviduvach' });
const olena = person('m3', 'Олена', 'Петренко', { userRole: 'KV.Zvyazkovyi', latestPlastLevel: 'Starshoplastun' });
const ivan = person('m4', 'Іван', 'Бойко', {
  phoneNumber: '+380501234567',
  warnings: [{ memberWarningKey: 'w1', level: 'Level2', issuedAtUtc: '2026-09-01T10:00:00Z', expiresAtUtc: future }],
});
const andrii = person('m5', 'Андрій', 'Мельник', { userRole: 'KV.Vykhovnyk', latestPlastLevel: 'Senior', mentoredGroupNames: ['Соколи'] });
marta.leadershipHistories = [office('h-g1', 'Hurtkoviy', marta, 'Group', 'Соколи')] as never[];

const card = (base: ReturnType<typeof person>, extra: Record<string, unknown> = {}) => ({
  ...base, email: `${base.memberKey}@example.com`, middleName: 'Петрович', kurinKey: 'k1', groupKey: 'g1',
  school: 'Львівська гімназія №1', address: 'Львів, вул. Франка, 1', profileVerifiedAtUtc: '2026-09-15T09:00:00Z',
  plastLevelHistories: [{ plastLevelHistoryKey: 'pl1', memberKey: base.memberKey, plastLevel: 'Uchasnyk', dateAchieved: '2022-04-23' }],
  awards: [], ...extra,
});
const award = (key: string, memberKey: string, level: string, status: string, dateAcquired = '2025-06-01') =>
  ({ memberAwardKey: key, memberKey, kurinKey: 'k1', level, dateAcquired, note: null, status });

const catalog = [
  { id: 'b-kukhar', title: 'Кухар', imagePath: 'kukhar.png', country: 'Україна', specialization: 'Табірництво' },
  { id: 'b-plav', title: 'Плавець', imagePath: 'plav.png', country: 'Україна', specialization: 'Спорт' },
  { id: 'b-vuzl', title: 'Вузлів майстер', imagePath: 'vuzl.png', country: 'Україна', specialization: 'Табірництво' },
];
const progress = (memberKey: string) => [
  { badgeProgressKey: 'bp1', memberKey, badgeId: 'b-kukhar', status: 'Submitted', submittedAtUtc: '2026-10-01T10:00:00Z', reviewedAtUtc: null },
  { badgeProgressKey: 'bp2', memberKey, badgeId: 'b-plav', status: 'Confirmed', submittedAtUtc: '2026-05-01T10:00:00Z', reviewedAtUtc: '2026-05-10T10:00:00Z' },
];

const groupedProbe = {
  id: 'probe-1', title: 'Перша проба', pointsCount: 3, sectionsCount: 2,
  sections: [
    { id: 's1', code: '1', title: 'Пластовий закон', points: [{ id: 'p1', title: 'Знати Пластовий закон' }, { id: 'p2', title: 'Знати Пластову присягу' }] },
    { id: 's2', code: '2', title: 'Табірництво', points: [{ id: 'p3', title: 'Розпалити ватру' }] },
  ],
};

/** What the probe endpoints remember between calls: which points are signed, and whether it is closed. */
interface ProbeState {
  signed: Set<string>;
  status: string;
}

function probeProgress(memberKey: string, state: ProbeState) {
  return {
    probeProgressKey: 'pp1', memberKey, kurinKey: 'k1', probeId: 'probe-1', status: state.status,
    completedAtUtc: state.status === 'Completed' ? '2026-10-10T10:00:00Z' : null, verifiedAtUtc: null,
    auditTrail: [],
    pointSignatures: ['p1', 'p2', 'p3'].map((pointId) => ({
      probePointProgressKey: null, pointId, isSigned: state.signed.has(pointId),
      signedAtUtc: state.signed.has(pointId) ? '2026-10-05T10:00:00Z' : null,
      signedByUserKey: 'u-m3', signedByName: state.signed.has(pointId) ? 'Олена Петренко' : null, signedByRole: 'Звʼязковий',
    })),
  };
}

/** The kurin's endpoints. `canUpdate` is the server's check-access answer per member. */
function kurinApi(options: { canUpdate: (memberKey: string) => boolean; duesGroups?: string[] }): ExtraApi {
  const probe: ProbeState = { signed: new Set(['p1']), status: 'InProgress' };
  const cards: Record<string, unknown> = {
    m1: card(ostap, { email: member.email, awards: [award('a1', 'm1', 'First', 'Confirmed')] }),
    m2: card(marta, { middleName: null, awards: [award('a2', 'm2', 'Second', 'Submitted')] }),
  };
  return ({ method, path, body }): Answer | null => {
    const memberMatch = /^member\/(m\d)(\/.*)?$/.exec(path);
    if (method === 'GET') {
      switch (path) {
        case 'kurin/k1': return ok(kurin);
        case 'group/groups': return ok(groups);
        case 'group/g1': return ok(groups[0]);
        case 'member/kurins/k1/members': return ok([ostap, marta, olena, ivan, andrii]);
        case 'member/groups/g1/members': return ok([ostap, marta, ivan]);
        case 'member/members/kv/k1': return ok([olena, andrii]);
        case 'group/groups/k1/mentor-assignments':
          return ok([{ mentorAssignmentKey: 'ma1', mentorUserKey: 'u-m5', groupKey: 'g1', groupName: 'Соколи', assignedAtUtc: '2025-09-01T00:00:00Z', revokedAtUtc: null, member: andrii }]);
        case 'leadership/type/kurin/k1':
          return ok({ leadershipKey: 'l1', startDate: '2024-09-01', endDate: null, leadershipHistories: [office('h1', 'Kurinnuy', marta), office('h2', 'Pysar', ivan)] });
        case 'leadership/type/group/g1':
          return ok({ leadershipKey: 'l2', startDate: '2024-09-01', endDate: null, leadershipHistories: [office('h-g1', 'Hurtkoviy', marta, 'Group', 'Соколи')] });
        case 'kurin/k1/dues/groups':
          return ok((options.duesGroups ?? []).map((groupKey) => ({ groupKey, groupName: 'Соколи' })));
        case 'catalog/badges': return ok(catalog);
        case 'kurin/k1/catalog/badges': return ok(catalog);
        case 'catalog/probes':
          return ok([{ id: 'probe-1', title: 'Перша проба', pointsCount: 3, sectionsCount: 2 }, { id: 'probe-2', title: 'Друга проба', pointsCount: 4, sectionsCount: 2 }]);
        case 'catalog/probes/probe-1/grouped': return ok(groupedProbe);
      }
      if (memberMatch) {
        const [, key, rest] = memberMatch;
        switch (rest ?? '') {
          case '': return cards[key] ? ok(cards[key]) : refuse(404);
          case '/dossier/memberships':
            return ok([
              { membershipKey: 'ms1', kurinKey: 'k1', kurinNumber: 7, branch: 'UPYu', kurinNamedAfter: 'Івана Франка', groupKey: 'g1', groupName: 'Соколи', kind: 'Youth', joinedAtUtc: '2020-09-01T00:00:00Z', leftAtUtc: null, isCurrent: true },
              { membershipKey: 'ms0', kurinKey: 'k9', kurinNumber: 12, branch: 'UPYu', kurinNamedAfter: null, groupKey: null, groupName: null, kind: 'Youth', joinedAtUtc: '2018-09-01T00:00:00Z', leftAtUtc: '2019-06-01T00:00:00Z', isCurrent: false },
            ]);
          // A youth reads only their own вкладка.
          case '/dues':
            return key !== 'm1' ? refuse(403) : ok({
              hasAccount: true, kurinKey: 'k1', currentQuarter: { year: 2026, number: 4 }, balance: -150,
              quarterRate: { stanytsia: 100, krai: 0, kurin: 30, group: 20, total: 150 }, isConcessionNow: false,
              currentGroupKey: 'g1', currentGroupName: 'Соколи', canOpenGroupDues: false, accounts: [],
              entries: [{ duesEntryKey: 'de1', kind: 'Contribution', amount: 150, occurredOn: '2026-07-02' }],
            });
          case '/badges/progress': return ok(progress(key));
          case '/probes/probe-1/progress': return ok(probeProgress(key, probe));
          case '/probes/probe-2/progress':
            return ok({ probeProgressKey: null, memberKey: key, probeId: 'probe-2', status: 'NotStarted', completedAtUtc: null, verifiedAtUtc: null, pointSignatures: [] });
        }
      }
      return null;
    }
    if (path === 'auth/check-access') return ok(body?.entityType === 'member' && options.canUpdate(body.entityKey));
    const sign = /^member\/m\d\/probes\/probe-1\/points\/(p\d)\/(sign|unsign)$/.exec(path);
    if (sign) {
      if (sign[2] === 'sign') probe.signed.add(sign[1]);
      else probe.signed.delete(sign[1]);
      return ok(probeProgress(memberMatch?.[1] ?? 'm1', probe));
    }
    if (/^member\/m\d\/probes\/probe-1\/progress\/status$/.test(path)) {
      probe.status = 'Completed';
      return ok(probeProgress(memberMatch?.[1] ?? 'm1', probe));
    }
    if (memberMatch && method === 'PUT' && !memberMatch[2]) return ok(cards[memberMatch[1]]);
    return null;
  };
}

const openKurinTab = async (page: Page) => {
  await page.locator('ion-tab-button[tab=kurin]').click();
  await expect(page.getByTestId('kurin-head')).toContainText('7 курінь');
};

// ── Youth ────────────────────────────────────────────────────────────────────────────────────────

test('shows the kurin with its groups, КВ, провід and people', async ({ page }, info) => {
  await signIn(page, member, kurinApi({ canUpdate: (key) => key === 'm1' }));
  await openKurinTab(page);
  const head = page.getByTestId('kurin-head');
  await expect(head).toContainText('ім. Івана Франка');
  await expect(head).toContainText('УПЮ');
  await expect(head).toContainText('Львівщина');
  // The web's red plaque with the kurin's number, in its small size.
  await expect(head.getByTestId('kurin-plaque')).toHaveCSS('background-color', 'rgb(179, 0, 3)');
  await expect(page.getByTestId('groups')).toContainText('Соколи');
  await expect(page.getByTestId('groups')).toContainText('Орли');
  // The Звʼязковий first, then the впорядники with their гуртки.
  await expect(page.getByTestId('kv').locator('ion-item').first()).toContainText('Петренко Олена');
  // The status and each гурток as tags, coloured as the web's КВ table colours them.
  await expect(page.getByTestId('kv').locator('ion-badge.lk-tag--success', { hasText: 'Впорядник' })).toBeVisible();
  await expect(page.getByTestId('kv').locator('ion-badge.lk-tag--info', { hasText: 'Соколи' })).toBeVisible();
  await expect(page.getByTestId('leadership').locator('ion-badge.lk-tag--danger', { hasText: 'Курінний' })).toBeVisible();
  // A youth manages and reviews nothing.
  await expect(page.getByTestId('new-group')).toHaveCount(0);
  await expect(page.getByText('Вмілості на перевірку')).toHaveCount(0);
  await shot(page, info.project.name, 'kurin-overview');

  await page.locator('ion-segment-button[value=members]').click();
  const members = page.getByTestId('member');
  await expect(members).toHaveCount(5);
  await expect(page.getByTestId('members')).toContainText('5 учасників · за прізвищем');
  await expect(members.first()).toContainText('Бойко Іван');
  await expect(members.filter({ hasText: 'Шевчук Марта' }).locator('ion-badge.lk-tag--danger')).toHaveText('Гуртковий: Соколи');
  await shot(page, info.project.name, 'kurin-members');

  // By office: the Звʼязковий leads.
  await page.getByRole('button', { name: 'Сортування' }).click();
  await page.getByRole('button', { name: 'За роллю' }).click();
  await expect(members.first()).toContainText('Петренко Олена');

  await page.locator('[data-testid=member-search] input').fill('бойко');
  await expect(members).toHaveCount(1);
  await members.first().click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/member\/m4$/);
});

test('opens a гурток with its провід, birthdays and people', async ({ page }, info) => {
  await signIn(page, member, kurinApi({ canUpdate: (key) => key === 'm1' }));
  await openKurinTab(page);
  await page.getByTestId('groups').getByText('Соколи').click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/group\/g1$/);
  await expect(page.getByTestId('group-head')).toContainText('Гурток, що любить мандрівки.');
  await expect(page.getByTestId('group-leadership')).toContainText('Гуртковий');
  const birthdays = page.getByTestId('birthdays');
  await expect(birthdays.locator('ion-item').first()).toContainText('Марта Шевчук');
  await expect(birthdays).toContainText('сьогодні');
  await expect(birthdays).toContainText('через 3 дн.');
  await expect(page.getByTestId('group-member')).toHaveCount(3);
  await expect(page.getByTestId('group-member').filter({ hasText: 'Іван Бойко' }).getByRole('img')).toHaveAttribute('aria-label', /Друга/);
  await expect(page.getByText('Точкування гуртка')).toBeVisible();
  // The box is the keeper's.
  await expect(page.getByTestId('group-dues')).toHaveCount(0);
  await shot(page, info.project.name, 'kurin-group');
});

test('shows one’s own card and edits it, adds a skill and an award', async ({ page }, info) => {
  const log = await signIn(page, member, kurinApi({ canUpdate: (key) => key === 'm1' }));
  await page.goto('./tabs/kurin/member/m1');
  const head = page.getByTestId('member-head');
  await expect(head).toContainText('Остап Коваль');
  await expect(head).toContainText('пл. уч.');
  await expect(head.getByLabel('Дані верифіковано')).toBeVisible();
  await expect(page.getByTestId('contacts')).toContainText('Дата народження');
  // The card shows school and address to whoever may see them.
  await expect(page.getByTestId('contacts')).toContainText('Львівська гімназія №1');
  await expect(page.getByTestId('contacts')).toContainText('Львів, вул. Франка, 1');
  await expect(page.getByTestId('skills')).toContainText('Плавець');
  await expect(page.getByTestId('skills')).toContainText('очікує');
  await expect(page.getByTestId('probe').first()).toContainText('Підписано 1 з 3');
  await expect(page.getByTestId('probes')).toContainText('Відкриється після закриття першої проби');
  await expect(page.getByTestId('awards')).toContainText('Перше відзначення');
  await expect(page.getByTestId('dues')).toContainText('−150 ₴');
  await expect(page.getByTestId('memberships')).toContainText('к. ч. 7 · ім. Івана Франка');
  await expect(page.getByText('к. ч. 12')).toBeVisible();
  await shot(page, info.project.name, 'kurin-member-own');
  await page.evaluate(() => document.querySelector<HTMLIonContentElement>('app-member ion-content')?.scrollToBottom(0));
  await shot(page, info.project.name, 'kurin-member-own-scrolled');
  await page.evaluate(() => document.querySelector<HTMLIonContentElement>('app-member ion-content')?.scrollToTop(0));

  // Own fields.
  await page.getByTestId('edit-profile').click();
  const phone = page.locator('[data-testid=profile-phoneNumber] input');
  await expect(page.locator('[data-testid=profile-firstName] input')).toHaveValue('Остап');
  await phone.fill('+380 67 111 11 11');
  await shot(page, info.project.name, 'kurin-member-edit');
  await page.getByTestId('save-profile').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'PUT' && w.path === 'member/m1')).toBeTruthy();
  const sent = String(log.writes.find((w) => w.path === 'member/m1')?.body);
  expect(sent).toContain('+380671111111');
  expect(sent).toContain(member.email);
  // The ступені go back as they were, or the server would drop them.
  expect(sent).toContain('plastLevelHistories[0].plastLevelHistoryKey');

  // A skill from the catalogue.
  await page.getByTestId('all-skills').click();
  await expect(page.getByTestId('pending-skills')).toContainText('Кухар');
  // Only the провід confirms.
  await expect(page.getByRole('button', { name: 'Підтвердити' })).toHaveCount(0);
  await shot(page, info.project.name, 'kurin-skills');
  await page.getByTestId('add-skill').click();
  await page.locator('[data-testid=skill-search] input').fill('вуз');
  await expect(page.getByTestId('catalog-skill')).toHaveCount(1);
  await shot(page, info.project.name, 'kurin-skill-add');
  await page.getByTestId('catalog-skill').getByRole('button', { name: 'Додати' }).click();
  await expect.poll(() => log.writes.some((w) => w.path === 'member/m1/badges/b-vuzl/submit')).toBe(true);
  await page.getByRole('button', { name: 'Готово' }).click();
  await expect(page.locator('ion-modal')).toHaveCount(0);

  // An award.
  await page.getByTestId('add-award').click();
  await page.getByTestId('award-level').click();
  await page.locator('ion-action-sheet button', { hasText: 'Третя' }).click();
  await page.locator('[data-testid=award-date] input').fill('2026-09-01');
  await page.getByTestId('save-award').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'member/m1/awards')?.body).toMatchObject({
    level: 'Third',
    dateAcquired: '2026-09-01',
  });
});

test('shows somebody else’s card read-only to a youth', async ({ page }) => {
  await signIn(page, member, kurinApi({ canUpdate: (key) => key === 'm1' }));
  await page.goto('./tabs/kurin/member/m2');
  await expect(page.getByTestId('member-head')).toContainText('Марта Шевчук');
  await expect(page.getByTestId('awards')).toContainText('Друге відзначення');
  await expect(page.getByTestId('edit-profile')).toHaveCount(0);
  await expect(page.getByTestId('add-award')).toHaveCount(0);
  // Not ours to see.
  await expect(page.getByTestId('dues')).toHaveCount(0);
});

test('opens the own card from «Мій профіль» in Меню', async ({ page }, info) => {
  await signIn(page, member, kurinApi({ canUpdate: (key) => key === 'm1' }));
  await page.locator('#tab-button-more').click();
  await page.getByTestId('account').click();
  await expect(page.locator('app-profile').getByTestId('member-head')).toContainText('Остап Коваль');
  await expect(page.locator('app-profile').getByTestId('edit-profile')).toBeVisible();
  await expect(page).toHaveURL(/\/tabs\/more\/profile$/);
  await shot(page, info.project.name, 'kurin-profile');
});

// ── Провід ───────────────────────────────────────────────────────────────────────────────────────

const reviewer = ['Group:Update:OwnGroups', 'GroupDues:Read:OwnGroups', 'BadgeProgress:Update:OwnGroups'];

test('lets a reviewer confirm skills and awards', async ({ page }, info) => {
  const log = await signIn(page, member, kurinApi({ canUpdate: () => true, duesGroups: ['g1'] }), reviewer);
  await openKurinTab(page);
  await expect(page.getByText('Вмілості на перевірку')).toBeVisible();
  await page.getByTestId('groups').getByText('Соколи').click();
  await expect(page.getByTestId('group-dues')).toBeVisible();

  await page.goto('./tabs/kurin/member/m2');
  await expect(page.getByTestId('member-head')).toContainText('Марта Шевчук');
  // Провід changes somebody else's card too (the full edit is in member-edit.pw.ts).
  await page.getByTestId('edit-profile').click();
  await expect(page.locator('ion-modal ion-title')).toContainText('Профіль учасника');
  await page.locator('[data-testid=profile-middleName] input').fill('Іванівна');
  await page.locator('[data-testid=profile-phoneNumber] input').fill('+380 50 222 22 22');
  await page.locator('[data-testid=profile-dateOfBirth] input').fill('2012-03-04');
  await page.getByTestId('save-profile').click();
  await expect.poll(() => String(log.writes.find((w) => w.method === 'PUT' && w.path === 'member/m2')?.body ?? '')).toContain('+380502222222');
  await expect(page.locator('ion-modal.show-modal')).toHaveCount(0);

  await page.getByTestId('all-skills').click();
  await page.getByTestId('pending-skills').getByRole('button', { name: 'Підтвердити' }).click();
  await expect.poll(() => log.writes.find((w) => w.path === 'member/m2/badges/b-kukhar/review')?.body).toEqual({ isApproved: true, note: null });
  await page.getByTestId('confirmed-skills').getByRole('button', { name: 'Зняти' }).click();
  await page.locator('ion-action-sheet').getByRole('button', { name: 'Видалити', exact: true }).click();
  await expect.poll(() => log.writes.find((w) => w.path === 'member/m2/badges/b-plav/review')?.body).toEqual({ isApproved: false, note: null });
  await page.getByRole('button', { name: 'Готово' }).click();

  await page.getByTestId('award').filter({ hasText: 'Друге відзначення' }).click();
  await expect(page.getByText('Очікує підтвердження')).toBeVisible();
  await shot(page, info.project.name, 'kurin-award');
  await page.getByTestId('approve-award').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'member/m2/awards/a2/review')?.body).toEqual({ isApproved: true, note: null });
});

test('lets a reviewer sign the probe and close it', async ({ page }, info) => {
  const log = await signIn(page, member, kurinApi({ canUpdate: () => true }), reviewer);
  await page.goto('./tabs/kurin/member/m2');
  await page.getByTestId('probe').first().click();
  await expect(page).toHaveURL(/\/member\/m2\/probe\?id=probe-1$/);
  await expect(page.getByTestId('probe-summary')).toContainText('Підписано 1 з 3');
  const points = page.getByTestId('probe-point');
  await expect(points.filter({ hasText: 'Знати Пластовий закон' })).toContainText('Олена Петренко (Звʼязковий)');
  await shot(page, info.project.name, 'kurin-probe');

  await points.filter({ hasText: 'Знати Пластову присягу' }).getByRole('button', { name: 'Підписати' }).click();
  await expect.poll(() => log.writes.some((w) => w.path === 'member/m2/probes/probe-1/points/p2/sign')).toBe(true);
  await expect(page.getByTestId('probe-summary')).toContainText('Підписано 2 з 3');

  // Unsigning asks first.
  await points.filter({ hasText: 'Знати Пластовий закон' }).getByRole('button', { name: 'Скасувати' }).click();
  await page.locator('ion-action-sheet').getByRole('button', { name: 'Так, скасувати' }).click();
  await expect.poll(() => log.writes.some((w) => w.path === 'member/m2/probes/probe-1/points/p1/unsign')).toBe(true);
  await expect(page.getByTestId('probe-summary')).toContainText('Підписано 1 з 3');

  await points.filter({ hasText: 'Знати Пластовий закон' }).getByRole('button', { name: 'Підписати' }).click();
  await page.getByTestId('probe-section').filter({ hasText: 'Табірництво' }).locator('ion-item[slot=header]').click();
  await points.filter({ hasText: 'Розпалити ватру' }).getByRole('button', { name: 'Підписати' }).click();
  await expect(page.getByTestId('close-probe')).toBeVisible();
  await shot(page, info.project.name, 'kurin-probe-all-signed');
  await page.getByTestId('close-probe').getByRole('button', { name: 'Здати і закрити пробу' }).click();
  await page.locator('ion-action-sheet').getByRole('button', { name: 'Здати і закрити' }).click();
  await expect.poll(() => log.writes.find((w) => w.path === 'member/m2/probes/probe-1/progress/status')?.body).toEqual({ status: 'Completed', note: null });
  await expect(page.getByTestId('close-probe')).toHaveCount(0);
  await expect(page.getByTestId('probe-summary')).toContainText('Пробу закрито');
});

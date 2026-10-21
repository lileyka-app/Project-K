import { expect, test, type Page } from '@playwright/test';
import { kurin, ok, refuse, shot, signIn, soon, type ExtraApi } from './support/mock-api';

/**
 * The home screen's bell and «Справи», and the leader tools they lead to: the inbox, the skills
 * review queue and a гурток's box. Fixtures are shaped like the API's DTOs.
 */

// ── Fixtures ─────────────────────────────────────────────────────────────────────────────────

const ago = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();

function notifications() {
  const base = { severity: 'Info', readAtUtc: null, isRead: false };
  const list = [
    { ...base, notificationKey: 'n1', type: 'MemberSkillSubmittedForReview', title: 'Вмілість на перевірку',
      body: 'Остап Коваль подав «Кухар»', entityType: 'BadgeProgress', entityKey: 'm3', route: '/kurin/k1/review/skills', createdAtUtc: ago(5) },
    { ...base, notificationKey: 'n2', type: 'MemberProfileVerified', severity: 'Success', title: 'Профіль перевірено',
      body: 'Звʼязковий перевірив твій профіль', entityType: 'Member', entityKey: 'm7', route: '/member/m7', createdAtUtc: ago(90) },
    { ...base, notificationKey: 'n3', type: 'AgendaItemAssigned', title: 'Задачу призначено вам', body: 'Принести намет',
      entityType: 'AgendaItem', entityKey: 't2', route: '/tasks/k1', createdAtUtc: ago(60 * 26), isRead: true, readAtUtc: ago(60) },
    { ...base, notificationKey: 'n4', type: 'LeadershipChanged', title: 'Змінено провід куреня',
      body: 'Курінь 7', entityType: 'Leadership', entityKey: 'l1', route: '/leadership/kurin/k1', createdAtUtc: ago(60 * 50) },
  ];
  // Enough older ones that the inbox reads a second page.
  for (let i = 0; i < 21; i++) {
    list.push({ ...base, notificationKey: `old${i}`, type: 'AgendaItemUpdated', title: 'Оновлено призначення',
      body: `Сходини №${i}`, entityType: 'AgendaItem', entityKey: `e${i}`, route: '/calendar/k1', createdAtUtc: ago(60 * 24 * (3 + i)),
      isRead: true, readAtUtc: ago(60) });
  }
  return list;
}

const duties = [
  { kind: 'BadgesToReview', kurin, count: 2, groupKey: null, groupName: null, agendaItemKey: null, occurrenceStartUtc: null, title: null },
  { kind: 'EntriesToVerify', kurin, count: 3, groupKey: 'g1', groupName: 'Соколи', agendaItemKey: null, occurrenceStartUtc: null, title: null },
  { kind: 'TransfersToConfirm', kurin, count: 1, groupKey: null, groupName: null, agendaItemKey: null, occurrenceStartUtc: null, title: null },
  { kind: 'EventWithoutAttendance', kurin, count: 1, groupKey: null, groupName: null, agendaItemKey: 'e5',
    occurrenceStartUtc: soon(-2, 17), title: 'Сходини куреня' },
];

const queue = [
  { badgeProgressKey: 'bp1', memberKey: 'm3', kurinKey: 'k1', badgeId: 'cook-1', status: 'Submitted', submittedAtUtc: ago(60 * 30),
    memberFirstName: 'Остап', memberLastName: 'Коваль' },
  { badgeProgressKey: 'bp2', memberKey: 'm4', kurinKey: 'k1', badgeId: 'knots-1', status: 'Submitted', submittedAtUtc: ago(60 * 5),
    memberFirstName: 'Марта', memberLastName: 'Бойко' },
];

const catalog = [
  { id: 'cook-1', title: 'Кухар', specialization: 'Табірництво', level: 1, seekerRequirements: '1. Приготувати обід на гурток.\n2. Знати правила гігієни.' },
  { id: 'knots-1', title: 'Вузлів', specialization: 'Мандрівництво', level: 1, seekerRequirements: 'Вʼязати 10 вузлів.' },
];

const q = (year: number, number: number) => ({ year, number });
const amount = (total: number) => ({ stanytsia: 0, krai: 0, kurin: 0, group: total, total });
const cell = (quarter: { year: number; number: number }, charged: number, paid: number, isConcession = false) => ({
  quarter, charged: amount(charged), paid: amount(paid), balance: paid - charged, isConcession,
});

const KEEPER = { canKeep: true, canVerify: true, canSetKurinRates: false };

function groupDues(viewer = KEEPER) {
  return {
    groupKey: 'g1', kurinKey: 'k1', groupName: 'Соколи', currentQuarter: q(2026, 4),
    years: [{ startYear: 2026, label: '2026/27', quarters: [q(2026, 3), q(2026, 4), q(2027, 1), q(2027, 2)] }],
    kurinRates: [{ fromQuarter: q(2026, 3), articles: [{ name: 'Станиця', recipient: 'Stanytsia', amount: 100, reducedAmount: 50 }, { name: 'Курінь', recipient: 'Kurin', amount: 30, reducedAmount: 30 }], stanytsiaFull: 100, stanytsiaReduced: 50, kurinShare: 30, kurinReduced: 30, kraiFull: 0, kraiReduced: 0 }],
    groupRates: [{ fromQuarter: q(2026, 3), groupShare: 20 }],
    accounts: [
      { membershipKey: 'ms1', memberKey: 'm3', fullName: 'Остап Коваль', standing: 'Current', isConcessionNow: false,
        quarters: [cell(q(2026, 3), 150, 150), cell(q(2026, 4), 150, 0)], charged: 300, payments: 150, balance: -150 },
      { membershipKey: 'ms2', memberKey: 'm4', fullName: 'Марта Бойко', standing: 'Current', isConcessionNow: true,
        quarters: [cell(q(2026, 3), 100, 100, true), cell(q(2026, 4), 100, 120, true)], charged: 200, payments: 220, balance: 20 },
      { membershipKey: 'ms3', memberKey: 'm5', fullName: 'Іван Ткач', standing: 'Moved', isConcessionNow: false,
        quarters: [cell(q(2026, 3), 150, 50)], charged: 150, payments: 50, balance: -100 },
    ],
    entries: [
      { duesEntryKey: 'd1', kind: 'Contribution', method: 'Cash', counterMethod: null, amount: 120, occurredOn: '2026-10-04',
        membershipKey: 'ms2', memberName: 'Марта Бойко', collectedByMemberKey: 'm1', collectedByName: 'Остап Коваль', note: null,
        isVerified: false, verifiedAtUtc: null, verifiedByName: null, createdAtUtc: ago(60 * 24 * 6) },
      { duesEntryKey: 'd2', kind: 'Expense', method: 'Card', counterMethod: null, amount: 40, occurredOn: '2026-10-02',
        membershipKey: null, memberName: null, collectedByMemberKey: null, collectedByName: null, note: 'Мотузка для табору',
        isVerified: false, verifiedAtUtc: null, verifiedByName: null, createdAtUtc: ago(60 * 24 * 8) },
      { duesEntryKey: 'd3', kind: 'Contribution', method: 'Cash', counterMethod: null, amount: 150, occurredOn: '2026-09-12',
        membershipKey: 'ms1', memberName: 'Остап Коваль', collectedByMemberKey: null, collectedByName: null, note: null,
        isVerified: true, verifiedAtUtc: ago(60 * 24 * 20), verifiedByName: 'Андрій Мельник', createdAtUtc: ago(60 * 24 * 28) },
    ],
    box: { cash: 230, card: -40, total: 190, toForward: 130, own: 60, inTransit: 0 },
    handover: { owedUp: 300, transferred: 100, received: 100, outstanding: 200 },
    people: [{ memberKey: 'm1', fullName: 'Остап Коваль' }, { memberKey: 'm9', fullName: 'Андрій Мельник' }],
    viewer,
  };
}

/** The leader endpoints; `inbox` keeps the read flags across calls. */
function leaderApi(options: { viewer?: Parameters<typeof groupDues>[0] } = {}): ExtraApi {
  const inbox = notifications();
  return ({ method, path, query }) => {
    if (method === 'GET' && path === 'me/duties') return ok(duties);
    if (method === 'GET' && path === 'notifications/unread-count') return ok(inbox.filter((n) => !n.isRead).length);
    if (method === 'GET' && path === 'notifications') return ok(inbox.slice(0, Number(query.get('take') ?? 50)));
    if (method === 'PUT' && path === 'notifications/read-all') {
      inbox.forEach((n) => Object.assign(n, { isRead: true, readAtUtc: ago(0) }));
      return ok(inbox.length);
    }
    const read = /^notifications\/([^/]+)\/read$/.exec(path);
    if (method === 'PUT' && read) {
      const item = inbox.find((n) => n.notificationKey === read[1]);
      if (!item) return refuse(404);
      Object.assign(item, { isRead: true, readAtUtc: ago(0) });
      return ok(item);
    }
    if (method === 'GET' && path === 'kurin/k1/badges/review') return ok(queue);
    if (method === 'GET' && path === 'catalog/badges') return ok(catalog);
    if (method === 'GET' && path === 'group/g1/dues') return ok(groupDues(options.viewer));
    return null;
  };
}

/** Picks an option of an ion-select's action sheet (radios there) and waits for the sheet to go. */
async function choose(page: Page, name: string): Promise<void> {
  await page.locator('ion-action-sheet').getByRole('radio', { name, exact: true }).click();
  await expect(page.locator('ion-action-sheet')).toHaveCount(0);
}

/** A button of the action sheet that is open now (a dismissed one can linger while it animates out). */
function sheetButton(page: Page, name: string) {
  return page.locator('ion-action-sheet:not(.overlay-hidden)').last().getByRole('button', { name, exact: true });
}

async function backHome(page: Page): Promise<void> {
  await page.locator('ion-tab-button[tab=home]').click();
  await expect(page).toHaveURL(/\/tabs\/home$/);
}

// ── Home ─────────────────────────────────────────────────────────────────────────────────────

test('shows the bell and the leader’s duties on Home, and opens what they point at', async ({ page }, info) => {
  await signIn(page, undefined, leaderApi());
  const bell = page.getByTestId('bell');
  await expect(page.getByRole('link', { name: 'Сповіщення, непрочитаних: 3' })).toBeVisible();
  await expect(bell.locator('ion-badge')).toHaveText('3');

  const card = page.getByTestId('duties');
  await expect(card).toContainText('Справи');
  await expect(card.getByTestId('duty')).toHaveCount(4);
  await expect(card).toContainText('Відмітити присутність: Сходини куреня');
  await expect(card).toContainText('Соколи');
  await shot(page, info.project.name, 'leader-home');

  await card.getByTestId('duty').filter({ hasText: 'Вмілості на перевірку' }).click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/review\/skills$/);
  await backHome(page);

  await card.getByTestId('duty').filter({ hasText: 'Операції перевірити' }).click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/group\/g1\/dues$/);
  await backHome(page);

  await card.getByTestId('duty').filter({ hasText: 'Відмітити присутність' }).click();
  await expect(page).toHaveURL(/\/tabs\/calendar\/attendance\/e5\?start=/);
  await backHome(page);

  // The kurin's own box opens on its transfers.
  await card.getByTestId('duty').filter({ hasText: 'Передачі від гуртків' }).click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/dues\?part=transfers$/);
  await backHome(page);

  // Rows of «Найближче» and «Мої задачі» open their details.
  await page.getByTestId('event').filter({ hasText: 'Мандрівка на Говерлу' }).getByText('Мандрівка на Говерлу').click();
  await expect(page).toHaveURL(/\/tabs\/calendar\/event\/e2$/);
  await backHome(page);
  await page.getByTestId('event').filter({ hasText: 'Сходини гуртка' }).getByText('Сходини гуртка').click();
  await expect(page).toHaveURL(/\/tabs\/calendar\/event\/e1\?start=/);
  await backHome(page);
  await page.getByTestId('task').filter({ hasText: 'Вивчити вузли' }).getByText('Вивчити вузли').click();
  await expect(page).toHaveURL(/\/tabs\/tasks\/task\/t1$/);
});

test('a youth sees no duties and no badge on the bell', async ({ page }) => {
  await signIn(page);
  await expect(page.getByRole('link', { name: 'Сповіщення', exact: true })).toBeVisible();
  await expect(page.getByTestId('bell').locator('ion-badge')).toHaveCount(0);
  await expect(page.getByTestId('duties')).toHaveCount(0);
});

// ── Notifications ────────────────────────────────────────────────────────────────────────────

test('reads the inbox, opens a notification and marks everything read', async ({ page }, info) => {
  const log = await signIn(page, undefined, leaderApi());
  await page.getByTestId('bell').click();
  await expect(page).toHaveURL(/\/tabs\/home\/notifications$/);
  const rows = page.getByTestId('notification');
  await expect(rows).toHaveCount(20);
  await expect(rows.first()).toContainText('Вмілість на перевірку');
  await expect(rows.first()).toContainText('нове');
  await shot(page, info.project.name, 'leader-notifications');

  // A full page means there may be more: scrolling down reads a longer list.
  await page.evaluate(() => document.querySelector<HTMLIonContentElement>('app-notifications ion-content')?.scrollToBottom(0));
  await expect(rows).toHaveCount(25);

  // An unknown route only marks it read.
  await rows.filter({ hasText: 'Змінено провід куреня' }).click();
  await expect.poll(() => log.writes.map((w) => w.path)).toContain('notifications/n4/read');
  await expect(page).toHaveURL(/\/tabs\/home\/notifications$/);
  await expect(rows.filter({ hasText: 'Змінено провід куреня' })).not.toContainText('нове');

  // A member route opens the member card.
  await rows.filter({ hasText: 'Профіль перевірено' }).click();
  await expect(page).toHaveURL(/\/tabs\/kurin\/member\/m7$/);
  expect(log.writes.map((w) => w.path)).toContain('notifications/n2/read');

  // Back on Home (the tab may come back on the inbox it left), the bell has caught up.
  await page.locator('ion-tab-button[tab=home]').click();
  await expect(page).toHaveURL(/\/tabs\/home(\/notifications)?$/);
  if (page.url().endsWith('/notifications')) await page.locator('app-notifications ion-back-button').click();
  await expect(page.getByTestId('bell').locator('ion-badge')).toHaveText('1');
  await page.getByTestId('bell').click();
  const markAll = page.getByRole('button', { name: 'Позначити всі як прочитані' });
  await markAll.click();
  await expect.poll(() => log.writes.map((w) => w.path)).toContain('notifications/read-all');
  await expect(page.getByText('нове', { exact: false })).toHaveCount(0);
  await expect(markAll).toHaveAttribute('disabled', '');

  // An agenda notification opens the item from its key.
  await rows.filter({ hasText: 'Принести намет' }).click();
  await expect(page).toHaveURL(/\/tabs\/tasks\/task\/t2$/);
});

test('the bell catches up while the app is open and when the person comes back to it', async ({ page }) => {
  let unread = 3;
  await page.clock.install();
  const endpoints = leaderApi();
  await signIn(page, undefined, (request) =>
    request.method === 'GET' && request.path === 'notifications/unread-count' ? ok(unread) : endpoints(request),
  );
  const badge = page.getByTestId('bell').locator('ion-badge');
  await expect(badge).toHaveText('3');

  // A minute on, the open app asks again.
  unread = 4;
  await page.clock.fastForward('01:01');
  await expect(badge).toHaveText('4');

  // Back from another app or tab: at once.
  unread = 6;
  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange')));
  await expect(badge).toHaveText('6');
});

// ── Skills review ────────────────────────────────────────────────────────────────────────────

test('reviews skills from the queue with a note', async ({ page }, info) => {
  const log = await signIn(page, undefined, leaderApi(), ['Group:Update:OwnGroups']);
  await page.goto('./tabs/kurin/review/skills');

  const items = page.getByTestId('review-item');
  await expect(items).toHaveCount(2);
  // Newest first, as on the web.
  await expect(items.first()).toContainText('Вузлів');
  await expect(page.getByText('2 заявки на розгляді')).toBeVisible();
  await shot(page, info.project.name, 'leader-skills-queue');

  await items.filter({ hasText: 'Кухар' }).click();
  const sheet = page.locator('ion-modal');
  await expect(sheet.getByText('Приготувати обід на гурток.', { exact: false })).toBeVisible();
  await expect(sheet.getByText('Остап Коваль')).toBeVisible();
  await shot(page, info.project.name, 'leader-skills-item');

  await sheet.getByRole('button', { name: 'Підтвердити' }).click();
  const alert = page.locator('ion-alert');
  await expect(alert).toContainText('Підтвердити вмілість «Кухар» для Остап Коваль?');
  await alert.locator('textarea').fill('Смачний борщ');
  await shot(page, info.project.name, 'leader-skills-confirm');
  await alert.getByRole('button', { name: 'Підтвердити' }).click();
  await expect(items).toHaveCount(1);
  expect(log.writes.find((w) => w.path === 'member/m3/badges/cook-1/review')?.body).toEqual({
    isApproved: true,
    note: 'Смачний борщ',
  });

  await items.first().click();
  await page.locator('ion-modal').getByRole('button', { name: 'Відхилити' }).click();
  await page.locator('ion-alert').getByRole('button', { name: 'Відхилити' }).click();
  await expect(page.getByText('Усе розглянуто', { exact: false })).toBeVisible();
  expect(log.writes.find((w) => w.path === 'member/m4/badges/knots-1/review')?.body).toEqual({
    isApproved: false,
    note: null,
  });
});

test('keeps the review queue from a youth, as the web does', async ({ page }) => {
  await signIn(page, undefined, leaderApi());
  await page.goto('./tabs/kurin/review/skills');
  await expect(page.getByText('Немає доступу до модерації вмілостей.')).toBeVisible();
  await expect(page.getByTestId('review-item')).toHaveCount(0);
});

// ── Group dues ───────────────────────────────────────────────────────────────────────────────

test('shows a гурток’s box, its youth by quarter and the history', async ({ page }, info) => {
  const project = info.project.name;
  await signIn(page, undefined, leaderApi());
  await page.goto('./tabs/kurin/group/g1/dues');

  const box = page.getByTestId('box');
  await expect(box).toContainText('190 ₴');
  await expect(box).toContainText('готівка 230 ₴ · картка −40 ₴');
  await expect(page.getByTestId('rates')).toContainText('Квартальна вкладка 150 ₴ = станиця 100 ₴ (з пільгою 50 ₴) · курінь 30 ₴ · гурток 20 ₴');
  await expect(page.getByText('Соколи · зараз IV кв. 2026 · 2 у складі · з боргом: 2')).toBeVisible();
  await shot(page, project, 'leader-dues-box');

  await page.locator('ion-segment-button[value=people]').click();
  const people = page.getByTestId('person');
  await expect(people).toHaveCount(3);
  await expect(people.filter({ hasText: 'Остап Коваль' })).toContainText('Сплачено 0 ₴ з 150 ₴');
  await expect(people.filter({ hasText: 'Остап Коваль' })).toContainText('−150 ₴');
  await expect(people.filter({ hasText: 'Марта Бойко' })).toContainText('пільгова');
  await expect(page.getByText('Переведені й вибулі з боргом')).toBeVisible();
  await shot(page, project, 'leader-dues-people');

  // The quarter picker turns the web's grid into one quarter at a time.
  await page.getByTestId('quarter-picker').click();
  await choose(page, 'III кв. 2026');
  await expect(people.filter({ hasText: 'Остап Коваль' })).toContainText('Сплачено 150 ₴ з 150 ₴');

  await page.locator('ion-segment-button[value=history]').click();
  const entries = page.getByTestId('entry');
  await expect(entries).toHaveCount(3);
  await expect(entries.filter({ hasText: 'Мотузка' })).toContainText('−40 ₴');
  await expect(entries.filter({ hasText: 'Марта Бойко' })).toContainText('+120 ₴');
  await shot(page, project, 'leader-dues-history');

  await page.getByTestId('history-kind').click();
  await choose(page, 'Витрата');
  await expect(entries).toHaveCount(1);
  await page.getByTestId('history-kind').click();
  await choose(page, 'Усі види');
  await page.getByTestId('history-quarter').click();
  await choose(page, 'III кв. 2026');
  await expect(entries).toHaveCount(1);
  await expect(entries.first()).toContainText('перевірив Андрій Мельник');
});

test('records, checks and deletes operations and sets a пільга', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, undefined, leaderApi());
  await page.goto('./tabs/kurin/group/g1/dues');
  await expect(page.getByTestId('box')).toBeVisible();

  // «Записати операцію»: the web's checks first, then the request.
  await page.getByRole('button', { name: 'Записати операцію' }).click();
  const form = page.locator('app-dues-entry-form');
  await expect(form.getByText('Нова операція')).toBeVisible();
  await form.getByTestId('entry-save').click();
  await expect(form.getByText('Сума потрібна.')).toBeVisible();
  await expect(form.getByText('Чия це вкладка?')).toBeVisible();
  await form.getByTestId('entry-person').click();
  await page.locator('ion-alert').getByRole('radio', { name: 'Остап Коваль' }).click();
  await page.locator('ion-alert').getByRole('button', { name: 'Готово' }).click();
  await form.getByTestId('entry-amount').locator('input').fill('150');
  await shot(page, project, 'leader-dues-entry');
  await form.getByTestId('entry-save').click();
  await expect(form).toHaveCount(0);
  const created = log.writes.find((w) => w.method === 'POST' && w.path === 'group/g1/dues/entries')?.body;
  expect(created).toMatchObject({ kind: 'Contribution', method: 'Cash', counterMethod: null, amount: 150, membershipKey: 'ms1', note: null });
  expect(created.occurredOn).toMatch(/^\d{4}-\d{2}-\d{2}$/);

  // «Перевірено» and deleting go through a sheet on the row.
  await page.locator('ion-segment-button[value=history]').click();
  await page.getByTestId('entry').filter({ hasText: 'Марта Бойко' }).click();
  await shot(page, project, 'leader-dues-entry-actions');
  await sheetButton(page, 'Позначити перевіреною').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'group/g1/dues/entries/d1/verified')?.body).toEqual({ isVerified: true });

  await page.getByTestId('entry').filter({ hasText: 'Мотузка' }).click();
  await sheetButton(page, 'Видалити').click();
  await expect(page.getByRole('dialog', { name: /Видалити операцію/ })).toContainText('Витрата на 40 ₴ зникне з каси.');
  await sheetButton(page, 'Видалити').click();
  await expect.poll(() => log.writes.some((w) => w.method === 'DELETE' && w.path === 'group/g1/dues/entries/d2')).toBe(true);

  // The пільга, from a person's row.
  await page.locator('ion-segment-button[value=people]').click();
  await page.getByTestId('person').filter({ hasText: 'Остап Коваль' }).click();
  await sheetButton(page, 'Пільгова вкладка…').click();
  const concession = page.locator('app-dues-quarter-form');
  await expect(concession.getByText('Пільга знижує лише станичну частину', { exact: false })).toBeVisible();
  await shot(page, project, 'leader-dues-concession');
  await concession.getByTestId('quarter-save').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'group/g1/dues/members/ms1/concession')?.body).toEqual({
    fromQuarter: { year: 2026, number: 4 },
    isConcession: true,
  });

  // The гурток's own share of the вкладка.
  await page.locator('ion-segment-button[value=box]').click();
  await page.getByTestId('group-rate').click();
  const rate = page.locator('app-dues-quarter-form');
  await rate.getByTestId('rate-share').locator('input').fill('25');
  await rate.getByTestId('quarter-save').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'group/g1/dues/rate')?.body).toEqual({
    fromQuarter: { year: 2026, number: 4 },
    groupShare: 25,
  });
});

test('sets the kurin’s rates from a гурток’s box for whoever may (`canSetKurinRates`)', async ({ page }, info) => {
  const project = info.project.name;
  const log = await signIn(page, undefined, leaderApi({ viewer: { ...KEEPER, canSetKurinRates: true } }));
  await page.goto('./tabs/kurin/group/g1/dues');
  await expect(page.getByTestId('group-rate')).toBeVisible();
  await page.getByTestId('kurin-rate').scrollIntoViewIfNeeded();
  await shot(page, project, 'leader-dues-rates');

  // The same sheet as on the kurin's box, starting from the rates in force.
  await page.getByTestId('kurin-rate').click();
  const rate = page.locator('app-dues-kurin-rate-form');
  await expect(rate.getByTestId('rate-full').locator('input')).toHaveValue('100');
  await expect(rate.getByTestId('rate-reduced').locator('input')).toHaveValue('50');
  await expect(rate.getByTestId('rate-kurin').locator('input')).toHaveValue('30');
  await rate.getByTestId('rate-full').locator('input').fill('120');
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await rate.locator('ion-content').evaluate((content: HTMLIonContentElement) => content.scrollToTop(0));
  await shot(page, project, 'leader-dues-kurin-rates');
  await rate.getByTestId('kurin-rate-save').click();
  await expect(rate).toHaveCount(0);
  // It is the kurin's endpoint, not the гурток's.
  expect(log.writes.find((w) => w.path === 'kurin/k1/dues/rate')?.body).toEqual({
    fromQuarter: { year: 2026, number: 4 },
    stanytsiaFull: 120,
    stanytsiaReduced: 50,
    kurinShare: 30,
  });
  await expect(page.getByText('Збережено')).toBeVisible();
});

test('shows the box read-only without the keeper’s flags', async ({ page }) => {
  await signIn(page, undefined, leaderApi({ viewer: { canKeep: false, canVerify: false, canSetKurinRates: false } }));
  await page.goto('./tabs/kurin/group/g1/dues');
  await expect(page.getByTestId('box')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Записати операцію' })).toHaveCount(0);
  await expect(page.getByTestId('group-rate')).toHaveCount(0);
  await expect(page.getByTestId('kurin-rate')).toHaveCount(0);
  await page.locator('ion-segment-button[value=history]').click();
  await page.getByTestId('entry').first().click();
  await expect(page.locator('ion-action-sheet')).toHaveCount(0);
});

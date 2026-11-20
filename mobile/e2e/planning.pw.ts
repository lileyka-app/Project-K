import { expect, test, type Page } from '@playwright/test';
import { member, ok, shot, signIn, type ExtraApi } from './support/mock-api';

/**
 * Camp planning under Календар: the list, a session with its availability strip, «Перенести в
 * календар», delete, and «Нове планування». Fixtures are shaped like the API's PlanningResponse;
 * `leader` gives the sessions the `canDelete` the API sends their author.
 */

// The coming summer, as the form starts on it; dates go as the web sends them, local midnights.
const now = new Date();
const year = now.getMonth() >= 8 ? now.getFullYear() + 1 : now.getFullYear();
const midnight = (month: number, day: number) => new Date(year, month - 1, day).toISOString();
const range = (fromMonth: number, fromDay: number, toMonth: number, toDay: number) => ({
  start: midnight(fromMonth, fromDay),
  end: midnight(toMonth, toDay),
});
// The year shows when the window is not in this one.
const yearSuffix = year === now.getFullYear() ? '' : ` ${year}`;

function person(memberKey: string, fullName: string, roleWeight: number, busyRanges: { start: string; end: string }[]) {
  return { memberKey, fullName, roleWeight, busyRanges };
}

function sessions(leader: boolean) {
  return [
    {
      planningSessionKey: 's1', name: `Табір ${year}`, kurinKey: 'k1', searchStart: midnight(6, 1), searchEnd: midnight(8, 31),
      durationDays: 10, isCalculated: true, optimalStartDate: midnight(7, 5), optimalEndDate: midnight(7, 15), conflictScore: 300,
      canDelete: leader,
      participants: [
        person('m1', 'Богдан Гончар', 5, [range(6, 1, 6, 20)]),
        person('m2', 'Ірина Бойко', 5, [range(7, 25, 8, 10)]),
        person('m3', 'Марко Шевчук', 3, [range(7, 12, 7, 18), range(8, 20, 8, 31)]),
        person('m4', 'Олена Ткачук-Мельничук', 1, []),
        person('m5', 'Тарас Лисенко', 0, [range(6, 25, 7, 3)]),
      ],
    },
    {
      planningSessionKey: 's2', name: 'Осіння мандрівка', kurinKey: 'k1', searchStart: midnight(9, 1), searchEnd: midnight(9, 30),
      durationDays: 3, isCalculated: false, optimalStartDate: null, optimalEndDate: null, conflictScore: 0, canDelete: false,
      participants: [],
    },
  ];
}

const kv = [
  { memberKey: 'm1', firstName: 'Богдан', lastName: 'Гончар', middleName: null },
  { memberKey: 'm2', firstName: 'Ірина', lastName: 'Бойко', middleName: null },
  { memberKey: 'm3', firstName: 'Марко', lastName: 'Шевчук', middleName: null },
];

/** The planning endpoints; `empty` is a kurin that has not planned anything yet. */
function planningApi(options: { leader?: boolean; empty?: boolean } = {}): ExtraApi {
  let list = options.empty ? [] : sessions(!!options.leader);
  return ({ method, path }) => {
    if (method === 'GET' && path === 'planning/k1') return ok(list);
    if (method === 'GET' && path.startsWith('planning/session/')) {
      const found = list.find((s) => s.planningSessionKey === path.split('/')[2]);
      return found ? ok(found) : { status: 404, json: { error: 'NotFound', message: 'NotFound' } };
    }
    if (method === 'DELETE' && path.startsWith('planning/')) {
      list = list.filter((s) => s.planningSessionKey !== path.split('/')[1]);
      // The API answers a delete with a line of text, not JSON.
      return { status: 200, file: Buffer.from('Planning deleted successfully'), contentType: 'text/plain; charset=utf-8' };
    }
    if (method === 'POST' && path === 'planning') return ok('s3');
    if (method === 'POST' && path === 'agenda') return ok('a1');
    if (method === 'GET' && path === 'kurin/k1/kv') return ok(kv);
    return null;
  };
}

/** Провід: the grants to open a planning and to put events into the calendar. */
const LEADER = ['PlanningSession:Create:OwnGroups', 'AgendaItem:Create:OwnGroups'];

async function openPlanning(page: Page) {
  await page.locator('ion-tab-button[tab=calendar]').click();
  await page.locator('app-calendar').getByTestId('open-planning').click();
  await expect(page).toHaveURL(/\/tabs\/calendar\/planning$/);
  await expect(page.locator('app-planning-list').getByTestId('session-row').first()).toBeVisible();
}

test('shows a youth the sessions, the dates and the strip, to read only', async ({ page }, info) => {
  await signIn(page, member, planningApi());
  await openPlanning(page);
  const list = page.locator('app-planning-list');
  await expect(list.getByTestId('session-row')).toHaveCount(2);
  await expect(list.getByTestId('session-row').first()).toContainText(`1 червня – 31 серпня${yearSuffix}`);
  await expect(list.getByTestId('session-row').first()).toContainText('Розраховано');
  await expect(list.getByTestId('session-row').last()).toContainText('Нове');
  await expect(list.getByTestId('new-planning')).toHaveCount(0);
  await expect(list.getByTestId('session-delete')).toHaveCount(0);
  await shot(page, info.project.name, 'planning-list');

  await list.getByText(`Табір ${year}`).click();
  const detail = page.locator('app-planning');
  await expect(detail.getByTestId('planning-name')).toContainText(`Табір ${year}`);
  await expect(detail.getByTestId('planning-window')).toHaveText(`1 червня – 31 серпня${yearSuffix}`);
  // Ten days from the 5th: the 5th to the 14th. Марко is away from the 12th.
  await expect(detail.getByTestId('optimal-dates')).toHaveText(`5–14 липня${yearSuffix}`);
  await expect(detail.getByTestId('optimal')).toContainText('Зайняті в ці дні: Марко Шевчук');
  await expect(detail.getByTestId('to-calendar')).toHaveCount(0);
  await expect(detail.getByTestId('delete')).toHaveCount(0);
  await shot(page, info.project.name, 'planning-detail');

  // The strip opens on the camp's days and scrolls sideways under the names.
  const strip = detail.getByTestId('strip');
  await strip.scrollIntoViewIfNeeded();
  await expect(detail.getByTestId('strip-row')).toHaveCount(5);
  await expect.poll(() => strip.evaluate((el) => el.scrollLeft)).toBeGreaterThan(400);
  expect(await strip.evaluate((el) => el.scrollWidth > el.clientWidth)).toBe(true);
  await expect(detail.getByTestId('participants')).toContainText(`Зайняті дні: 12–17 липня${yearSuffix}, 20–30 серпня${yearSuffix}`);
  await expect(detail.getByTestId('participants')).toContainText('Важливість голосу: Неважливий');
  await shot(page, info.project.name, 'planning-strip');
  await detail.locator('ion-content').evaluate((el: HTMLIonContentElement) => el.scrollToBottom(0));
  await shot(page, info.project.name, 'planning-participants');

  // A session not worked out yet says so and has nothing to draw.
  await page.goto('./tabs/calendar/planning/s2');
  await expect(detail.getByTestId('planning-name')).toContainText('Осіння мандрівка');
  await expect(detail.getByText('Дати ще не пораховано.')).toBeVisible();
  await expect(detail.getByText('Немає даних для відображення')).toBeVisible();
});

test('lets the провід move the dates into the calendar and delete a session', async ({ page }, info) => {
  const log = await signIn(page, member, planningApi({ leader: true }), LEADER);
  // Меню's «Планування» opens it in the app.
  await page.locator('#tab-button-more').click();
  await page.locator('app-more').getByTestId('menu-planning').click();
  await expect(page).toHaveURL(/\/tabs\/calendar\/planning$/);
  const list = page.locator('app-planning-list');
  await expect(list.getByTestId('new-planning')).toBeVisible();
  await list.getByText(`Табір ${year}`).click();

  const detail = page.locator('app-planning');
  await detail.getByTestId('to-calendar').click();
  await expect(detail.getByTestId('to-calendar')).toContainText('Додано в календар куреня');
  const event = log.writes.find((w) => w.method === 'POST' && w.path === 'agenda')?.body;
  expect(event).toMatchObject({
    kurinKey: 'k1',
    kind: 'Event',
    title: expect.stringContaining(`Табір ${year}`),
    description: 'Створено з планування табору',
    isAllDay: true,
    startUtc: `${year}-07-05T00:00:00.000Z`,
    endUtc: `${year}-07-14T00:00:00.000Z`,
    targets: [{ targetType: 'Kurin', targetKey: 'k1' }],
  });
  await detail.locator('ion-content').evaluate((el: HTMLIonContentElement) => el.scrollToBottom(0));
  await shot(page, info.project.name, 'planning-detail-leader');

  await detail.getByTestId('delete').click();
  const sheet = page.locator('ion-action-sheet');
  await expect(sheet).toContainText(`Видалити планування «Табір ${year}`);
  await sheet.getByRole('button', { name: 'Видалити планування' }).click();
  await expect.poll(() => log.writes.find((w) => w.method === 'DELETE')?.path).toBe('planning/s1');
  await expect(page).toHaveURL(/\/tabs\/calendar\/planning$/);
  await expect(list.getByTestId('session-row')).toHaveCount(1);
});

test('opens a new planning with the КВ, weights and busy days', async ({ page }, info) => {
  const log = await signIn(page, member, planningApi({ leader: true }), LEADER);
  await page.goto('./tabs/calendar/planning');
  const list = page.locator('app-planning-list');
  await list.getByTestId('new-planning').click();

  const form = page.locator('app-planning-form');
  await expect(form.locator('[data-testid=name] input')).toHaveValue(`Табір ${year}`);
  await expect(form.getByTestId('duration')).toHaveText('10 днів');
  await expect(form.getByTestId('from')).toHaveText(`1 червня ${year}`);
  await expect(form.getByTestId('to')).toHaveText(`31 серпня ${year}`);
  await expect(form.getByTestId('participant')).toHaveCount(3);
  await shot(page, info.project.name, 'planning-form');

  await form.locator('[data-testid=name] input').fill('Літній табір');
  await form.getByRole('button', { name: 'Більше днів' }).click();
  await form.getByRole('button', { name: 'Більше днів' }).click();
  await expect(form.getByTestId('duration')).toHaveText('12 днів');

  // Богдан: high weight and busy from the 1st to the 12th of June (picked in the calendar).
  const bohdan = form.getByTestId('participant').first();
  await bohdan.getByTestId('weight').click();
  await page.locator('ion-action-sheet').getByRole('radio', { name: 'Високий', exact: true }).click();
  await expect(page.locator('ion-action-sheet')).toHaveCount(0);
  await bohdan.getByTestId('add-range').click();
  await expect(bohdan.getByTestId('busy-range')).toHaveCount(1);
  await bohdan.getByRole('button', { name: /^Зайнятий до / }).click();
  const picker = page.locator('ion-datetime');
  await picker.locator('.calendar-day[data-month="6"][data-day="12"]').click();
  await picker.getByRole('button', { name: 'Готово' }).click();
  await expect(bohdan.getByTestId('busy-range')).toContainText('12 чер');
  // Ірина: a range added and taken off again.
  const iryna = form.getByTestId('participant').nth(1);
  await iryna.getByTestId('add-range').click();
  await iryna.getByRole('button', { name: 'Прибрати період' }).click();
  await expect(iryna.getByTestId('busy-range')).toHaveCount(0);
  await bohdan.scrollIntoViewIfNeeded();
  await shot(page, info.project.name, 'planning-form-busy');

  await form.getByTestId('save').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'POST' && w.path === 'planning')?.body).toMatchObject({
    name: 'Літній табір',
    kurinKey: 'k1',
    durationDays: 12,
    searchStart: midnight(6, 1),
    searchEnd: midnight(8, 31),
    participants: [
      // Both picked days are busy: up to the midnight after the 12th.
      { memberKey: 'm1', fullName: expect.stringContaining('Богдан'), roleWeight: 5, busyRanges: [{ start: midnight(6, 1), end: midnight(6, 13) }] },
      { memberKey: 'm2', fullName: expect.stringContaining('Ірина'), roleWeight: 3, busyRanges: [] },
      { memberKey: 'm3', fullName: expect.stringContaining('Марко'), roleWeight: 3, busyRanges: [] },
    ],
  });
  await expect(page).toHaveURL(/\/tabs\/calendar\/planning$/);
});

test('shows an empty kurin the sleeping calendar, and a youth no new planning', async ({ page }, info) => {
  await signIn(page, member, planningApi({ empty: true }));
  await page.goto('./tabs/calendar/planning');
  const list = page.locator('app-planning-list');
  await expect(list.getByTestId('planning-empty')).toContainText('Календар ще спить');
  await expect(list.getByTestId('new-planning')).toHaveCount(0);
  await shot(page, info.project.name, 'planning-empty');

  await page.goto('./tabs/calendar/planning/new');
  const form = page.locator('app-planning-form');
  await expect(form.getByText('Немає доступу')).toBeVisible();
  await expect(form.getByTestId('save')).toHaveCount(0);
});

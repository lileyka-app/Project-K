import { expect, test, type Page } from '@playwright/test';
import { ok, refuse, shot, signIn, type Answer, type ExtraApi } from './support/mock-api';

// Fixtures shaped like the API's score DTOs (kept in step with src/app/features/score/score.models.ts).

const START = '2026-10-08T15:00:00.000Z';
const periods = {
  years: [
    { kind: 'Year', year: 2026, stageKey: null, label: '2025/2026', from: '2025-09-01', to: '2026-08-31' },
    { kind: 'Year', year: 2025, stageKey: null, label: '2024/2025', from: '2024-09-01', to: '2025-08-31' },
  ],
  stages: [{ kind: 'Stage', year: null, stageKey: 's1', label: 'Осінь', from: '2025-09-01', to: '2025-12-31' }],
};
const period = (year: number) => periods.years.find((p) => p.year === year) ?? periods.years[0];

function groupRow(key: string, name: string, place: number, score: number, canOpen: boolean) {
  return { groupKey: key, groupName: name, place, score, otherScore: score * 6, youthPoints: score * 6, youthCount: 6, average: score, groupPoints: place === 1 ? 5 : 0, canOpen };
}

function kurinScore(year: number) {
  return {
    kurinKey: 'k1', algorithm: 'Average', period: period(year), periods,
    groups: [groupRow('g1', 'Соколи', 1, 14.5, true), groupRow('g2', 'Вовки', 2, 11, false), groupRow('g3', 'Лиси', 3, 6.25, false)],
    viewer: { canScore: true, canManage: false },
  };
}

const items = [
  { scoreItemKey: 'i1', name: 'Чергування', points: 2, isArchived: false },
  { scoreItemKey: 'i2', name: 'Пісня', points: 3, isArchived: false },
];

function entry(key: string, over: Record<string, unknown>) {
  return {
    scoreEntryKey: key, membershipKey: 'ms1', memberKey: 'm1', memberName: 'Остап Коваль', groupKey: 'g1', groupName: 'Соколи',
    isForGroup: false, scoreItemKey: null, itemName: null, points: 2, reason: 'Допоміг на зборі', agendaItemKey: null,
    occurrenceStartUtc: null, occurredOn: '2026-10-03', createdByName: 'Марта Гнатів', createdAtUtc: '2026-10-03T10:00:00Z', ...over,
  };
}

function groupScore(canScore: boolean) {
  return {
    groupKey: 'g1', kurinKey: 'k1', groupName: 'Соколи', algorithm: 'Average', period: period(2026), periods,
    standing: groupRow('g1', 'Соколи', 1, 14.5, true), groupCount: 3,
    people: [
      { membershipKey: 'ms1', memberKey: 'm1', fullName: 'Остап Коваль', standing: 'Current', total: 23, bySource: { Attendance: 12, Item: 5, Free: 2, Skill: 4 } },
      { membershipKey: 'ms2', memberKey: 'm2', fullName: 'Тарас Бойко', standing: 'Current', total: 15, bySource: { Attendance: 10, Probe: 5 } },
      { membershipKey: 'ms3', memberKey: 'm3', fullName: 'Іван Ткач', standing: 'Moved', total: 4, bySource: { Attendance: 4 } },
    ],
    entries: [
      entry('se1', {}),
      entry('se2', { isForGroup: true, membershipKey: null, memberKey: null, memberName: null, scoreItemKey: 'i2', itemName: 'Пісня', points: 3, reason: null, agendaItemKey: 'e1', occurrenceStartUtc: START, occurredOn: '2026-10-08' }),
    ],
    items, canScore,
  };
}

function sheetPerson(key: string, name: string, over: Record<string, unknown>) {
  return { membershipKey: key, memberKey: `m-${key}`, fullName: name, groupKey: 'g1', groupName: 'Соколи', rsvp: null, isAssigned: false, attendance: null, canScore: true, entries: [], ...over };
}

function sheet() {
  return {
    kurinKey: 'k1', agendaItemKey: 'e1', occurrenceStartUtc: START, occurrenceEndUtc: '2026-10-08T17:00:00.000Z', isAllDay: false, isRecurring: true,
    title: 'Сходини гуртка', categoryKey: 'c1', categoryName: 'Сходини', categoryColorHex: '#0e6e4e', categoryIcon: null,
    attendancePoints: 2, hasOwnRate: false, canManage: true, items,
    people: [
      sheetPerson('ms1', 'Остап Коваль', { rsvp: 'Going', isAssigned: true, entries: [entry('se1', { scoreItemKey: 'i1', itemName: 'Чергування', points: 2, reason: null, agendaItemKey: 'e1', occurrenceStartUtc: START })] }),
      sheetPerson('ms2', 'Тарас Бойко', { rsvp: 'Maybe', isAssigned: true }),
      sheetPerson('ms4', 'Назар Олійник', { rsvp: 'Going', attendance: { markedByName: 'Марта Гнатів', markedAtUtc: START } }),
      sheetPerson('ms5', 'Юрко Невдаха', { rsvp: 'Going' }),
      sheetPerson('ms6', 'Марко Савчук', { isAssigned: true }),
      sheetPerson('ms7', 'Данило Кравець', { rsvp: 'NotGoing' }),
      sheetPerson('ms8', 'Богдан Шевчук', { groupKey: 'g2', groupName: 'Вовки', rsvp: 'Going', canScore: false }),
    ],
    groups: [
      { groupKey: 'g1', groupName: 'Соколи', canScore: true, entries: [] },
      { groupKey: 'g2', groupName: 'Вовки', canScore: false, entries: [] },
    ],
  };
}

interface Asked {
  kurinYears: (string | null)[];
}

/** The score endpoints; `scorer` gives the гурток's canScore, `youth` refuses its page. */
function scoreApi(asked: Asked, options: { scorer?: boolean; youth?: boolean } = {}): ExtraApi {
  const read = (path: string, decoded: string, query: URLSearchParams): Answer | null => {
    if (path === 'kurin/k1/score') {
      asked.kurinYears.push(query.get('year'));
      return ok(kurinScore(Number(query.get('year') ?? 2026)));
    }
    if (path === 'kurin/k1/score/groups/g1') return options.youth ? refuse(403, 'Forbidden') : ok(groupScore(options.scorer ?? true));
    if (path === 'agenda/item/e1') return ok({ agendaItemKey: 'e1', startUtc: START });
    if (decoded === `kurin/k1/score/events/e1/${START}`) return ok(sheet());
    return null;
  };
  const mark = (keys: string[]): Answer => {
    if (keys.includes('ms5')) return refuse(500);
    return ok(keys.map((k) => ({ membershipKey: k, outcome: k === 'ms2' ? 'AlreadyMarked' : 'Marked', markedByName: k === 'ms2' ? 'Марта Гнатів' : null })));
  };
  return ({ method, path, query, body }): Answer | null => {
    const decoded = decodeURIComponent(path);
    if (method === 'GET') return read(path, decoded, query);
    if (method === 'POST' && decoded === `kurin/k1/score/events/e1/${START}/attendance`) return mark(body.membershipKeys ?? []);
    if (method === 'DELETE' && decoded.startsWith(`kurin/k1/score/events/e1/${START}/attendance/`)) return { status: 200, empty: true };
    if (method === 'POST' && path === 'kurin/k1/score/entries') return { status: 201, json: 'new-entry' };
    if (method === 'PUT' && path.startsWith('kurin/k1/score/entries/')) return { status: 200, empty: true };
    if (method === 'DELETE' && path.startsWith('kurin/k1/score/entries/')) return { status: 200, empty: true };
    if (method === 'PUT' && path === 'kurin/k1/score/settings/attendance-rates') return { status: 200, empty: true };
    return null;
  };
}

async function pickInSheet(page: Page, select: string, option: string): Promise<void> {
  await page.locator(`[data-testid=${select}]`).click();
  await page.locator('ion-action-sheet button', { hasText: option }).click();
  await expect(page.locator('ion-action-sheet')).toHaveCount(0);
}

/** Typing scrolls the field into view; the screenshot wants the sheet from its top. */
async function topOfSheet(page: Page): Promise<void> {
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await page.locator('ion-modal ion-content').evaluate((content) => (content as HTMLIonContentElement).scrollToTop());
}

const person = (page: Page, name: string) => page.locator('[data-testid=sheet-person]', { hasText: name });

test('shows the kurin table, changes the period and opens a гурток', async ({ page }, info) => {
  const asked: Asked = { kurinYears: [] };
  await signIn(page, undefined, scoreApi(asked));
  await page.goto('./tabs/kurin/score');

  const rows = page.locator('[data-testid=score-group]');
  await expect(rows).toHaveCount(3);
  await expect(rows.first()).toContainText('Соколи');
  await expect(rows.first()).toContainText('14,5');
  await expect(rows.nth(2)).toContainText('6,25');
  await expect(page.getByTestId('score-subline')).toContainText('2025/2026');
  await expect(page.getByText('Відмічати присутність і давати бали')).toBeVisible();
  await shot(page, info.project.name, 'score-kurin');

  await pickInSheet(page, 'score-period', '2024/2025');
  await expect(page.getByTestId('score-subline')).toContainText('2024/2025');
  expect(asked.kurinYears).toContain('2025');
  await expect(page).toHaveURL(/score\?year=2025$/);

  await rows.first().click();
  await expect(page).toHaveURL(/tabs\/kurin\/group\/g1\/score\?year=2025$/);
  await expect(page.getByTestId('score-place')).toHaveText('1');
});

test('shows a гурток’s youth by source and the entries given by hand', async ({ page }, info) => {
  await signIn(page, undefined, scoreApi({ kurinYears: [] }));
  await page.goto('./tabs/kurin/group/g1/score');

  await expect(page.getByTestId('score-place')).toHaveText('1');
  const people = page.locator('[data-testid=score-person]');
  await expect(people).toHaveCount(3);
  await expect(people.nth(2)).toContainText('переведений');
  await people.first().click();
  await expect(page.locator('ion-accordion.accordion-expanded')).toContainText('Присутність');
  await expect(page.locator('ion-accordion.accordion-expanded')).toContainText('Вмілості');
  await shot(page, info.project.name, 'score-group');

  await page.getByTestId('tab-entries').click();
  await expect(page.locator('[data-testid=score-entry]')).toHaveCount(2);
  await expect(page.locator('[data-testid=score-entry]').first()).toContainText('Допоміг на зборі');
  await shot(page, info.project.name, 'score-group-entries');
});

test('gives, edits and deletes points on a гурток’s page', async ({ page }, info) => {
  const log = await signIn(page, undefined, scoreApi({ kurinYears: [] }));
  await page.goto('./tabs/kurin/group/g1/score');
  await expect(page.getByTestId('score-place')).toHaveText('1');

  // «Записати бал»: whom, then a free amount with a reason.
  await page.getByTestId('give-score').click();
  await pickInSheet(page, 'entry-target', 'Тарас Бойко');
  await page.locator('ion-segment-button', { hasText: 'Свій бал' }).click();
  await page.locator('[data-testid=entry-amount] input').fill('-1');
  await page.locator('[data-testid=entry-reason] textarea').fill('Запізнився');
  await topOfSheet(page);
  await shot(page, info.project.name, 'score-entry');
  await page.getByTestId('entry-save').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'POST')).toBeTruthy();
  const created = log.writes.find((w) => w.method === 'POST')!;
  expect(created.path).toBe('kurin/k1/score/entries');
  expect(created.body).toMatchObject({ membershipKey: 'ms2', groupKey: null, scoreItemKey: null, points: -1, reason: 'Запізнився' });
  expect(created.body.occurredOn).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  await expect(page.locator('ion-modal.show-modal')).toHaveCount(0);

  // Editing an entry: tap it, change the amount, save.
  await page.getByTestId('tab-entries').click();
  await page.locator('[data-testid=score-entry]').first().click();
  await expect(page.locator('ion-modal ion-title')).toHaveText('Змінити бал');
  await page.locator('[data-testid=entry-amount] input').fill('4');
  await page.getByTestId('entry-save').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'PUT')?.path).toBe('kurin/k1/score/entries/se1');
  expect(log.writes.find((w) => w.method === 'PUT')!.body).toMatchObject({ membershipKey: 'ms1', points: 4, reason: 'Допоміг на зборі' });

  // Deleting goes through a confirming action sheet.
  await page.locator('[data-testid=score-entry]').first().click();
  await page.getByText('Видалити бал').click();
  await page.locator('ion-action-sheet button', { hasText: 'Видалити' }).click();
  await expect.poll(() => log.writes.find((w) => w.method === 'DELETE')?.path).toBe('kurin/k1/score/entries/se1');
});

test('reads a гурток read-only without canScore', async ({ page }) => {
  await signIn(page, undefined, scoreApi({ kurinYears: [] }, { scorer: false }));
  await page.goto('./tabs/kurin/group/g1/score');
  await expect(page.getByTestId('score-place')).toHaveText('1');
  await expect(page.getByTestId('give-score')).toHaveCount(0);
  await page.locator('[data-testid=score-person]').first().click();
  await expect(page.getByText('Дати бал')).toHaveCount(0);
});

test('says so kindly when the server keeps a youth out of a гурток’s page', async ({ page }, info) => {
  await signIn(page, undefined, scoreApi({ kurinYears: [] }, { youth: true }));
  await page.goto('./tabs/kurin/group/g1/score');
  await expect(page.getByTestId('score-forbidden')).toContainText('бачать його провід і судді');
  await shot(page, info.project.name, 'score-group-forbidden');
});

test('marks attendance on the spot, in bulk, and rolls back a refusal', async ({ page }, info) => {
  const log = await signIn(page, undefined, scoreApi({ kurinYears: [] }));
  await page.goto(`./tabs/calendar/attendance/e1?start=${encodeURIComponent(START)}`);

  await expect(page.getByTestId('sheet-title')).toHaveText('Сходини гуртка');
  await expect(page.getByTestId('sheet-marked')).toHaveText('1');
  // «Мої гуртки» first: the other гурток's youth only on asking.
  await expect(person(page, 'Богдан Шевчук')).toHaveCount(0);
  await expect(page.locator('[data-testid=sheet-person]')).toHaveCount(4);
  await shot(page, info.project.name, 'score-attendance');

  // One tap marks, at once.
  await person(page, 'Остап Коваль').click();
  await expect(person(page, 'Остап Коваль')).toHaveAttribute('data-marked', 'yes');
  await expect.poll(() => log.writes.filter((w) => w.method === 'POST').length).toBe(1);
  expect(log.writes[0].body).toEqual({ membershipKeys: ['ms1'] });
  expect(decodeURIComponent(log.writes[0].path)).toBe(`kurin/k1/score/events/e1/${START}/attendance`);

  // A second tap unmarks.
  await person(page, 'Остап Коваль').click();
  await expect(person(page, 'Остап Коваль')).toHaveAttribute('data-marked', 'no');
  await expect.poll(() => log.writes.some((w) => w.method === 'DELETE' && w.path.endsWith('/attendance/ms1'))).toBe(true);

  // A refusal goes back and says so.
  await person(page, 'Юрко Невдаха').click();
  await expect(page.locator('ion-toast')).toContainText('Не вдалося відмітити');
  await expect(person(page, 'Юрко Невдаха')).toHaveAttribute('data-marked', 'no');

  // Search, then «Призначені».
  await page.locator('ion-searchbar input').fill('тарас');
  await expect(page.locator('[data-testid=sheet-person]')).toHaveCount(1);
  await page.locator('ion-searchbar input').fill('');
  await page.getByTestId('shelf-assigned').click();
  await expect(page.locator('[data-testid=sheet-person]')).toHaveCount(1);
  await expect(person(page, 'Марко Савчук')).toBeVisible();
  await page.getByTestId('shelf-answered').click();

  // «Усі, хто відповів, були»: everyone mine who answered and is not marked, in one call.
  await page.locator('ion-toast').evaluateAll((toasts) => toasts.forEach((t) => (t as HTMLIonToastElement).dismiss()));
  await expect(page.locator('ion-toast')).toHaveCount(0); // dismissing animates; a slow WebKit keeps the old one a while
  await expect(page.getByTestId('mark-answered')).toContainText('(3)');
  const before = log.writes.length;
  await page.getByTestId('mark-answered').click();
  await expect.poll(() => log.writes.length).toBeGreaterThan(before);
  expect(log.writes[before].body.membershipKeys.sort()).toEqual(['ms1', 'ms2', 'ms5']);
  await expect(page.locator('ion-toast')).toContainText('Не вдалося відмітити');

  // Every гурток on asking.
  await pickInSheet(page, 'sheet-groups', 'Усі гуртки');
  await expect(person(page, 'Богдан Шевчук')).toHaveAttribute('data-marked', 'no');
});

test('gives points at the event and changes its rate', async ({ page }, info) => {
  const log = await signIn(page, undefined, scoreApi({ kurinYears: [] }));
  // A one-off link without the occurrence: the item's own start is used.
  await page.goto('./tabs/calendar/attendance/e1');
  await expect(page.getByTestId('sheet-title')).toHaveText('Сходини гуртка');

  await person(page, 'Остап Коваль').getByRole('button', { name: 'Дати бал: Остап Коваль', exact: true }).click();
  await expect(page.locator('ion-modal')).toContainText('Уже є на цій події');
  await pickInSheet(page, 'entry-item', 'Пісня');
  await topOfSheet(page);
  await shot(page, info.project.name, 'score-attendance-entry');
  await page.getByTestId('entry-save').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'kurin/k1/score/entries')?.body).toMatchObject({
    membershipKey: 'ms1', scoreItemKey: 'i2', points: null, agendaItemKey: 'e1', occurrenceStartUtc: START,
  });

  // A гурток as a whole.
  await page.locator('[data-testid=sheet-group]', { hasText: 'Соколи' }).click();
  await expect(page.locator('ion-modal')).toContainText('Гурток Соколи цілим');
  await page.getByRole('button', { name: 'Скасувати' }).click();
  await expect(page.locator('ion-modal.show-modal')).toHaveCount(0);

  // The event's own rate.
  await page.getByTestId('sheet-rate').click();
  await page.locator('ion-alert input').fill('5');
  await page.locator('ion-alert button', { hasText: 'Зберегти' }).click();
  await expect.poll(() => log.writes.find((w) => w.path === 'kurin/k1/score/settings/attendance-rates')?.body).toEqual({
    agendaCategoryKey: null, agendaItemKey: 'e1', points: 5,
  });
});

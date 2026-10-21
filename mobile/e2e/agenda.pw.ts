import { expect, test, type Page } from '@playwright/test';
import { member, ok, refuse, shot, signIn, soon, type Answer, type ExtraApi } from './support/mock-api';

/**
 * The agenda: calendar, event and task details, the board and the forms. Fixtures are shaped like
 * the API's AgendaItemResponse; `leader` flips the per-item flags the API would send to провід.
 */

const localDay = (iso: string) => {
  const date = new Date(iso);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
};
const allDay = (iso: string) => `${localDay(iso)}T00:00:00Z`;

const sokoly = { agendaAssignmentKey: 'a1', targetType: 'Group', targetKey: 'g1', label: 'Соколи', completionMode: 'Shared',
  status: 'Todo', statusChangedByName: null, statusChangedAtUtc: null, canChangeStatus: false, doneCount: null, peopleCount: null, parts: null };

function item(overrides: Record<string, unknown>) {
  return {
    kurinKey: 'k1', kind: 'Event', description: null, location: null, status: 'Todo', viewerStatus: 'Todo', startUtc: null,
    endUtc: null, isAllDay: false, createdByUserKey: 'u1', createdByName: 'Богдан Гончар', createdUtc: soon(-10, 9),
    updatedUtc: soon(-1, 9), completedAtUtc: null, archivedAtUtc: null, archivedByName: null, canEdit: false,
    canChangeStatus: false, addressedToViewer: true, categoryKey: null, categoryName: null, categoryColorHex: null,
    categoryIcon: null, isKurinSchedule: false, audience: 'Assigned', recurrenceFrequency: 'None', recurrenceInterval: 1,
    recurrenceByWeekday: 0, recurrenceEndUtc: null, recurrenceCount: null, isRecurrenceInstance: false,
    seriesStartUtc: null, seriesEndUtc: null, assignments: [sokoly],
    ...overrides,
  };
}

interface Fixtures {
  extra: ExtraApi;
  reads: { path: string; query: URLSearchParams }[];
}

/**
 * The agenda endpoints as the API answers a youth, or (`leader`) the провід who may edit. `busy`
 * adds two more events to tomorrow (one overlapping the сходини, one short) for the day's grid.
 */
function agendaApi(leader = false, busy = false): Fixtures {
  const reads: Fixtures['reads'] = [];
  const meeting = soon(1, 17);
  const series = item({
    agendaItemKey: 'e1', title: 'Сходини гуртка', startUtc: meeting, endUtc: soon(1, 19), location: 'Пласт-дім, Львів',
    description: 'Вузли і карта. Принеси зошит.', categoryKey: 'c1', categoryName: 'Сходини', categoryColorHex: '#2e7d32',
    recurrenceFrequency: 'Weekly', recurrenceByWeekday: 1 << new Date(meeting).getDay(), isRecurrenceInstance: true,
    seriesStartUtc: soon(-6, 17), seriesEndUtc: soon(-6, 19), canEdit: leader,
  });
  const hike = item({
    agendaItemKey: 'e2', title: 'Мандрівка на Говерлу', startUtc: allDay(soon(5, 8)), endUtc: allDay(soon(6, 8)), isAllDay: true,
    location: 'Заросляк', canEdit: leader, assignments: [{ ...sokoly, targetType: 'Kurin', targetKey: 'k1', label: 'Курінь 7' }],
  });
  const orly = item({
    agendaItemKey: 'e3', title: 'Сходини', startUtc: soon(2, 18), endUtc: soon(2, 20), isKurinSchedule: true, audience: 'Schedule',
    assignments: [{ ...sokoly, targetKey: 'g2', label: 'Орли' }],
  });
  const knots = item({
    agendaItemKey: 't1', kind: 'Task', title: 'Вивчити вузли', startUtc: allDay(soon(0, 8)), endUtc: allDay(soon(3, 8)), isAllDay: true,
    viewerStatus: 'InProgress', status: 'InProgress', canChangeStatus: true, canEdit: leader,
    assignments: [{
      ...sokoly, completionMode: 'PerMember', status: 'InProgress', doneCount: 1, peopleCount: 2,
      parts: leader ? [
        { memberKey: 'm1', name: 'Остап Коваль', status: 'Done', changedByName: 'Остап Коваль', changedAtUtc: soon(-1, 12), canChangeStatus: true },
        { memberKey: 'm2', name: 'Марко Шевчук', status: 'Todo', changedByName: null, changedAtUtc: null, canChangeStatus: true },
      ] : null,
    }],
  });
  const tent = item({ agendaItemKey: 't2', kind: 'Task', title: 'Принести намет', viewerStatus: 'InProgress', status: 'InProgress',
    endUtc: soon(-1, 20), canChangeStatus: true, canEdit: leader });
  const done = item({ agendaItemKey: 't3', kind: 'Task', title: 'Зібрати внески', viewerStatus: 'Done', status: 'Done', canEdit: leader,
    assignments: [{ ...sokoly, status: 'Done', statusChangedByName: 'Богдан Гончар', statusChangedAtUtc: soon(-2, 10) }] });
  const todo = [
    { ...knots, viewerStatus: 'Todo' },
    ...Array.from({ length: 20 }, (_, i) => item({ agendaItemKey: `tt${i}`, kind: 'Task', title: `Задача ${i + 1}`, canEdit: leader, canChangeStatus: true })),
  ];
  const columns = { Todo: todo, InProgress: [tent], Done: [done] } as Record<string, any[]>;
  const calendar = [series, orly, hike, knots];
  if (busy) {
    const at = (hour: number, minute: number) => {
      const date = new Date(soon(1, hour));
      date.setMinutes(minute);
      return date.toISOString();
    };
    calendar.push(
      item({ agendaItemKey: 'e4', title: 'Підготовка до ватри', startUtc: at(18, 0), endUtc: at(20, 30), location: 'Стрийський парк' }),
      item({ agendaItemKey: 'e5', title: 'Дзвінок з проводом', startUtc: at(9, 0), endUtc: at(9, 30), categoryColorHex: '#6a4c93' }),
    );
  }
  // The archive, newest first; what the провід restores or deletes leaves it.
  const archived = [
    item({ agendaItemKey: 'x1', kind: 'Task', title: 'Підготувати ватру', viewerStatus: 'Done', status: 'Done', canEdit: leader,
      archivedAtUtc: soon(-2, 12), archivedByName: 'Богдан Гончар' }),
    item({ agendaItemKey: 'x2', kind: 'Task', title: 'Здати звіт за вересень', viewerStatus: 'Done', status: 'Done', canEdit: leader,
      archivedAtUtc: soon(-9, 3), archivedByName: null }),
    item({ agendaItemKey: 'x3', kind: 'Task', title: 'Купити мотузки', viewerStatus: 'Done', status: 'Done', canEdit: leader,
      archivedAtUtc: soon(-20, 3), archivedByName: null }),
  ];
  let myStatus: string | null = null;

  const picture = () => ({
    agendaItemKey: 'e1', occurrenceStartUtc: meeting, capacity: null, waitlistEnabled: false, myStatus,
    goingConfirmedCount: 5 + (myStatus === 'Going' ? 1 : 0), goingWaitlistCount: 0, notGoingCount: 1, maybeCount: 2,
    responses: [
      { userKey: 'u2', displayName: 'Марко Шевчук', status: 'Going', respondedAtUtc: soon(-1, 9), isWaitlisted: false },
      { userKey: 'u3', displayName: 'Ірина Бойко', status: 'Maybe', respondedAtUtc: soon(-1, 10), isWaitlisted: false },
    ],
  });

  const read = (path: string, query: URLSearchParams): Answer | null => {
    if (path === 'agenda/k1') {
      const from = new Date(query.get('fromUtc')!).getTime();
      const to = new Date(query.get('toUtc')!).getTime();
      const schedules = query.get('includeSchedules') === 'true';
      return ok(calendar.filter((i) => {
        const start = new Date(i.startUtc as string).getTime();
        const end = new Date((i.endUtc ?? i.startUtc) as string).getTime();
        return start <= to && end >= from && (schedules || i.audience !== 'Schedule');
      }));
    }
    if (path === 'agenda/k1/board') {
      const search = (query.get('search') ?? '').toLowerCase();
      const mine = query.get('onlyMine') === 'true';
      const pick = (list: any[]) => list.filter((t) => t.title.toLowerCase().includes(search) && (!mine || t.agendaItemKey === 't1'));
      const status = query.get('status');
      const skip = Number(query.get('skip') ?? 0);
      const take = Number(query.get('take') ?? 20);
      const targets = [{ targetType: 'Group', targetKey: 'g1', label: 'Соколи' }, { targetType: 'Member', targetKey: 'm1', label: 'Остап Коваль' }];
      const statuses = status ? [status] : ['Todo', 'InProgress', 'Done'];
      return ok({
        columns: statuses.map((s) => ({ status: s, total: pick(columns[s]).length, items: pick(columns[s]).slice(skip, skip + take) })),
        targets,
      });
    }
    if (path.startsWith('agenda/item/')) {
      const key = path.split('/')[2];
      const found = [series, hike, knots, tent, done, ...archived].find((i) => i.agendaItemKey === key);
      if (!found) return refuse(404, 'AGENDA_NOT_FOUND');
      // The item endpoint gives the series itself, not an occurrence.
      return ok(key === 'e1' ? { ...found, startUtc: found.seriesStartUtc, endUtc: found.seriesEndUtc, isRecurrenceInstance: false } : found);
    }
    if (path === 'agenda/e1/responses') return ok(picture());
    if (path.endsWith('/responses')) return ok({ ...picture(), responses: [], goingConfirmedCount: 0, maybeCount: 0, notGoingCount: 0 });
    if (path === 'agenda/k1/assign-targets') {
      return ok({
        canTargetKurin: true, kurinKey: 'k1', kurinLabel: 'Курінь 7',
        kurinLeaderships: [{ leadershipKey: 'l0', label: 'Курінний провід', canTarget: true }],
        groups: [
          { groupKey: 'g1', name: 'Соколи', canTargetGroup: true, leadership: { leadershipKey: 'l1', label: 'Провід Соколів', canTarget: true },
            members: [{ memberKey: 'm1', fullName: 'Остап Коваль' }, { memberKey: 'm2', fullName: 'Марко Шевчук' }] },
          { groupKey: 'g2', name: 'Орли', canTargetGroup: true, leadership: null, members: [{ memberKey: 'm3', fullName: 'Ірина Бойко' }] },
        ],
      });
    }
    if (path === 'agenda/k1/categories') {
      return ok([{ agendaCategoryKey: 'c1', kurinKey: 'k1', name: 'Сходини', colorHex: '#2e7d32', icon: null, capacity: null,
        waitlistEnabled: false, defaultDescription: 'Принеси зошит.', rsvpRequired: true, defaultDurationMinutes: 120,
        reminderLeadMinutes: null, isArchived: false, isKurinSchedule: true }]);
    }
    if (path === 'agenda/k1/archive') {
      const search = (query.get('search') ?? '').toLowerCase();
      const found = archived.filter((t) => t.title.toLowerCase().includes(search));
      const skip = Number(query.get('skip') ?? 0);
      const take = Number(query.get('take') ?? 20);
      return ok({ total: found.length, items: found.slice(skip, skip + take), policy: { autoArchiveAfterDays: 14, purgeAfterDays: 90 } });
    }
    return null;
  };

  const extra: ExtraApi = ({ method, path, query, body }) => {
    if (method === 'GET') {
      reads.push({ path, query });
      return read(path, query);
    }
    if (method === 'PUT' && path === 'agenda/e1/response') {
      myStatus = body.status;
      return ok(picture());
    }
    if (method === 'PUT' && /^agenda\/[^/]+\/archive$/.test(path)) {
      const key = path.split('/')[1];
      if (!body.archived) archived.splice(archived.findIndex((t) => t.agendaItemKey === key), 1);
      return { status: 200, empty: true };
    }
    if (method === 'DELETE' && path.startsWith('agenda/x')) {
      archived.splice(archived.findIndex((t) => t.agendaItemKey === path.split('/')[1]), 1);
      return { status: 200, empty: true };
    }
    if (method === 'POST' && path === 'agenda') return ok('new-item');
    return null;
  };
  return { extra, reads };
}

/** Провід: the youth's sign-in with the agenda and scoring grants added, as the web reads them. */
async function signInAsLeader(page: Page, extra: ExtraApi, path: string) {
  const log = await signIn(page, member, extra, ['AgendaItem:Create:OwnGroups', 'GroupScore:Create:OwnGroups']);
  await page.goto(path);
  return log;
}

async function openCalendar(page: Page) {
  await page.locator('ion-tab-button[tab=calendar]').click();
  await expect(page.locator('app-calendar [data-testid=month]')).toBeVisible();
}

/** The day's cell; a day past the grid's last week is one month on. */
async function pickDay(page: Page, iso: string) {
  await expect(page.locator('app-calendar [data-testid=month]')).toBeVisible();
  const cell = page.locator(`app-calendar [data-day="${localDay(iso)}"]`);
  if (!(await cell.count())) await page.locator('app-calendar').getByRole('button', { name: 'Наступний місяць' }).click();
  await cell.click();
}

test('shows the month with its dots, the chosen day and the coming list', async ({ page }, info) => {
  const { extra, reads } = agendaApi();
  await signIn(page, member, extra);
  await openCalendar(page);
  const calendar = page.locator('app-calendar');
  await expect(calendar.getByRole('button', { name: 'Нова подія' })).toHaveCount(0);

  await pickDay(page, soon(1, 17));
  const agenda = calendar.getByTestId('day-agenda');
  await expect(agenda.getByText('Сходини гуртка')).toBeVisible();
  await expect(agenda.getByText('17:00')).toBeVisible();
  await expect(calendar.locator(`[data-day="${localDay(soon(1, 17))}"] .dots i`)).toHaveCount(2); // the сходини and the task running that week
  await shot(page, info.project.name, 'agenda-calendar-month');

  // «Графіки гуртків» is on by default; off, the other гурток's сходини go.
  await pickDay(page, soon(2, 18));
  await expect(agenda.getByText('Графік куреня · Орли')).toBeVisible();
  await calendar.getByTestId('schedules').click();
  await expect(agenda.getByText('Графік куреня · Орли')).toHaveCount(0);
  expect(reads.some((r) => r.path === 'agenda/k1' && r.query.get('includeSchedules') === 'false')).toBe(true);
  await calendar.getByTestId('schedules').click();

  await calendar.locator('ion-segment-button[value=list]').click();
  await expect(calendar.getByTestId('list-day').first()).toBeVisible();
  await expect(calendar.getByText('Мандрівка на Говерлу').first()).toBeVisible();
  await expect(calendar.getByText('Завтра')).toBeVisible();
  await shot(page, info.project.name, 'agenda-calendar-list');
});

test('opens an event, answers it for the occurrence and sees who answered', async ({ page }, info) => {
  const { extra } = agendaApi();
  const log = await signIn(page, member, extra);
  await openCalendar(page);
  await pickDay(page, soon(1, 17));
  await page.locator('app-calendar').getByTestId('day-agenda').getByText('Сходини гуртка').click();

  const event = page.locator('app-event');
  await expect(event.getByTestId('item-title')).toHaveText('Сходини гуртка');
  await expect(event.getByText('Пласт-дім, Львів')).toBeVisible();
  await expect(event.getByText(/^Щотижня: /)).toBeVisible();
  await expect(event.getByText('Вузли і карта. Принеси зошит.')).toBeVisible();
  await expect(event.getByTestId('rsvp-list')).toContainText('Марко Шевчук');
  await expect(event.getByTestId('rsvp-counts')).toContainText('5');
  await expect(event.getByTestId('edit')).toHaveCount(0);
  await expect(event.getByTestId('delete')).toHaveCount(0);
  await expect(event.getByTestId('attendance')).toHaveCount(0);

  await event.getByTestId('rsvp').locator('ion-segment-button').filter({ hasText: /^Іду$/ }).click();
  await expect(event.getByTestId('rsvp-counts')).toContainText('6');
  const answer = log.writes.find((w) => w.path === 'agenda/e1/response');
  expect(answer?.body.status).toBe('Going');
  expect(new Date(answer?.body.occurrenceStartUtc).getTime()).toBe(new Date(soon(1, 17)).getTime());
  await shot(page, info.project.name, 'agenda-event');
});

test('lets the провід edit, score and delete a series', async ({ page }, info) => {
  const { extra } = agendaApi(true);
  const log = await signInAsLeader(page, extra, `./tabs/calendar/event/e1?start=${encodeURIComponent(soon(1, 17))}`);
  const event = page.locator('app-event');
  await expect(event.getByTestId('item-title')).toHaveText('Сходини гуртка');
  await expect(event.getByTestId('edit')).toBeVisible();
  await expect(event.getByTestId('attendance')).toBeVisible();
  await expect(event.getByTestId('attendance')).toHaveAttribute('href', /\/tabs\/calendar\/attendance\/e1\?start=/);
  await event.locator('ion-content').evaluate((el: HTMLIonContentElement) => el.scrollToBottom(0));
  await shot(page, info.project.name, 'agenda-event-leader');

  await event.getByTestId('delete').click();
  await expect(page.getByText('«Сходини гуртка» повторюється. Видалити всю серію?')).toBeVisible();
  await page.getByRole('button', { name: 'Видалити всі повторення' }).click();
  await expect.poll(() => log.writes.find((w) => w.method === 'DELETE')?.path).toBe('agenda/e1');
  await expect(page.locator('app-calendar')).toBeVisible();
});

test('creates an event for a гурток on the chosen day', async ({ page }, info) => {
  const { extra } = agendaApi(true);
  const log = await signInAsLeader(page, extra, './tabs/calendar');
  const calendar = page.locator('app-calendar');
  await pickDay(page, soon(2, 9));
  await calendar.getByTestId('new-event').click();

  const form = page.locator('app-event-form');
  await expect(form.locator('ion-title').first()).toHaveText('Нова подія');
  await expect(form.getByTestId('save')).toHaveAttribute('disabled', '');
  await form.locator('[data-testid=title] input').fill('Ватра');
  await form.locator('[data-testid=location] input').fill('Стрийський парк');
  await form.getByTestId('targets').click();
  const picker = page.locator('ion-modal').filter({ hasText: 'Для кого' });
  await expect(picker.getByText('Провід Соколів')).toBeVisible();
  await picker.locator('ion-checkbox').filter({ hasText: 'Увесь гурток' }).first().click();
  await shot(page, info.project.name, 'agenda-targets');
  await picker.getByTestId('targets-done').click();
  await expect(form.getByTestId('targets')).toContainText('Соколи');
  await shot(page, info.project.name, 'agenda-event-form');

  await form.getByTestId('save').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'POST' && w.path === 'agenda')?.body).toMatchObject({
    kurinKey: 'k1',
    kind: 'Event',
    title: 'Ватра',
    location: 'Стрийський парк',
    isAllDay: true,
    startUtc: `${localDay(soon(2, 9))}T00:00:00.000Z`,
    recurrenceFrequency: 'None',
    targets: [{ targetType: 'Group', targetKey: 'g1', completionMode: 'Shared' }],
  });
  await expect(calendar).toBeVisible();
});

test('edits an event and keeps its targets', async ({ page }) => {
  const { extra } = agendaApi(true);
  const log = await signInAsLeader(page, extra, './tabs/calendar/edit/e2');
  const form = page.locator('app-event-form');
  await expect(form.locator('[data-testid=title] input')).toHaveValue('Мандрівка на Говерлу');
  await expect(form.getByTestId('targets')).toContainText('Курінь 7');
  await form.locator('[data-testid=title] input').fill('Мандрівка на Петрос');
  await form.getByTestId('save').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'PUT' && w.path === 'agenda/e2')?.body).toMatchObject({
    agendaItemKey: 'e2',
    title: 'Мандрівка на Петрос',
    isAllDay: true,
    startUtc: `${localDay(soon(5, 8))}T00:00:00.000Z`,
    endUtc: `${localDay(soon(6, 8))}T00:00:00.000Z`,
    targets: [{ targetType: 'Kurin', targetKey: 'k1', completionMode: 'Shared' }],
  });
});

test('works the board: columns, search, «Моє», paging and a status move', async ({ page }, info) => {
  const { extra, reads } = agendaApi();
  const log = await signIn(page, member, extra);
  await page.locator('ion-tab-button[tab=tasks]').click();
  const board = page.locator('app-tasks');
  await expect(board.getByTestId('columns')).toContainText('Зробити 21');
  await expect(board.getByTestId('columns')).toContainText('В процесі 1');
  await expect(board.getByText('Вивчити вузли')).toBeVisible();
  await expect(board.getByTestId('new-task')).toHaveCount(0);
  await shot(page, info.project.name, 'agenda-tasks');

  await board.getByTestId('load-more').click();
  await expect(board.getByText('Задача 20')).toBeVisible();
  expect(reads.some((r) => r.path === 'agenda/k1/board' && r.query.get('status') === 'Todo' && r.query.get('skip') === '20')).toBe(true);
  await expect(board.getByTestId('load-more')).toHaveCount(0);

  await board.locator('ion-segment-button').filter({ hasText: 'В процесі' }).click();
  await expect(board.getByText('Принести намет')).toBeVisible();
  await expect(board.getByText(/^прострочено, /)).toBeVisible();
  await board.locator('ion-segment-button').filter({ hasText: 'Зробити' }).click();

  await board.getByTestId('mine').click();
  await expect(board.getByTestId('columns')).toContainText('Зробити 1');
  expect(reads.some((r) => r.path === 'agenda/k1/board' && r.query.get('onlyMine') === 'true')).toBe(true);
  await board.getByTestId('mine').click();

  await board.locator('[data-testid=search] input').fill('намет');
  await expect(board.getByTestId('columns')).toContainText('Зробити 0');
  await board.locator('[data-testid=search] input').fill('');
  await expect(board.getByTestId('columns')).toContainText('Зробити 21');

  // A tap on the mark offers the other columns.
  await board.getByTestId('task-row').filter({ hasText: 'Вивчити вузли' }).getByTestId('status').click();
  await page.getByRole('button', { name: 'В процесі' }).click();
  await expect.poll(() => log.writes.find((w) => w.path === 'agenda/t1/status')?.body).toEqual({ status: 'InProgress' });

  // A swipe does the same.
  const row = board.getByTestId('task-row').filter({ hasText: 'Задача 1' }).first();
  await row.scrollIntoViewIfNeeded();
  await row.evaluate((el: HTMLIonItemSlidingElement) => el.open('start'));
  await row.locator('ion-item-option').filter({ hasText: 'В процесі' }).dispatchEvent('click');
  await expect.poll(() => log.writes.find((w) => w.path === 'agenda/tt0/status')?.body).toEqual({ status: 'InProgress' });
});

test('shows a task with its progress and moves the own part', async ({ page }, info) => {
  const { extra } = agendaApi();
  const log = await signIn(page, member, extra);
  await page.locator('ion-tab-button[tab=tasks]').click();
  await page.locator('app-tasks').getByText('Вивчити вузли').click();

  const task = page.locator('app-task');
  await expect(task.getByTestId('item-title')).toHaveText('Вивчити вузли');
  await expect(task.getByTestId('target')).toContainText('1 з 2');
  await expect(task.getByTestId('target')).toContainText('Кожному окремо');
  await expect(task.getByTestId('own-part')).toBeVisible();
  await expect(task.getByTestId('edit')).toHaveCount(0);
  await shot(page, info.project.name, 'agenda-task');

  await task.getByTestId('own-part').locator('ion-segment-button').filter({ hasText: 'Зроблено' }).click();
  await expect.poll(() => log.writes.find((w) => w.path === 'agenda/t1/status')?.body).toEqual({ status: 'Done' });
});

test('lets the провід tick a person off and archive or delete from the board', async ({ page }, info) => {
  const { extra } = agendaApi(true);
  const log = await signInAsLeader(page, extra, './tabs/tasks/task/t1');
  const task = page.locator('app-task');
  await expect(task.getByTestId('part')).toHaveCount(2);
  await expect(task.getByTestId('own-part')).toHaveCount(0);
  await task.getByTestId('part').filter({ hasText: 'Марко Шевчук' }).locator('ion-checkbox').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'agenda/t1/assignments/a1/status')?.body).toEqual({
    status: 'Done',
    memberKey: 'm2',
  });
  await shot(page, info.project.name, 'agenda-task-leader');

  await page.goto('./tabs/tasks');
  const board = page.locator('app-tasks');
  await expect(board.getByTestId('new-task')).toBeVisible();
  const row = board.getByTestId('task-row').filter({ hasText: 'Задача 2' }).first();
  await row.scrollIntoViewIfNeeded();
  await row.evaluate((el: HTMLIonItemSlidingElement) => el.open('end'));
  await row.locator('ion-item-option').filter({ hasText: 'Ще' }).dispatchEvent('click');
  await page.getByRole('button', { name: 'В архів' }).click();
  await expect.poll(() => log.writes.find((w) => w.path === 'agenda/tt1/archive')?.body).toEqual({ archived: true });

  const other = board.getByTestId('task-row').filter({ hasText: 'Задача 3' }).first();
  await other.scrollIntoViewIfNeeded();
  await other.evaluate((el: HTMLIonItemSlidingElement) => el.open('end'));
  await other.locator('ion-item-option').filter({ hasText: 'Видалити' }).dispatchEvent('click');
  await page.getByRole('button', { name: 'Видалити задачу' }).click();
  await expect.poll(() => log.writes.find((w) => w.method === 'DELETE')?.path).toBe('agenda/tt2');
});

test('creates a task done by each person', async ({ page }, info) => {
  const { extra } = agendaApi(true);
  const log = await signInAsLeader(page, extra, './tabs/tasks');
  await page.locator('app-tasks').getByTestId('new-task').click();
  const form = page.locator('app-task-form');
  await expect(form.locator('ion-segment-button[value=Task]')).toHaveClass(/segment-button-checked/);
  await form.locator('[data-testid=title] input').fill('Вивчити гімн');
  // The row sits at the foot of the form; bring it into the open first.
  await form.locator('ion-content').evaluate((el: HTMLIonContentElement) => el.scrollToBottom(0));
  await form.getByTestId('targets').click();
  const picker = page.locator('ion-modal').filter({ hasText: 'Для кого' });
  await picker.locator('ion-searchbar input').fill('сокол');
  await picker.locator('ion-checkbox').filter({ hasText: 'Увесь гурток' }).click();
  await picker.getByTestId('targets-done').click();
  await form.locator('ion-radio').filter({ hasText: 'Кожному окремо' }).click();
  await shot(page, info.project.name, 'agenda-task-form');
  await form.getByTestId('save').click();
  await expect.poll(() => log.writes.find((w) => w.method === 'POST' && w.path === 'agenda')?.body).toMatchObject({
    kind: 'Task',
    title: 'Вивчити гімн',
    location: null,
    agendaCategoryKey: null,
    targets: [{ targetType: 'Group', targetKey: 'g1', completionMode: 'PerMember' }],
  });
});

/** The week holding the day; one on when today's week ends before it. */
async function weekWith(page: Page, iso: string) {
  const day = page.locator(`app-calendar [data-week-day="${localDay(iso)}"]`);
  if (!(await day.count())) await page.locator('app-calendar').getByRole('button', { name: 'Наступний тиждень' }).click();
  await expect(day).toBeVisible();
  return day;
}

test('shows a week and a day, steps through them and remembers the view', async ({ page }, info) => {
  const { extra } = agendaApi(false, true);
  await signIn(page, member, extra);
  await openCalendar(page);
  const calendar = page.locator('app-calendar');

  await calendar.locator('ion-segment-button[value=week]').click();
  await expect(calendar.getByTestId('week-day')).toHaveCount(7);
  const tomorrow = await weekWith(page, soon(1, 17));
  await expect(tomorrow.getByText('Сходини гуртка')).toBeVisible();
  await expect(tomorrow.getByText('Вивчити вузли')).toBeVisible();
  await expect(calendar.getByRole('button', { name: 'Нова подія' })).toHaveCount(0);
  await calendar.locator('ion-content').evaluate((el: HTMLIonContentElement) => el.scrollToTop(0));
  await shot(page, info.project.name, 'calendar-week');

  // A day's chevron opens it on the hour grid: all-day items on top, the overlapping ones side by side.
  await tomorrow.getByTestId('open-day').click();
  await expect(calendar.locator('ion-segment-button[value=day]')).toHaveClass(/segment-button-checked/);
  await expect(calendar.getByTestId('all-day')).toContainText('Вивчити вузли');
  const blocks = calendar.getByTestId('day-block');
  await expect(blocks).toHaveCount(3);
  const meeting = blocks.filter({ hasText: 'Сходини гуртка' });
  const fire = blocks.filter({ hasText: 'Підготовка до ватри' });
  await expect(meeting).toContainText('17:00–19:00');
  const [a, b] = [await meeting.boundingBox(), await fire.boundingBox()];
  expect(a!.x + a!.width).toBeLessThanOrEqual(b!.x + 1);
  expect(a!.y).toBeLessThan(b!.y);
  await expect(calendar.locator(`[data-testid=week-strip] [data-day="${localDay(soon(1, 17))}"]`)).toHaveAttribute('aria-pressed', 'true');
  await shot(page, info.project.name, 'calendar-day');
  await fire.evaluate((el) => el.scrollIntoView({ block: 'center' }));
  await shot(page, info.project.name, 'calendar-day-evening');

  // Day by day; the strip picks any day of the week.
  const title = calendar.getByTestId('period-title');
  const shown = await title.textContent();
  await calendar.getByRole('button', { name: 'Наступний день' }).click();
  await expect(title).not.toHaveText(shown!);
  await calendar.getByRole('button', { name: 'Попередній день' }).click();
  await expect(title).toHaveText(shown!);

  // Chosen once, the day view is what the calendar opens with on this device.
  await page.reload();
  await expect(page.locator('app-calendar [data-testid=day]')).toBeVisible();
  await expect(page.locator('app-calendar ion-segment-button[value=day]')).toHaveClass(/segment-button-checked/);

  // Today, then a tap on the strip, then a step on: tomorrow can be in the next week.
  await page.locator('app-calendar').getByRole('button', { name: 'Сьогодні' }).click();
  const strip = page.locator('app-calendar [data-testid=week-strip]');
  await strip.locator(`[data-day="${localDay(soon(0, 12))}"]`).click();
  await expect(strip.locator(`[data-day="${localDay(soon(0, 12))}"]`)).toHaveAttribute('aria-pressed', 'true');
  await page.locator('app-calendar').getByRole('button', { name: 'Наступний день' }).click();
  await expect(strip.locator(`[data-day="${localDay(soon(1, 17))}"]`)).toHaveAttribute('aria-pressed', 'true');
  await page.locator('app-calendar').getByTestId('day-block').filter({ hasText: 'Сходини гуртка' }).click();
  await expect(page.locator('app-event').getByTestId('item-title')).toHaveText('Сходини гуртка');
});

test('lets the провід raise an event from the day on screen', async ({ page }) => {
  const { extra } = agendaApi(true);
  await signInAsLeader(page, extra, './tabs/calendar');
  const calendar = page.locator('app-calendar');
  await calendar.locator('ion-segment-button[value=day]').click();
  await calendar.getByRole('button', { name: 'Наступний день' }).click();
  await calendar.getByRole('button', { name: 'Наступний день' }).click();
  await calendar.getByTestId('new-event').click();
  await expect(page).toHaveURL(new RegExp(String.raw`/tabs/calendar/new\?date=${localDay(soon(2, 9))}`));
  await expect(page.locator('app-event-form ion-title').first()).toHaveText('Нова подія');
});

test('lets the провід restore and delete archived tasks', async ({ page }, info) => {
  const { extra, reads } = agendaApi(true);
  const log = await signInAsLeader(page, extra, './tabs/tasks');
  await page.locator('app-tasks').getByTestId('open-archive').click();

  const archive = page.locator('app-tasks-archive');
  await expect(archive.getByTestId('archive-row')).toHaveCount(3);
  await expect(archive.getByTestId('archive-policy')).toHaveText(
    'Зроблені задачі переходять сюди через 14 дн. Видаляються назавжди через 90 дн. в архіві.',
  );
  await expect(archive.getByText('(Богдан Гончар)')).toBeVisible();
  await expect(archive.getByText(/\(автоматично\)/).first()).toBeVisible();
  await expect(archive.getByText(/^Видалиться \d\d\.\d\d\.\d{4}$/).first()).toBeVisible();
  await shot(page, info.project.name, 'tasks-archive');

  await archive.locator('[data-testid=archive-search] input').fill('звіт');
  await expect(archive.getByTestId('archive-row')).toHaveCount(1);
  expect(reads.some((r) => r.path === 'agenda/k1/archive' && r.query.get('search') === 'звіт')).toBe(true);
  await archive.locator('[data-testid=archive-search] input').fill('');
  await expect(archive.getByTestId('archive-row')).toHaveCount(3);

  await archive.getByTestId('archive-row').filter({ hasText: 'Підготувати ватру' }).getByTestId('archive-actions').click();
  await page.getByRole('button', { name: 'Повернути на дошку' }).click();
  await expect.poll(() => log.writes.find((w) => w.path === 'agenda/x1/archive')?.body).toEqual({ archived: false });
  await expect(archive.getByTestId('archive-row')).toHaveCount(2);

  await archive.getByTestId('archive-row').filter({ hasText: 'Купити мотузки' }).getByTestId('archive-actions').click();
  await page.getByRole('button', { name: 'Видалити назавжди' }).click();
  await expect(page.getByText('Видалити задачу «Купити мотузки»? Її не можна буде повернути.')).toBeVisible();
  await page.getByRole('button', { name: 'Видалити задачу' }).click();
  await expect.poll(() => log.writes.find((w) => w.method === 'DELETE')?.path).toBe('agenda/x3');
  await expect(archive.getByTestId('archive-row')).toHaveCount(1);

  // From the task itself: it says it is archived and goes back to the board from there.
  await archive.getByText('Здати звіт за вересень').click();
  const task = page.locator('app-task');
  await expect(task.getByTestId('archived')).toHaveText(/^В архіві з \d\d\.\d\d\.\d{4} \(автоматично\)$/);
  await expect(task.getByTestId('delete')).toContainText('Видалити назавжди');
  await task.locator('ion-content').evaluate((el: HTMLIonContentElement) => el.scrollToBottom(0));
  await task.getByTestId('archive').click();
  await expect.poll(() => log.writes.find((w) => w.path === 'agenda/x2/archive')?.body).toEqual({ archived: false });
  await expect(archive.getByTestId('archive-empty')).toBeVisible();
});

test('shows a youth the archive to read only', async ({ page }) => {
  const { extra } = agendaApi();
  await signIn(page, member, extra);
  await page.locator('ion-tab-button[tab=tasks]').click();
  await page.locator('app-tasks').getByTestId('open-archive').click();
  const archive = page.locator('app-tasks-archive');
  await expect(archive.getByTestId('archive-row')).toHaveCount(3);
  await expect(archive.getByTestId('archive-actions')).toHaveCount(0);
  await archive.getByText('Підготувати ватру').click();
  const task = page.locator('app-task');
  await expect(task.getByTestId('archived')).toContainText('Богдан Гончар');
  await expect(task.getByTestId('archive')).toHaveCount(0);
  await expect(task.getByTestId('delete')).toHaveCount(0);
});

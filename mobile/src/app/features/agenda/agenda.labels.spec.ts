import { AgendaAssignTargets, AgendaItemDto } from './agenda.models';
import {
  archivePolicyLabel,
  archivedLabel,
  canManageAgenda,
  dayTimeline,
  purgeDate,
  weekOf,
  weekTitle,
  canScore,
  dayKey,
  dueLabel,
  gridWindow,
  itemDays,
  itemsByDay,
  monthGrid,
  recurrenceLabel,
  rowTime,
  toLocalValue,
  toWire,
  whenLines,
} from './agenda.labels';
import { filterSections, targetNames, targetSections } from './agenda-form';

function item(overrides: Partial<AgendaItemDto>): AgendaItemDto {
  return {
    agendaItemKey: 'a', kurinKey: 'k', kind: 'Event', title: 'Сходини', description: null, location: null,
    status: 'Todo', viewerStatus: 'Todo', startUtc: null, endUtc: null, isAllDay: false, createdByUserKey: 'u',
    createdByName: null, createdUtc: '', updatedUtc: '', completedAtUtc: null, archivedAtUtc: null, archivedByName: null,
    canEdit: false, canChangeStatus: false, addressedToViewer: true, categoryKey: null, categoryName: null,
    categoryColorHex: null, categoryIcon: null, isKurinSchedule: false, audience: 'Assigned', recurrenceFrequency: 'None',
    recurrenceInterval: 1, recurrenceByWeekday: 0, recurrenceEndUtc: null, recurrenceCount: null,
    isRecurrenceInstance: false, seriesStartUtc: null, seriesEndUtc: null, assignments: [],
    ...overrides,
  };
}

/** Local wall time as the ISO instant the API would carry. */
const at = (local: string) => new Date(local).toISOString();

describe('monthGrid', () => {
  it('lays out whole weeks from Monday', () => {
    const weeks = monthGrid(new Date(2026, 9, 1)); // October 2026 starts on a Thursday
    expect(weeks).toHaveLength(5);
    expect(dayKey(weeks[0][0])).toBe('2026-09-28');
    expect(weeks[0][0].getDay()).toBe(1);
    expect(dayKey(weeks[4][6])).toBe('2026-11-01');
    expect(weeks.every((week) => week.length === 7)).toBe(true);
  });

  it('takes six rows when the month needs them', () => {
    expect(monthGrid(new Date(2026, 7, 1))).toHaveLength(6); // August 2026 starts on a Saturday
  });

  it('asks the API for the whole grid', () => {
    const weeks = monthGrid(new Date(2026, 9, 1));
    const window = gridWindow(weeks);
    expect(window.fromUtc).toBe(new Date(2026, 8, 28).toISOString());
    expect(window.toUtc).toBe(new Date(2026, 10, 2).toISOString());
  });
});

describe('itemDays', () => {
  it('reads an all-day item by its date alone', () => {
    expect(itemDays(item({ isAllDay: true, startUtc: '2026-10-10T00:00:00Z', endUtc: '2026-10-12T00:00:00Z' }))).toEqual([
      '2026-10-10', '2026-10-11', '2026-10-12',
    ]);
  });

  it('places a timed item on its local days, an end at midnight not reaching the next', () => {
    expect(itemDays(item({ startUtc: at('2026-10-10T17:00'), endUtc: at('2026-10-10T19:00') }))).toEqual(['2026-10-10']);
    expect(itemDays(item({ startUtc: at('2026-10-10T20:00'), endUtc: at('2026-10-11T00:00') }))).toEqual(['2026-10-10']);
    expect(itemDays(item({ startUtc: at('2026-10-10T20:00'), endUtc: at('2026-10-11T10:00') }))).toEqual([
      '2026-10-10', '2026-10-11',
    ]);
  });

  it('skips undated items and sorts a day with all-day items first', () => {
    const byDay = itemsByDay([
      item({ agendaItemKey: 'late', startUtc: at('2026-10-10T18:00') }),
      item({ agendaItemKey: 'early', startUtc: at('2026-10-10T09:00') }),
      item({ agendaItemKey: 'all', isAllDay: true, startUtc: '2026-10-10T00:00:00Z' }),
      item({ agendaItemKey: 'none' }),
    ]);
    expect(byDay.get('2026-10-10')?.map((i) => i.agendaItemKey)).toEqual(['all', 'early', 'late']);
    expect(byDay.size).toBe(1);
  });
});

describe('wording', () => {
  const today = new Date(2026, 9, 7);

  it('says when an event happens', () => {
    expect(whenLines(item({ startUtc: at('2026-10-10T17:00'), endUtc: at('2026-10-10T19:00') }), today)).toEqual([
      'субота, 10 жовтня', '17:00–19:00',
    ]);
    expect(whenLines(item({ isAllDay: true, startUtc: '2026-10-10T00:00:00Z' }), today)).toEqual(['субота, 10 жовтня', 'Весь день']);
    expect(whenLines(item({ isAllDay: true, startUtc: '2026-10-10T00:00:00Z', endUtc: '2026-10-12T00:00:00Z' }), today)).toEqual([
      'субота, 10 жовтня –', 'понеділок, 12 жовтня',
    ]);
  });

  it('gives the time column for each day of a span', () => {
    const span = item({ startUtc: at('2026-10-10T20:00'), endUtc: at('2026-10-11T10:00') });
    expect(rowTime(span, '2026-10-10')).toEqual({ top: '20:00', bottom: '' });
    expect(rowTime(span, '2026-10-11')).toEqual({ top: 'до', bottom: '10:00' });
    expect(rowTime(item({ isAllDay: true, startUtc: '2026-10-10T00:00:00Z' }), '2026-10-10').top).toBe('весь день');
  });

  it('describes a repetition', () => {
    expect(recurrenceLabel(item({ recurrenceFrequency: 'Weekly', recurrenceByWeekday: (1 << 1) | (1 << 3) }))).toBe('Щотижня: пн, ср');
    expect(recurrenceLabel(item({ recurrenceFrequency: 'Monthly', recurrenceInterval: 2, recurrenceEndUtc: '2026-12-20T00:00:00Z' }))).toBe(
      'Кожні 2 місяці · до 20 грудня 2026',
    );
    expect(recurrenceLabel(item({}))).toBe('');
  });

  it('marks a late task', () => {
    const now = new Date(2026, 9, 10, 12);
    expect(dueLabel(item({ kind: 'Task', isAllDay: true, endUtc: '2026-10-12T00:00:00Z' }), now)).toBe('до 12 жовтня');
    expect(dueLabel(item({ kind: 'Task', isAllDay: true, endUtc: '2026-10-09T00:00:00Z' }), now)).toBe('прострочено, 9 жовтня');
    expect(dueLabel(item({ kind: 'Task', viewerStatus: 'Done', isAllDay: true, endUtc: '2026-10-09T00:00:00Z' }), now)).toBe(
      'до 9 жовтня',
    );
  });
});

describe('form values', () => {
  it('sends an all-day date as UTC midnight and a timed one as its instant', () => {
    expect(toWire('2026-10-10T00:00', true)).toBe('2026-10-10T00:00:00.000Z');
    expect(toWire('2026-10-10T17:30', false)).toBe(new Date(2026, 9, 10, 17, 30).toISOString());
    expect(toWire(null, false)).toBeNull();
  });

  it('reads stored values back into the picker', () => {
    expect(toLocalValue('2026-10-10T00:00:00Z', true)).toBe('2026-10-10T00:00');
    expect(toLocalValue(new Date(2026, 9, 10, 17, 30).toISOString(), false)).toBe('2026-10-10T17:30');
  });
});

describe('permissions', () => {
  const user = (permissions: string[], isAdmin = false) => ({
    userKey: 'u', memberKey: 'm', email: 'e', isAdmin, permissions, roles: [], kurinKey: 'k', accessToken: null,
  });

  it('reads the web predicates off the permission strings', () => {
    expect(canManageAgenda(user(['AgendaItem:Create:OwnGroups']))).toBe(true);
    expect(canManageAgenda(user(['AgendaItem:Read:Own']))).toBe(false);
    expect(canManageAgenda(user([], true))).toBe(true);
    expect(canScore(user(['GroupScore:Create:OwnGroups']))).toBe(true);
    expect(canScore(user(['GroupScore:Read:Own']))).toBe(false);
    expect(canScore(null)).toBe(false);
  });
});

describe('targets', () => {
  const tree: AgendaAssignTargets = {
    canTargetKurin: false,
    kurinKey: 'k1',
    kurinLabel: 'Курінь 7',
    kurinLeaderships: [{ leadershipKey: 'l0', label: 'Курінний провід', canTarget: false }],
    groups: [
      { groupKey: 'g1', name: 'Соколи', canTargetGroup: true, leadership: { leadershipKey: 'l1', label: 'Провід Соколів', canTarget: true },
        members: [{ memberKey: 'm1', fullName: 'Остап Коваль' }] },
      { groupKey: 'g2', name: 'Орли', canTargetGroup: false, leadership: null, members: [] },
    ],
  };

  it('keeps only what can be picked', () => {
    const sections = targetSections(tree);
    expect(sections.map((s) => s.title)).toEqual(['Соколи']);
    expect(sections[0].rows.map((r) => r.id)).toEqual(['Group:g1', 'Leadership:l1', 'Member:m1']);
  });

  it('searches rows, and a matching heading keeps its rows', () => {
    const sections = targetSections(tree);
    expect(filterSections(sections, 'коваль')[0].rows.map((r) => r.label)).toEqual(['Остап Коваль']);
    expect(filterSections(sections, 'сокол')[0].rows).toHaveLength(3);
    expect(filterSections(sections, 'нема')).toEqual([]);
  });

  it('names a chosen гурток by its name', () => {
    expect(targetNames(tree).get('Group:g1')).toBe('Соколи');
  });
});

describe('weekOf and weekTitle', () => {
  it('runs from Monday to Sunday', () => {
    const week = weekOf(new Date(2026, 9, 10)); // a Saturday
    expect(dayKey(week[0])).toBe('2026-10-05');
    expect(dayKey(week[6])).toBe('2026-10-11');
    expect(dayKey(weekOf(new Date(2026, 9, 11))[0])).toBe('2026-10-05'); // Sunday ends the week
  });

  it('names the week by its days, the year only when it is not this one', () => {
    const today = new Date(2026, 9, 10);
    expect(weekTitle(weekOf(new Date(2026, 9, 10)), today)).toBe('5–11 жовтня');
    expect(weekTitle(weekOf(new Date(2026, 9, 1)), today)).toBe('28 вер – 4 жов');
    expect(weekTitle(weekOf(new Date(2026, 11, 31)), today)).toBe('28 гру 2026 – 3 січ 2027');
    expect(weekTitle(weekOf(new Date(2027, 1, 3)), today)).toBe('1–7 лютого 2027');
  });
});

describe('dayTimeline', () => {
  const day = '2026-10-10';

  it('puts all-day items, and timed ones running through the day, on top', () => {
    const hike = item({ agendaItemKey: 'h', isAllDay: true, startUtc: '2026-10-10T00:00:00Z' });
    const camp = item({ agendaItemKey: 'c', startUtc: at('2026-10-09T18:00'), endUtc: at('2026-10-11T12:00') });
    const meeting = item({ agendaItemKey: 'm', startUtc: at('2026-10-10T17:00'), endUtc: at('2026-10-10T19:00') });
    const timeline = dayTimeline([hike, camp, meeting], day);
    expect(timeline.allDay.map((i) => i.agendaItemKey)).toEqual(['h', 'c']);
    expect(timeline.timed.map((b) => [b.item.agendaItemKey, b.startMinute, b.endMinute])).toEqual([['m', 17 * 60, 19 * 60]]);
  });

  it('cuts a block to the day and keeps the usual hours in sight', () => {
    const late = item({ startUtc: at('2026-10-09T22:00'), endUtc: at('2026-10-10T06:30') });
    const timeline = dayTimeline([late], day);
    expect(timeline.timed[0].startMinute).toBe(0);
    expect(timeline.timed[0].endMinute).toBe(6 * 60 + 30);
    expect([timeline.fromHour, timeline.toHour]).toEqual([0, 21]);
    expect([dayTimeline([], day).fromHour, dayTimeline([], day).toHour]).toEqual([8, 21]);
    const night = item({ startUtc: at('2026-10-10T22:30'), endUtc: at('2026-10-10T23:45') });
    expect(dayTimeline([night], day).toHour).toBe(24);
  });

  it('sets overlapping blocks side by side and the rest full width', () => {
    const a = item({ agendaItemKey: 'a', startUtc: at('2026-10-10T10:00'), endUtc: at('2026-10-10T12:00') });
    const b = item({ agendaItemKey: 'b', startUtc: at('2026-10-10T11:00'), endUtc: at('2026-10-10T13:00') });
    const c = item({ agendaItemKey: 'c', startUtc: at('2026-10-10T12:00'), endUtc: at('2026-10-10T12:30') });
    const d = item({ agendaItemKey: 'd', startUtc: at('2026-10-10T15:00'), endUtc: at('2026-10-10T16:00') });
    const lanes = Object.fromEntries(dayTimeline([a, b, c, d], day).timed.map((x) => [x.item.agendaItemKey, [x.lane, x.lanes]]));
    expect(lanes).toEqual({ a: [0, 2], b: [1, 2], c: [0, 2], d: [0, 1] });
  });

  it('gives a block without an end the shortest height when deciding overlaps', () => {
    const a = item({ agendaItemKey: 'a', startUtc: at('2026-10-10T10:00') });
    const b = item({ agendaItemKey: 'b', startUtc: at('2026-10-10T10:15'), endUtc: at('2026-10-10T11:00') });
    const timed = dayTimeline([a, b], day).timed;
    expect(timed.map((x) => x.lanes)).toEqual([2, 2]);
    expect(timed[0].endMinute).toBe(600);
  });
});

describe('archive wording', () => {
  it('says since when and by whom, or that the sweep did it', () => {
    expect(archivedLabel({ archivedAtUtc: at('2026-10-03T12:00'), archivedByName: 'Богдан Гончар' })).toBe('В архіві з 03.10.2026 (Богдан Гончар)');
    expect(archivedLabel({ archivedAtUtc: at('2026-10-03T12:00'), archivedByName: null })).toBe('В архіві з 03.10.2026 (автоматично)');
    expect(archivedLabel({ archivedAtUtc: null, archivedByName: null })).toBe('');
  });

  it('counts the purge from the day it was archived, if the kurin purges at all', () => {
    expect(purgeDate({ archivedAtUtc: '2026-10-03T12:00:00Z' }, 30)?.toISOString()).toBe('2026-11-02T12:00:00.000Z');
    expect(purgeDate({ archivedAtUtc: '2026-10-03T12:00:00Z' }, null)).toBeNull();
    expect(archivePolicyLabel({ autoArchiveAfterDays: 14, purgeAfterDays: null })).toBe(
      'Зроблені задачі переходять сюди через 14 дн. З архіву нічого не видаляється.',
    );
    expect(archivePolicyLabel({ autoArchiveAfterDays: null, purgeAfterDays: 90 })).toBe(
      'Автоматичний архів вимкнено. Видаляються назавжди через 90 дн. в архіві.',
    );
  });
});

import { AuthState } from '../../auth/auth.models';
import {
  AgendaAssignmentDto,
  AgendaBoardSort,
  AgendaCompletionMode,
  AgendaItemDto,
  AgendaItemStatus,
  AgendaRsvpStatus,
  RecurrenceFrequency,
} from './agenda.models';

/** The web's AGENDA_STATUS_META, in the board's left-to-right order. */
export const STATUSES: { value: AgendaItemStatus; label: string }[] = [
  { value: 'Todo', label: 'Зробити' },
  { value: 'InProgress', label: 'В процесі' },
  { value: 'Done', label: 'Зроблено' },
];

export function statusLabel(status: AgendaItemStatus): string {
  return STATUSES.find((s) => s.value === status)?.label ?? status;
}

export const RSVP_OPTIONS: { value: AgendaRsvpStatus; label: string }[] = [
  { value: 'Going', label: 'Іду' },
  { value: 'Maybe', label: 'Можливо' },
  { value: 'NotGoing', label: 'Не йду' },
];

export const SORT_OPTIONS: { value: AgendaBoardSort; label: string }[] = [
  { value: 'Due', label: 'За терміном' },
  { value: 'Recent', label: 'Нещодавно змінені' },
  { value: 'Created', label: 'Нові спершу' },
  { value: 'Title', label: 'За назвою' },
];

export const COMPLETION_MODES: { value: AgendaCompletionMode; label: string; hint: string }[] = [
  { value: 'Shared', label: 'Одна на всіх — закриває провід', hint: 'Гурток бачить задачу, закриває її гуртковий чи впорядник.' },
  { value: 'SharedByAnyone', label: 'Одна на всіх — закриває будь-хто', hint: 'Досить, щоб зробив один; видно, хто саме.' },
  { value: 'PerMember', label: 'Кожному окремо', hint: 'Кожен робить свою частину; задача зроблена, коли зробили всі.' },
];

export const FREQUENCIES: { value: RecurrenceFrequency; label: string }[] = [
  { value: 'None', label: 'Не повторюється' },
  { value: 'Weekly', label: 'Щотижня' },
  { value: 'Monthly', label: 'Щомісяця' },
  { value: 'Yearly', label: 'Щороку' },
];

/** RecurrenceByWeekday bits (bit 0 = Sunday), listed from Monday as a Ukrainian week runs. */
export const WEEKDAY_BITS: { label: string; bit: number }[] = [
  { label: 'Пн', bit: 1 << 1 },
  { label: 'Вт', bit: 1 << 2 },
  { label: 'Ср', bit: 1 << 3 },
  { label: 'Чт', bit: 1 << 4 },
  { label: 'Пт', bit: 1 << 5 },
  { label: 'Сб', bit: 1 << 6 },
  { label: 'Нд', bit: 1 },
];

export const WEEKDAY_HEADERS = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Нд'];
export const MONTH_NAMES = ['Січень', 'Лютий', 'Березень', 'Квітень', 'Травень', 'Червень', 'Липень', 'Серпень', 'Вересень', 'Жовтень', 'Листопад', 'Грудень'];
export const MONTHS_OF = ['січня', 'лютого', 'березня', 'квітня', 'травня', 'червня', 'липня', 'серпня', 'вересня', 'жовтня', 'листопада', 'грудня'];
export const MONTHS_SHORT = ['січ', 'лют', 'бер', 'кві', 'тра', 'чер', 'лип', 'сер', 'вер', 'жов', 'лис', 'гру'];
const WEEKDAYS = ['неділя', 'понеділок', 'вівторок', 'середа', 'четвер', 'пʼятниця', 'субота'];

// ── Permissions: the web's PermissionService predicates, read off the same permission strings ──

/** The whole провід raises agenda items (web: canManageAgenda); the API decides who edits one. */
export function canManageAgenda(user: AuthState | null): boolean {
  return !!user && (user.isAdmin || user.permissions.some((p) => p.startsWith('AgendaItem:Create')));
}

/** Scores somewhere — the web's canScore, which shows «Точкування» on an event. */
export function canScore(user: AuthState | null): boolean {
  return !!user && (user.isAdmin || user.permissions.some((p) => p.startsWith('GroupScore:Create')));
}

// ── Days ──────────────────────────────────────────────────────────────────────────────────────

/** A local calendar day as «2026-10-07». */
export function dayKey(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** «2026-10-07» as local midnight. */
export function fromDayKey(key: string): Date {
  const [year, month, day] = key.split('-').map(Number);
  return new Date(year, month - 1, day);
}

export function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
}

/** «Жовтень 2026» */
export function monthTitle(month: Date): string {
  return `${MONTH_NAMES[month.getMonth()]} ${month.getFullYear()}`;
}

/** «середа, 7 жовтня» (the year only when it is not this one). */
export function longDayLabel(date: Date, today = new Date()): string {
  const year = date.getFullYear() === today.getFullYear() ? '' : ` ${date.getFullYear()}`;
  return `${WEEKDAYS[date.getDay()]}, ${date.getDate()} ${MONTHS_OF[date.getMonth()]}${year}`;
}

/** «7 жов» style short date: «7.10» for compact rows. */
export function shortDate(date: Date): string {
  return `${date.getDate()} ${MONTHS_OF[date.getMonth()]}`;
}

/**
 * The month as a phone calendar shows it: whole weeks from Monday, so 4 to 6 rows of 7 days,
 * the days of the neighbouring months included to fill the first and last week.
 */
export function monthGrid(month: Date): Date[][] {
  const first = new Date(month.getFullYear(), month.getMonth(), 1);
  const lead = (first.getDay() + 6) % 7; // days back to Monday
  const start = addDays(first, -lead);
  const last = new Date(month.getFullYear(), month.getMonth() + 1, 0);
  const trail = 6 - ((last.getDay() + 6) % 7);
  const end = addDays(last, trail);
  const weeks: Date[][] = [];
  for (let day = start; day <= end; day = addDays(day, 7)) {
    weeks.push(Array.from({ length: 7 }, (_, i) => addDays(day, i)));
  }
  return weeks;
}

/** The API window for a grid: its first day's midnight to the midnight after its last day. */
export function gridWindow(weeks: Date[][]): { fromUtc: string; toUtc: string } {
  const first = weeks[0][0];
  const last = weeks.at(-1)?.[6] ?? first;
  return { fromUtc: first.toISOString(), toUtc: addDays(last, 1).toISOString() };
}

/** The Monday-to-Sunday week a day falls in. */
export function weekOf(date: Date): Date[] {
  const monday = addDays(date, -((date.getDay() + 6) % 7));
  return Array.from({ length: 7 }, (_, i) => addDays(monday, i));
}

/** «5–11 жовтня», «28 вер – 4 жов»; the year only when the week is not in this one. */
export function weekTitle(week: Date[], today = new Date()): string {
  const first = week[0];
  const last = week.at(-1) ?? first;
  const thisYear = first.getFullYear() === today.getFullYear() && last.getFullYear() === today.getFullYear();
  if (first.getMonth() === last.getMonth()) {
    const year = thisYear ? '' : ` ${first.getFullYear()}`;
    return `${first.getDate()}–${last.getDate()} ${MONTHS_OF[first.getMonth()]}${year}`;
  }
  const yearOf = (date: Date) => (thisYear ? '' : ` ${date.getFullYear()}`);
  const short = (date: Date) => `${date.getDate()} ${MONTHS_SHORT[date.getMonth()]}${yearOf(date)}`;
  return `${short(first)} – ${short(last)}`;
}

/** A timed item on a day's hour grid: minutes from that day's midnight, and its lane among overlaps. */
export interface TimedBlock {
  item: AgendaItemDto;
  startMinute: number;
  endMinute: number;
  /** Which of `lanes` side-by-side columns it takes. */
  lane: number;
  lanes: number;
}

export interface DayTimeline {
  allDay: AgendaItemDto[];
  timed: TimedBlock[];
  /** The hours the grid shows: the usual day, stretched to whatever starts earlier or ends later. */
  fromHour: number;
  toHour: number;
}

/** Shortest height a block takes on the grid, so a 10-minute item is still a target. */
export const MIN_BLOCK_MINUTES = 30;

/**
 * One day as an hour grid lays it out (iOS Calendar's day view): all-day items, and items that
 * run through the whole day, on top; timed ones as blocks cut to the day, overlapping ones side by
 * side. `items` are the day's items as `itemsByDay` gives them.
 */
export function dayTimeline(items: AgendaItemDto[], day: string): DayTimeline {
  const midnight = fromDayKey(day).getTime();
  const nextMidnight = addDays(fromDayKey(day), 1).getTime();
  const allDay: AgendaItemDto[] = [];
  const spans: { item: AgendaItemDto; startMinute: number; endMinute: number }[] = [];
  for (const item of items) {
    if (!item.startUtc || item.isAllDay) {
      allDay.push(item);
      continue;
    }
    const start = new Date(item.startUtc).getTime();
    const end = item.endUtc ? new Date(item.endUtc).getTime() : start;
    if (start <= midnight && end >= nextMidnight) {
      allDay.push(item);
      continue;
    }
    const startMinute = Math.max(0, Math.round((start - midnight) / 60_000));
    const endMinute = Math.min(24 * 60, Math.round((end - midnight) / 60_000));
    spans.push({ item, startMinute, endMinute: Math.max(startMinute, endMinute) });
  }
  spans.sort((a, b) => a.startMinute - b.startMinute || b.endMinute - a.endMinute);

  // Overlapping blocks form a cluster; each takes the first lane free at its start.
  const timed: TimedBlock[] = [];
  let cluster: TimedBlock[] = [];
  let laneEnds: number[] = [];
  let clusterEnd = -1;
  const close = () => {
    for (const block of cluster) block.lanes = laneEnds.length;
    cluster = [];
    laneEnds = [];
  };
  for (const span of spans) {
    const shownEnd = Math.max(span.endMinute, span.startMinute + MIN_BLOCK_MINUTES);
    if (span.startMinute >= clusterEnd) close();
    let lane = laneEnds.findIndex((end) => end <= span.startMinute);
    if (lane < 0) lane = laneEnds.push(shownEnd) - 1;
    else laneEnds[lane] = shownEnd;
    const block: TimedBlock = { ...span, lane, lanes: 1 };
    cluster.push(block);
    timed.push(block);
    clusterEnd = Math.max(clusterEnd, shownEnd);
  }
  close();

  const earliest = Math.min(24 * 60, ...timed.map((b) => b.startMinute));
  const latest = Math.max(0, ...timed.map((b) => Math.max(b.endMinute, b.startMinute + MIN_BLOCK_MINUTES)));
  return {
    allDay,
    timed,
    fromHour: Math.min(8, Math.floor(earliest / 60)),
    toHour: Math.min(24, Math.max(21, Math.ceil(latest / 60))),
  };
}

/**
 * The local days an item covers. An all-day item is stored as UTC midnight of its day and is read
 * by the date alone (every viewer sees the same day); a timed one by its instants in local time.
 * An end exactly at midnight does not reach into that day.
 */
export function itemDays(item: Pick<AgendaItemDto, 'startUtc' | 'endUtc' | 'isAllDay'>): string[] {
  if (!item.startUtc) return [];
  let first: Date;
  let last: Date;
  if (item.isAllDay) {
    first = fromDayKey(item.startUtc.slice(0, 10));
    last = item.endUtc ? fromDayKey(item.endUtc.slice(0, 10)) : first;
  } else {
    const start = new Date(item.startUtc);
    first = new Date(start.getFullYear(), start.getMonth(), start.getDate());
    last = first;
    if (item.endUtc) {
      const end = new Date(new Date(item.endUtc).getTime() - 1);
      if (end > start) last = new Date(end.getFullYear(), end.getMonth(), end.getDate());
    }
  }
  const days: string[] = [];
  // Bounded so a broken range cannot hang the screen.
  for (let day = first; day <= last && days.length < 400; day = addDays(day, 1)) days.push(dayKey(day));
  return days;
}

/** Items by the days they cover, each day in start order (all-day first). */
export function itemsByDay(items: AgendaItemDto[]): Map<string, AgendaItemDto[]> {
  const byDay = new Map<string, AgendaItemDto[]>();
  for (const item of items) {
    for (const day of itemDays(item)) {
      const list = byDay.get(day) ?? [];
      list.push(item);
      byDay.set(day, list);
    }
  }
  for (const list of byDay.values()) list.sort(byStart);
  return byDay;
}

function byStart(a: AgendaItemDto, b: AgendaItemDto): number {
  if (a.isAllDay !== b.isAllDay) return a.isAllDay ? -1 : 1;
  return (a.startUtc ?? '').localeCompare(b.startUtc ?? '') || a.title.localeCompare(b.title, 'uk');
}

/** The occurrence a row stands for: start and key together (a series has one row per occurrence). */
export function occurrenceId(item: AgendaItemDto): string {
  return `${item.agendaItemKey}|${item.startUtc ?? ''}`;
}

/** The answer belongs to the occurrence: a series row carries its own start, a one-off has none. */
export function occurrenceOf(item: AgendaItemDto): string | null {
  return item.isRecurrenceInstance ? item.startUtc : null;
}

// ── Wording ───────────────────────────────────────────────────────────────────────────────────

export function clock(date: Date): string {
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** The time column of a day's row: «весь день», «17:00», or «до 19:00» on a later day of a span. */
export function rowTime(item: AgendaItemDto, day: string): { top: string; bottom: string } {
  if (!item.startUtc || item.isAllDay) return { top: 'весь день', bottom: '' };
  const start = new Date(item.startUtc);
  const end = item.endUtc ? new Date(item.endUtc) : null;
  const startsToday = dayKey(start) === day;
  const endsToday = end ? dayKey(new Date(end.getTime() - 1)) === day : true;
  if (startsToday) return { top: clock(start), bottom: end && endsToday ? clock(end) : '' };
  if (end && endsToday) return { top: 'до', bottom: clock(end) };
  return { top: 'весь день', bottom: '' };
}

/**
 * When an item happens, as the details screen says it: the day (or days) on the first line and the
 * hours on the second. All-day items by the date alone.
 */
export function whenLines(item: Pick<AgendaItemDto, 'startUtc' | 'endUtc' | 'isAllDay'>, today = new Date()): string[] {
  if (!item.startUtc) return [];
  if (item.isAllDay) {
    const start = fromDayKey(item.startUtc.slice(0, 10));
    const end = item.endUtc ? fromDayKey(item.endUtc.slice(0, 10)) : null;
    if (!end || dayKey(end) === dayKey(start)) return [longDayLabel(start, today), 'Весь день'];
    return [`${longDayLabel(start, today)} –`, longDayLabel(end, today)];
  }
  const start = new Date(item.startUtc);
  const end = item.endUtc ? new Date(item.endUtc) : null;
  if (!end) return [longDayLabel(start, today), clock(start)];
  if (dayKey(end) === dayKey(start)) return [longDayLabel(start, today), `${clock(start)}–${clock(end)}`];
  return [`${longDayLabel(start, today)}, ${clock(start)} –`, `${longDayLabel(end, today)}, ${clock(end)}`];
}

/** «Щотижня: пн, ср · до 20 грудня 2026», «Кожні 2 місяці». */
export function recurrenceLabel(item: Pick<AgendaItemDto, 'recurrenceFrequency' | 'recurrenceInterval' | 'recurrenceByWeekday' | 'recurrenceEndUtc'>): string {
  const frequency = item.recurrenceFrequency;
  if (!frequency || frequency === 'None') return '';
  const every = Math.max(1, item.recurrenceInterval || 1);
  const units: Record<Exclude<RecurrenceFrequency, 'None'>, [string, string]> = {
    Weekly: ['Щотижня', 'тижні'],
    Monthly: ['Щомісяця', 'місяці'],
    Yearly: ['Щороку', 'роки'],
  };
  const [single, plural] = units[frequency];
  let text = every === 1 ? single : `Кожні ${every} ${plural}`;
  if (frequency === 'Weekly' && item.recurrenceByWeekday) {
    const days = WEEKDAY_BITS.filter((d) => item.recurrenceByWeekday & d.bit).map((d) => d.label.toLowerCase());
    text += `: ${days.join(', ')}`;
  }
  if (item.recurrenceEndUtc) {
    const end = fromDayKey(item.recurrenceEndUtc.slice(0, 10));
    text += ` · до ${end.getDate()} ${MONTHS_OF[end.getMonth()]} ${end.getFullYear()}`;
  }
  return text;
}

/** «Соколи, Орли» — whom the item is for. */
export function targetsLabel(item: AgendaItemDto): string {
  return item.assignments.map((a) => a.label).filter(Boolean).join(', ');
}

/** «Графік куреня · Соколи» — whose schedule an event seen only through it belongs to. */
export function scheduleOf(item: AgendaItemDto): string | null {
  if (item.audience !== 'Schedule') return null;
  return targetsLabel(item) || null;
}

/** The due day on a task card: the end, else the start. */
export function dueOf(item: AgendaItemDto): Date | null {
  const due = item.endUtc ?? item.startUtc;
  if (!due) return null;
  return item.isAllDay ? fromDayKey(due.slice(0, 10)) : new Date(due);
}

/** Overdue: an open task whose due day is behind us. */
export function isLate(item: AgendaItemDto, now = new Date()): boolean {
  const due = dueOf(item);
  if (!due || item.viewerStatus === 'Done') return false;
  return item.isAllDay ? dayKey(due) < dayKey(now) : due < now;
}

/** «до 12 жовтня» on a task card, «прострочено, 9 жовтня» once it is late. */
export function dueLabel(item: AgendaItemDto, now = new Date()): string {
  const due = dueOf(item);
  if (!due) return '';
  const day = due.getFullYear() === now.getFullYear() ? shortDate(due) : `${shortDate(due)} ${due.getFullYear()}`;
  return isLate(item, now) ? `прострочено, ${day}` : `до ${day}`;
}

/** «Соколи · 3 з 8» / «закрито: Богдан» — the card's note for one target. */
export function targetNote(assignment: AgendaAssignmentDto): string | null {
  if (assignment.peopleCount !== null) return `${assignment.doneCount ?? 0} з ${assignment.peopleCount}`;
  if (assignment.status === 'Done' && assignment.statusChangedByName) return `закрито: ${assignment.statusChangedByName}`;
  return null;
}

/** «10.10, 14:05» */
export function stamp(iso: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}, ${clock(date)}`;
}

// ── Form values ───────────────────────────────────────────────────────────────────────────────
// ion-datetime speaks local wall time without a zone («2026-10-07T17:00»); the API speaks UTC.

/** A stored value as the picker's local wall time: an all-day date by its calendar day. */
export function toLocalValue(iso: string | null, allDay: boolean): string | null {
  if (!iso) return null;
  if (allDay) return `${iso.slice(0, 10)}T00:00`;
  const date = new Date(iso);
  return `${dayKey(date)}T${clock(date)}`;
}

/**
 * What the API stores: all-day items as UTC midnight of the chosen day (no drift between viewers),
 * timed ones as the real instant of the local wall time.
 */
export function toWire(local: string | null, allDay: boolean): string | null {
  if (!local) return null;
  if (allDay) return utcMidnight(local);
  return new Date(local.length === 16 ? `${local}:00` : local).toISOString();
}

/** «2026-10-07…» as that day's UTC midnight. */
export function utcMidnight(local: string): string {
  return `${local.slice(0, 10)}T00:00:00.000Z`;
}

/** A local wall time moved by minutes, kept in the picker's format. */
export function addMinutes(local: string, minutes: number): string {
  const date = new Date(new Date(local.length === 16 ? `${local}:00` : local).getTime() + minutes * 60_000);
  return `${dayKey(date)}T${clock(date)}`;
}

/** The picker's value for a fresh item: that day at the next whole hour (or the day, for all-day). */
export function defaultStart(day: string | null, now = new Date()): string {
  if (day && day !== dayKey(now)) return `${day}T10:00`;
  const next = new Date(now.getFullYear(), now.getMonth(), now.getDate(), now.getHours() + 1);
  return `${dayKey(next)}T${clock(next)}`;
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}


// ── Archive ───────────────────────────────────────────────────────────────────────────────────

/** «10.10.2026» */
export function shortStamp(date: Date): string {
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}`;
}

/** «В архіві з 10.10.2026 (Богдан Гончар)», «(автоматично)» when the nightly sweep put it there. */
export function archivedLabel(item: Pick<AgendaItemDto, 'archivedAtUtc' | 'archivedByName'>): string {
  if (!item.archivedAtUtc) return '';
  return `В архіві з ${shortStamp(new Date(item.archivedAtUtc))} (${item.archivedByName || 'автоматично'})`;
}

/** When the nightly sweep deletes an archived task by the kurin's rules; null when it is kept. */
export function purgeDate(item: Pick<AgendaItemDto, 'archivedAtUtc'>, purgeAfterDays: number | null): Date | null {
  if (!purgeAfterDays || !item.archivedAtUtc) return null;
  return new Date(new Date(item.archivedAtUtc).getTime() + purgeAfterDays * 86_400_000);
}

/** The web's line over the archive: how tasks get here and how long they stay. */
export function archivePolicyLabel(policy: { autoArchiveAfterDays: number | null; purgeAfterDays: number | null }): string {
  const into = policy.autoArchiveAfterDays
    ? `Зроблені задачі переходять сюди через ${policy.autoArchiveAfterDays} дн.`
    : 'Автоматичний архів вимкнено.';
  const out = policy.purgeAfterDays
    ? `Видаляються назавжди через ${policy.purgeAfterDays} дн. в архіві.`
    : 'З архіву нічого не видаляється.';
  return `${into} ${out}`;
}

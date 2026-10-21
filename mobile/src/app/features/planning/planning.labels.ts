import { AuthState } from '../../auth/auth.models';
import { CreateAgendaItemRequest } from '../agenda/agenda.models';
import {
  MONTHS_OF,
  MONTH_NAMES,
  addDays,
  dayKey,
  fromDayKey,
  utcMidnight,
} from '../agenda/agenda.labels';
import { utcDate } from '../kurin/kurin.labels';
import { DateRangeDto, PlanningSessionDto, ROLE_WEIGHTS } from './planning.models';

const DAY_MS = 86_400_000;

/** Opening a planning session: the web's canCreatePlanning (the whole провід, each in their scope). */
export function canCreatePlanning(user: AuthState | null): boolean {
  return (
    !!user && (user.isAdmin || user.permissions.some((p) => p.startsWith('PlanningSession:Create')))
  );
}

// ── Days ──────────────────────────────────────────────────────────────────────────────────────

/** The local day an instant falls on, as that day's midnight. */
export function localDay(iso: string): Date {
  const date = utcDate(iso) ?? new Date(Number.NaN);
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

/**
 * The days a span covers. Its end is exclusive, as the solver reads it (a 10-day camp from 1 July
 * ends at midnight on the 11th, a busy range up to midnight on a day leaves that day free); a span
 * with no length stands for its first day.
 */
export function spanDays(range: DateRangeDto): { first: Date; last: Date } {
  const start = utcDate(range.start)!;
  const end = utcDate(range.end)!;
  const first = localDay(range.start);
  if (end.getTime() <= start.getTime()) return { first, last: first };
  return { first, last: localDay(new Date(end.getTime() - 1).toISOString()) };
}

/** «5 липня», «5–14 липня», «28 червня – 3 липня»; the year when it is not this one or the span crosses one. */
export function dayRange(first: Date, last: Date, today = new Date()): string {
  const crosses = first.getFullYear() !== last.getFullYear();
  const year = (date: Date) =>
    crosses || date.getFullYear() !== today.getFullYear() ? ` ${date.getFullYear()}` : '';
  const day = (date: Date) => `${date.getDate()} ${MONTHS_OF[date.getMonth()]}`;
  if (dayKey(first) === dayKey(last)) return `${day(first)}${year(first)}`;
  if (!crosses && first.getMonth() === last.getMonth()) {
    return `${first.getDate()}–${last.getDate()} ${MONTHS_OF[last.getMonth()]}${year(last)}`;
  }
  return crosses
    ? `${day(first)}${year(first)} – ${day(last)}${year(last)}`
    : `${day(first)} – ${day(last)}${year(last)}`;
}

/** The search window as the web shows it: both ends are days of it. */
export function windowLabel(session: PlanningSessionDto, today = new Date()): string {
  return dayRange(localDay(session.searchStart), localDay(session.searchEnd), today);
}

/** The camp's days, once the server has worked them out. */
export function optimalDays(session: PlanningSessionDto): { first: Date; last: Date } | null {
  if (!session.isCalculated || !session.optimalStartDate) return null;
  return spanDays({
    start: session.optimalStartDate,
    end: session.optimalEndDate ?? session.optimalStartDate,
  });
}

/** «1 день», «3 дні», «10 днів». */
export function daysLabel(count: number): string {
  return `${count} ${plural(count, 'день', 'дні', 'днів')}`;
}

/** «1 учасник», «3 учасники», «12 учасників». */
export function participantsLabel(count: number): string {
  return `${count} ${plural(count, 'учасник', 'учасники', 'учасників')}`;
}

function plural(count: number, one: string, few: string, many: string): string {
  const tens = count % 100;
  const ones = count % 10;
  if (tens >= 11 && tens <= 14) return many;
  if (ones === 1) return one;
  return ones >= 2 && ones <= 4 ? few : many;
}

export function weightLabel(value: number): string {
  return ROLE_WEIGHTS.find((weight) => weight.value === value)?.label ?? String(value);
}

/** Who has something on during the chosen days, by the solver's own overlap rule. */
export function busyDuringCamp(session: PlanningSessionDto): string[] {
  if (!session.isCalculated || !session.optimalStartDate || !session.optimalEndDate) return [];
  const start = utcDate(session.optimalStartDate)!.getTime();
  const end = utcDate(session.optimalEndDate)!.getTime();
  return session.participants
    .filter((p) =>
      p.busyRanges.some(
        (r) => utcDate(r.start)!.getTime() < end && start < utcDate(r.end)!.getTime(),
      ),
    )
    .map((p) => p.fullName);
}

// ── The availability strip ───────────────────────────────────────────────────────────────────

/** The strip's time axis: one column per day of the search window. */
export interface StripAxis {
  /** The first day's local midnight. */
  start: number;
  days: Date[];
}

export function stripAxis(session: PlanningSessionDto): StripAxis {
  const first = localDay(session.searchStart);
  const last = localDay(session.searchEnd);
  const days: Date[] = [];
  for (let day = first; day <= last && days.length < 732; day = addDays(day, 1)) days.push(day);
  if (!days.length) days.push(first);
  return { start: first.getTime(), days };
}

/** Where a span sits on the strip, in days from its left edge; null when it misses the window. */
export function stripBar(
  axis: StripAxis,
  range: DateRangeDto,
): { left: number; width: number } | null {
  const at = (iso: string) => (utcDate(iso)!.getTime() - axis.start) / DAY_MS;
  const left = Math.max(0, at(range.start));
  const right = Math.min(axis.days.length, at(range.end));
  return right - left > 0.01 ? { left, width: right - left } : null;
}

/** The month headings over the strip: where each month's days start and how many it has there. */
export function stripMonths(axis: StripAxis): { label: string; from: number; span: number }[] {
  const months: { label: string; from: number; span: number }[] = [];
  axis.days.forEach((day, index) => {
    const last = months.at(-1);
    const label = MONTH_NAMES[day.getMonth()];
    if (last && day.getDate() !== 1) last.span++;
    else months.push({ label, from: index, span: 1 });
  });
  return months;
}

// ── The new session ──────────────────────────────────────────────────────────────────────────

/** The web's starting point, June to August, but of the coming summer once this one is over. */
export function defaultWindow(today = new Date()): { name: string; from: string; to: string } {
  const year = today.getMonth() >= 8 ? today.getFullYear() + 1 : today.getFullYear();
  return {
    name: `Табір ${year}`,
    from: dayKey(new Date(year, 5, 1)),
    to: dayKey(new Date(year, 7, 31)),
  };
}

/** A picked day as the web sends its picker's dates: the local midnight, as an instant. */
export function dayToWire(key: string): string {
  return fromDayKey(key).toISOString();
}

/**
 * A busy range picked as «from – to», both days included: up to the midnight after `to`, so the
 * last day counts as busy for the solver (the web sends that day's own midnight and loses it).
 */
export function busyToWire(from: string, to: string): DateRangeDto {
  return { start: dayToWire(from), end: addDays(fromDayKey(to), 1).toISOString() };
}

/** What stops the window from being searched, if anything. */
export function windowError(from: string, to: string, durationDays: number): string | null {
  if (to < from) return 'Кінець вікна не може бути раніше за початок.';
  const length = Math.round((fromDayKey(to).getTime() - fromDayKey(from).getTime()) / DAY_MS) + 1;
  if (length < durationDays)
    return `Вікно коротше за табір: ${daysLabel(length)} замість ${daysLabel(durationDays)}.`;
  return null;
}

/**
 * «Перенести в календар»: the web's kurin-wide all-day event named after the session. The days go
 * as the agenda keeps all-day items, UTC midnights of the first and the last day.
 */
export function calendarRequest(session: PlanningSessionDto): CreateAgendaItemRequest | null {
  const days = optimalDays(session);
  if (!days) return null;
  return {
    kurinKey: session.kurinKey,
    kind: 'Event',
    title: session.name,
    description: 'Створено з планування табору',
    location: null,
    startUtc: utcMidnight(dayKey(days.first)),
    endUtc: utcMidnight(dayKey(days.last)),
    isAllDay: true,
    agendaCategoryKey: null,
    recurrenceFrequency: 'None',
    recurrenceInterval: 1,
    recurrenceByWeekday: 0,
    recurrenceEndUtc: null,
    recurrenceCount: null,
    targets: [{ targetType: 'Kurin', targetKey: session.kurinKey }],
  };
}

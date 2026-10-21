import { Params } from '@angular/router';
import {
  SCORE_SOURCE_ORDER,
  ScorePeriodDto,
  ScorePeriodQuery,
  ScorePeriodsDto,
  ScorePersonRowDto,
  ScoreSource,
  SheetPersonDto,
} from './score.models';

// The web's scoreModule/functions/score-format.function.ts, plus the sheet's shelves.

const decimal = new Intl.NumberFormat('uk-UA', { minimumFractionDigits: 0, maximumFractionDigits: 2 });

/** «12,5» — a гурток's score, which may be an average. A real minus, not a hyphen. */
export function score(value: number): string {
  return value < 0 ? `−${decimal.format(Math.abs(value))}` : decimal.format(value);
}

/** «+3», «−1», «0» — points given or taken. */
export function points(value: number): string {
  if (value > 0) return `+${decimal.format(value)}`;
  return value < 0 ? `−${decimal.format(Math.abs(value))}` : '0';
}

/** One string a select can hold for a period: «year:2026» or «stage:<key>». */
export function periodValue(period: ScorePeriodDto): string {
  return period.kind === 'Year' ? `year:${period.year}` : `stage:${period.stageKey}`;
}

export function periodQueryOf(value: string | null | undefined): ScorePeriodQuery {
  if (!value) return {};
  const [kind, rest] = value.split(':');
  return kind === 'stage' ? { stageKey: rest } : { year: Number(rest) };
}

/** The query string the pages carry the chosen period in, so a link between them keeps it. */
export function periodParams(query: ScorePeriodQuery): Params {
  if (query.stageKey) return { stage: query.stageKey };
  return query.year ? { year: query.year } : {};
}

export function periodQueryFromParams(params: Params): ScorePeriodQuery {
  if (params['stage']) return { stageKey: String(params['stage']) };
  const year = Number(params['year']);
  return Number.isFinite(year) && year > 0 ? { year } : {};
}

/** What the API reads the period from: `stageKey` wins over `year`, neither is «now». */
export function periodApiQuery(query: ScorePeriodQuery): Record<string, string> {
  if (query.stageKey) return { stageKey: query.stageKey };
  return query.year ? { year: String(query.year) } : {};
}

export interface PeriodOption {
  value: string;
  label: string;
}

/** Years first, then stages; a stage says it is one. Nothing to pick while there is only one. */
export function periodOptions(periods: ScorePeriodsDto): PeriodOption[] {
  const years = periods.years.map((p) => ({ value: periodValue(p), label: p.label }));
  const stages = periods.stages.map((p) => ({ value: periodValue(p), label: `Етап · ${p.label}` }));
  const all = [...years, ...stages];
  return all.length > 1 ? all : [];
}

/** Only the sources the person has points from, in the web's order: a row of zeros says nothing. */
export function personSources(person: ScorePersonRowDto): { source: ScoreSource; value: number }[] {
  return SCORE_SOURCE_ORDER.map((source) => ({ source, value: person.bySource[source] ?? 0 })).filter(
    (row) => row.value !== 0,
  );
}

/** The three shelves of the sheet, in the order a суддя works through them. */
export type Shelf = 'answered' | 'assigned' | 'others';

export function shelfOf(person: SheetPersonDto): Shelf {
  if (person.rsvp === 'Going' || person.rsvp === 'Maybe') return 'answered';
  return person.isAssigned ? 'assigned' : 'others';
}

export function rsvpLabel(person: SheetPersonDto): string | null {
  switch (person.rsvp) {
    case 'Going':
      return 'іде';
    case 'Maybe':
      return 'можливо';
    case 'NotGoing':
      return 'не іде';
    default:
      return null;
  }
}

/** «mine» — the гуртки I score; «all» — the whole kurin; else one гурток's key. */
export type SheetGroupFilter = string;

function inGroup(person: SheetPersonDto, group: SheetGroupFilter): boolean {
  if (group === 'mine') return person.canScore;
  return group === 'all' || person.groupKey === group;
}

/** Who the sheet lists: by гурток, then by name, case-insensitive. */
export function filterSheet(people: SheetPersonDto[], group: SheetGroupFilter, term: string): SheetPersonDto[] {
  const needle = term.trim().toLocaleLowerCase('uk');
  return people
    .filter((p) => inGroup(p, group))
    .filter((p) => !needle || p.fullName.toLocaleLowerCase('uk').includes(needle));
}

/** «2026-10-10» in the phone's own day, as the API's DateOnly takes it. */
export function dateOnly(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** A DateOnly «2026-10-10» as a local date, or an instant's local day. */
export function parseDay(value: string): Date {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : new Date(value);
}

/** «10.10» / «10.10.2026» the way the web's tables show dates. */
export function shortDate(value: string, withYear = false): string {
  const date = parseDay(value);
  const dm = `${String(date.getDate()).padStart(2, '0')}.${String(date.getMonth() + 1).padStart(2, '0')}`;
  return withYear ? `${dm}.${date.getFullYear()}` : dm;
}

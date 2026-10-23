import {
  dateOnly,
  filterSheet,
  notHeldLine,
  parseDay,
  periodApiQuery,
  periodOptions,
  periodParams,
  periodQueryFromParams,
  periodQueryOf,
  periodValue,
  personSources,
  points,
  score,
  shelfOf,
  shortDate,
} from './score.format';
import { ScorePeriodDto, ScorePersonRowDto, SheetPersonDto } from './score.models';

const year = (y: number): ScorePeriodDto => ({ kind: 'Year', year: y, stageKey: null, label: `${y - 1}/${y}`, from: '', to: '' });
const stage = (key: string, label: string): ScorePeriodDto => ({ kind: 'Stage', year: null, stageKey: key, label, from: '', to: '' });

function person(over: Partial<SheetPersonDto>): SheetPersonDto {
  return {
    membershipKey: 'ms', memberKey: 'm', fullName: 'Остап Коваль', groupKey: 'g1', groupName: 'Соколи',
    rsvp: null, isAssigned: false, attendance: null, canScore: true, entries: [], ...over,
  };
}

describe('score format', () => {
  it('writes points with a sign and a real minus', () => {
    expect(points(3)).toBe('+3');
    expect(points(-1)).toBe('−1');
    expect(points(0)).toBe('0');
    expect(score(12.5)).toBe('12,5');
    expect(score(-2)).toBe('−2');
  });

  it('carries a period through a select, a query string and the API', () => {
    expect(periodValue(year(2026))).toBe('year:2026');
    expect(periodValue(stage('s1', 'Осінь'))).toBe('stage:s1');
    expect(periodQueryOf('stage:s1')).toEqual({ stageKey: 's1' });
    expect(periodQueryOf('year:2025')).toEqual({ year: 2025 });
    expect(periodQueryOf(null)).toEqual({});
    expect(periodParams({ stageKey: 's1', year: 2025 })).toEqual({ stage: 's1' });
    expect(periodParams({ year: 2025 })).toEqual({ year: 2025 });
    expect(periodQueryFromParams({ year: '2025' })).toEqual({ year: 2025 });
    expect(periodQueryFromParams({ year: 'x' })).toEqual({});
    expect(periodQueryFromParams({ stage: 's1' })).toEqual({ stageKey: 's1' });
    expect(periodApiQuery({ year: 2025 })).toEqual({ year: '2025' });
    expect(periodApiQuery({})).toEqual({});
  });

  it('offers no choice of one period', () => {
    expect(periodOptions({ years: [year(2026)], stages: [] })).toEqual([]);
    expect(periodOptions({ years: [year(2026), year(2025)], stages: [stage('s1', 'Осінь')] })).toEqual([
      { value: 'year:2026', label: '2025/2026' },
      { value: 'year:2025', label: '2024/2025' },
      { value: 'stage:s1', label: 'Етап · Осінь' },
    ]);
  });

  it('lists only the sources a person has points from, in order', () => {
    const row: ScorePersonRowDto = {
      membershipKey: 'ms', memberKey: 'm', fullName: 'X', standing: 'Current', total: 4,
      bySource: { Skill: 2, Attendance: 3, Free: 0, Warning: -1 },
    };
    expect(personSources(row)).toEqual([
      { source: 'Attendance', value: 3 },
      { source: 'Skill', value: 2 },
      { source: 'Warning', value: -1 },
    ]);
  });

  it('shelves the sheet: answered first, then assigned, then the rest', () => {
    expect(shelfOf(person({ rsvp: 'Going' }))).toBe('answered');
    expect(shelfOf(person({ rsvp: 'Maybe', isAssigned: true }))).toBe('answered');
    expect(shelfOf(person({ rsvp: 'NotGoing', isAssigned: true }))).toBe('assigned');
    expect(shelfOf(person({ rsvp: 'NotGoing' }))).toBe('others');
  });

  it('filters the sheet by гурток and name', () => {
    const people = [
      person({ membershipKey: 'a', fullName: 'Андрій Мельник', groupKey: 'g1' }),
      person({ membershipKey: 'b', fullName: 'Богдан Шевчук', groupKey: 'g2', canScore: false }),
    ];
    expect(filterSheet(people, 'mine', '').map((p) => p.membershipKey)).toEqual(['a']);
    expect(filterSheet(people, 'all', '').map((p) => p.membershipKey)).toEqual(['a', 'b']);
    expect(filterSheet(people, 'g2', '').map((p) => p.membershipKey)).toEqual(['b']);
    expect(filterSheet(people, 'all', 'шев').map((p) => p.membershipKey)).toEqual(['b']);
  });

  it('keeps days in the phone’s own calendar', () => {
    expect(dateOnly(new Date(2026, 9, 5))).toBe('2026-10-05');
    expect(parseDay('2026-10-05').getDate()).toBe(5);
    expect(shortDate('2026-10-05')).toBe('05.10');
    expect(shortDate('2026-10-05', true)).toBe('05.10.2026');
  });

  it('says who called the meeting off, and when, in the phone’s own time', () => {
    const at = new Date(2026, 9, 6, 19, 5);
    expect(notHeldLine({ markedByName: 'Гуртковий', markedAtUtc: at.toISOString() })).toBe('Гуртковий, 6 жовтня 2026, 19:05');
    expect(notHeldLine({ markedByName: null, markedAtUtc: at.toISOString() })).toMatch(/^хтось із проводу, /);
  });
});

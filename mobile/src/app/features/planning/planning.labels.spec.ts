import { AuthState } from '../../auth/auth.models';
import { PlanningSessionDto } from './planning.models';
import {
  busyDuringCamp,
  busyToWire,
  calendarRequest,
  canCreatePlanning,
  dayRange,
  dayToWire,
  daysLabel,
  defaultWindow,
  optimalDays,
  participantsLabel,
  spanDays,
  stripAxis,
  stripBar,
  stripMonths,
  windowError,
  windowLabel,
} from './planning.labels';

/** A local day's midnight as the instant the API carries (how the web sends picked dates). */
const midnight = (year: number, month: number, day: number) => new Date(year, month - 1, day).toISOString();

function session(overrides: Partial<PlanningSessionDto> = {}): PlanningSessionDto {
  return {
    planningSessionKey: 's1',
    name: 'Табір 2027',
    kurinKey: 'k1',
    searchStart: midnight(2027, 6, 1),
    searchEnd: midnight(2027, 8, 31),
    durationDays: 10,
    isCalculated: true,
    optimalStartDate: midnight(2027, 7, 5),
    optimalEndDate: midnight(2027, 7, 15),
    conflictScore: 0,
    canDelete: false,
    participants: [
      { memberKey: 'm1', fullName: 'Остап Коваль', roleWeight: 3, busyRanges: [{ start: midnight(2027, 7, 14), end: midnight(2027, 7, 20) }] },
      { memberKey: 'm2', fullName: 'Ірина Бойко', roleWeight: 5, busyRanges: [{ start: midnight(2027, 6, 1), end: midnight(2027, 7, 5) }] },
    ],
    ...overrides,
  };
}

const today = new Date(2027, 2, 10);

describe('canCreatePlanning', () => {
  const user = (permissions: string[], isAdmin = false) => ({ isAdmin, permissions }) as AuthState;

  it('follows the web: the planning grant or an administrator', () => {
    expect(canCreatePlanning(user(['PlanningSession:Create:OwnGroups']))).toBe(true);
    expect(canCreatePlanning(user([], true))).toBe(true);
    expect(canCreatePlanning(user(['PlanningSession:Read:KurinWide']))).toBe(false);
    expect(canCreatePlanning(null)).toBe(false);
  });
});

describe('days of a span', () => {
  it('reads the end as exclusive, as the solver does', () => {
    const { first, last } = spanDays({ start: midnight(2027, 7, 5), end: midnight(2027, 7, 15) });
    expect([first.getDate(), last.getDate()]).toEqual([5, 14]);
  });

  it('takes a span with no length for its first day', () => {
    const { first, last } = spanDays({ start: midnight(2027, 6, 10), end: midnight(2027, 6, 10) });
    expect([first.getDate(), last.getDate()]).toEqual([10, 10]);
  });

  it('gives the camp’s days only once they are worked out', () => {
    expect(optimalDays(session({ isCalculated: false }))).toBeNull();
    expect(optimalDays(session())?.last.getDate()).toBe(14);
  });
});

describe('dayRange', () => {
  it('names one day, one month, two months and two years', () => {
    expect(dayRange(new Date(2027, 6, 5), new Date(2027, 6, 5), today)).toBe('5 липня');
    expect(dayRange(new Date(2027, 6, 5), new Date(2027, 6, 14), today)).toBe('5–14 липня');
    expect(dayRange(new Date(2027, 5, 28), new Date(2027, 6, 3), today)).toBe('28 червня – 3 липня');
    expect(dayRange(new Date(2027, 11, 28), new Date(2028, 0, 3), today)).toBe('28 грудня 2027 – 3 січня 2028');
  });

  it('adds the year when it is not this one', () => {
    expect(dayRange(new Date(2028, 6, 5), new Date(2028, 6, 14), today)).toBe('5–14 липня 2028');
  });

  it('keeps both ends of the search window', () => {
    expect(windowLabel(session(), today)).toBe('1 червня – 31 серпня');
  });
});

describe('plurals', () => {
  it('counts days and people the Ukrainian way', () => {
    expect([1, 3, 5, 11, 21, 22].map(daysLabel)).toEqual(['1 день', '3 дні', '5 днів', '11 днів', '21 день', '22 дні']);
    expect([1, 2, 12].map(participantsLabel)).toEqual(['1 учасник', '2 учасники', '12 учасників']);
  });
});

describe('busyDuringCamp', () => {
  it('names who overlaps the chosen days, by the solver’s rule', () => {
    // Ірина is busy up to the camp’s first midnight: free. Остап from the 14th: busy.
    expect(busyDuringCamp(session())).toEqual(['Остап Коваль']);
    expect(busyDuringCamp(session({ isCalculated: false }))).toEqual([]);
  });
});

describe('the strip', () => {
  const axis = stripAxis(session());

  it('has a column for every day of the window, both ends in', () => {
    expect(axis.days).toHaveLength(30 + 31 + 31);
    expect(axis.days[0].getDate()).toBe(1);
    expect(axis.days[axis.days.length - 1].getDate()).toBe(31);
  });

  it('puts a span at its days and cuts it to the window', () => {
    const camp = stripBar(axis, { start: midnight(2027, 7, 5), end: midnight(2027, 7, 15) })!;
    expect(Math.round(camp.left)).toBe(34);
    expect(Math.round(camp.width)).toBe(10);
    const early = stripBar(axis, { start: midnight(2027, 5, 20), end: midnight(2027, 6, 3) })!;
    expect(early.left).toBe(0);
    expect(Math.round(early.width)).toBe(2);
    expect(stripBar(axis, { start: midnight(2027, 9, 2), end: midnight(2027, 9, 5) })).toBeNull();
  });

  it('heads each month where its days start', () => {
    expect(stripMonths(axis)).toEqual([
      { label: 'Червень', from: 0, span: 30 },
      { label: 'Липень', from: 30, span: 31 },
      { label: 'Серпень', from: 61, span: 31 },
    ]);
  });
});

describe('a new session', () => {
  it('starts on this summer, or the next once this one is over', () => {
    expect(defaultWindow(new Date(2027, 2, 10))).toEqual({ name: 'Табір 2027', from: '2027-06-01', to: '2027-08-31' });
    expect(defaultWindow(new Date(2027, 9, 11))).toEqual({ name: 'Табір 2028', from: '2028-06-01', to: '2028-08-31' });
  });

  it('sends days as the web does, and a busy range through its last day', () => {
    expect(dayToWire('2027-06-01')).toBe(midnight(2027, 6, 1));
    expect(busyToWire('2027-06-10', '2027-06-15')).toEqual({ start: midnight(2027, 6, 10), end: midnight(2027, 6, 16) });
  });

  it('refuses a window that ends first or cannot hold the camp', () => {
    expect(windowError('2027-06-10', '2027-06-01', 10)).toBe('Кінець вікна не може бути раніше за початок.');
    expect(windowError('2027-06-01', '2027-06-05', 10)).toBe('Вікно коротше за табір: 5 днів замість 10 днів.');
    expect(windowError('2027-06-01', '2027-06-10', 10)).toBeNull();
  });
});

describe('calendarRequest', () => {
  it('makes the web’s kurin-wide all-day event over the camp’s days', () => {
    expect(calendarRequest(session())).toMatchObject({
      kurinKey: 'k1',
      kind: 'Event',
      title: 'Табір 2027',
      description: 'Створено з планування табору',
      isAllDay: true,
      startUtc: '2027-07-05T00:00:00.000Z',
      endUtc: '2027-07-14T00:00:00.000Z',
      recurrenceFrequency: 'None',
      targets: [{ targetType: 'Kurin', targetKey: 'k1' }],
    });
    expect(calendarRequest(session({ isCalculated: false }))).toBeNull();
  });
});

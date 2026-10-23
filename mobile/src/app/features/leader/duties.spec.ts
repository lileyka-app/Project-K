import { MyDutyDto } from '../../me/me.models';
import { dutiesSpanKurins, dutyRows } from './duties';

function duty(overrides: Partial<MyDutyDto>): MyDutyDto {
  return {
    kind: 'BadgesToReview',
    kurin: { kurinKey: 'k1', kurinNumber: 1, namedAfter: null, isCurrent: true },
    count: 1,
    groupKey: null,
    groupName: null,
    agendaItemKey: null,
    occurrenceStartUtc: null,
    title: null,
    ...overrides,
  };
}

describe('dutyRows', () => {
  it('leads each duty to the phone screen where it is done', () => {
    const start = new Date(2026, 9, 5, 17, 0).toISOString();
    const rows = dutyRows(
      [
        duty({ kind: 'BadgesToReview', count: 3 }),
        duty({ kind: 'TransfersToConfirm', count: 2 }),
        duty({ kind: 'EntriesToVerify', count: 4, groupKey: 'g1', groupName: 'Соколи' }),
        duty({ kind: 'EntriesToVerify', count: 1 }),
        duty({ kind: 'EventWithoutAttendance', agendaItemKey: 'e1', occurrenceStartUtc: start, title: 'Сходини куреня' }),
      ],
      new Date(2026, 9, 5, 9, 0),
      false,
    );

    expect(rows.map((r) => r.link)).toEqual([
      '/tabs/kurin/review/skills',
      '/tabs/kurin/dues',
      '/tabs/kurin/group/g1/dues',
      '/tabs/kurin/dues',
      '/tabs/calendar/attendance/e1',
    ]);
    expect(rows.map((r) => r.queryParams)).toEqual([null, { part: 'transfers' }, null, { part: 'history' }, { start }]);
    expect(rows[2].detail).toBe('Соколи');
    expect(rows[3].detail).toBe('каса куреня');
    expect(rows[4].label).toBe('Відмітити присутність: Сходини куреня');
    expect(rows[4].detail).toBe('сьогодні · 17:00');
    expect(rows[4].count).toBeNull();
    expect(rows[0].count).toBe(3);
  });

  it('says a duty in another kurin without opening it, naming the kurin', () => {
    const other = duty({ kurin: { kurinKey: 'k2', kurinNumber: 7, namedAfter: null, isCurrent: false }, count: 2 });
    expect(dutiesSpanKurins([other])).toBe(true);
    const [row] = dutyRows([other], new Date(), true);
    expect(row.link).toBeNull();
    expect(row.detail).toBe('к. ч. 7');
  });

  it('names no kurin when every duty is in the current one', () => {
    expect(dutiesSpanKurins([duty({}), duty({ kind: 'EntriesToVerify' })])).toBe(false);
  });

  it('lets «Сходин не було» be said only of an event in the current kurin', () => {
    const start = new Date(2026, 9, 5, 17, 0).toISOString();
    const rows = dutyRows(
      [
        duty({ kind: 'BadgesToReview', count: 3 }),
        duty({ kind: 'EventWithoutAttendance', agendaItemKey: 'e1', occurrenceStartUtc: start }),
        duty({
          kind: 'EventWithoutAttendance',
          agendaItemKey: 'e2',
          occurrenceStartUtc: start,
          kurin: { kurinKey: 'k2', kurinNumber: 7, namedAfter: null, isCurrent: false },
        }),
      ],
      new Date(2026, 9, 5, 9, 0),
      true,
    );

    expect(rows.map((r) => r.canSayNotHeld)).toEqual([false, true, false]);
  });
});

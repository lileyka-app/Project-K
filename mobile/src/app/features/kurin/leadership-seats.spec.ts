import { kvArchive, kvRows } from './kurin.labels';
import { LeadershipHistoryDto, MemberLookupDto, MentorAssignmentDto } from './kurin.models';
import { handoverRequest, holdersLine, holdsSeveral, kvSeatRequest, leadershipRequest, officePeriod, pastOffices, sameSeats, seatsOf } from './leadership-seats';

const person = (memberKey: string, lastName: string, extra: Partial<MemberLookupDto> = {}): MemberLookupDto => ({
  memberKey, firstName: 'Імʼя', lastName, middleName: null, ...extra,
});
const office = (role: string, member: MemberLookupDto, startDate: string, endDate: string | null = null): LeadershipHistoryDto => ({
  leadershipHistoryKey: `${role}-${member.memberKey}-${startDate}`, leadershipKey: 'l1', role, startDate, endDate, member,
});

describe('провід seats', () => {
  it('seats every active holder of each office of that провід and leaves the rest empty', () => {
    const a = person('m1', 'Коваль');
    const b = person('m2', 'Шевчук');
    const seats = seatsOf('group', [
      office('Hurtkoviy', a, '2024-09-01', '2025-06-01'),
      office('Hurtkoviy', b, '2025-06-01'),
      office('Suddya', a, '2025-09-01'),
      office('Kurinnuy', a, '2025-09-01'),
    ]);
    expect(seats['Hurtkoviy'].map((m) => m.memberKey)).toEqual(['m2']);
    expect(seats['Suddya'].map((m) => m.memberKey)).toEqual(['m1']);
    expect(seats['Pysar']).toEqual([]);
    expect('Kurinnuy' in seats).toBe(false);
    expect(Object.keys(seatsOf('kurin', []))).toEqual(['Kurinnuy', 'Suddya', 'Pysar', 'Skarbnyk', 'Horunjiy', 'Gospodar', 'Hronikar', 'OtherKurin']);
  });

  it('keeps two people of an office meant for one, as the web rows do', () => {
    const a = person('m1', 'Коваль');
    const b = person('m2', 'Шевчук');
    const seats = seatsOf('kurin', [office('Pysar', b, '2025-09-01'), office('Pysar', a, '2024-09-01')]);
    expect(seats['Pysar'].map((m) => m.memberKey)).toEqual(['m1', 'm2']);
    expect(holdersLine(seats['Pysar'])).toBe('Коваль Імʼя, Шевчук Імʼя');
    expect(holdersLine([])).toBe('');
  });

  it('tells a changed провід from the one read, whatever the order of several', () => {
    const a = person('m1', 'Коваль');
    const b = person('m2', 'Шевчук');
    const before = seatsOf('kv', [office('Vykhovnyk', a, '2025-01-01'), office('Vykhovnyk', b, '2025-02-01')]);
    expect(sameSeats(before, { ...before, Vykhovnyk: [b, a] })).toBe(true);
    expect(sameSeats(before, { ...before, Vykhovnyk: [a] })).toBe(false);
    expect(sameSeats(before, { ...before, Instruktor: [a] })).toBe(false);
    expect(holdsSeveral('Vykhovnyk')).toBe(true);
    expect(holdsSeveral('Pysar')).toBe(false);
  });

  it('sends only the offices held now, in the web form’s order', () => {
    const a = person('m1', 'Коваль', { middleName: 'Петрович', profilePhotoUrl: 'x' });
    const b = person('m2', 'Шевчук');
    const seats = { ...seatsOf('kurin', []), Pysar: [b], Kurinnuy: [a] };
    expect(leadershipRequest('kurin', 'k1', seats, '2026-10-11')).toEqual({
      type: 'Kurin',
      entityKey: 'k1',
      startDate: '2026-10-11',
      endDate: null,
      leadershipHistories: [
        { role: 'Kurinnuy', startDate: '2026-10-11', endDate: null, member: { memberKey: 'm1', firstName: 'Імʼя', lastName: 'Коваль', middleName: 'Петрович' } },
        { role: 'Pysar', startDate: '2026-10-11', endDate: null, member: { memberKey: 'm2', firstName: 'Імʼя', lastName: 'Шевчук', middleName: null } },
      ],
    });
    expect(leadershipRequest('group', 'g1', seatsOf('group', []), '2026-10-11')).toMatchObject({ type: 'Group', leadershipHistories: [] });
  });

  it('lists the archive by office and name, and searches it by either', () => {
    const a = person('m1', 'Коваль');
    const b = person('m2', 'Шевчук');
    const histories = [
      office('Pysar', b, '2023-09-01', '2024-08-31'),
      office('Kurinnuy', a, '2023-09-01', '2025-08-31'),
      office('Kurinnuy', b, '2025-09-01'),
    ];
    expect(pastOffices(histories).map((h) => `${h.role}:${h.member.memberKey}`)).toEqual(['Kurinnuy:m1', 'Pysar:m2']);
    expect(pastOffices(histories, 'писар').map((h) => h.member.memberKey)).toEqual(['m2']);
    expect(pastOffices(histories, 'коваль').map((h) => h.role)).toEqual(['Kurinnuy']);
    expect(officePeriod('2023-09-01', '2024-08-31')).toBe('01.09.23 – 31.08.24');
    expect(officePeriod('2025-09-01', null)).toBe('з 01.09.25');
  });

  it('hands the Звʼязковий office over and keeps the rest of the КВ seated', () => {
    const zv = person('m1', 'Коваль');
    const mentor = person('m2', 'Шевчук');
    const incoming = person('m3', 'Бойко', { userKey: 'u3', profilePhotoUrl: 'x' });
    const kv = {
      leadershipKey: 'kv1', startDate: '2024-09-01', endDate: null,
      leadershipHistories: [
        office('Zvyazkovyi', zv, '2024-09-01'),
        office('Vykhovnyk', mentor, '2024-10-01'),
        office('Instruktor', zv, '2022-09-01', '2023-09-01'),
      ],
    };
    expect(handoverRequest(kv, 'k1', incoming, '2026-10-11')).toEqual({
      type: 'KV', entityKey: 'k1', startDate: '2026-10-11', endDate: null,
      leadershipHistories: [
        { role: 'Vykhovnyk', startDate: '2024-10-01', endDate: null, member: { memberKey: 'm2', firstName: 'Імʼя', lastName: 'Шевчук', middleName: null } },
        { role: 'Zvyazkovyi', startDate: '2026-10-11', endDate: null, member: { memberKey: 'm3', firstName: 'Імʼя', lastName: 'Бойко', middleName: null } },
      ],
    });
    expect(handoverRequest(null, 'k1', incoming, '2026-10-11').leadershipHistories.map((h) => h.role)).toEqual(['Zvyazkovyi']);
  });

  describe('КВ seat by гуртки', () => {
    const zv = person('m1', 'Коваль');
    const who = person('m2', 'Шевчук', { userKey: 'u2' });
    const kvOf = (...histories: LeadershipHistoryDto[]) => ({ leadershipKey: 'kv1', startDate: '2024-09-01', endDate: null, leadershipHistories: histories });
    const seats = (kv: ReturnType<typeof kvOf> | null, hasGroups: boolean) =>
      kvSeatRequest(kv, 'k1', who, hasGroups, '2026-10-11')?.leadershipHistories.map((h) => `${h.role}:${h.member.memberKey}`) ?? null;

    it('seats someone without a гурток as інструктор, a first КВ провід included', () => {
      expect(seats(kvOf(office('Zvyazkovyi', zv, '2024-09-01')), false)).toEqual(['Zvyazkovyi:m1', 'Instruktor:m2']);
      expect(seats(null, false)).toEqual(['Instruktor:m2']);
    });

    it('turns a впорядник seat without гуртки into an інструктор one', () => {
      expect(seats(kvOf(office('Zvyazkovyi', zv, '2024-09-01'), office('Vykhovnyk', who, '2025-01-01')), false))
        .toEqual(['Zvyazkovyi:m1', 'Instruktor:m2']);
    });

    it('ends the інструктор seat once a гурток is assigned', () => {
      expect(seats(kvOf(office('Zvyazkovyi', zv, '2024-09-01'), office('Instruktor', who, '2025-01-01')), true)).toEqual(['Zvyazkovyi:m1']);
    });

    it('writes nothing when the seat already matches', () => {
      expect(seats(kvOf(office('Instruktor', who, '2025-01-01')), false)).toBeNull();
      expect(seats(kvOf(office('Zvyazkovyi', zv, '2024-09-01')), true)).toBeNull();
    });
  });
});

describe('КВ', () => {
  const assignment = (key: string, groupKey: string, member: MemberLookupDto, revokedAtUtc: string | null): MentorAssignmentDto => ({
    mentorAssignmentKey: key, mentorUserKey: member.userKey ?? '', groupKey, groupName: groupKey, assignedAtUtc: '2024-09-01T00:00:00Z', revokedAtUtc, member,
  });

  it('keeps the гуртки of each row by key too, and marks the Звʼязковий', () => {
    const zv = person('z', 'Коваль', { userKey: 'uz', userRole: 'KV.Zvyazkovyi' });
    const groups = [{ groupKey: 'g1', name: 'Соколи' }, { groupKey: 'g2', name: 'Орли' }] as never[];
    const rows = kvRows([zv], [assignment('a1', 'g1', zv, null), assignment('a2', 'g2', zv, null)], groups);
    expect(rows).toEqual([{ member: zv, status: 'Звʼязковий', groups: ['Соколи', 'Орли'], groupKeys: ['g1', 'g2'], isManager: true }]);
  });

  it('lists ended assignments, the latest ended first', () => {
    const m = person('m', 'Мельник', { userKey: 'um' });
    const list = kvArchive([
      assignment('a1', 'g1', m, '2025-01-01T10:00:00'),
      assignment('a2', 'g2', m, null),
      assignment('a3', 'g3', m, '2026-01-01T10:00:00Z'),
    ]);
    expect(list.map((a) => a.mentorAssignmentKey)).toEqual(['a3', 'a1']);
  });
});

import {
  activeWarning,
  awardGroups,
  birthdayWhen,
  duesAmountLabel,
  duesMoney,
  filterMembers,
  kvRowTags,
  kvRows,
  memberBranch,
  memberRoleTags,
  officeTag,
  roleTone,
  membershipPeriod,
  phoneHref,
  plastLevelLabel,
  probeRows,
  probeSections,
  signerLabel,
  sortMembers,
  upcomingBirthdays,
} from './kurin.labels';
import { GroupDto, GroupedProbeDto, MemberAwardDto, MemberLookupDto, MembershipDto, MentorAssignmentDto, ProbeProgressDto, ProbeSummaryDto } from './kurin.models';

function member(memberKey: string, firstName: string, lastName: string, extra: Partial<MemberLookupDto> = {}): MemberLookupDto {
  return { memberKey, userKey: `u-${memberKey}`, firstName, lastName, middleName: null, ...extra } as MemberLookupDto;
}

const office = (role: string, extra: object = {}) => ({ role, endDate: null, startDate: '2025-09-01', leadershipType: 'Kurin', member: {}, ...extra });

describe('kurin labels', () => {
  it('lists birthdays in the window, soonest first, across the new year', () => {
    const people = [
      member('a', 'Марта', 'Шевчук', { dateOfBirth: '2012-10-10' }),
      member('b', 'Остап', 'Коваль', { dateOfBirth: '2012-10-13' }),
      member('c', 'Іван', 'Бойко', { dateOfBirth: '2011-01-05' }),
      member('d', 'Олена', 'Петренко', { dateOfBirth: '2011-12-01' }),
      member('e', 'Без', 'Дати'),
    ];
    const soon = upcomingBirthdays(people, 30, new Date(2026, 9, 10));
    expect(soon.map((b) => [b.member.memberKey, b.daysUntil])).toEqual([
      ['a', 0],
      ['b', 3],
    ]);
    const december = upcomingBirthdays(people, 30, new Date(2026, 11, 20));
    expect(december.map((b) => b.member.memberKey)).toEqual(['c']);
    expect(december[0].date.getFullYear()).toBe(2027);
    expect([0, 1, 5].map(birthdayWhen)).toEqual(['сьогодні', 'завтра', 'через 5 дн.']);
  });

  it('takes the birthday without the year when the kurin hides the full date', () => {
    const people = [
      member('a', 'Марта', 'Шевчук', { birthday: '10-12' }),
      member('b', 'Остап', 'Коваль', { dateOfBirth: '2012-10-11', birthday: '10-11' }),
    ];
    const soon = upcomingBirthdays(people, 30, new Date(2026, 9, 10));
    expect(soon.map((b) => [b.member.memberKey, b.daysUntil])).toEqual([
      ['b', 1],
      ['a', 2],
    ]);
  });

  it('sorts by surname or by office and searches by name, ступінь and phone', () => {
    const people = [
      member('a', 'Іван', 'Бойко', { latestPlastLevel: 'Uchasnyk' }),
      member('b', 'Марта', 'Шевчук', { leadershipHistories: [office('Hurtkoviy', { leadershipType: 'Group', groupName: 'Соколи' })] } as never),
      member('c', 'Олена', 'Петренко', { userRole: 'KV.Zvyazkovyi', phoneNumber: '+380671112233' }),
    ];
    expect(sortMembers(people, 'name').map((m) => m.memberKey)).toEqual(['a', 'c', 'b']);
    expect(sortMembers(people, 'role').map((m) => m.memberKey)).toEqual(['c', 'b', 'a']);
    expect(filterMembers(people, 'марта').map((m) => m.memberKey)).toEqual(['b']);
    expect(filterMembers(people, 'пл. уч').map((m) => m.memberKey)).toEqual(['a']);
    expect(filterMembers(people, '111').map((m) => m.memberKey)).toEqual(['c']);
    expect(filterMembers(people, '  ')).toHaveLength(3);
    expect(memberRoleTags(people[1])).toEqual([{ label: 'Гуртковий: Соколи', tone: 'danger' }]);
    expect(memberRoleTags(member('d', 'А', 'Б', { mentoredGroupNames: ['Орли', 'Соколи'] }))).toEqual([
      { label: 'Впорядник: Орли, Соколи', tone: 'info' },
    ]);
  });

  it('colours an office as the web does, and an ended one grey', () => {
    expect(['Zvyazkovyi', 'Kurinnuy', 'Hurtkoviy', 'Suddya', 'Skarbnyk', 'Pysar', 'Vykhovnyk'].map(roleTone)).toEqual([
      'danger',
      'danger',
      'danger',
      'warn',
      'success',
      'info',
      'info',
    ]);
    expect(officeTag({ role: 'Skarbnyk', endDate: null })).toEqual({ label: 'Скарбник', tone: 'success' });
    expect(officeTag({ role: 'Skarbnyk', endDate: '2025-06-01' })).toEqual({ label: 'Скарбник', tone: 'secondary' });
  });

  it('tags a КВ row by its status, not its office, then each гурток', () => {
    expect(kvRowTags({ status: 'Звʼязковий', groups: [], isManager: true })).toEqual([{ label: 'Звʼязковий', tone: 'danger' }]);
    expect(kvRowTags({ status: 'Впорядник', groups: ['Соколи'], isManager: false })).toEqual([
      { label: 'Впорядник', tone: 'success' },
      { label: 'Соколи', tone: 'info' },
    ]);
    expect(kvRowTags({ status: 'Інструктор', groups: [], isManager: false })).toEqual([{ label: 'Інструктор', tone: 'warn' }]);
  });

  it('puts the Звʼязковий first in КВ, then the rest named by their гуртки', () => {
    const zv = member('z', 'Олена', 'Петренко', { userRole: 'KV.Zvyazkovyi' });
    const mentor = member('m', 'Андрій', 'Мельник', { userRole: 'KV.Instruktor' });
    const idle = member('i', 'Богдан', 'Антонюк', { userRole: 'KV.Vykhovnyk' });
    const instructor = member('n', 'Ірина', 'Ничипорук', { userRole: 'KV.Instruktor' });
    const groups = [{ groupKey: 'g1', name: 'Соколи' }, { groupKey: 'g2', name: 'Орли' }] as GroupDto[];
    const assignments = [
      { groupKey: 'g1', revokedAtUtc: null, member: mentor },
      { groupKey: 'g2', revokedAtUtc: '2026-01-01T00:00:00Z', member: mentor },
      { groupKey: 'g9', revokedAtUtc: null, member: idle },
    ] as MentorAssignmentDto[];
    const rows = kvRows([mentor, zv, idle, instructor], assignments, groups);
    expect(rows.map((r) => [r.member.memberKey, r.status, r.groups])).toEqual([
      ['z', 'Звʼязковий', []],
      ['i', 'Інструктор', []],
      ['m', 'Впорядник', ['Соколи']],
      ['n', 'Інструктор', []],
    ]);
  });

  it('names the ступінь and the branch as the web does', () => {
    expect(plastLevelLabel({ latestPlastLevel: 'Uchasnyk' } as never)).toBe('пл. уч.');
    expect(plastLevelLabel({ latestPlastLevel: 'Entry' } as never)).toBeNull();
    expect(plastLevelLabel({ latestPlastLevel: 'Starshoplastun', plastLevelHistories: [{ plastLevel: 'Skob' }] } as never)).toBe('ст. пл. скоб');
    const youth = [{ isCurrent: true, kurinKey: 'k1', branch: 'UPYu' }] as MembershipDto[];
    expect(memberBranch({ latestPlastLevel: 'Uchasnyk', kurinKey: 'k1' } as never, youth)).toBe('UPYu');
    expect(memberBranch({ latestPlastLevel: 'Starshoplastun', kurinKey: 'k1' } as never, youth)).toBe('USP');
    expect(memberBranch({ latestPlastLevel: 'SeniorPratsi', kurinKey: 'k1' } as never, [])).toBe('UPS');
  });

  it('keeps the highest warning in force with its days left', () => {
    const now = Date.UTC(2026, 9, 10);
    const warnings = [
      { level: 'Level3', revokedAtUtc: '2026-10-01T00:00:00Z', expiresAtUtc: '2027-01-01T00:00:00Z' },
      { level: 'Level1', revokedAtUtc: null, expiresAtUtc: '2026-12-01T00:00:00Z' },
      { level: 'Level2', revokedAtUtc: null, expiresAtUtc: '2026-10-15T00:00:00' },
      { level: 'Level3', revokedAtUtc: null, expiresAtUtc: '2026-10-01T00:00:00Z' },
    ];
    expect(activeWarning(warnings as never, now)).toEqual({ level: 2, daysLeft: 5 });
    expect(activeWarning([], now)).toBeNull();
  });

  it('unlocks the second probe once the first is closed and counts a closed probe as fully signed', () => {
    const probes = [
      { id: 'probe-1', pointsCount: 4 },
      { id: 'probe-2', pointsCount: 10 },
    ] as ProbeSummaryDto[];
    const open = probeRows(probes, [{ probeId: 'probe-1', status: 'InProgress', pointSignatures: [{ isSigned: true }, { isSigned: false }] }] as never);
    expect(open.map((r) => [r.probeId, r.canOpen, r.signedPoints, r.percent])).toEqual([
      ['probe-1', true, 1, 25],
      ['probe-2', false, 0, 0],
      ['probe-3', false, 0, null],
    ]);
    const closed = probeRows(probes, [{ probeId: 'probe-1', status: 'Completed', completedAtUtc: '2026-05-01T10:00:00Z', pointSignatures: [] }] as never);
    expect(closed[0]).toMatchObject({ isCompleted: true, signedPoints: 4, percent: 100 });
    expect(closed[1]).toMatchObject({ isDisabled: false, canOpen: true });
  });

  it('shows who signed each point, falling back to whoever closed the probe', () => {
    const probe = {
      id: 'probe-1',
      sections: [{ id: 's1', code: '1', title: 'Закон', points: [{ id: 'p1', title: 'Знати закон' }, { id: 'p2', title: 'Знати присягу' }] }],
    } as GroupedProbeDto;
    const progress = {
      status: 'InProgress',
      pointSignatures: [{ pointId: 'p1', isSigned: true, signedByName: 'Олена Петренко', signedByRole: 'Звʼязковий', signedAtUtc: '2026-10-05T00:00:00Z' }],
    } as ProbeProgressDto;
    const [section] = probeSections(probe, progress);
    expect(section.points.map((p) => p.isSigned)).toEqual([true, false]);
    expect(signerLabel(section.points[0])).toBe('Олена Петренко (Звʼязковий)');
    expect(signerLabel(section.points[1])).toBe('Не підписано');

    const closedWhole = { status: 'Completed', pointSignatures: [], completedByName: 'Андрій Мельник', completedByRole: null } as unknown as ProbeProgressDto;
    const points = probeSections(probe, closedWhole)[0].points;
    expect(points.every((p) => p.isSigned)).toBe(true);
    expect(signerLabel(points[1])).toBe('Андрій Мельник');
    expect(probeSections(null, progress)).toEqual([]);
  });

  it('groups awards by level with the latest one, leaving out rejected', () => {
    const awards = [
      { memberAwardKey: 'a1', level: 'Second', status: 'Submitted', dateAcquired: '2025-06-01' },
      { memberAwardKey: 'a2', level: 'First', status: 'Confirmed', dateAcquired: '2024-06-01' },
      { memberAwardKey: 'a3', level: 'First', status: 'Confirmed', dateAcquired: '2025-01-01' },
      { memberAwardKey: 'a4', level: 'Third', status: 'Rejected', dateAcquired: '2025-02-01' },
    ] as MemberAwardDto[];
    expect(awardGroups(awards).map((g) => [g.level, g.count, g.latest.memberAwardKey])).toEqual([
      ['First', 2, 'a3'],
      ['Second', 1, 'a1'],
    ]);
  });

  it('signs dues amounts and formats money with a real minus', () => {
    const nbsp = (s: string) => s.replace(/\s/g, ' ');
    expect(nbsp(duesMoney(-1240))).toBe('−1 240 ₴');
    expect(nbsp(duesAmountLabel({ kind: 'Contribution', amount: 150 }))).toBe('+150 ₴');
    expect(nbsp(duesAmountLabel({ kind: 'Expense', amount: 150 }))).toBe('−150 ₴');
    expect(nbsp(duesAmountLabel({ kind: 'Correction', amount: -20 }))).toBe('−20 ₴');
    expect(nbsp(duesAmountLabel({ kind: 'Exchange', amount: 300 }))).toBe('300 ₴');
  });

  it('prints membership periods and contact links', () => {
    expect(membershipPeriod({ isCurrent: true, joinedAtUtc: '2020-09-01T00:00:00Z' } as MembershipDto)).toBe('з 2020');
    expect(membershipPeriod({ isCurrent: false, joinedAtUtc: '2018-09-01T00:00:00Z', leftAtUtc: '2019-06-01T00:00:00Z' } as MembershipDto)).toBe('2018 — 2019');
    expect(phoneHref('+380 (67) 111-22-33')).toBe('tel:+380671112233');
    expect(phoneHref('12')).toBeNull();
  });
});

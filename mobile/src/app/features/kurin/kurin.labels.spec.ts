import {
  activeWarning,
  currentDegree,
  isSeniorPerson,
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
  officesIn,
  degreeRows,
  seniorAwardRows,
  seniorAwardBlocks,
  orderAwardBlocks,
  SENIOR_LADDER,
  YOUTH_LADDER,
  membershipOf,
  roleTone,
  membershipPeriod,
  phoneHref,
  plastLevelLabel,
  probeLine,
  probeMeta,
  probeRows,
  probeSections,
  signerLabel,
  sortMembers,
  startedProbeIds,
  upcomingBirthdays,
  actInKurin,
  rememberKurinRoleNames,
  roleName,
  warningRows,
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
      { id: 'probe-1', title: 'Перша проба', pointsCount: 4, order: 1 },
      { id: 'probe-2', title: 'Друга проба', pointsCount: 10, order: 2 },
      { id: 'probe-3', title: 'Третя проба', pointsCount: 0, order: 3 },
    ] as ProbeSummaryDto[];
    const open = probeRows(probes, [{ probeId: 'probe-1', status: 'InProgress', pointSignatures: [{ isSigned: true }, { isSigned: false }] }] as never);
    expect(open.map((r) => [r.probeId, r.canOpen, r.signedPoints, r.percent])).toEqual([
      ['probe-1', true, 1, 25],
      ['probe-2', false, 0, 0],
      ['probe-3', false, 0, null],
    ]);
    expect(probeMeta(open[1])).toBe('Відкриється після закриття: Перша проба');
    expect(open[2].hasNoPoints).toBe(true);
    const closed = probeRows(probes, [{ probeId: 'probe-1', status: 'Completed', completedAtUtc: '2026-05-01T10:00:00Z', pointSignatures: [] }] as never);
    expect(closed[0]).toMatchObject({ isCompleted: true, signedPoints: 4, percent: 100 });
    expect(closed[1]).toMatchObject({ isDisabled: false, canOpen: true });
    // The third has no points but its projects: it opens once the second is closed.
    const second = probeRows(probes, [
      { probeId: 'probe-1', status: 'Completed', completedAtUtc: '2026-05-01T10:00:00Z', pointSignatures: [] },
      { probeId: 'probe-2', status: 'Completed', completedAtUtc: '2026-09-01T10:00:00Z', pointSignatures: [] },
    ] as never);
    expect(second[2]).toMatchObject({ hasNoPoints: true, isDisabled: false, canOpen: true });
    expect(probeMeta(second[2])).toBe('Складається з проєктів: опис і фото на сторінці проби');
  });

  it('falls back to the unnamed edition, with a started probe of another edition in its place', () => {
    const probes = [
      { id: 'probe-1', title: 'Перша проба', pointsCount: 4, order: 1 },
      { id: 'probe-2', title: 'Друга проба', pointsCount: 10, order: 2 },
      { id: 'znak-2026', title: 'Знак прихильника', pointsCount: 3, order: 0, edition: 'upyu-2026' },
      { id: 'probe-2-2026', title: 'Друга проба', pointsCount: 8, order: 2, edition: 'upyu-2026' },
    ] as ProbeSummaryDto[];
    expect(probeLine(probes, new Set()).map((p) => p.id)).toEqual(['probe-1', 'probe-2']);
    const progresses = [{ probeId: 'probe-2-2026', status: 'InProgress', pointSignatures: [] }] as never as ProbeProgressDto[];
    expect(probeLine(probes, startedProbeIds(progresses)).map((p) => p.id)).toEqual(['probe-1', 'probe-2-2026']);
  });

  it('lays out rows in the order the server sent, named edition included', () => {
    const line = [
      { id: 'znak-2026', title: 'Знак прихильника', pointsCount: 3, order: 0, edition: 'upyu-2026' },
      { id: 'probe-1-2026', title: 'Перша проба', pointsCount: 8, order: 1, edition: 'upyu-2026' },
    ] as ProbeSummaryDto[];
    expect(probeRows(line, []).map((r) => [r.probeId, r.lockedBehind])).toEqual([
      ['znak-2026', null],
      ['probe-1-2026', 'Знак прихильника'],
    ]);
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

describe('kurin role names', () => {
  afterEach(() => actInKurin(null));

  it('says the current kurin\'s own name, and keeps it when another kurin\'s names arrive', () => {
    rememberKurinRoleNames('orden', { Pysar: 'Скриптор' });
    actInKurin('orden');
    rememberKurinRoleNames('chorty', { Pysar: 'Літописець' });

    expect(roleName('Pysar')).toBe('Скриптор');
    expect(roleName('Pysar', 'chorty')).toBe('Літописець');
    actInKurin(null);
    expect(roleName('Pysar')).toBe('Писар');
  });

  it('names an office by its own kurin and keeps offices of other kurins off this card', () => {
    rememberKurinRoleNames('orden', { Pysar: 'Скриптор' });
    actInKurin('orden');
    const office = (role: string, kurinKey: string) =>
      ({ leadershipHistoryKey: role + kurinKey, leadershipKey: 'l', role, kurinKey, startDate: '2026-01-01', endDate: null }) as never;
    const held = [office('Pysar', 'orden'), office('Kurinnyi', 'plain')];

    expect(officeTag(held[0])).toEqual({ label: 'Скриптор', tone: roleTone('Pysar') });
    expect(officeTag(office('Pysar', 'plain')).label).toBe('Писар');
    expect(officesIn(held, 'orden').map((h) => h.role)).toEqual(['Pysar']);
    expect(memberRoleTags({ leadershipHistories: [office('Pysar', 'plain')] } as never)).toEqual([{ label: 'Писар', tone: roleTone('Pysar') }]);
  });
});

describe('senior warning rows', () => {
  it('puts a current пересторога first and says how long it lasts', () => {
    const now = Date.parse('2026-11-01T00:00:00Z');
    const rows = warningRows(
      [
        { memberWarningKey: 'old', level: 'Level1', issuedAtUtc: '2026-01-10T00:00:00Z', expiresAtUtc: '2026-04-10T00:00:00Z' },
        { memberWarningKey: 'now', level: 'Level2', issuedAtUtc: '2026-10-20T00:00:00Z', expiresAtUtc: '2026-11-11T00:00:00Z' },
        { memberWarningKey: 'closed', level: 'Level1', issuedAtUtc: '2025-12-01T00:00:00Z', expiresAtUtc: '2026-03-01T00:00:00Z', revokedAtUtc: '2026-03-01T00:00:00Z' },
        { memberWarningKey: 'lifted', level: 'Level1', issuedAtUtc: '2025-11-01T00:00:00Z', expiresAtUtc: '2026-02-01T00:00:00Z', revokedAtUtc: '2025-11-05T00:00:00Z', revokedByUserKey: 'u1' },
      ],
      now,
    );
    expect(rows.map((r) => r.key)).toEqual(['now', 'old', 'closed', 'lifted']);
    expect(rows[2].line).toContain('минула');
    expect(rows[3].line).toContain('знято');
    expect(rows[0].title).toBe('Друга пересторога');
    expect(rows[0].line).toContain('ще 10 дн.');
    expect(rows[1].line).toContain('минула');
  });
});

describe('membership in a line of text', () => {
  it('names a senior kurin by its own name and a УПЮ kurin by its number', () => {
    expect(membershipOf({ branch: 'USP', kurinNumber: 5, kurinNamedAfter: 'Орден Хрестоносців' } as MembershipDto)).toBe('Членство (Орден Хрестоносців)');
    expect(membershipOf({ branch: 'USP', kurinNumber: 5, kurinNamedAfter: null } as MembershipDto)).toBe('Членство (5-й курінь УСП)');
    expect(membershipOf({ branch: 'UPYu', kurinNumber: 47, kurinNamedAfter: 'Івана Богуна' } as MembershipDto)).toBe('Членство в курені ч. 47');
  });
});

describe('ступені й старші відзначення з каталогу', () => {
  const honours = [
    { id: 'usp-starsha-plastunka', kind: 'Degree', title: 'Старша пластунка', group: 'usp', ulad: 'usp', order: 2, imageUrl: 'https://catalog.test/spl.svg' },
    { id: 'upyu-virlytsia', kind: 'Degree', title: 'Пластунка вірлиця', group: 'upyu', ulad: 'upyu', order: 7, imageUrl: '' },
    { id: 'orden-sv-yuriia-bronza', kind: 'Award', title: 'Орден святого Юрія в бронзі', group: 'orden-sv-yuriia', ulad: 'usp', order: 0, imageUrl: 'https://catalog.test/o.png' },
  ] as const;
  const histories = [
    { plastLevel: 'Skob', dateAchieved: '2014-06-10', isPlastunka: true, catalogId: 'upyu-virlytsia' },
    { plastLevel: 'Starshoplastun', dateAchieved: '2016-12-03', isPlastunka: true, catalogId: 'usp-starsha-plastunka' },
  ];

  it('shows each membership its own ladder, named and drawn for a пластунка', () => {
    expect(degreeRows(histories, YOUTH_LADDER, [...honours]).map((r) => r.title)).toEqual(['Пластунка вірлиця']);
    expect(degreeRows(histories, SENIOR_LADDER, [...honours]).map((r) => [r.title, r.imageUrl])).toEqual([['Старша пластунка', 'https://catalog.test/spl.svg']]);
    expect(degreeRows(histories, SENIOR_LADDER, []).map((r) => r.title)).toEqual(['ст. пл.']);
  });

  it('shows the current ступінь, the highest on the whole ladder: a сеніор over the скоб of 2002', () => {
    const zvyazkovyi = [
      { plastLevel: 'Skob', dateAchieved: '2002-06-01' },
      { plastLevel: 'Senior', dateAchieved: '2015-04-26' },
    ];
    expect(currentDegree(zvyazkovyi, [])?.title).toBe('пл. сен.');
    expect(currentDegree(histories, [...honours])?.title).toBe('Старша пластунка');
    expect(currentDegree([], [])).toBeNull();
  });

  it('counts a сеніор who leads an УПЮ kurin as a senior person, a юнак as not', () => {
    const upyu = [{ membershipKey: 'm', kurinKey: 'k', kurinNumber: 5, branch: 'UPYu', kind: 'Staff', isCurrent: true, joinedAtUtc: '2020-01-01' }] as never;
    expect(isSeniorPerson({ kurinKey: 'k', latestPlastLevel: 'Senior', plastLevelHistories: [{ plastLevel: 'Skob' }, { plastLevel: 'Senior' }] } as never, upyu)).toBe(true);
    expect(isSeniorPerson({ kurinKey: 'k', latestPlastLevel: 'Skob', plastLevelHistories: [{ plastLevel: 'Skob' }] } as never, upyu)).toBe(false);
  });

  it('lists senior відзначення apart from the УПЮ levels', () => {
    const base = { memberKey: 'm', kurinKey: 'k', status: 'Confirmed' as const };
    const awards = [
      { ...base, memberAwardKey: 'a', level: 'First' as const, catalogId: null, dateAcquired: '2015-01-01T00:00:00' },
      { ...base, memberAwardKey: 'b', level: null, catalogId: 'orden-sv-yuriia-bronza', dateAcquired: '2018-04-23T00:00:00' },
    ];
    expect(seniorAwardRows(awards, [...honours]).map((r) => [r.award.memberAwardKey, r.title, r.imageUrl, r.section])).toEqual([
      ['b', 'Орден святого Юрія в бронзі', 'https://catalog.test/o.png', 'УСП'],
    ]);
    expect(awardGroups(awards).map((g) => g.latest.memberAwardKey)).toEqual(['a']);
  });
});

describe('відзначення блоками', () => {
  const base = { memberKey: 'm', kurinKey: 'k', status: 'Confirmed' as const };
  const honours = [
    { id: 'usp-hramota-zasluhy', kind: 'Award', title: 'Грамота заслуги', group: 'usp-vidznachennia', ulad: 'usp', order: 0, imageUrl: '' },
    { id: 'ups-hramota-zasluhy', kind: 'Award', title: 'Грамота заслуги', group: 'ups-vidznachennia', ulad: 'ups', order: 1, imageUrl: '' },
  ] as const;

  it('puts every grant of one відзначення under one block, newest first, and pinned blocks before the rest', () => {
    const blocks = seniorAwardBlocks(
      [
        { ...base, memberAwardKey: 'a', catalogId: 'usp-hramota-zasluhy', dateAcquired: '2012-10-20T00:00:00', note: 'За табір' },
        { ...base, memberAwardKey: 'b', catalogId: 'usp-hramota-zasluhy', dateAcquired: '2015-03-14T00:00:00' },
        { ...base, memberAwardKey: 'c', catalogId: 'ups-hramota-zasluhy', dateAcquired: '2020-01-01T00:00:00', isPinned: true },
        { ...base, memberAwardKey: 'd', level: 'First' as const, dateAcquired: '2021-01-01T00:00:00' },
      ],
      [...honours],
      true,
    );

    expect(blocks.map((b) => [b.key, b.section, b.grants.map((g) => g.award.memberAwardKey), b.pinned])).toEqual([
      ['ups-hramota-zasluhy', 'УПС', ['c'], true],
      ['usp-hramota-zasluhy', 'УСП', ['b', 'a'], false],
    ]);
  });

  it('orders unpinned blocks by the latest grant', () => {
    const block = (key: string, date: string, pinned = false) =>
      ({ key, title: key, imageUrl: null, section: '', pinned, pending: false, pin: {}, grants: [{ award: { ...base, memberAwardKey: key, dateAcquired: date }, pending: false, canOpen: true }] });
    expect(orderAwardBlocks([block('old', '2010-01-01'), block('new', '2024-01-01'), block('liked', '2005-01-01', true)]).map((b) => b.key)).toEqual(['liked', 'new', 'old']);
  });
});

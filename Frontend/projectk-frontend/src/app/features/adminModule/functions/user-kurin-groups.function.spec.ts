import { KurinBranch } from '../../kurinModule/models/enums/kurin-branch.enum';
import { UserDto } from '../models/user.dto';
import { NO_KURIN_GROUP, usersByKurin } from './user-kurin-groups.function';

function user(userId: string, kurin: Partial<UserDto> | null): UserDto {
  return {
    userId,
    email: `${userId}@example.com`,
    role: 'Member',
    firstName: userId,
    lastName: 'Тест',
    kurinKey: null,
    kurinNumber: null,
    ...(kurin ?? {})
  };
}

const youth5 = { kurinKey: 'youth-5', kurinNumber: 5, kurinBranch: KurinBranch.UPYu, kurinBranches: [{ branch: KurinBranch.UPYu, number: 5 }], kurinNamedAfter: 'Олени Степанів' };
const orden = {
  kurinKey: 'orden',
  kurinNumber: 5,
  kurinBranch: KurinBranch.USP,
  kurinBranches: [{ branch: KurinBranch.USP, number: 5 }, { branch: KurinBranch.UPS, number: 20 }],
  kurinNamedAfter: 'Орден Хрестоносців'
};
const youth1 = { kurinKey: 'youth-1', kurinNumber: 1, kurinBranch: KurinBranch.UPYu, kurinNamedAfter: 'Івана Богуна' };

describe('usersByKurin', () => {
  it('keeps 5-й курінь УПЮ and 5-й курінь УСП apart, each under its own name', () => {
    const rows = usersByKurin([user('a', orden), user('b', youth5), user('c', orden)]);

    const groups = [...new Map(rows.map(row => [row.group, row.groupTitle])).entries()];
    expect(groups).toEqual([
      ['youth-5', 'Курінь ч. 5 УПЮ ім. Олени Степанів'],
      ['orden', 'Орден Хрестоносців · 5-й курінь УСП · 20-й курінь УПС']
    ]);
    expect(rows.filter(row => row.group === 'orden').map(row => row.userId)).toEqual(['a', 'c']);
  });

  it('lists УПЮ kurins by number, then senior ones, then accounts in no kurin', () => {
    const rows = usersByKurin([user('none', null), user('s', orden), user('five', youth5), user('one', youth1)]);

    expect(rows.map(row => row.userId)).toEqual(['one', 'five', 's', 'none']);
    expect(rows.map(row => row.groupOrder)).toEqual([0, 1, 2, 3]);
    expect(rows.at(-1)).toMatchObject({ group: NO_KURIN_GROUP, groupTitle: 'Без куреня' });
  });

  it('names the край only where two kurins would read the same', () => {
    const lviv5 = { ...youth5, kurinNamedAfter: null, kurinKraiCode: 'ua' };
    const praha5 = { ...youth5, kurinKey: 'youth-5-cz', kurinNamedAfter: null, kurinKraiCode: 'cz' };
    const rows = usersByKurin([user('ua', lviv5), user('cz', praha5), user('o', { ...orden, kurinKraiCode: 'ua' })], new Map([['ua', 'Україна'], ['cz', 'Чехія']]));

    expect([...new Set(rows.map(row => row.groupTitle))]).toEqual([
      'Курінь ч. 5 УПЮ · Україна',
      'Курінь ч. 5 УПЮ · Чехія',
      'Орден Хрестоносців · 5-й курінь УСП · 20-й курінь УПС'
    ]);
    // Without the names yet, the code still tells them apart.
    expect(usersByKurin([user('ua', lviv5), user('cz', praha5)]).map(row => row.groupTitle)).toEqual(['Курінь ч. 5 УПЮ · ua', 'Курінь ч. 5 УПЮ · cz']);
  });

  it('reads a kurin without its улад as УПЮ, as the server did before it sent one', () => {
    const rows = usersByKurin([user('old', { kurinKey: 'k', kurinNumber: 12 })]);

    expect(rows[0].groupTitle).toBe('Курінь ч. 12 УПЮ');
  });
});

import { KurinBranch } from '../../features/kurinModule/models/enums/kurin-branch.enum';
import { compareKurinNumbers, compareKurinsByBranch, kurinBranchesName, kurinGroupTitle, kurinBranchTags, kurinInText, kurinLabel } from './kurin-label.function';

describe('kurinLabel', () => {
  it('names a numbered kurin by its number', () => {
    expect(kurinLabel(47)).toBe('к. ч. 47');
  });

  it('names a підготовчий курінь without a number', () => {
    expect(kurinLabel(null)).toBe('підг. курінь');
  });

  it('names a senior kurin by its гілка', () => {
    expect(kurinLabel(5, KurinBranch.USP)).toBe('5-й курінь УСП');
    expect(kurinLabel(47, KurinBranch.UPYu)).toBe('к. ч. 47');
  });

  it('sorts підготовчі курені after the numbered ones', () => {
    expect([null, 12, 3].sort(compareKurinNumbers)).toEqual([3, 12, null]);
  });
});

describe('kurinBranchesName', () => {
  it('names one senior kurin in both of its гілки', () => {
    expect(kurinBranchesName([
      { branch: KurinBranch.USP, number: 5 },
      { branch: KurinBranch.UPS, number: 20 }
    ])).toBe('5-й курінь УСП · 20-й курінь УПС');
    expect(kurinBranchTags([{ branch: KurinBranch.USP, number: 5 }, { branch: KurinBranch.UPS, number: 20 }])).toBe('УСП · УПС');
  });

  it('keeps a УПЮ kurin as it always read', () => {
    expect(kurinBranchesName([{ branch: KurinBranch.UPYu, number: 47 }])).toBe('Курінь ч. 47');
    expect(kurinBranchesName(undefined, 12)).toBe('Курінь ч. 12');
  });
});

describe('kurinGroupTitle', () => {
  it('names the улад, so the same number in УПЮ and УСП reads as two kurins', () => {
    expect(kurinGroupTitle({ number: 5, branch: KurinBranch.UPYu, namedAfter: 'Олени Степанів' })).toBe('Курінь ч. 5 УПЮ ім. Олени Степанів');
    expect(kurinGroupTitle({ number: 5 })).toBe('Курінь ч. 5 УПЮ');
    expect(kurinGroupTitle({
      number: 5,
      branch: KurinBranch.USP,
      branches: [{ branch: KurinBranch.USP, number: 5 }, { branch: KurinBranch.UPS, number: 20 }],
      namedAfter: 'Орден Хрестоносців'
    })).toBe('Орден Хрестоносців · 5-й курінь УСП · 20-й курінь УПС');
    expect(kurinGroupTitle({ number: null })).toBe('Підготовчий курінь УПЮ');
  });
});

describe('compareKurinsByBranch', () => {
  it('puts УПЮ kurins before senior ones, each by number with підготовчі last', () => {
    const kurins = [
      { number: 5, branch: KurinBranch.USP },
      { number: null, branch: KurinBranch.UPYu },
      { number: 5, branch: KurinBranch.UPYu },
      { number: 1, branch: KurinBranch.UPYu },
      { number: 3, branch: KurinBranch.UPS }
    ];
    expect([...kurins].sort(compareKurinsByBranch).map(k => `${k.number}${k.branch}`))
      .toEqual(['1UPYu', '5UPYu', 'nullUPYu', '3UPS', '5USP']);
  });
});

describe('kurinInText', () => {
  it('names a senior kurin by its гілки, not by the УПЮ number it shares', () => {
    expect(kurinInText([{ branch: KurinBranch.USP, number: 5 }])).toBe('5-й курінь УСП');
    expect(kurinInText([{ branch: KurinBranch.UPYu, number: 5 }])).toBe('к. ч. 5');
    expect(kurinInText([], 7)).toBe('к. ч. 7');
    expect(kurinInText([{ branch: KurinBranch.UPYu, number: null }])).toBe('підг. курінь');
  });
});

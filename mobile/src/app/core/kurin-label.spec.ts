import { compareKurinNumbers, inKurin, kurinBranchTags, kurinBranchesName, kurinInText, kurinLabel, kurinName, kurinTitle } from './kurin-label';

describe('kurin labels', () => {
  it('names a numbered kurin by its number', () => {
    expect(kurinLabel(47)).toBe('к. ч. 47');
    expect(kurinName(47)).toBe('Курінь ч. 47');
    expect(kurinTitle(47)).toBe('47 курінь');
    expect(inKurin(47)).toBe('курені ч. 47');
  });

  it('names a підготовчий курінь without a number', () => {
    expect(kurinLabel(null)).toBe('підг. курінь');
    expect(kurinName(undefined)).toBe('Підготовчий курінь');
    expect(kurinTitle(null)).toBe('Підготовчий курінь');
    expect(inKurin(null)).toBe('підготовчому курені');
  });

  it('sorts підготовчі last', () => {
    expect([null, 12, 3].sort(compareKurinNumbers)).toEqual([3, 12, null]);
  });
});

describe('senior kurins', () => {
  it('names one kurin in both of its гілки', () => {
    expect(kurinBranchesName([{ branch: 'USP', number: 5 }, { branch: 'UPS', number: 20 }])).toBe('5-й курінь УСП · 20-й курінь УПС');
    expect(kurinBranchTags([{ branch: 'USP', number: 5 }, { branch: 'UPS', number: 20 }])).toBe('УСП · УПС');
    expect(kurinLabel(5, 'USP')).toBe('5-й курінь УСП');
    expect(kurinLabel(5, 'UPYu')).toBe('к. ч. 5');
  });
});

describe('kurinInText', () => {
  it('names a senior kurin by its гілки, not by the УПЮ number it shares', () => {
    expect(kurinInText([{ branch: 'USP', number: 5 }])).toBe('5-й курінь УСП');
    expect(kurinInText([{ branch: 'UPYu', number: 5 }])).toBe('к. ч. 5');
    expect(kurinInText([], null)).toBe('підг. курінь');
  });
});

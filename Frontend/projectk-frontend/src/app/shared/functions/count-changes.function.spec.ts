import { countChanges, unsavedChangesLabel } from './count-changes.function';

describe('countChanges', () => {
  it('counts each key that differs, and keys only one side has', () => {
    expect(countChanges({ a: 1, b: true, c: 'x' }, { a: 1, b: false, d: 'y' })).toBe(3);
  });

  it('counts nothing until both sides are known', () => {
    expect(countChanges(null, { a: 1 })).toBe(0);
  });

  it('treats a missing key and null alike', () => {
    expect(countChanges({ number: null }, {})).toBe(0);
  });
});

describe('unsavedChangesLabel', () => {
  it('says nothing for no changes', () => {
    expect(unsavedChangesLabel(0)).toBe('');
  });

  it.each([
    [1, '1 незбережена зміна'],
    [2, '2 незбережені зміни'],
    [4, '4 незбережені зміни'],
    [5, '5 незбережених змін'],
    [11, '11 незбережених змін'],
    [12, '12 незбережених змін'],
    [21, '21 незбережена зміна'],
    [22, '22 незбережені зміни']
  ])('agrees %i with its noun', (count, label) => {
    expect(unsavedChangesLabel(count)).toBe(label);
  });
});

import { isUsableKey } from './is-usable-key.function';

describe('isUsableKey', () => {
  it('should accept a real key', () => {
    expect(isUsableKey('3f2b1c4d-0000-4a1b-9c2d-5e6f7a8b9c0d')).toBe(true);
  });

  it('should reject an empty guid in any case', () => {
    expect(isUsableKey('00000000-0000-0000-0000-000000000000')).toBe(false);
    expect(isUsableKey('00000000-0000-0000-0000-000000000000'.toUpperCase())).toBe(false);
  });

  it('should reject blank and missing values', () => {
    expect(isUsableKey('')).toBe(false);
    expect(isUsableKey('   ')).toBe(false);
    expect(isUsableKey(null)).toBe(false);
    expect(isUsableKey(undefined)).toBe(false);
  });
});

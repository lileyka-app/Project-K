import { formatPhone, isPossiblePhone, phoneCountryOfKrai, splitPhone, toStoredPhone } from './phone.function';

describe('phone.function', () => {
  it('stores a number as + and digits, in the chosen country', () => {
    expect(toStoredPhone('CZ', '777 123 456')).toBe('+420777123456');
    expect(toStoredPhone('UA', '050 123 45 67')).toBe('+380501234567');
    expect(toStoredPhone('UA', '+420 777 123 456')).toBe('+420777123456');
    expect(toStoredPhone('UA', '  ')).toBe('');
  });

  it('splits a stored number back into its country and national part', () => {
    expect(splitPhone('+420777123456', 'UA')).toEqual({ country: 'CZ', national: '777 123 456' });
    expect(splitPhone('', 'CZ')).toEqual({ country: 'CZ', national: '' });
    expect(splitPhone('тел. 050', 'UA')).toEqual({ country: 'UA', national: 'тел. 050' });
  });

  it('defaults to the край when it is a country, else to Ukraine', () => {
    expect(phoneCountryOfKrai('cz')).toBe('CZ');
    expect(phoneCountryOfKrai('ua')).toBe('UA');
    expect(phoneCountryOfKrai('xx')).toBe('UA');
    expect(phoneCountryOfKrai(null)).toBe('UA');
  });

  it('reads a stored number with spaces, and leaves an unreadable one as it is', () => {
    expect(formatPhone('+420777123456')).toBe('+420 777 123 456');
    expect(formatPhone('old text')).toBe('old text');
    expect(formatPhone(null)).toBe('');
  });

  it('flags a number that cannot be a real one', () => {
    expect(isPossiblePhone('+420777123456')).toBe(true);
    expect(isPossiblePhone('')).toBe(true);
    expect(isPossiblePhone('+42077')).toBe(false);
  });
});

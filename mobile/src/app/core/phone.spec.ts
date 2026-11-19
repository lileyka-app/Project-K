import { formatPhone, isPossiblePhone, phoneCountryOfKrai, toStoredPhone } from './phone';

describe('phone', () => {
  it('stores a number as + and digits, read in the край country when typed without +', () => {
    expect(toStoredPhone('777 123 456', 'CZ')).toBe('+420777123456');
    expect(toStoredPhone('050 123 45 67', 'UA')).toBe('+380501234567');
    expect(toStoredPhone('+380 50 123 45 67', 'CZ')).toBe('+380501234567');
    expect(toStoredPhone('  ', 'UA')).toBe('');
  });

  it('defaults to the край when it is a country, else to Ukraine', () => {
    expect(phoneCountryOfKrai('cz')).toBe('CZ');
    expect(phoneCountryOfKrai('xx')).toBe('UA');
    expect(phoneCountryOfKrai(undefined)).toBe('UA');
  });

  it('reads a stored number with spaces, and leaves an unreadable one as it is', () => {
    expect(formatPhone('+420777123456')).toBe('+420 777 123 456');
    expect(formatPhone('old text')).toBe('old text');
    expect(formatPhone(null)).toBe('');
  });

  it('flags a number that cannot be a real one', () => {
    expect(isPossiblePhone('777 123 456', 'CZ')).toBe(true);
    expect(isPossiblePhone('', 'CZ')).toBe(true);
    expect(isPossiblePhone('77', 'CZ')).toBe(false);
  });

  it('holds one length range for every country instead of each country own', () => {
    expect(isPossiblePhone('050 123 45', 'UA')).toBe(true);
    expect(isPossiblePhone('+123 4567 8901 2345', 'UA')).toBe(true);
    expect(isPossiblePhone('+1234 5678 9012 3456', 'UA')).toBe(false);
    expect(isPossiblePhone('050 123 45 67 89 01 2', 'UA')).toBe(false);
  });
});

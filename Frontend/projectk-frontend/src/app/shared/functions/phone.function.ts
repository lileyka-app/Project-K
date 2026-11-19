import {
  AsYouType,
  CountryCode,
  getCountries,
  getCountryCallingCode,
  parsePhoneNumberFromString
} from 'libphonenumber-js';

/** A country the phone field offers: its code, its calling code and a label in Ukrainian. */
export interface PhoneCountry {
  code: CountryCode;
  dialCode: string;
  label: string;
}

export const DEFAULT_PHONE_COUNTRY: CountryCode = 'UA';

const names = new Intl.DisplayNames(['uk'], { type: 'region' });

/** Every country, by name; the ones a край is most likely to need are not singled out. */
export const PHONE_COUNTRIES: PhoneCountry[] = getCountries()
  .map(code => ({ code, dialCode: `+${getCountryCallingCode(code)}`, label: names.of(code) ?? code }))
  .sort((a, b) => a.label.localeCompare(b.label, 'uk'));

/** The country a край's numbers default to: its code when that is a country (ua, cz, pl…), else Ukraine. */
export function phoneCountryOfKrai(kraiCode: string | null | undefined): CountryCode {
  const code = (kraiCode ?? '').toUpperCase() as CountryCode;
  return PHONE_COUNTRIES.some(country => country.code === code) ? code : DEFAULT_PHONE_COUNTRY;
}

/**
 * A stored number split for the field: its country and the national part. A number the library
 * cannot read (an old one) is kept whole in the national part, so nothing is lost on edit.
 */
export function splitPhone(value: string | null | undefined, fallback: CountryCode): { country: CountryCode; national: string } {
  const text = (value ?? '').trim();
  if (!text) {
    return { country: fallback, national: '' };
  }

  const parsed = parsePhoneNumberFromString(text, fallback);
  return parsed?.country && parsed.isPossible()
    ? { country: parsed.country, national: parsed.formatNational() }
    : { country: fallback, national: text };
}

/**
 * The number as it is stored: «+», the calling code and the digits, as +420777123456. Empty when
 * no digits were typed. A national part that already starts with «+» is taken as a whole number.
 */
export function toStoredPhone(country: CountryCode, national: string | null | undefined): string {
  const text = (national ?? '').trim();
  const digits = text.replaceAll(/\D/g, '');
  if (!digits) {
    return '';
  }

  if (text.startsWith('+')) {
    return `+${digits}`;
  }

  const parsed = parsePhoneNumberFromString(text, country);
  return parsed ? parsed.number : `+${getCountryCallingCode(country)}${digits.replace(/^0+/, '')}`;
}

/**
 * How many digits a number has with its calling code, the same for every country: E.164 allows at
 * most 15, and no country's numbers come shorter than 7. A country's own lengths are not checked,
 * so a number from any край goes through. The server holds the same bounds (`PhoneNumberRules`).
 */
export const PHONE_DIGITS = { min: 7, max: 15 } as const;

export type PhoneLengthIssue = 'short' | 'long';

/** What is wrong with a stored number's length, or null; empty counts as fine, a required check is separate. */
export function phoneLengthIssue(stored: string | null | undefined): PhoneLengthIssue | null {
  const digits = (stored ?? '').replaceAll(/\D/g, '').length;
  if (!digits) {
    return null;
  }

  if (digits < PHONE_DIGITS.min) {
    return 'short';
  }

  return digits > PHONE_DIGITS.max ? 'long' : null;
}

/** Whether a stored number could be a real one; empty counts as fine, a required check is separate. */
export function isPossiblePhone(stored: string | null | undefined): boolean {
  return phoneLengthIssue(stored) === null;
}

/** The national part formatted as it is typed, as 777 123 456 for Czechia. */
export function formatAsYouType(country: CountryCode, national: string): string {
  return national.trim().startsWith('+') ? national : new AsYouType(country).input(national);
}

/** A stored number for reading: +420 777 123 456. Anything the library cannot read is shown as it is. */
export function formatPhone(value: string | null | undefined): string {
  const text = (value ?? '').trim();
  if (!text) {
    return '';
  }

  return parsePhoneNumberFromString(text)?.formatInternational() ?? text;
}

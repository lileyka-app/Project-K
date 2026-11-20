import { CountryCode, getCountries, getCountryCallingCode, parsePhoneNumberFromString } from 'libphonenumber-js';

/** The country a number without «+» is read in when the край does not name one. */
export const DEFAULT_PHONE_COUNTRY: CountryCode = 'UA';

const COUNTRIES = new Set<string>(getCountries());

/** The country a край's numbers default to: its code when that is a country (ua, cz, pl…), else Ukraine. */
export function phoneCountryOfKrai(kraiCode: string | null | undefined): CountryCode {
  const code = (kraiCode ?? '').toUpperCase();
  return COUNTRIES.has(code) ? (code as CountryCode) : DEFAULT_PHONE_COUNTRY;
}

/**
 * A typed number as it is stored: «+», the calling code and the digits, as +420777123456. A number
 * typed without «+» is read in the край's country (777 123 456 in Czechia). Empty stays empty;
 * one that cannot be read goes as typed, for the server to refuse in its own words.
 */
export function toStoredPhone(text: string | null | undefined, country: CountryCode): string {
  const typed = (text ?? '').trim();
  if (!typed) return '';
  return parsePhoneNumberFromString(typed, country)?.number ?? typed;
}

/**
 * How many digits a number has with its calling code, the same for every country: E.164 allows at
 * most 15, and no country's numbers come shorter than 7. A country's own lengths are not checked,
 * so a number from any край goes through. The web and the server hold the same bounds.
 */
export const PHONE_DIGITS = { min: 7, max: 15 } as const;

/** Whether a typed number could be a real one; empty counts as fine, a required check is separate. */
export function isPossiblePhone(text: string | null | undefined, country: CountryCode): boolean {
  const stored = toStoredPhone(text, country);
  if (!stored) return true;
  const digits = stored.startsWith('+') ? stored : getCountryCallingCode(country) + stored;
  const count = digits.replaceAll(/\D/g, '').length;
  return count >= PHONE_DIGITS.min && count <= PHONE_DIGITS.max;
}

/** A stored number for reading: +420 777 123 456. Anything the library cannot read is shown as it is. */
export function formatPhone(value: string | null | undefined): string {
  const text = (value ?? '').trim();
  if (!text) return '';
  return parsePhoneNumberFromString(text)?.formatInternational() ?? text;
}

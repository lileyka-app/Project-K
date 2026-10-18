import { DuesEntryKind, INCOMING_DUES_KINDS } from '../models/dues.enums';
import { DuesEntryDto, QuarterDto } from '../models/group-dues.dto';

/** Lilejka counted in гривні before краї had profiles; a response without a currency is still that. */
export const DEFAULT_CURRENCY = 'UAH';

/** How numbers were written before краї had profiles; a response without a locale is still that. */
export const DEFAULT_NUMBER_LOCALE = 'uk-UA';

const amountFormats = new Map<string, Intl.NumberFormat>();
const symbols = new Map<string, string>();

/** «1 240,5» in uk-UA, «1,240.5» in en-CA. A locale the browser does not know falls back to uk-UA. */
function amountFormat(locale: string): Intl.NumberFormat {
  let format = amountFormats.get(locale);
  if (format === undefined) {
    try {
      format = new Intl.NumberFormat(locale, { minimumFractionDigits: 0, maximumFractionDigits: 2 });
    } catch {
      format = new Intl.NumberFormat(DEFAULT_NUMBER_LOCALE, { minimumFractionDigits: 0, maximumFractionDigits: 2 });
    }
    amountFormats.set(locale, format);
  }
  return format;
}

/** «₴», «Kč», «zł»: how the край's currency is written after an amount. */
export function currencySymbol(currency: string = DEFAULT_CURRENCY): string {
  let symbol = symbols.get(currency);
  if (symbol === undefined) {
    try {
      symbol = new Intl.NumberFormat('uk-UA', { style: 'currency', currency, currencyDisplay: 'narrowSymbol' })
        .formatToParts(0)
        .find(part => part.type === 'currency')?.value ?? currency;
    } catch {
      symbol = currency;
    }
    symbols.set(currency, symbol);
  }
  return symbol;
}

/** «1 240 ₴», «−300 Kč», the number written the край's way. A real minus, not a hyphen. */
export function money(amount: number, currency: string = DEFAULT_CURRENCY, locale: string = DEFAULT_NUMBER_LOCALE): string {
  const sign = amount < 0 ? '−' : '';
  return `${sign}${amountFormat(locale).format(Math.abs(amount))} ${currencySymbol(currency)}`;
}

/** The same with an explicit plus, for movements in and out of the box. */
export function signedMoney(amount: number, currency: string = DEFAULT_CURRENCY, locale: string = DEFAULT_NUMBER_LOCALE): string {
  return amount > 0 ? `+${money(amount, currency, locale)}` : money(amount, currency, locale);
}

/**
 * What an operation does to the box, signed: money in is plus, money out is minus, a correction is
 * as entered. An exchange moves money between cash and card and is neither — null.
 */
export function signedDuesAmount(entry: Pick<DuesEntryDto, 'kind' | 'amount'>): number | null {
  if (entry.kind === DuesEntryKind.Exchange) {
    return null;
  }
  if (entry.kind === DuesEntryKind.Correction) {
    return entry.amount;
  }
  return INCOMING_DUES_KINDS.has(entry.kind) ? entry.amount : -entry.amount;
}

/** The amount as the history shows it: signed, or bare for an exchange. */
export function duesAmountLabel(
  entry: Pick<DuesEntryDto, 'kind' | 'amount'>,
  currency: string = DEFAULT_CURRENCY,
  locale: string = DEFAULT_NUMBER_LOCALE
): string {
  const signed = signedDuesAmount(entry);
  return signed === null ? money(entry.amount, currency, locale) : signedMoney(signed, currency, locale);
}

/** Whether the history paints the amount as coming in; null for an exchange. */
export function isIncomingDues(entry: Pick<DuesEntryDto, 'kind' | 'amount'>): boolean | null {
  const signed = signedDuesAmount(entry);
  return signed === null ? null : signed > 0;
}

const ROMAN = ['I', 'II', 'III', 'IV'];

/** «II кв. 2026». */
export function quarterLabel(quarter: QuarterDto): string {
  return `${ROMAN[quarter.number - 1]} кв. ${quarter.year}`;
}

/** «II 2026» — for a column head. */
export function quarterShort(quarter: QuarterDto): string {
  return `${ROMAN[quarter.number - 1]} ${quarter.year}`;
}

export function quarterKey(quarter: QuarterDto): string {
  return `${quarter.year}-${quarter.number}`;
}

export function sameQuarter(a: QuarterDto, b: QuarterDto): boolean {
  return a.year === b.year && a.number === b.number;
}

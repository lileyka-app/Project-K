const WEEKDAYS = ['неділя', 'понеділок', 'вівторок', 'середа', 'четвер', 'пʼятниця', 'субота'];
const WEEKDAYS_SHORT = ['нд', 'пн', 'вт', 'ср', 'чт', 'пт', 'сб'];
const MONTHS = ['січня', 'лютого', 'березня', 'квітня', 'травня', 'червня', 'липня', 'серпня', 'вересня', 'жовтня', 'листопада', 'грудня'];
const MONTHS_SHORT = ['січ', 'лют', 'бер', 'кві', 'тра', 'чер', 'лип', 'сер', 'вер', 'жов', 'лис', 'гру'];

/** The web dashboard's wording (dashboardModule/functions), so both greet the same way. */
export function greeting(now: Date): string {
  const hour = now.getHours();
  if (hour < 5) return 'Доброї ночі';
  if (hour < 12) return 'Доброго ранку';
  return hour < 17 ? 'Доброго дня' : 'Доброго вечора';
}

/** «середа, 7 жовтня» */
export function todayLabel(now: Date): string {
  return `${WEEKDAYS[now.getDay()]}, ${now.getDate()} ${MONTHS[now.getMonth()]}`;
}

/** «сьогодні», «завтра», else «сб, 11 жов». */
export function dayLabel(date: Date, today: Date): string {
  const days = Math.round((startOfDay(date) - startOfDay(today)) / 86_400_000);
  if (days === 0) return 'сьогодні';
  if (days === 1) return 'завтра';
  return `${WEEKDAYS_SHORT[date.getDay()]}, ${date.getDate()} ${MONTHS_SHORT[date.getMonth()]}`;
}

/** «10:00» or «10:00–12:00»; nothing for an all-day item. */
export function timeLabel(start: Date, end: Date | null, isAllDay: boolean): string {
  if (isAllDay) return '';
  return end ? `${clock(start)}–${clock(end)}` : clock(start);
}

/** «7 жовтня 2010» */
export function dateLabel(date: Date): string {
  return `${date.getDate()} ${MONTHS[date.getMonth()]} ${date.getFullYear()}`;
}

export function initials(firstName: string | null | undefined, lastName: string | null | undefined): string {
  return `${(firstName ?? '').trim().charAt(0)}${(lastName ?? '').trim().charAt(0)}`.toUpperCase();
}

/** Lilejka counted in гривні before краї had profiles; a response without a currency is still that. */
export const DEFAULT_CURRENCY = 'UAH';

/** How numbers were written before краї had profiles; a response without a locale is still that. */
export const DEFAULT_NUMBER_LOCALE = 'uk-UA';

const numberFormats = new Map<string, Intl.NumberFormat>();

/** «1 240,5» in uk-UA, «1,240.5» in en-CA (the web's dues-format). An unknown locale falls back to uk-UA. */
export function amountFormat(locale: string = DEFAULT_NUMBER_LOCALE): Intl.NumberFormat {
  let format = numberFormats.get(locale);
  if (!format) {
    try {
      format = new Intl.NumberFormat(locale, { minimumFractionDigits: 0, maximumFractionDigits: 2 });
    } catch {
      format = new Intl.NumberFormat(DEFAULT_NUMBER_LOCALE, { minimumFractionDigits: 0, maximumFractionDigits: 2 });
    }
    numberFormats.set(locale, format);
  }
  return format;
}

const symbols = new Map<string, string>();

function symbolOf(currency: string): string {
  try {
    const parts = new Intl.NumberFormat('uk-UA', { style: 'currency', currency, currencyDisplay: 'narrowSymbol' }).formatToParts(0);
    return parts.find((part) => part.type === 'currency')?.value ?? currency;
  } catch {
    return currency;
  }
}

/** «₴», «Kč», «zł»: how the край's currency is written after an amount (the web's dues-format). */
export function currencySymbol(currency: string = DEFAULT_CURRENCY): string {
  if (!symbols.has(currency)) symbols.set(currency, symbolOf(currency));
  return symbols.get(currency) ?? currency;
}

/** «150 ₴» with the sign kept for a debt, the number written the край's way. */
export function money(amount: number, currency: string = DEFAULT_CURRENCY, locale: string = DEFAULT_NUMBER_LOCALE): string {
  return `${amountFormat(locale).format(amount)} ${currencySymbol(currency)}`;
}

function clock(date: Date): string {
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
}

function startOfDay(date: Date): number {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}

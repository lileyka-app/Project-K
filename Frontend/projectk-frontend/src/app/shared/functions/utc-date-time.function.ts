const UTC_OFFSET_PATTERN = /(Z|[+-]\d{2}:?\d{2})$/i;
const DATE_TIME_PATTERN = /^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}/;
const DEFAULT_FORMAT: Readonly<Intl.DateTimeFormatOptions> = { dateStyle: 'medium', timeStyle: 'short' };

export function normalizeUtcDateTimeString(value: string): string {
  const trimmed = value.trim();

  if (!DATE_TIME_PATTERN.test(trimmed) || UTC_OFFSET_PATTERN.test(trimmed)) {
    return trimmed;
  }

  return `${trimmed.replace(' ', 'T')}Z`;
}

export function parseUtcDateTime(value: string | Date | null | undefined): Date | null {
  if (!value) {
    return null;
  }

  const date = value instanceof Date
    ? value
    : new Date(normalizeUtcDateTimeString(value));

  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatUtcDateTime(
  value: string | Date | null | undefined,
  options: Intl.DateTimeFormatOptions = DEFAULT_FORMAT,
  locale = 'uk-UA'
): string | null {
  if (!value) {
    return null;
  }

  const date = parseUtcDateTime(value);
  return date ? date.toLocaleString(locale, options) : String(value);
}

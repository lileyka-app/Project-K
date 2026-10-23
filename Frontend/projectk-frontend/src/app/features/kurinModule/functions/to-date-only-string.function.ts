type DateOnlyInput = Date | string | null | undefined;

export function toDateOnlyString(d: DateOnlyInput): string | null {
    if (!d) return null;
    if (typeof d === 'string') {
      if (/^\d{4}-\d{2}-\d{2}$/.test(d)) return d;
      const only = d.split('T')[0];
      return only;
    }
    const y = d.getFullYear();
    const m = (d.getMonth() + 1).toString().padStart(2, '0');
    const day = d.getDate().toString().padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

export function parseDateOnlyString(value: DateOnlyInput): Date | null {
    if (!value) return null;
    if (value instanceof Date) return value;

    const dateOnly = toDateOnlyString(value);
    if (!dateOnly) return null;

    const [year, month, day] = dateOnly.split('-').map(Number);
    if (!year || !month || !day) return null;

    return new Date(year, month - 1, day);
  }

export function dateOnlyTime(value: DateOnlyInput): number {
    const parsed = parseDateOnlyString(value);
    return parsed?.getTime() ?? 0;
  }

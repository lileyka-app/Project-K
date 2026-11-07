import { KurinDto } from './kurin.models';

/** The API's limits on a kurin's record (UpdateKurinRequest). */
export const KURIN_LIMITS = { namedAfter: 200, stanytsia: 120, regionOrCountry: 120, description: 4000 } as const;

/** What the «Дані куреня» sheet edits: the web kurin-panel's profile form. */
export interface KurinProfileDraft {
  namedAfter: string;
  stanytsia: string;
  regionOrCountry: string;
  description: string;
}

export function kurinProfileDraft(kurin: KurinDto): KurinProfileDraft {
  return {
    namedAfter: kurin.namedAfter ?? '',
    stanytsia: kurin.stanytsia ?? '',
    regionOrCountry: kurin.regionOrCountry ?? '',
    description: kurin.description ?? '',
  };
}

export function kurinProfileValid(draft: KurinProfileDraft): boolean {
  return (Object.keys(KURIN_LIMITS) as (keyof KurinProfileDraft)[]).every((field) => draft[field].trim().length <= KURIN_LIMITS[field]);
}

/**
 * `PUT kurin/{key}` rewrites the whole record, so the rest of it (number, verification) goes back
 * as it came, as the web sends it; empty fields go as null. Role names stay out: they belong to the
 * kurin's settings, and a УПЮ kurin is refused any.
 */
export function kurinProfileRequest(kurin: KurinDto, draft: KurinProfileDraft): KurinDto {
  const text = (value: string) => value.trim() || null;
  const record: KurinDto = { ...kurin };
  delete record.roleNames;
  return {
    ...record,
    namedAfter: text(draft.namedAfter),
    stanytsia: text(draft.stanytsia),
    regionOrCountry: text(draft.regionOrCountry),
    description: text(draft.description),
  };
}

/** The API answers an update that changes nothing with an error, so an unchanged form is not sent. */
export function kurinProfileChanged(kurin: KurinDto, draft: KurinProfileDraft): boolean {
  const request = kurinProfileRequest(kurin, draft);
  return (['namedAfter', 'stanytsia', 'regionOrCountry', 'description'] as const).some(
    (field) => request[field] !== (kurin[field]?.trim() || null),
  );
}

/** The reader's IANA zone, which the report needs to print the hours their clock showed. */
export function browserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone ?? '';
  } catch {
    return '';
  }
}

/** The report's file name, like the server's (its own header is not readable cross-origin). */
export function reportFileName(number: number | null, at: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  const stamp = `${at.getFullYear()}${pad(at.getMonth() + 1)}${pad(at.getDate())}-${pad(at.getHours())}${pad(at.getMinutes())}`;
  return `kurin-${number ?? 'pidhotovchyi'}-report-${stamp}.pdf`;
}

import { DuesAmountDto, QuarterDto } from './group-dues.dto';

/** How a пільга of the край changes the вкладка: a percent off, a sum off, or nothing to pay. */
export type ConcessionKind = 'percent' | 'amount' | 'exempt';

/** A part of a quarter's вкладка a пільга may lower. */
export type DuesPart = 'stanytsia' | 'krai' | 'kurin' | 'group';

export const CONCESSION_KINDS: ConcessionKind[] = ['percent', 'amount', 'exempt'];
export const DUES_PARTS: DuesPart[] = ['stanytsia', 'krai', 'kurin', 'group'];

export const CONCESSION_KIND_LABELS: Record<ConcessionKind, string> = {
  percent: 'Знижка у відсотках',
  amount: 'Знижка на суму',
  exempt: 'Повне звільнення'
};

export const DUES_PART_LABELS: Record<DuesPart, string> = {
  stanytsia: 'Станиця',
  krai: 'Край',
  kurin: 'Курінь',
  group: 'Гурток'
};

/** A пільга of the край as its profile keeps it. */
export interface KraiConcessionDocument {
  /** Stays the same when the name changes: the grants point at it. */
  key: string;
  name: string;
  kind: ConcessionKind;
  /** The percent or the sum taken off; left out for an exemption. */
  value?: number;
  appliesTo: DuesPart[];
}

/** A пільга the край offers, as the гурток reads it. */
export interface KraiConcessionDto {
  key: string;
  name: string;
  kind: ConcessionKind;
  value: number;
  appliesTo: DuesPart[];
}

/** A пільга given to a person, with its rule as it was when given. */
export interface DuesConcessionGrantDto extends KraiConcessionDto {
  grantKey: string;
  concessionKey: string;
  fromQuarter: QuarterDto;
  toQuarter: QuarterDto | null;
  reason: string | null;
  isActiveNow: boolean;
}

export interface GrantDuesConcessionRequest {
  concessionKey: string;
  fromQuarter: QuarterDto;
  toQuarter: QuarterDto | null;
  reason: string | null;
}

export interface UpdateDuesConcessionGrantRequest {
  toQuarter: QuarterDto | null;
  reason: string | null;
}

/** `GET api/krai/{code}/dues/concessions` — what the край's пільги took off in one quarter. */
export interface KraiConcessionReportDto {
  kraiCode: string;
  kraiName: string;
  currency: string;
  numberLocale: string;
  quarter: QuarterDto;
  kurinCount: number;
  concessions: KraiConcessionReportRowDto[];
}

export interface KraiConcessionReportRowDto {
  key: string;
  name: string;
  people: number;
  reduction: DuesAmountDto;
  kurins: { kurinKey: string; number: number | null; people: number; reduction: number }[];
}

/** «−20 % · станиця, курінь», «−100 ₴ · гурток», «звільнення · уся вкладка». */
export function describeConcession(
  concession: Pick<KraiConcessionDto, 'kind' | 'value' | 'appliesTo'>,
  money: (amount: number) => string
): string {
  const parts = concession.appliesTo.length === DUES_PARTS.length
    ? 'уся вкладка'
    : concession.appliesTo.map(part => DUES_PART_LABELS[part].toLowerCase()).join(', ');
  return `${concessionRule(concession, money)} · ${parts}`;
}

function concessionRule(concession: Pick<KraiConcessionDto, 'kind' | 'value'>, money: (amount: number) => string): string {
  switch (concession.kind) {
    case 'exempt':
      return 'звільнення';
    case 'percent':
      return `−${concession.value} %`;
    default:
      return `−${money(concession.value ?? 0)}`;
  }
}

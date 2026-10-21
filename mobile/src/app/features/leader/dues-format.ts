import { amountFormat, currencySymbol, DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE } from '../../me/labels';
import {
  DuesAccountStanding,
  DuesEntryDto,
  DuesEntryKind,
  DuesPaymentMethod,
  DuesUpstream,
  KurinDuesRateDto,
  PlastYearDto,
  QuarterDto,
  SetKurinDuesRateRequest,
  UpsertDuesEntryRequest,
} from './leader.models';

/** The web's wording and arithmetic for a box (duesModule/models/dues.enums, functions/dues-format). */
export const KIND_LABELS: Record<DuesEntryKind, string> = {
  Contribution: 'Вкладка',
  Refund: 'Повернення',
  TransferToKurin: 'Передача курінному',
  TransferToStanytsia: 'Передача в станицю',
  TransferToKrai: 'Передача в край',
  Expense: 'Витрата',
  OtherIncome: 'Інший прихід',
  Exchange: 'Обмін готівка ↔ картка',
  Correction: 'Корекція',
};

export const METHOD_LABELS: Record<DuesPaymentMethod, string> = { Cash: 'Готівка', Card: 'Картка' };

/** Kinds about one person's вкладка, which name a membership. */
export const PERSONAL_KINDS: ReadonlySet<DuesEntryKind> = new Set<DuesEntryKind>(['Contribution', 'Refund', 'Correction']);

/** What a гурток's box records; handing to the станиця is the kurin's. */
export const GROUP_KINDS: readonly DuesEntryKind[] = [
  'Contribution',
  'Refund',
  'Expense',
  'OtherIncome',
  'TransferToKurin',
  'Exchange',
  'Correction',
];

/** What the kurin's own box records: nothing personal, and what goes up to the станиця. */
export const KURIN_KINDS: readonly DuesEntryKind[] = ['Expense', 'OtherIncome', 'TransferToStanytsia', 'TransferToKrai', 'Exchange'];

export const paysStanytsia = (upstream: DuesUpstream): boolean => upstream !== 'krai';
export const paysKrai = (upstream: DuesUpstream): boolean => upstream !== 'stanytsia';

/** The web's kurinDuesKinds: a transfer only where the край takes money. */
export function kurinKinds(upstream: DuesUpstream): DuesEntryKind[] {
  return KURIN_KINDS.filter(
    (kind) => (kind !== 'TransferToStanytsia' || paysStanytsia(upstream)) && (kind !== 'TransferToKrai' || paysKrai(upstream)),
  );
}

export const STANDING_LABELS: Record<DuesAccountStanding, string | null> = {
  Current: null,
  Moved: 'переведений',
  Left: 'вибув',
};

const INCOMING: ReadonlySet<DuesEntryKind> = new Set<DuesEntryKind>(['Contribution', 'OtherIncome']);
const ROMAN = ['I', 'II', 'III', 'IV'];

/** «1 240 ₴», «−300 Kč»: a real minus, not a hyphen. */
export function duesMoney(amount: number, currency: string = DEFAULT_CURRENCY, locale: string = DEFAULT_NUMBER_LOCALE): string {
  return `${amount < 0 ? '−' : ''}${amountFormat(locale).format(Math.abs(amount))} ${currencySymbol(currency)}`;
}

/** What an operation does to the box: plus in, minus out, a correction as entered, null for an exchange. */
export function signedAmount(entry: Pick<DuesEntryDto, 'kind' | 'amount'>): number | null {
  if (entry.kind === 'Exchange') return null;
  if (entry.kind === 'Correction') return entry.amount;
  return INCOMING.has(entry.kind) ? entry.amount : -entry.amount;
}

/** The amount as the history shows it: signed, or bare for an exchange. */
export function amountLabel(
  entry: Pick<DuesEntryDto, 'kind' | 'amount'>,
  currency: string = DEFAULT_CURRENCY,
  locale: string = DEFAULT_NUMBER_LOCALE,
): string {
  const signed = signedAmount(entry);
  if (signed === null) return duesMoney(entry.amount, currency, locale);
  return signed > 0 ? `+${duesMoney(signed, currency, locale)}` : duesMoney(signed, currency, locale);
}

/** A person's balance: «сплачено», «+20 ₴» ahead, «−150 ₴» owed. */
export function balanceLabel(balance: number, currency: string = DEFAULT_CURRENCY, locale: string = DEFAULT_NUMBER_LOCALE): string {
  if (balance === 0) return 'сплачено';
  return balance > 0 ? `+${duesMoney(balance, currency, locale)}` : duesMoney(balance, currency, locale);
}

export function methodLabel(entry: Pick<DuesEntryDto, 'method' | 'counterMethod'>): string {
  const from = METHOD_LABELS[entry.method];
  return entry.counterMethod ? `${from} → ${METHOD_LABELS[entry.counterMethod]}` : from;
}

/** «II кв. 2026». */
export function quarterLabel(quarter: QuarterDto): string {
  return `${ROMAN[quarter.number - 1]} кв. ${quarter.year}`;
}

export function quarterKey(quarter: QuarterDto): string {
  return `${quarter.year}-${quarter.number}`;
}

export function sameQuarter(a: QuarterDto, b: QuarterDto): boolean {
  return a.year === b.year && a.number === b.number;
}

/** The quarter a date-only string («2026-10-04») falls in. */
export function quarterOfDate(date: string): QuarterDto {
  const [year, month] = date.split('-').map(Number);
  return { year, number: Math.floor((month - 1) / 3) + 1 };
}

/** Every quarter on the table, oldest first. */
export function allQuarters(years: PlastYearDto[]): QuarterDto[] {
  return unique(years.flatMap((y) => y.quarters));
}

/** Quarters a rate or a пільга may start from: the years on the table plus the one after now. */
export function fromQuarterOptions(years: PlastYearDto[], current: QuarterDto): QuarterDto[] {
  const next: QuarterDto = current.number === 4 ? { year: current.year + 1, number: 1 } : { year: current.year, number: current.number + 1 };
  return unique([...years.flatMap((y) => y.quarters), next]);
}

/** The rate in force: the latest that started at or before the quarter. */
export function rateAt<T extends { fromQuarter: QuarterDto }>(rates: T[], quarter: QuarterDto): T | null {
  const index = (q: QuarterDto) => q.year * 4 + q.number;
  return (
    rates
      .filter((rate) => index(rate.fromQuarter) <= index(quarter))
      .sort((a, b) => index(b.fromQuarter) - index(a.fromQuarter))[0] ?? null
  );
}

/** A Date as the API's date-only string, in the phone's own day. */
export function dateOnly(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

/** «04.10.2026» from «2026-10-04», as the web's history shows dates. */
export function shortDate(date: string): string {
  const [year, month, day] = date.split('-');
  return `${day}.${month}.${year}`;
}

/** The draft the entry form holds; amount is what was typed. */
export interface EntryDraft {
  kind: DuesEntryKind;
  method: DuesPaymentMethod;
  amount: string;
  occurredOn: string;
  membershipKey: string | null;
  collectedByMemberKey: string | null;
  note: string;
}

export interface EntryProblems {
  amount?: string;
  person?: string;
  date?: string;
}

/** The web form's rules: a personal kind names a person, only a correction goes below zero, a date. */
export function entryProblems(draft: EntryDraft): EntryProblems {
  const problems: EntryProblems = {};
  const amount = Number(draft.amount.replace(',', '.'));
  if (!draft.amount.trim()) problems.amount = 'Сума потрібна.';
  else if (!Number.isFinite(amount) || (draft.kind === 'Correction' ? amount === 0 : amount <= 0)) {
    problems.amount = 'Сума має бути більшою за нуль.';
  }
  if (PERSONAL_KINDS.has(draft.kind) && !draft.membershipKey) problems.person = 'Чия це вкладка?';
  if (!/^\d{4}-\d{2}-\d{2}$/.test(draft.occurredOn)) problems.date = 'Вкажи дату.';
  return problems;
}

/** The request the web's dues-entry-dialog sends for a valid draft. */
export function entryRequest(draft: EntryDraft): UpsertDuesEntryRequest {
  const personal = PERSONAL_KINDS.has(draft.kind);
  return {
    kind: draft.kind,
    method: draft.method,
    counterMethod: draft.kind === 'Exchange' ? (draft.method === 'Cash' ? 'Card' : 'Cash') : null,
    amount: Number(draft.amount.replace(',', '.')),
    occurredOn: draft.occurredOn,
    membershipKey: personal ? draft.membershipKey : null,
    collectedByMemberKey: draft.collectedByMemberKey,
    note: draft.note.trim() || null,
  };
}

/** The web's line under the kurin's box about the станиця and kurin parts in force now. */
export function kurinRatesText(
  rate: KurinDuesRateDto | null,
  currency: string = DEFAULT_CURRENCY,
  locale: string = DEFAULT_NUMBER_LOCALE,
): string {
  if (!rate) return 'Ставки ще не задано — гуртки нічого не нараховують, доки їх немає.';
  const articles = rate.articles
    .map((a) => `${a.name} ${duesMoney(a.amount, currency, locale)}${withPilha(a.amount, a.reducedAmount, currency, locale)}`)
    .join(' · ');
  return `${articles} за квартал з кожного юнака. Гурткову частину кожен гурток задає собі.`;
}

/** " (з пільгою …)" when a пільга lowers the amount, else nothing. */
export function withPilha(
  full: number,
  reduced: number,
  currency: string = DEFAULT_CURRENCY,
  locale: string = DEFAULT_NUMBER_LOCALE,
): string {
  return reduced !== full ? ` (з пільгою ${duesMoney(reduced, currency, locale)})` : '';
}

/**
 * The server's KurinDuesArticles.AreStandard: only the two standard статті, which the phone's
 * three-amount form can change without losing anything. Any other set is edited on the web.
 */
export function articlesAreStandard(rate: KurinDuesRateDto | null): boolean {
  if (!rate || rate.articles.length === 0) return true;
  const [up, kurin] = rate.articles;
  return (
    rate.articles.length === 2 &&
    ((up.name === 'Станиця' && up.recipient === 'Stanytsia') || (up.name === 'Край' && up.recipient === 'Krai')) &&
    kurin.name === 'Курінь' &&
    kurin.recipient === 'Kurin' &&
    kurin.reducedAmount === kurin.amount
  );
}

/** The kurin-rate form as typed. */
export interface KurinRateDraft {
  full: string;
  reduced: string;
  share: string;
}

const typed = (value: string): number | null => (value.trim() === '' ? null : Number(value.replace(',', '.')));

/** The web's check that the пільгова part is not above the full one. */
export function reducedAboveFull(draft: KurinRateDraft): boolean {
  const full = typed(draft.full);
  const reduced = typed(draft.reduced);
  return full !== null && reduced !== null && reduced > full;
}

/**
 * The request the web's kurin-rate-dialog sends, or null while the draft cannot be saved: the full
 * станиця part and the kurin's are needed, nothing is below zero, and an empty пільгова is the full.
 */
export function kurinRateRequest(draft: KurinRateDraft, fromQuarter: QuarterDto): SetKurinDuesRateRequest | null {
  const full = typed(draft.full);
  const share = typed(draft.share);
  const reduced = typed(draft.reduced) ?? full;
  if (full === null || share === null || reducedAboveFull(draft)) return null;
  if (![full, share, reduced].every((n) => n !== null && Number.isFinite(n) && n >= 0)) return null;
  return { fromQuarter, stanytsiaFull: full, stanytsiaReduced: reduced ?? full, kurinShare: share };
}

function unique(quarters: QuarterDto[]): QuarterDto[] {
  const seen = new Set<string>();
  return quarters
    .filter((q) => {
      const key = quarterKey(q);
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .sort((a, b) => a.year - b.year || a.number - b.number);
}

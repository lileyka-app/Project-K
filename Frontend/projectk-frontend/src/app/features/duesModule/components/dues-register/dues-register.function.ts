import { quarterLabel, sameQuarter } from '../../functions/dues-format.function';
import { DuesAccountDto, DuesAccountQuarterDto, DuesTagDto, PlastYearDto, QuarterDto } from '../../models/group-dues.dto';

/** One person in the treasurer's register, summed the way the treasurer reads it. */
export interface RegisterRow {
  account: DuesAccountDto;
  /** Paid towards the quarters of the chosen пластовий рік. */
  paidThisYear: number;
  /** Still owed for quarters already over. The running quarter is not in it. */
  debtPast: number;
  /** The running quarter, or null while it is not charged yet. */
  current: DuesAccountQuarterDto | null;
  /** The last quarter paid in full, counting what a surplus covers ahead at today's rate; null while in debt. */
  paidUntil: QuarterDto | null;
}

const index = (q: QuarterDto): number => q.year * 4 + q.number - 1;
const fromIndex = (i: number): QuarterDto => ({ year: Math.floor(i / 4), number: (i % 4) + 1 });

export function registerRow(account: DuesAccountDto, year: PlastYearDto | null, current: QuarterDto): RegisterRow {
  const inYear = (q: QuarterDto) => !!year?.quarters.some(y => sameQuarter(y, q));
  const paidThisYear = account.quarters.filter(q => inYear(q.quarter)).reduce((sum, q) => sum + q.paid.total, 0);
  const debtPast = account.quarters
    .filter(q => index(q.quarter) < index(current) && q.balance < 0)
    .reduce((sum, q) => sum - q.balance, 0);
  return {
    account,
    paidThisYear,
    debtPast,
    current: account.quarters.find(q => sameQuarter(q.quarter, current)) ?? null,
    paidUntil: paidUntil(account)
  };
}

function paidUntil(account: DuesAccountDto): QuarterDto | null {
  if (account.balance < 0 || account.quarters.length === 0) {
    return null;
  }
  const last = Math.max(...account.quarters.map(q => index(q.quarter)));
  const rate = account.rateNow ?? 0;
  const ahead = rate > 0 ? Math.floor(account.balance / rate) : 0;
  return fromIndex(last + ahead);
}

/** The register as a semicolon-separated file, the way a Ukrainian Excel opens it. */
export function registerCsv(rows: RegisterRow[], tags: DuesTagDto[], nameHeader: string, current: QuarterDto): string {
  const header = [nameHeader, 'Теги', 'Ставка за квартал', 'Сплачено за рік', 'Борг за минулі', quarterLabel(current), 'Сплачено до', 'Баланс'];
  const lines = rows.map(row => [
    text(row.account.fullName),
    text(tags.filter(t => row.account.tagKeys?.includes(t.tagKey)).map(t => t.name).join(', ')),
    number(row.account.rateNow),
    number(row.paidThisYear),
    number(row.debtPast),
    row.current ? `${number(row.current.paid.total)} / ${number(row.current.charged.total)}` : '',
    row.paidUntil ? quarterLabel(row.paidUntil) : '',
    number(row.account.balance)
  ]);
  return [header, ...lines].map(line => line.map(cell).join(';')).join('\r\n');
}

function number(value: number | null | undefined): string {
  return value === null || value === undefined ? '' : String(value).replace('.', ',');
}

/** A name a spreadsheet would run as a formula, kept as plain text. Numbers do not come through here: -300 stays a number. */
function text(value: string): string {
  return /^[=+\-@\t\r]/.test(value) ? `'${value}` : value;
}

function cell(value: string): string {
  return /[";\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
}

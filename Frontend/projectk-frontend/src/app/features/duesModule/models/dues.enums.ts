import type { DuesAccountStanding } from './group-dues.dto';

/** Mirrors `DuesEntryKind` on the backend; enums travel as strings. */
export enum DuesEntryKind {
  Contribution = 'Contribution',
  Refund = 'Refund',
  TransferToKurin = 'TransferToKurin',
  TransferToStanytsia = 'TransferToStanytsia',
  Expense = 'Expense',
  OtherIncome = 'OtherIncome',
  Exchange = 'Exchange',
  Correction = 'Correction',
  TransferToKrai = 'TransferToKrai'
}

export enum DuesPaymentMethod {
  Cash = 'Cash',
  Card = 'Card'
}

/** Where the money of a стаття goes: up to the станиця, up to the край on its own transfer, or kept by the kurin. */
export enum DuesRecipient {
  Stanytsia = 'Stanytsia',
  Kurin = 'Kurin',
  Krai = 'Krai'
}

/** Where the край's kurins hand the вкладка up: through the станиця, straight to the край, or both. */
export enum DuesUpstream {
  Stanytsia = 'stanytsia',
  Krai = 'krai',
  Both = 'both'
}

export const DUES_UPSTREAM_LABELS: Record<DuesUpstream, string> = {
  [DuesUpstream.Stanytsia]: 'Станиці, а вона — краю',
  [DuesUpstream.Krai]: 'Напряму краю',
  [DuesUpstream.Both]: 'І станиці, і краю — за статтями'
};

export const paysStanytsia = (upstream: DuesUpstream): boolean => upstream !== DuesUpstream.Krai;
export const paysKrai = (upstream: DuesUpstream): boolean => upstream !== DuesUpstream.Stanytsia;

export const DUES_RECIPIENT_LABELS: Record<DuesRecipient, string> = {
  [DuesRecipient.Stanytsia]: 'Станиці',
  [DuesRecipient.Krai]: 'Краю',
  [DuesRecipient.Kurin]: 'Куреню'
};

export const DUES_ENTRY_KIND_LABELS: Record<DuesEntryKind, string> = {
  [DuesEntryKind.Contribution]: 'Вкладка',
  [DuesEntryKind.Refund]: 'Повернення',
  [DuesEntryKind.TransferToKurin]: 'Передача курінному',
  [DuesEntryKind.TransferToStanytsia]: 'Передача в станицю',
  [DuesEntryKind.TransferToKrai]: 'Передача в край',
  [DuesEntryKind.Expense]: 'Витрата',
  [DuesEntryKind.OtherIncome]: 'Інший прихід',
  [DuesEntryKind.Exchange]: 'Обмін готівка ↔ картка',
  [DuesEntryKind.Correction]: 'Корекція'
};

export const DUES_PAYMENT_METHOD_LABELS: Record<DuesPaymentMethod, string> = {
  [DuesPaymentMethod.Cash]: 'Готівка',
  [DuesPaymentMethod.Card]: 'Картка'
};

/** Kinds that are about one person's вкладка and so name a membership. */
export const PERSONAL_DUES_KINDS: ReadonlySet<DuesEntryKind> = new Set([
  DuesEntryKind.Contribution,
  DuesEntryKind.Refund,
  DuesEntryKind.Correction
]);

/** What a гурток's box records; handing to the станиця is the kurin's. */
export const GROUP_DUES_KINDS: readonly DuesEntryKind[] = [
  DuesEntryKind.Contribution,
  DuesEntryKind.Refund,
  DuesEntryKind.Expense,
  DuesEntryKind.OtherIncome,
  DuesEntryKind.TransferToKurin,
  DuesEntryKind.Exchange,
  DuesEntryKind.Correction
];

/** What the kurin's own box records: nothing personal, and what goes up to the станиця and the край. */
export const KURIN_DUES_KINDS: readonly DuesEntryKind[] = [
  DuesEntryKind.Expense,
  DuesEntryKind.OtherIncome,
  DuesEntryKind.TransferToStanytsia,
  DuesEntryKind.TransferToKrai,
  DuesEntryKind.Exchange
];

/** What the kurin's box offers under its край's setting: a transfer only where the край takes money. */
export function kurinDuesKinds(upstream: DuesUpstream): DuesEntryKind[] {
  return KURIN_DUES_KINDS.filter(kind =>
    (kind !== DuesEntryKind.TransferToStanytsia || paysStanytsia(upstream))
    && (kind !== DuesEntryKind.TransferToKrai || paysKrai(upstream)));
}

/** Where a new стаття may send its money under the край's setting. */
export function duesRecipients(upstream: DuesUpstream): DuesRecipient[] {
  return [DuesRecipient.Stanytsia, DuesRecipient.Krai, DuesRecipient.Kurin].filter(recipient =>
    (recipient !== DuesRecipient.Stanytsia || paysStanytsia(upstream))
    && (recipient !== DuesRecipient.Krai || paysKrai(upstream)));
}

/** How a person stands towards a гурток's box, in a word; one who is still here needs none. */
export const DUES_STANDING_LABELS: Record<DuesAccountStanding, string | null> = {
  Current: null,
  Moved: 'переведений',
  Left: 'вибув'
};

/** Kinds that bring money into the box, as the history shows them with a plus. */
export const INCOMING_DUES_KINDS: ReadonlySet<DuesEntryKind> = new Set([
  DuesEntryKind.Contribution,
  DuesEntryKind.OtherIncome
]);

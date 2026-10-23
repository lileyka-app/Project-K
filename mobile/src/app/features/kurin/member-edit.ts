import { HttpErrorResponse } from '@angular/common/http';
import { dateLabel } from '../../me/labels';
import { dateOnly, utcDate } from './kurin.labels';
import {
  CardExtraField,
  CardFieldMode,
  CardFields,
  MemberDataGroup,
  MemberDto,
  MemberWarningDto,
  PlastLevelHistoryDto,
  WarningLevel,
} from './kurin.models';

/*
 * The rules of the web's member edit (pages/upsert-member) the phone sheet follows: who changes the
 * email, the ступені with their branch switches, and the three перестороги with who may add and
 * lift each. Pure, so the sheet stays a view and these stay tested.
 */

// ── Email ────────────────────────────────────────────────────────────────────────────────────────

/**
 * Web canEditEmail: a record without an account, an account still waiting for its letter, or an
 * admin. Anyone else's address is the account's, changed by its owner in the account settings.
 */
export function canEditEmail(member: Pick<MemberDto, 'userKey' | 'accountStatus'>, isAdmin: boolean): boolean {
  return !member.userKey || member.accountStatus === 'PendingActivation' || isAdmin;
}

export function isPendingAccount(member: Pick<MemberDto, 'userKey' | 'accountStatus'>): boolean {
  return !!member.userKey && member.accountStatus === 'PendingActivation';
}

export function looksLikeEmail(value: string): boolean {
  // One «@» with something before it, and a dot inside the domain; no spaces. Checked by hand: the
  // equivalent regex backtracks on long input.
  const text = value.trim();
  if (/\s/.test(text)) return false;
  const [local, domain, ...rest] = text.split('@');
  return rest.length === 0 && !!local && domain?.slice(1, -1).includes('.') === true;
}

// ── Ступені ──────────────────────────────────────────────────────────────────────────────────────

export type RankBranch = 'base' | 'UPYu' | 'USP' | 'UPS';

/** The web's date fields, in its order and wording. */
export const RANK_FIELDS: { level: string; label: string; branch: RankBranch }[] = [
  { level: 'Entry', label: 'Дата вступу до Пласту', branch: 'base' },
  { level: 'Prykhylnyk', label: 'Дата здобуття прихильника', branch: 'base' },
  { level: 'Uchasnyk', label: 'Дата заприсяження', branch: 'base' },
  { level: 'Rozviduvach', label: 'Дата здобуття розвідувача', branch: 'UPYu' },
  { level: 'Skob', label: 'Дата здобуття скоба/вірлиці', branch: 'UPYu' },
  { level: 'HetmanskiySkob', label: 'Дата здобуття гетьманського скоба/вірлиці', branch: 'UPYu' },
  { level: 'Starshoplastun', label: 'Дата переходу в УСП', branch: 'USP' },
  { level: 'Senior', label: 'Дата переходу в УПС', branch: 'UPS' },
  { level: 'SeniorPratsi', label: 'Дата здобуття пл. сен. праці', branch: 'UPS' },
  { level: 'SeniorDovirja', label: 'Дата здобуття пл. сен. довірʼя', branch: 'UPS' },
  { level: 'SeniorKerivnytstva', label: 'Дата здобуття пл. сен. керівництва', branch: 'UPS' },
];

/** The switches above the dates (web «УПЮ / УСП / УПС»). */
export const RANK_BRANCHES: { branch: Exclude<RankBranch, 'base'>; label: string }[] = [
  { branch: 'UPYu', label: 'УПЮ' },
  { branch: 'USP', label: 'УСП' },
  { branch: 'UPS', label: 'УПС' },
];

/** Level → «YYYY-MM-DD», or '' when not achieved. */
export type RankDates = Record<string, string>;

export function rankDates(histories: PlastLevelHistoryDto[] | null | undefined): RankDates {
  const dates: RankDates = Object.fromEntries(RANK_FIELDS.map((f) => [f.level, '']));
  for (const history of histories ?? []) {
    if (history.plastLevel in dates && history.dateAchieved) dates[history.plastLevel] = history.dateAchieved.slice(0, 10);
  }
  return dates;
}

/** A branch's switch is on when any of its dates is set (web setupAccordionAndToggles). */
export function openBranches(dates: RankDates): Record<Exclude<RankBranch, 'base'>, boolean> {
  const any = (branch: RankBranch) => RANK_FIELDS.some((f) => f.branch === branch && !!dates[f.level]);
  return { UPYu: any('UPYu'), USP: any('USP'), UPS: any('UPS') };
}

/** Turning a branch off clears its dates, as the web's onUpuToggle and the rest do. */
export function closeBranch(dates: RankDates, branch: RankBranch): RankDates {
  const next = { ...dates };
  for (const field of RANK_FIELDS) if (field.branch === branch) next[field.level] = '';
  return next;
}

/**
 * The ступені to send: every achieved one, an existing row keeping its key so the server updates it
 * rather than adding another (web buildPlastLevelsPayload).
 */
export function rankHistories(existing: PlastLevelHistoryDto[] | null | undefined, dates: RankDates): PlastLevelHistoryDto[] {
  return RANK_FIELDS.filter((f) => /^\d{4}-\d{2}-\d{2}$/.test(dates[f.level] ?? '')).map((f) => {
    const row = (existing ?? []).find((h) => h.plastLevel === f.level);
    return row ? { ...row, dateAchieved: dates[f.level] } : { plastLevel: f.level, dateAchieved: dates[f.level] };
  });
}

// ── Перестороги ──────────────────────────────────────────────────────────────────────────────────

export const WARNING_LEVELS: { level: WarningLevel; label: string }[] = [
  { level: 'Level1', label: 'Перша пересторога (3 місяці)' },
  { level: 'Level2', label: 'Друга пересторога (6 місяців)' },
  { level: 'Level3', label: 'Третя пересторога (12 місяців)' },
];

/** What the sheet will do on «Зберегти»: перестороги to add, and the keys of ones to lift. */
export interface WarningDraft {
  assign: WarningLevel[];
  cancel: string[];
}

export const NO_WARNING_CHANGES: WarningDraft = { assign: [], cancel: [] };

function inForce(warning: MemberWarningDto, now: number): boolean {
  return !warning.revokedAtUtc && (utcDate(warning.expiresAtUtc)?.getTime() ?? 0) > now;
}

/** The пересторога of this level in force now, if any. */
export function warningInForce(warnings: MemberWarningDto[] | null | undefined, level: WarningLevel, now = Date.now()): MemberWarningDto | null {
  return (warnings ?? []).find((w) => w.level === level && inForce(w, now)) ?? null;
}

/** Ticked: in force and not being lifted, or about to be added. */
export function warningChecked(warnings: MemberWarningDto[] | null | undefined, draft: WarningDraft, level: WarningLevel, now = Date.now()): boolean {
  if (draft.assign.includes(level)) return true;
  const current = warningInForce(warnings, level, now);
  return !!current && !draft.cancel.includes(current.memberWarningKey);
}

/** Each level comes only after the one before it (web canSelectWarning). */
export function canSelectWarning(warnings: MemberWarningDto[] | null | undefined, draft: WarningDraft, level: WarningLevel, now = Date.now()): boolean {
  if (level === 'Level1') return true;
  return warningChecked(warnings, draft, level === 'Level2' ? 'Level1' : 'Level2', now);
}

/**
 * Who gave a пересторога lifts it, and `anyone` (the kurin's «Скасовувати чужі перестороги») lifts
 * any; one not saved yet can always be unticked (web canCancel).
 */
export function canClearWarning(
  warnings: MemberWarningDto[] | null | undefined,
  draft: WarningDraft,
  level: WarningLevel,
  userKey: string | null | undefined,
  now = Date.now(),
  anyone = false,
): boolean {
  if (draft.assign.includes(level)) return true;
  const current = warningInForce(warnings, level, now);
  if (!current) return false;
  return anyone || (!!userKey && (current.issuedByUserKey ?? '').toLowerCase() === userKey.toLowerCase());
}

/** The draft after ticking or unticking one level; a move the rules refuse leaves it as it was. */
export function toggleWarning(
  warnings: MemberWarningDto[] | null | undefined,
  draft: WarningDraft,
  level: WarningLevel,
  on: boolean,
  userKey: string | null | undefined,
  now = Date.now(),
  anyone = false,
): WarningDraft {
  const current = warningInForce(warnings, level, now);
  if (on) {
    if (!canSelectWarning(warnings, draft, level, now) || warningChecked(warnings, draft, level, now)) return draft;
    if (current && draft.cancel.includes(current.memberWarningKey)) {
      return { ...draft, cancel: draft.cancel.filter((key) => key !== current.memberWarningKey) };
    }
    return { ...draft, assign: [...draft.assign, level] };
  }
  if (!canClearWarning(warnings, draft, level, userKey, now, anyone)) return draft;
  if (draft.assign.includes(level)) return { ...draft, assign: draft.assign.filter((l) => l !== level) };
  return current ? { ...draft, cancel: [...draft.cancel, current.memberWarningKey] } : draft;
}

/** The calls to make, lifts first and then additions from the lowest level (web processPendingWarnings). */
export function warningCalls(draft: WarningDraft): ({ kind: 'cancel'; key: string } | { kind: 'assign'; level: WarningLevel })[] {
  return [
    ...draft.cancel.map((key) => ({ kind: 'cancel' as const, key })),
    ...[...draft.assign].sort((a, b) => a.localeCompare(b)).map((level) => ({ kind: 'assign' as const, level })),
  ];
}

// ── Поля картки (профіль краю) ───────────────────────────────────────────────────────────────────

/** A kurin whose край has no profile asks for what the card always asked: name and phone, address, school and parents if known. */
export const DEFAULT_CARD_FIELDS: CardFields = {
  fields: { middleName: 'required', phoneNumber: 'required', address: 'optional', school: 'optional', parents: 'optional' },
  extra: [],
};

type StandardField = keyof CardFields['fields'];

const CARD_FIELD_MODES: ReadonlySet<string> = new Set(['required', 'optional', 'hidden']);

/** `GET kurin/{k}/profile` as far as the card needs it; any part may be missing. */
export interface KraiProfileCardFields {
  cardFields?: {
    fields?: Partial<Record<StandardField, string>> | null;
    extra?: Partial<CardExtraField>[] | null;
  } | null;
}

/** The profile's card fields, each part missing or not understood read as the default. */
export function cardFieldsOf(profile: KraiProfileCardFields | null | undefined): CardFields {
  const section = profile?.cardFields;
  const mode = (field: StandardField): CardFieldMode => {
    const value = section?.fields?.[field] ?? '';
    return CARD_FIELD_MODES.has(value) ? (value as CardFieldMode) : DEFAULT_CARD_FIELDS.fields[field];
  };
  return {
    fields: { middleName: mode('middleName'), phoneNumber: mode('phoneNumber'), address: mode('address'), school: mode('school'), parents: mode('parents') },
    extra: (section?.extra ?? [])
      .filter((field) => !!field?.key && !!field.label)
      .map((field) => ({ key: field.key!, label: field.label!, type: field.type === 'date' ? 'date' : 'text', required: !!field.required })),
  };
}

/** The parents' contacts: one block of the card, each value on its own optional. */
export const PARENT_FIELDS = ['motherFirstName', 'motherLastName', 'motherPhone', 'fatherFirstName', 'fatherLastName', 'fatherPhone'] as const;

export type ParentField = (typeof PARENT_FIELDS)[number];

const isParentField = (key: string): key is ParentField => (PARENT_FIELDS as readonly string[]).includes(key);

/**
 * Middle name, phone, address, school and parents as the край says; the rest of the form is always
 * asked. A parent's single value is never required on its own: required parents mean one phone (the
 * server says so in its own words).
 */
export function fieldMode(card: CardFields, key: keyof MemberForm): CardFieldMode {
  if (isParentField(key)) return card.fields.parents === 'hidden' ? 'hidden' : 'optional';
  return key in card.fields ? card.fields[key as StandardField] : 'required';
}

/** Extra field key → value; dates as «YYYY-MM-DD». */
export type CardExtras = Record<string, string>;

/** What the sheet edits: one value per extra field of the profile, the stored one or empty. */
export function cardExtrasDraft(card: CardFields, stored: CardExtras | null | undefined): CardExtras {
  return Object.fromEntries(card.extra.map((field) => [field.key, stored?.[field.key] ?? '']));
}

/**
 * The extra fields to send: only those changed from what was read, so a value this viewer was not
 * shown (`cardExtras` comes empty to who may not see private details) is not wiped. One emptied
 * goes as '', which clears it; a new member's start empty, so only the filled ones go.
 */
export function changedCardExtras(loaded: CardExtras, values: CardExtras): CardExtras {
  return Object.fromEntries(Object.entries(values).filter(([key, value]) => value.trim() !== (loaded[key] ?? '').trim()));
}

const DAY = /^\d{4}-\d{2}-\d{2}$/;

export function cardExtraValid(field: CardExtraField, value: string | null | undefined): boolean {
  const text = (value ?? '').trim();
  if (!text) return !field.required;
  return field.type !== 'date' || DAY.test(text);
}

export function cardExtrasValid(card: CardFields, values: CardExtras): boolean {
  return card.extra.every((field) => cardExtraValid(field, values[field.key]));
}

/** The card's lines: each extra field that has a value, under its label, a date written as the card writes dates. */
export function cardExtraRows(card: CardFields, stored: CardExtras | null | undefined): { key: string; label: string; value: string }[] {
  return card.extra.flatMap((field) => {
    const value = (stored?.[field.key] ?? '').trim();
    if (!value) return [];
    const date = field.type === 'date' ? dateOnly(value) : null;
    return [{ key: field.key, label: field.label, value: date ? dateLabel(date) : value }];
  });
}

/** The server's own wording when a field the край requires was left empty (400 `CardFieldRequired`). */
export function cardFieldError(error: unknown): string | null {
  if (!(error instanceof HttpErrorResponse)) return null;
  const body = error.error as { error?: string; message?: string } | null;
  return body?.error === 'CardFieldRequired' && body.message ? body.message : null;
}

// ── The request ──────────────────────────────────────────────────────────────────────────────────

export interface MemberForm {
  firstName: string;
  middleName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  dateOfBirth: string;
  address: string;
  school: string;
  motherFirstName: string;
  motherLastName: string;
  motherPhone: string;
  fatherFirstName: string;
  fatherLastName: string;
  fatherPhone: string;
}

export interface PhotoChange {
  /** A new picture to upload. */
  blob: Blob | null;
  name: string;
  /** «Очистити фото». */
  remove: boolean;
}

/**
 * The multipart body of `PUT member/{key}`, as the web's MemberService.buildFormData lays it out.
 * The address, school and parents' contacts go only when changed: they are private, so someone who could not see them
 * read them blank, and one left out keeps what the server has. Emptied, they go as '' to clear.
 */
/** The fields of the form each group of hidden data covers (the server's MemberDataFields). */
const HIDDEN_FIELDS: Record<Exclude<MemberDataGroup, 'birthday' | 'extras'>, readonly (keyof MemberForm)[]> = {
  contacts: ['email', 'phoneNumber'],
  birthDate: ['dateOfBirth'],
  address: ['address', 'school'],
  parents: PARENT_FIELDS,
};

/** The group of data a field of the form belongs to; null for one the kurin never hides. */
export function hiddenGroupOf(key: string): MemberDataGroup | null {
  const entry = Object.entries(HIDDEN_FIELDS).find(([, keys]) => (keys as readonly string[]).includes(key));
  return entry ? (entry[0] as MemberDataGroup) : null;
}

/**
 * The multipart body of `PUT member/{key}`. A group of data the kurin hides from the writer is left
 * out: the form showed it as «Приховано», and the server keeps what it has.
 */
export function memberFormData(
  member: MemberDto,
  form: MemberForm,
  histories: PlastLevelHistoryDto[],
  photo: PhotoChange,
  extras: CardExtras = {},
): FormData {
  const hidden = member.hiddenData ?? [];
  const body = new FormData();
  appendProfile(body, form, histories, hidden.includes('extras') ? {} : extras);
  for (const key of ['address', 'school', ...PARENT_FIELDS] as const) {
    if (form[key].trim() !== (member[key] ?? '').trim()) body.append(key, form[key].trim());
  }
  for (const group of hidden) {
    if (group in HIDDEN_FIELDS) HIDDEN_FIELDS[group as keyof typeof HIDDEN_FIELDS].forEach((key) => body.delete(key));
  }
  body.append('removeProfilePhoto', String(photo.remove && !photo.blob));
  if (photo.blob) body.append('blob', photo.blob, photo.name);
  return body;
}

/** Where a new member goes: a гурток, or the kurin with no гурток yet (web upsert-member routes). */
export type NewMemberPlacement = { groupKey: string } | { kurinKey: string };

/**
 * The multipart body of `POST member` (a гурток) or `POST member/kurins/{k}/members` (the kurin),
 * as the web's create sends it. Every new member gets an account: the letter is how they get in
 * (web createUserAccount, always on).
 */
export function newMemberFormData(
  placement: NewMemberPlacement,
  form: MemberForm,
  histories: PlastLevelHistoryDto[],
  photo: Pick<PhotoChange, 'blob' | 'name'>,
  extras: CardExtras = {},
): FormData {
  const body = new FormData();
  if ('groupKey' in placement) body.append('groupKey', placement.groupKey);
  else body.append('kurinKey', placement.kurinKey);
  body.append('createUserAccount', 'true');
  appendProfile(body, form, histories, extras);
  for (const key of ['address', 'school', ...PARENT_FIELDS] as const) {
    if (form[key].trim()) body.append(key, form[key].trim());
  }
  if (photo.blob) body.append('blob', photo.blob, photo.name);
  return body;
}

/** The extra fields given (see changedCardExtras), an empty one as '' to clear it; one left out keeps its value. */
function appendProfile(body: FormData, form: MemberForm, histories: PlastLevelHistoryDto[], extras: CardExtras): void {
  body.append('firstName', form.firstName.trim());
  body.append('middleName', form.middleName.trim());
  body.append('lastName', form.lastName.trim());
  body.append('email', form.email.trim());
  body.append('phoneNumber', form.phoneNumber.trim());
  body.append('dateOfBirth', form.dateOfBirth);
  histories.forEach((history, index) => {
    for (const [key, value] of Object.entries(history)) {
      if (value === null || value === undefined) continue;
      body.append(`plastLevelHistories[${index}].${key}`, key === 'dateAchieved' ? String(value).slice(0, 10) : String(value));
    }
  });
  for (const [key, value] of Object.entries(extras)) body.append(`cardExtras[${key}]`, value.trim());
}

/** The web's minAge validator: a member is at least six on the day the form is saved. */
export const MIN_AGE = 6;

export function oldEnough(dateOfBirth: string, today: string): boolean {
  const birth = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateOfBirth);
  const now = /^(\d{4})-(\d{2})-(\d{2})$/.exec(today);
  if (!birth || !now) return false;
  const [, by, bm, bd] = birth.map(Number);
  const [, ny, nm, nd] = now.map(Number);
  const age = ny - by - (nm < bm || (nm === bm && nd < bd) ? 1 : 0);
  return age >= MIN_AGE;
}

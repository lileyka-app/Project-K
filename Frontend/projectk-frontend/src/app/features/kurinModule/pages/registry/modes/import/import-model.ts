import { PlastLevel } from '../../../../models/enums/plast-level.enum';
import { PLAST_LADDER, PLAST_LEVEL_COLUMN_LABELS } from '../../../../models/enums/plast-ladder';

/** Дзеркалить `RosterField` на бекенді. Значення — контракт, підписи — ні. */
export enum RosterField {
  Ignore = 'Ignore',
  LastName = 'LastName',
  FirstName = 'FirstName',
  MiddleName = 'MiddleName',
  DateOfBirth = 'DateOfBirth',
  PlastLevel = 'PlastLevel',
  PlastLevelDate = 'PlastLevelDate',
  GroupName = 'GroupName',
  KurinNumber = 'KurinNumber',
  PhoneNumber = 'PhoneNumber',
  Email = 'Email',
  Address = 'Address',
  School = 'School',
  LevelDate = 'LevelDate'
}

export interface ColumnMapping {
  index: number;
  field: RosterField;
  level?: PlastLevel | null;
}

export interface RosterColumnPreview {
  index: number;
  header: string;
  sample: string | null;
  suggestedField: RosterField;
  suggestedLevel: PlastLevel | null;
}

export interface SheetRow {
  number: number;
  cells: string[];
}

export interface RosterPreview {
  columns: RosterColumnPreview[];
  rows: SheetRow[];
}

export type RowOutcome = 'Created' | 'Attached' | 'AlreadyHere' | 'Rejected';

export interface RowResult {
  rowNumber: number;
  name: string;
  outcome: RowOutcome;
  reason: string | null;
  /** An account is (or would be) opened for the row's address and the activation letter sent. */
  accountInvited: boolean;
}

export interface RosterImportReport {
  rows: RowResult[];
  missingGroups: string[];
  foreignKurinRows: number[];
  dryRun: boolean;
  createdCount: number;
  attachedCount: number;
  alreadyHereCount: number;
  rejectedCount: number;
  invitedCount: number;
}

export interface ApplyRosterRequest {
  rows: SheetRow[];
  mapping: ColumnMapping[];
  createMissingGroups: boolean;
  dryRun: boolean;
}

/**
 * Один пункт випадайки на екрані зіставлення. Дати окремих ступенів розгорнуті в самостійні пункти:
 * бекенд бере їх як `LevelDate` плюс сам ступінь, а людині простіше вибрати «Дата: Скоб», ніж
 * спочатку поле, а тоді ще й ступінь.
 */
export interface FieldOption {
  value: string;
  label: string;
  field: RosterField;
  level: PlastLevel | null;
}

const REQUIRED_LABELS: Record<string, string> = {
  [RosterField.LastName]: 'Прізвище *',
  [RosterField.FirstName]: "Ім'я *",
  [RosterField.DateOfBirth]: 'Дата народження *',
  [RosterField.PlastLevel]: 'Пластовий ступінь *',
  [RosterField.PlastLevelDate]: 'Дата ступеня *',
  [RosterField.GroupName]: 'Гурток *',
  [RosterField.Email]: 'Пошта *'
};

/* Курінь тут необов'язковий: імпорт завжди йде в той курінь, з якого його запустили, а колонка
   з іншим числом лише попереджає. */
const OPTIONAL_LABELS: Record<string, string> = {
  [RosterField.MiddleName]: 'По батькові',
  [RosterField.KurinNumber]: 'Курінь',
  [RosterField.PhoneNumber]: 'Телефон'
};

function plain(field: RosterField, label: string): FieldOption {
  return { value: field, label, field, level: null };
}

export const FIELD_OPTIONS: FieldOption[] = [
  plain(RosterField.Ignore, 'Не імпортувати'),
  ...Object.entries(REQUIRED_LABELS).map(([field, label]) => plain(field as RosterField, label)),
  ...Object.entries(OPTIONAL_LABELS).map(([field, label]) => plain(field as RosterField, label)),
  ...PLAST_LADDER.map(level => ({
    value: `LevelDate:${level}`,
    label: `Дата: ${PLAST_LEVEL_COLUMN_LABELS[level]}`,
    field: RosterField.LevelDate,
    level
  }))
];

/** Поля, без яких рядок не імпортується — їх треба зіставити, перш ніж рухатись далі. */
export const REQUIRED_FIELDS: RosterField[] = Object.keys(REQUIRED_LABELS) as RosterField[];

/** Назва обов'язкового поля без зірочки — щоб назвати людині, чого саме ще бракує. */
export function requiredFieldName(field: RosterField): string {
  return (REQUIRED_LABELS[field] ?? field).replace(/\s\*$/, '');
}

/**
 * The option a column starts on. The server still suggests Address and School for such columns, but
 * those are not offered here (not shown or kept in the app yet), so they start on «Не імпортувати».
 */
export function optionValueOf(field: RosterField, level: PlastLevel | null | undefined): string {
  const value = field === RosterField.LevelDate && level ? `LevelDate:${level}` : field;
  return FIELD_OPTIONS.some(option => option.value === value) ? value : RosterField.Ignore;
}

export function optionByValue(value: string): FieldOption {
  return FIELD_OPTIONS.find(option => option.value === value) ?? FIELD_OPTIONS[0];
}

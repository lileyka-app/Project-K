import { PlastLevel } from '../../../../models/enums/plast-level.enum';
import { PLAST_LEVEL_COLUMN_LABELS } from '../../../../models/enums/plast-ladder';

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

/** Що імпорт цього куреня питає. Каже сервер — за гілкою куреня, тими самими правилами, що й перевірка рядка. */
export interface RosterImportShape {
  required: RosterField[];
  optional: RosterField[];
  levelDates: PlastLevel[];
  /** УПЮ кладе кожного в гурток; старший курінь гуртків не має. */
  placesInGroups: boolean;
}

export interface RosterPreview {
  columns: RosterColumnPreview[];
  rows: SheetRow[];
  shape: RosterImportShape;
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

/** Підписи полів. Що з них обовʼязкове і що взагалі питати, каже сервер у `RosterImportShape`. */
const FIELD_LABELS: Partial<Record<RosterField, string>> = {
  [RosterField.LastName]: 'Прізвище',
  [RosterField.FirstName]: "Ім'я",
  [RosterField.MiddleName]: 'По батькові',
  [RosterField.DateOfBirth]: 'Дата народження',
  [RosterField.PlastLevel]: 'Пластовий ступінь',
  [RosterField.PlastLevelDate]: 'Дата ступеня',
  [RosterField.GroupName]: 'Гурток',
  [RosterField.Email]: 'Пошта',
  // Курінь лише попереджає: імпорт завжди йде в той курінь, з якого його запустили.
  [RosterField.KurinNumber]: 'Курінь',
  [RosterField.PhoneNumber]: 'Телефон'
};

const IGNORE: FieldOption = { value: RosterField.Ignore, label: 'Не імпортувати', field: RosterField.Ignore, level: null };

/** Назва поля, як її бачить людина. */
export function fieldName(field: RosterField): string {
  return FIELD_LABELS[field] ?? field;
}

/**
 * Пункти випадайки для цього куреня: спершу обовʼязкові (із зірочкою), далі необовʼязкові, далі дати
 * ступенів його гілки. Адреси й школи серед них нема: застосунок їх поки не показує й не тримає.
 */
export function fieldOptionsFor(shape: RosterImportShape): FieldOption[] {
  const plain = (field: RosterField, mark: string): FieldOption =>
    ({ value: field, label: fieldName(field) + mark, field, level: null });

  return [
    IGNORE,
    ...shape.required.map(field => plain(field, ' *')),
    ...shape.optional.map(field => plain(field, '')),
    ...shape.levelDates.map(level => ({
      value: `LevelDate:${level}`,
      label: `Дата: ${PLAST_LEVEL_COLUMN_LABELS[level]}`,
      field: RosterField.LevelDate,
      level
    }))
  ];
}

/** Пункт, з якого колонка починає: вгадане сервером, якщо цей курінь таке питає, інакше «Не імпортувати». */
export function optionValueOf(options: FieldOption[], field: RosterField, level: PlastLevel | null | undefined): string {
  const value = field === RosterField.LevelDate && level ? `LevelDate:${level}` : field;
  return options.some(option => option.value === value) ? value : RosterField.Ignore;
}

export function optionByValue(options: FieldOption[], value: string): FieldOption {
  return options.find(option => option.value === value) ?? IGNORE;
}

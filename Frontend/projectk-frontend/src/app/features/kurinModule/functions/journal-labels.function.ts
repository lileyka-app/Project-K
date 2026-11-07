import { JournalArea, JournalChangeDto, KurinJournalEntryDto } from '../models/kurin-journal.dto';
import { AUDIENCE_LABELS, CAPABILITY_LABELS, labelOf, REACH_LABELS } from '../models/access-policy-labels.model';
import { parseOfficeRole } from './system-role.function';
import { leadershipRoleDisplayName } from './leadership-role-display.function';
import { REGULAR_PROBE_LABEL } from '../models/group-probe-edition.dto';
import { TAG_COLOR_NAMES } from '../models/kurin-tag.dto';

/** The journal's sections, in the order the «Розділ» filter lists them. */
export const JOURNAL_AREA_LABELS: Record<JournalArea, string> = {
  Probe: 'Проби',
  Badge: 'Вмілості',
  Award: 'Відзначення',
  Warning: 'Перестороги',
  Agenda: 'Календар і задачі',
  Member: 'Картки',
  Office: 'Ролі',
  Membership: 'Членство',
  Settings: 'Налаштування куреня',
  Tag: 'Теги',
  Archive: 'Архів'
};

/**
 * What happened, as a sentence without a subject: the actor stands next to it, and an impersonal
 * form reads the same whoever did it. Keyed by the stored action string, which never changes.
 */
const ACTION_LABELS: Record<string, string> = {
  'Probe.PointSigned': 'Підписано точку проби',
  'Probe.PointUnsigned': 'Знято підпис точки проби',
  'Probe.Reopened': 'Пробу повернуто в роботу',
  'Probe.Started': 'Пробу розпочато',
  'Probe.Completed': 'Пробу закрито',
  'Probe.Verified': 'Пробу підтверджено',
  'Probe.Unverified': 'Знято підтвердження проби',
  'Probe.ProjectAdded': 'Додано проєкт проби',
  'Probe.ProjectEdited': 'Змінено проєкт проби',
  'Probe.ProjectRemoved': 'Видалено проєкт проби',
  'Probe.ProjectApproved': 'Дано дозвіл на проєкт',
  'Probe.ProjectApprovalRemoved': 'Знято дозвіл на проєкт',
  'Probe.ProjectCompleted': 'Проєкт виконано',
  'Probe.ProjectCompletionRemoved': 'Знято позначку «виконано»',
  'Probe.ProjectsSwapped': 'Запасний проєкт поставлено замість основного',
  'Probe.FileEdited': 'Змінено дані третьої проби',
  'Probe.ConferenceConfirmed': 'Підтверджено конференцію третьої проби',
  'Probe.ConferenceUnconfirmed': 'Знято підтвердження конференції',
  'Probe.CampConfirmed': 'Зараховано табір третьої проби',
  'Probe.CampUnconfirmed': 'Знято зарахування табору',
  'Probe.CertificateAttached': 'Прикріплено посвідку табору',
  'Probe.CertificateRemoved': 'Прибрано посвідку табору',
  'Badge.Submitted': 'Вмілість подано на підтвердження',
  'Badge.Resubmitted': 'Вмілість подано повторно',
  'Badge.Confirmed': 'Вмілість підтверджено',
  'Badge.Rejected': 'Вмілість відхилено',
  'Badge.ConfirmationRemoved': 'Знято підтвердження вмілості',
  'Award.Added': 'Додано відзначення',
  'Award.Changed': 'Змінено відзначення',
  'Award.Confirmed': 'Підтверджено відзначення',
  'Award.Rejected': 'Відхилено відзначення',
  'Award.Deleted': 'Видалено відзначення',
  'Warning.Given': 'Дано пересторогу',
  'Warning.Cancelled': 'Скасовано пересторогу',
  'Agenda.Created': 'Створено подію чи задачу',
  'Agenda.Changed': 'Змінено подію чи задачу',
  'Agenda.Deleted': 'Видалено подію чи задачу',
  'Agenda.StatusChanged': 'Задачу переставлено',
  'Agenda.Archived': 'Задачу відправлено в архів',
  'Agenda.Restored': 'Задачу повернуто з архіву',
  'Agenda.OccurrenceNotHeld': 'Позначено, що подія не відбулася',
  'Agenda.OccurrenceHeld': 'Знято позначку «не відбулася»',
  'Member.Added': 'Додано картку',
  'Member.Changed': 'Змінено картку',
  'Member.PhotoChanged': 'Змінено фото',
  'Member.PhotoRemoved': 'Прибрано фото',
  'Member.Deleted': 'Видалено картку',
  'Member.ProfileVerified': 'Профіль верифіковано',
  'Member.VerificationReset': 'Знято верифікацію профілю',
  'Office.Assigned': 'Призначено на роль',
  'Office.Ended': 'Роль звільнено',
  'Office.MentorAssigned': 'Закріплено впорядника за гуртком',
  'Office.MentorRevoked': 'Знято впорядника з гуртка',
  'Membership.Joined': 'Прийнято до куреня',
  'Membership.Left': 'Виведено з куреня',
  'Membership.MovedToGroup': 'Переведено в інший гурток',
  'Office.Corrected': 'Виправлено роль в архіві',
  'Membership.Corrected': 'Виправлено членство в архіві',
  'Settings.KurinChanged': 'Змінено налаштування куреня',
  'Settings.AccessPolicyChanged': 'Змінено права й видимість',
  'Settings.CardFieldsChanged': 'Змінено поля картки',
  'Settings.ArchivePolicyChanged': 'Змінено строки архіву задач',
  'Settings.GroupProbeEditionChanged': 'Змінено пробу гуртка',
  'Settings.TagSettingsChanged': 'Змінено налаштування тегів',
  'Tag.Created': 'Заведено тег',
  'Tag.Changed': 'Змінено тег',
  'Tag.Deleted': 'Видалено тег',
  'Tag.Put': 'Поставлено тег',
  'Tag.TakenOff': 'Знято тег',
  'Archive.GroupDissolved': 'Розформовано гурток',
  'Archive.DangerZoneEntered': 'Увімкнено небезпечний режим',
  'Archive.DangerZoneRefused': 'Невдала спроба увімкнути небезпечний режим',
  'Archive.DangerZoneEnded': 'Вимкнено небезпечний режим',
  'Archive.OfficeDeleted': 'Видалено запис ролі',
  'Archive.MembershipDeleted': 'Видалено членство',
  'Archive.GroupDeleted': 'Видалено гурток назавжди'
};

/** For an action this build does not know yet: what part of the kurin it touched. */
const AREA_FALLBACKS: Record<JournalArea, string> = {
  Probe: 'Зміна в пробі',
  Badge: 'Зміна вмілості',
  Award: 'Зміна відзначення',
  Warning: 'Зміна перестороги',
  Agenda: 'Зміна в календарі чи задачах',
  Member: 'Зміна картки',
  Office: 'Зміна ролей',
  Membership: 'Зміна членства',
  Settings: 'Зміна налаштувань куреня',
  Tag: 'Зміна тегів',
  Archive: 'Зміна в архіві'
};

const FIELD_LABELS: Record<string, string> = {
  status: 'Стан',
  taskStatus: 'Стан задачі',
  level: 'Ступінь',
  award: 'Відзначення',
  note: 'Примітка',
  dateAcquired: 'Дата здобуття',
  point: 'Точка',
  project: 'Проєкт',
  conference: 'Конференція',
  camp: 'Табір',
  issuedAtUtc: 'Дано',
  expiresAtUtc: 'Діє до',
  title: 'Назва',
  description: 'Опис',
  start: 'Початок',
  end: 'Кінець',
  location: 'Місце',
  kind: 'Вид',
  recurrence: 'Повторення',
  targets: 'Для кого',
  firstName: 'Імʼя',
  middleName: 'По батькові',
  lastName: 'Прізвище',
  email: 'Пошта',
  phoneNumber: 'Телефон',
  dateOfBirth: 'Дата народження',
  address: 'Адреса',
  school: 'Школа',
  motherFirstName: 'Імʼя мами',
  motherLastName: 'Прізвище мами',
  motherPhone: 'Телефон мами',
  fatherFirstName: 'Імʼя тата',
  fatherLastName: 'Прізвище тата',
  fatherPhone: 'Телефон тата',
  cardExtras: 'Додаткові поля',
  plastLevels: 'Ступені',
  profileVerification: 'Верифікація',
  group: 'Гурток',
  hiddenFields: 'Приховані поля',
  stanytsia: 'Станиця',
  region: 'Край',
  regionOrCountry: 'Край',
  namedAfter: 'Імені',
  profileVerificationEnabled: 'Верифікація профілів',
  holder: 'Хто',
  role: 'Роль',
  number: 'Число куреня',
  hidden: 'Приховані поля',
  purgeAfterDays: 'Видалення з архіву, днів',
  autoArchiveAfterDays: 'Перенесення в архів, днів',
  probeEdition: 'Проба гуртка',
  name: 'Назва',
  color: 'Колір',
  carriedBy: 'Мали тег',
  enabled: 'Теги ввімкнені',
  ownTagsVisible: 'Кожен бачить свої теги',
  startDate: 'Від',
  endDate: 'До',
  joinedOn: 'Від',
  leftOn: 'До',
  offices: 'Записів ролей',
  dissolvedOn: 'Розформовано',
  method: 'Чим',
  reason: 'Як'
};

const STATUS_LABELS: Record<string, string> = {
  NotStarted: 'Не розпочато',
  InProgress: 'В процесі',
  Completed: 'Завершено',
  Verified: 'Підтверджено',
  Draft: 'Чернетка',
  Submitted: 'Очікує підтвердження',
  Confirmed: 'Підтверджено',
  Rejected: 'Відхилено'
};

/** The archive's own values: how the danger zone was entered and how it ended, the kind of a membership. */
const ARCHIVE_VALUE_LABELS: Record<string, Record<string, string>> = {
  method: { password: 'пароль', code: 'код' },
  reason: { manual: 'вручну', left: 'вихід зі сторінки', time: 'минув час', spent: 'вичерпано видалення' },
  kind: { Youth: 'юнацтво', Staff: 'виховний склад' }
};

const LEVEL_LABELS: Record<string, string> = {
  Level1: 'Перша',
  Level2: 'Друга',
  Level3: 'Третя',
  First: 'Перше',
  Second: 'Друге',
  Third: 'Третє',
  Fourth: 'Четверте'
};

/**
 * One «було → стало» row as the page shows it: an empty side means there was or is nothing (added,
 * removed). A private field says only that it changed.
 */
export interface JournalChangeView {
  field: string;
  from: string;
  to: string;
  redacted: boolean;
}

export function journalActionLabel(entry: Pick<KurinJournalEntryDto, 'area' | 'action'>): string {
  return ACTION_LABELS[entry.action] ?? AREA_FALLBACKS[entry.area] ?? 'Зміна';
}

/**
 * The actor's role as a tag: the office's name, «Адміністратор», or «Учасник» for the baseline.
 * Null when nothing was recorded.
 */
export function journalRoleLabel(role: string | null | undefined): string | null {
  if (!role) {
    return null;
  }
  const office = parseOfficeRole(role);
  if (office) {
    return leadershipRoleDisplayName(office.role);
  }
  if (role === 'Admin') {
    return 'Адміністратор';
  }
  if (role === 'Member') {
    return 'Учасник';
  }
  return role;
}

/** A field's name; a «Права й видимість» cell, stored as `Capability/Audience`, reads as both. */
export function journalFieldLabel(field: string): string {
  const cell = policyCell(field);
  if (cell) {
    return `${labelOf(CAPABILITY_LABELS, cell.capability)} · ${labelOf(AUDIENCE_LABELS, cell.audience)}`;
  }
  return FIELD_LABELS[field] ?? field;
}

/** A value as a person reads it: names of states and levels in Ukrainian, dates as on the rest of the site. */
export function journalValueLabel(field: string, value: string | null | undefined): string {
  // The journal keeps the catalogue's name of the edition; none means the regular probes.
  if (field === 'probeEdition') {
    return value || REGULAR_PROBE_LABEL;
  }
  if (value === null || value === undefined || value === '') {
    return '—';
  }
  if (policyCell(field)) {
    return labelOf(REACH_LABELS, value);
  }
  if (field === 'status' && STATUS_LABELS[value]) {
    return STATUS_LABELS[value];
  }
  if (field === 'color' && TAG_COLOR_NAMES[value]) {
    return TAG_COLOR_NAMES[value];
  }
  if (field === 'level' && LEVEL_LABELS[value]) {
    return LEVEL_LABELS[value];
  }
  const archived = ARCHIVE_VALUE_LABELS[field]?.[value];
  if (archived) {
    return archived;
  }
  if (value === 'true' || value === 'false') {
    return value === 'true' ? 'так' : 'ні';
  }
  return formatJournalDate(value) ?? value;
}

export function journalChangeView(change: JournalChangeDto): JournalChangeView {
  if (change.redacted) {
    return { field: journalFieldLabel(change.field), from: '', to: 'змінено', redacted: true };
  }
  const side = (value: string | null | undefined) =>
    (value || change.field === 'probeEdition' ? journalValueLabel(change.field, value) : '');
  return { field: journalFieldLabel(change.field), from: side(change.from), to: side(change.to), redacted: false };
}

function policyCell(field: string): { capability: string; audience: string } | null {
  const match = /^([A-Za-z0-9]+)\/([A-Za-z]+)$/.exec(field);
  return match ? { capability: match[1], audience: match[2] } : null;
}

const pad = (value: number): string => String(value).padStart(2, '0');

/** `2026-10-23` → «23.10.2026»; `2026-10-23T14:05:00Z` → «23.10.2026 17:05» in the reader's time. */
function formatJournalDate(value: string): string | null {
  const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (dateOnly) {
    return `${dateOnly[3]}.${dateOnly[2]}.${dateOnly[1]}`;
  }
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?Z$/.test(value)) {
    return null;
  }
  const moment = new Date(value);
  if (Number.isNaN(moment.getTime())) {
    return null;
  }
  return `${pad(moment.getDate())}.${pad(moment.getMonth() + 1)}.${moment.getFullYear()} ${pad(moment.getHours())}:${pad(moment.getMinutes())}`;
}

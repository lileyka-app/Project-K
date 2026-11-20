import { MemberDto } from '../../../../models/member.dto';
import { KurinBranch, hasGroups } from '../../../../models/enums/kurin-branch.enum';
import { PlastLevel } from '../../../../models/enums/plast-level.enum';
import { PLAST_LADDER, PLAST_LEVEL_COLUMN_LABELS, defaultLevelsFor } from '../../../../models/enums/plast-ladder';

export type RegistryColumnKind = 'text' | 'date' | 'phone' | 'email' | 'tags';

export interface RegistryColumn {
  readonly id: string;
  readonly header: string;
  /** Група в перемикачі колонок, щоб одинадцять дат не лежали одним списком. */
  readonly group: 'Особа' | 'Контакти' | 'Належність' | 'Ступені';
  readonly kind: RegistryColumnKind;
  /** Значення для сортування, фільтра й друку. */
  readonly value: (member: MemberDto) => string | Date | null;
}

function levelDate(member: MemberDto, level: PlastLevel): Date | null {
  const entry = (member.plastLevelHistories ?? []).find(history => history.plastLevel === level);
  if (!entry?.dateAchieved) {
    return null;
  }

  return entry.dateAchieved instanceof Date ? entry.dateAchieved : new Date(entry.dateAchieved);
}

const LEVEL_COLUMNS: RegistryColumn[] = PLAST_LADDER.map(level => ({
  id: `level:${level}`,
  header: PLAST_LEVEL_COLUMN_LABELS[level],
  group: 'Ступені' as const,
  kind: 'date' as const,
  value: (member: MemberDto) => levelDate(member, level)
}));

/**
 * Кожна колонка реєстру. Прізвище й ім'я не тут — вони в замороженій першій колонці, яку не
 * вимкнути: рядок без імені нічого не означає.
 */
export const REGISTRY_COLUMNS: RegistryColumn[] = [
  {
    id: 'dateOfBirth',
    header: 'Дата народження',
    group: 'Особа',
    kind: 'date',
    value: member => member.dateOfBirth ?? null
  },
  {
    id: 'plastLevel',
    header: 'Пластовий ступінь',
    group: 'Особа',
    kind: 'text',
    value: member => member.latestPlastLevelDisplay ?? null
  },
  {
    id: 'groupName',
    header: 'Гурток',
    group: 'Належність',
    kind: 'text',
    value: member => member.groupName ?? null
  },
  {
    id: 'mentoredGroups',
    header: 'Гурток (закріплення)',
    group: 'Належність',
    kind: 'text',
    value: member => member.mentoredGroupNames?.join(', ') || null
  },
  {
    // Лише коли курінь користується тегами; плашки малює сторінка, бо назви дає довідник тегів.
    id: 'tags',
    header: 'Теги',
    group: 'Належність',
    kind: 'tags',
    value: () => null
  },
  {
    id: 'phoneNumber',
    header: 'Телефон',
    group: 'Контакти',
    kind: 'phone',
    value: member => member.phoneNumber || null
  },
  {
    id: 'email',
    header: 'Пошта',
    group: 'Контакти',
    kind: 'email',
    value: member => member.email || null
  },
  ...LEVEL_COLUMNS
];

/**
 * Що курінь цієї гілки бачить, поки нічого не вибирав. Особа й належність — завжди; з дат — те, що
 * гілка звично записує.
 *
 * `mentoredGroups` тут немає навмисно: у таблиці впорядників вона стоїть завжди, а в таблиці юнаків
 * не значить нічого — вмикати її вручну ніде.
 */
export function defaultColumnIdsFor(branch: KurinBranch | null | undefined): string[] {
  return [
    'dateOfBirth',
    'plastLevel',
    ...(hasGroups(branch) ? ['groupName'] : []),
    'tags',
    'phoneNumber',
    ...defaultLevelsFor(branch).map(level => `level:${level}`)
  ];
}

/** Колонки, які курінь може обрати: у старшому курені гуртків немає, тож і колонок про них. */
export function columnsFor(columns: readonly RegistryColumn[], branch: KurinBranch | null | undefined): RegistryColumn[] {
  return hasGroups(branch) ? [...columns] : columns.filter(column => column.id !== 'groupName' && column.id !== 'mentoredGroups');
}

/**
 * Колонки таблиці впорядників: закріплення попереду, а власний гурток прибрано — виховника членство
 * ставить у курінь і зазвичай у жоден гурток, тож ця колонка в них порожня й лише збиває з пантелику.
 */
export function staffColumnsOf(columns: readonly RegistryColumn[]): RegistryColumn[] {
  const assignment = REGISTRY_COLUMNS.find(column => column.id === 'mentoredGroups')!;
  return [assignment, ...columns.filter(column => column.id !== 'groupName' && column.id !== 'mentoredGroups')];
}

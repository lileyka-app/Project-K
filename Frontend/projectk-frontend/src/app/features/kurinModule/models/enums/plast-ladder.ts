import { KurinBranch } from './kurin-branch.enum';
import { PlastLevel } from './plast-level.enum';

/**
 * Порядок, у якому людина проходить ступені, і що з них курінь кожної гілки звично записує.
 *
 * Порядок живе тут, а не в самому enum: ступінь зберігається числом, і числа заморожені —
 * `Prykhylnyk` дописаний останнім, хоч на драбині стоїть другим. Дзеркалить `PlastLadder` на бекенді.
 */
export const PLAST_LADDER: readonly PlastLevel[] = [
  PlastLevel.Entry,
  PlastLevel.Prykhylnyk,
  PlastLevel.Uchasnyk,
  PlastLevel.Rozviduvach,
  PlastLevel.Skob,
  PlastLevel.HetmanskiySkob,
  PlastLevel.Starshoplastun,
  PlastLevel.Senior,
  PlastLevel.SeniorPratsi,
  PlastLevel.SeniorDovirja,
  PlastLevel.SeniorKerivnytstva
];

/**
 * Підпис ступеня — так, як його пишуть у звітах. Дата вступу до Пласту стоїть під «пл. неім.»: це і
 * є ступінь пластуна неіменованого, а не окремий службовий рядок перед драбиною.
 *
 * Дзеркалить `PlastLevelNames` на бекенді — те саме джерело для реєстру, вивантаження й PDF-звіту.
 * Розійтися їм не можна: провід читає екран і звіт поруч.
 */
export const PLAST_LEVEL_COLUMN_LABELS: Record<PlastLevel, string> = {
  [PlastLevel.Entry]: 'пл. неім.',
  [PlastLevel.Prykhylnyk]: 'пл. прих.',
  [PlastLevel.Uchasnyk]: 'пл. уч.',
  [PlastLevel.Rozviduvach]: 'пл. розв.',
  [PlastLevel.Skob]: 'пл. скоб / вірл.',
  [PlastLevel.HetmanskiySkob]: 'пл. гетьм. скоб / вірл.',
  [PlastLevel.Starshoplastun]: 'ст. пл.',
  [PlastLevel.Senior]: 'пл. сен.',
  [PlastLevel.SeniorPratsi]: 'пл. сен. праці',
  [PlastLevel.SeniorDovirja]: "пл. сен. довір'я",
  [PlastLevel.SeniorKerivnytstva]: 'пл. сен. керівництва'
};

const THROUGH_YOUTH: readonly PlastLevel[] = PLAST_LADDER.slice(0, 6);
const THROUGH_SENIOR: readonly PlastLevel[] = PLAST_LADDER.slice(0, 7);

/**
 * Які ступені показувати куреню цієї гілки **за замовчуванням**. Накопичувально: УПС-курінь однаково
 * хоче бачити, коли його люди були заприсяжені. Це дефолт, а не обмеження — далі провід вмикає й
 * вимикає колонки сам.
 */
export function defaultLevelsFor(branch: KurinBranch | null | undefined): readonly PlastLevel[] {
  switch (branch) {
    case KurinBranch.USP:
      return THROUGH_SENIOR;
    case KurinBranch.UPS:
      return PLAST_LADDER;
    default:
      return THROUGH_YOUTH;
  }
}

const SENIOR_LEVELS: ReadonlySet<PlastLevel> = new Set<PlastLevel>([PlastLevel.Senior, PlastLevel.SeniorPratsi, PlastLevel.SeniorDovirja, PlastLevel.SeniorKerivnytstva]);

/** Гілка, до якої належить сам ступінь: ст. пл. — УСП, будь-який сеніорський — УПС, решта — УПЮ. */
export function branchOfLevel(level: PlastLevel): KurinBranch {
  if (level === PlastLevel.Starshoplastun) {
    return KurinBranch.USP;
  }

  return SENIOR_LEVELS.has(level) ? KurinBranch.UPS : KurinBranch.UPYu;
}

/** Найвищий ступінь за місцем на драбині, а не за значенням enum. */
export function highestLevel(levels: readonly PlastLevel[]): PlastLevel | null {
  return levels.reduce<PlastLevel | null>(
    (best, level) => best === null || PLAST_LADDER.indexOf(level) > PLAST_LADDER.indexOf(best) ? level : best,
    null);
}

/**
 * Гілка самої людини там, де на неї дивляться. Старший ступінь переважає курінь: впорядник у курені
 * УПЮ — сам старший пластун чи сеніор, і юнацький вишкіл йому не належить. Без старшого ступеня
 * вирішує гілка куреня.
 */
export function personalBranch(
  levels: readonly PlastLevel[],
  membershipBranch: KurinBranch | null | undefined
): KurinBranch {
  const top = highestLevel(levels);
  const levelBranch = top === null ? KurinBranch.UPYu : branchOfLevel(top);

  return levelBranch !== KurinBranch.UPYu ? levelBranch : membershipBranch ?? KurinBranch.UPYu;
}

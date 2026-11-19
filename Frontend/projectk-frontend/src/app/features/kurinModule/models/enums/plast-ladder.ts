import { KurinBranch } from './kurin-branch.enum';
import { PlastLevel } from './plast-level.enum';

/**
 * Порядок, у якому людина проходить ступені, і що з них курінь кожної гілки звично записує.
 *
 * Порядок живе тут, а не в самому enum: ступінь зберігається числом, і числа заморожені —
 * `Prykhylnyk` дописаний останнім, хоч на драбині стоїть другим. Дзеркалить `PlastLadderRules` на бекенді.
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

/**
 * Ступені, за якими курінь рахує чисельність: від початку його гілки. Не накопичувально, як
 * `defaultLevelsFor`: людина стоїть на одному ступені, а в старшому курені юнацьких рядків нема кому
 * заповнювати — хто там є, йде в «Без ступеня / інший». Дзеркалить `PlastLadderRules.TallyFor`.
 */
export function tallyLevelsFor(branch: KurinBranch | null | undefined): readonly PlastLevel[] {
  switch (branch) {
    case KurinBranch.USP:
      return PLAST_LADDER.slice(6);
    case KurinBranch.UPS:
      return PLAST_LADDER.slice(7);
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

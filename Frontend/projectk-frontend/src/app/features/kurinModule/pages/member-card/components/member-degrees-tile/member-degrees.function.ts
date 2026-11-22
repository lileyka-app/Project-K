import { PlastLevel } from '../../../../models/enums/plast-level.enum';
import { PlastLevelHistoryDto } from '../../../../models/plast-level-history.dto';
import { CatalogHonour } from '../../../../models/honour.dto';
import { PLAST_LEVEL_DISPLAY_MAP } from '../../../../functions/member-view-mapper.function';
import { parseDateOnlyString } from '../../../../functions/to-date-only-string.function';

/** The ступені of an УПЮ membership: прихильник to гетьманський скоб. */
export const YOUTH_LADDER: readonly PlastLevel[] = [
  PlastLevel.Prykhylnyk, PlastLevel.Uchasnyk, PlastLevel.Rozviduvach, PlastLevel.Skob, PlastLevel.HetmanskiySkob
];

/** The ступені of a УСП or УПС membership: старший пластун to сеніор керівництва. */
export const SENIOR_LADDER: readonly PlastLevel[] = [
  PlastLevel.Starshoplastun, PlastLevel.Senior, PlastLevel.SeniorPratsi, PlastLevel.SeniorDovirja, PlastLevel.SeniorKerivnytstva
];

/**
 * Every ступінь with a відзнака, прихильник to сеніор керівництва. Ступені come one after another, so
 * the highest here is the person's current one, whichever kurin the card is opened in.
 */
export const DEGREE_LADDER: readonly PlastLevel[] = [...YOUTH_LADDER, ...SENIOR_LADDER];

/** How the скоб reads after «Старший пластун» / «Старша пластунка». */
const CARRIED_SKOB_NAMES: Record<PlastLevel.Skob | PlastLevel.HetmanskiySkob, { plastun: string; plastunka: string }> = {
  [PlastLevel.Skob]: { plastun: 'скоб', plastunka: 'вірлиця' },
  [PlastLevel.HetmanskiySkob]: { plastun: 'гетьманський скоб', plastunka: 'гетьманська вірлиця' }
};

/** The скоб a старший пластун carries from УПЮ: the гетьманський before the plain one. */
function carriedSkob(histories: readonly PlastLevelHistoryDto[]): PlastLevelHistoryDto | undefined {
  return histories.find(history => history.plastLevel === PlastLevel.HetmanskiySkob)
    ?? histories.find(history => history.plastLevel === PlastLevel.Skob);
}

/** A ступінь as the card shows it. */
export interface DegreeRow {
  key: string;
  level: PlastLevel;
  title: string;
  imageUrl: string;
  date: Date | null;
}

/**
 * The person's ступені on one ladder, in the ladder's order, named and drawn as the catalogue has them
 * for a пластун or a пластунка. Without the catalogue the ступінь keeps its own name and no picture.
 */
export function degreeRows(
  histories: readonly PlastLevelHistoryDto[] | null | undefined,
  ladder: readonly PlastLevel[],
  honours: readonly CatalogHonour[]
): DegreeRow[] {
  const byId = new Map(honours.map(item => [item.id, item]));
  const all = histories ?? [];
  const titleOf = (history: PlastLevelHistoryDto) => {
    const item = history.catalogId ? byId.get(history.catalogId) : undefined;
    return item?.title ?? PLAST_LEVEL_DISPLAY_MAP[history.plastLevel] ?? history.plastLevel;
  };
  const skob = carriedSkob(all);
  return ladder
    .map(level => all.find(history => history.plastLevel === level))
    .filter((history): history is PlastLevelHistoryDto => !!history)
    .map(history => {
      const item = history.catalogId ? byId.get(history.catalogId) : undefined;
      // «Старший пластун скоб», «Старша пластунка гетьманська вірлиця»: the УПЮ скоб goes with the ступінь.
      const names = skob ? CARRIED_SKOB_NAMES[skob.plastLevel as PlastLevel.Skob | PlastLevel.HetmanskiySkob] : undefined;
      const title = history.plastLevel === PlastLevel.Starshoplastun && names
        ? `${titleOf(history)} ${(history.isPlastunka ?? skob?.isPlastunka) ? names.plastunka : names.plastun}`
        : titleOf(history);
      return {
        key: history.plastLevelHistoryKey ?? history.plastLevel,
        level: history.plastLevel,
        title,
        imageUrl: item?.imageUrl ?? '',
        date: parseDateOnlyString(history.dateAchieved ?? null)
      };
    });
}

/**
 * The person's current ступінь: the highest on the whole ladder, not on the kurin's. A сеніор who leads
 * an УПЮ kurin still has the скоб of 2002 on record; the card shows the сеніор.
 */
export function currentDegree(
  histories: readonly PlastLevelHistoryDto[] | null | undefined,
  honours: readonly CatalogHonour[]
): DegreeRow | null {
  const rows = degreeRows(histories, DEGREE_LADDER, honours);
  const current = rows.at(-1) ?? null;
  // A старший пластун with a скоб wears the скоб: its picture, the старшопластунство's day.
  const skob = carriedSkob(histories ?? []);
  const skobRow = skob && rows.find(row => row.level === skob.plastLevel);
  return current?.level === PlastLevel.Starshoplastun && skobRow?.imageUrl
    ? { ...current, imageUrl: skobRow.imageUrl }
    : current;
}

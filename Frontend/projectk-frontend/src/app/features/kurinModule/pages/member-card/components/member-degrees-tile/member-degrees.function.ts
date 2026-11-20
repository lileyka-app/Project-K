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

/** A ступінь as the card shows it. */
export interface DegreeRow {
  key: string;
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
  return ladder
    .map(level => (histories ?? []).find(history => history.plastLevel === level))
    .filter((history): history is PlastLevelHistoryDto => !!history)
    .map(history => {
      const item = history.catalogId ? byId.get(history.catalogId) : undefined;
      return {
        key: history.plastLevelHistoryKey ?? history.plastLevel,
        title: item?.title ?? PLAST_LEVEL_DISPLAY_MAP[history.plastLevel] ?? history.plastLevel,
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
  return degreeRows(histories, DEGREE_LADDER, honours).at(-1) ?? null;
}

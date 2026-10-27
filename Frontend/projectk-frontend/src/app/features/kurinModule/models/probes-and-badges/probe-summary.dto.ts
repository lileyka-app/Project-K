export interface ProbeSummaryDto {
  id: string;
  title: string;
  pointsCount: number;
  sectionsCount: number;

  /**
   * Редакція програми, до якої належить проба (напр. `upyu-2026`); null — та, якою курені ходять
   * від початку. Поки гурток не обирає редакцію (PROBE-06), людина бачить саме її.
   */
  edition?: string | null;

  /** Місце проби в її редакції: проби відкриваються одна за одною в цьому порядку. */
  order?: number;
  kraiCode?: string;

  /** `approved` або `trial`. */
  statusCode?: string;
}

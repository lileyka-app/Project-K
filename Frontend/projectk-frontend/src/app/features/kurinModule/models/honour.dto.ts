/** A ступінь or a senior відзначення from the catalogue, with its picture (`GET api/catalog/honours`). */
export interface CatalogHonour {
  id: string;
  kind: 'Degree' | 'Award';
  title: string;
  /** A ступінь's улад, or an award's ladder: `usp-vidznachennia`, `orden-sv-yuriia`… */
  group: string;
  ulad: string;
  order: number;
  /** Empty when the catalogue has no picture. */
  imageUrl: string;
}

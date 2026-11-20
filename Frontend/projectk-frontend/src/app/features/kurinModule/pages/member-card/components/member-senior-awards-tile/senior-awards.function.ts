import { MemberAwardDto } from '../../../../models/member-award.dto';
import { CatalogHonour } from '../../../../models/honour.dto';
import { AwardBlock, groupGrants } from '../award-blocks/award-blocks.function';

/** A senior відзначення as the card shows it. */
export interface SeniorAwardRow {
  award: MemberAwardDto;
  title: string;
  imageUrl: string;
  /** «УСП», «УПС» or «Хрести»: the same грамота is in both улади. */
  section: string;
}

/** Where a senior відзначення belongs: its улад, or the хрести, which are for everyone. */
export function honourSection(item: CatalogHonour | undefined): string {
  if (!item) return '';
  if (item.group === 'khresty') return 'Хрести';
  return item.ulad === 'ups' ? 'УПС' : item.ulad === 'usp' ? 'УСП' : '';
}

/** The picker's choices, by section (УСП, УПС, Хрести), each in the catalogue's order. */
export function seniorAwardOptions(honours: readonly CatalogHonour[]): { label: string; items: CatalogHonour[] }[] {
  const awards = honours.filter(item => item.kind === 'Award');
  return ['УСП', 'УПС', 'Хрести']
    .map(label => ({ label, items: awards.filter(item => honourSection(item) === label) }))
    .filter(group => group.items.length > 0);
}

/**
 * The person's senior відзначення, newest first, named and drawn from the catalogue. One the catalogue
 * no longer knows keeps its id for a name: it was recorded, it does not vanish.
 */
export function seniorAwardRows(awards: readonly MemberAwardDto[] | null | undefined, honours: readonly CatalogHonour[]): SeniorAwardRow[] {
  const byId = new Map(honours.map(item => [item.id, item]));
  return (awards ?? [])
    .filter(award => !!award.catalogId)
    .sort((a, b) => b.dateAcquired.localeCompare(a.dateAcquired))
    .map(award => {
      const item = byId.get(award.catalogId!);
      return { award, title: item?.title ?? award.catalogId!, imageUrl: item?.imageUrl ?? '', section: honourSection(item) };
    });
}

/**
 * The senior відзначення as card blocks: every grant of one under one block, with the catalogue's
 * name and picture. One the catalogue no longer knows keeps its id for a name.
 */
export function seniorAwardBlocks(awards: readonly MemberAwardDto[] | null | undefined, honours: readonly CatalogHonour[], canOpen: boolean): AwardBlock[] {
  const byId = new Map(honours.map(item => [item.id, item]));
  const groups = groupGrants((awards ?? []).filter(award => !!award.catalogId), award => award.catalogId!);
  return [...groups.entries()].map(([catalogId, grants]) => {
    const item = byId.get(catalogId);
    return {
      key: catalogId,
      title: item?.title ?? catalogId,
      imageUrl: item?.imageUrl || null,
      section: honourSection(item),
      grants: grants.map(award => ({ award, pending: false, stamp: null, canOpen })),
      pinned: grants.some(award => award.isPinned),
      pending: false,
      pin: { catalogId }
    };
  });
}

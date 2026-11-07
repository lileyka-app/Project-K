import { MemberAwardDto } from '../../../../models/member-award.dto';
import { dateOnlyTime } from '../../../../functions/to-date-only-string.function';

/** One grant of a відзначення, as its details list it. */
export interface AwardGrant {
  award: MemberAwardDto;
  /** Waits for the kurin to confirm it (УПЮ only). */
  pending: boolean;
  /** The kurin it was given in, when the person has stood in more than one. */
  stamp: string | null;
  /** The viewer may open it: change, confirm or remove. */
  canOpen: boolean;
}

/** A відзначення on the card: every grant of it under one block. */
export interface AwardBlock {
  key: string;
  title: string;
  imageUrl: string | null;
  /** «УСП», «УПС», «Хрести»; empty for an УПЮ відзначення. */
  section: string;
  /** Newest first. */
  grants: AwardGrant[];
  pinned: boolean;
  pending: boolean;
  /** For PinMyAward: the level of an УПЮ відзначення or the catalogue id of a senior one. */
  pin: { level?: MemberAwardDto['level']; catalogId?: string | null };
}

/** Grants under one block each, newest first inside. */
export function groupGrants<T extends MemberAwardDto>(awards: readonly T[], keyOf: (award: T) => string): Map<string, T[]> {
  const groups = new Map<string, T[]>();
  for (const award of [...awards].sort((a, b) => dateOnlyTime(b.dateAcquired) - dateOnlyTime(a.dateAcquired))) {
    const key = keyOf(award);
    groups.set(key, [...(groups.get(key) ?? []), award]);
  }
  return groups;
}

/** Pinned first, then the most recently given. */
export function orderBlocks(blocks: readonly AwardBlock[]): AwardBlock[] {
  const latest = (block: AwardBlock) => dateOnlyTime(block.grants[0]?.award.dateAcquired ?? null);
  return [...blocks].sort((a, b) => Number(b.pinned) - Number(a.pinned) || latest(b) - latest(a));
}

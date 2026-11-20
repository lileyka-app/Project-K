import { KurinBranch, KurinNumbering } from './enums/kurin-branch.enum';
import { MembershipKind } from './enums/membership-kind.enum';

/**
 * Один відтинок належності: курінь, гурток і час, коли це тривало. Курінь названий, а не лише
 * ключований, — тека приходить з іншого модуля й донести назву мусить вона сама.
 */
export interface MembershipDto {
  membershipKey: string;
  kurinKey: string;
  /** Null for a підготовчий курінь. */
  kurinNumber: number | null;
  branch: KurinBranch;
  kurinNamedAfter?: string | null;
  /** Every гілка of the kurin with its number there, main one first. */
  numbering?: KurinNumbering[];
  groupKey?: string | null;
  groupName?: string | null;
  kind: MembershipKind;
  joinedAtUtc: string;
  leftAtUtc?: string | null;
  isCurrent: boolean;
}

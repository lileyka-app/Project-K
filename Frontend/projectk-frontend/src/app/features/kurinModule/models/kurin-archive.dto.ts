import { LeadershipRole } from './enums/leadership-role.enum';
import { MembershipKind } from './enums/membership-kind.enum';

export type ArchivedLeadershipType = 'Kurin' | 'Group' | 'KV';

/** One holding of an office; `endDate` null while it is held. Dates are `yyyy-MM-dd`. */
export interface ArchivedOfficeDto {
  historyKey: string;
  type: ArchivedLeadershipType;
  role: LeadershipRole;
  memberKey: string;
  memberName: string;
  startDate: string;
  endDate: string | null;
  groupKey: string | null;
  groupName: string | null;
  groupDissolved: boolean;
  canCorrect: boolean;
  isOwn: boolean;
}

/** A stretch in one гурток; `from` null when it began before the record did. */
export interface ArchivedStintDto {
  groupKey: string;
  groupName: string | null;
  from: string | null;
  to: string | null;
}

export interface ArchivedMembershipDto {
  membershipKey: string;
  memberKey: string;
  memberName: string;
  kind: MembershipKind;
  joinedOn: string;
  leftOn: string;
  groups: ArchivedStintDto[];
  canCorrect: boolean;
  isOwn: boolean;
}

export interface DissolvedGroupDto {
  groupKey: string;
  name: string;
  dissolvedOn: string;
}

export interface KurinArchiveDto {
  offices: ArchivedOfficeDto[];
  memberships: ArchivedMembershipDto[];
  dissolvedGroups: DissolvedGroupDto[];
  canDelete: boolean;
}

/** The danger zone as the server holds it. The ticket stays in the page's memory and nowhere else. */
export interface ArchiveDangerZoneDto {
  ticket: string;
  expiresAtUtc: string;
  deletionsLeft: number;
}

export interface ArchiveDeletionDto {
  deletionsLeft: number;
}

export type ArchiveDangerZoneEnd = 'Manual' | 'Left';

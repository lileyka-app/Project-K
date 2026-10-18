/**
 * The kurin, its groups and member cards as the API sends them. Kept in step with the web's
 * kurinModule/models (kurin.dto, group.dto, member.dto, membership.dto, member-award.dto,
 * requests/member/member-lookup.dto, requests/leadership/leadership.dto, probes-and-badges/*) and
 * duesModule/models/group-dues.dto (MemberDuesDto). Enums travel as strings (JsonStringEnumConverter).
 */

export type KurinBranch = 'UPYu' | 'USP' | 'UPS';

export interface KurinDto {
  kurinKey: string;
  /** Null for a підготовчий курінь. */
  number: number | null;
  branch?: KurinBranch;
  stanytsia?: string | null;
  regionOrCountry?: string | null;
  namedAfter?: string | null;
  description?: string | null;
  profileVerificationEnabled?: boolean;
}

export interface GroupDto {
  groupKey: string;
  kurinKey: string;
  name: string;
  description?: string | null;
  silhouetteUrl?: string | null;
  /** Null for a підготовчий курінь. */
  kurinNumber: number | null;
}

export type AccountStatus = 'RegisteredInactive' | 'PendingActivation' | 'Active' | 'Suspended' | 'Archived';

export type ProfileVerificationStatus = 'Unverified' | 'VerifiedStale' | 'VerifiedCurrent';
export type WarningLevel = 'Level1' | 'Level2' | 'Level3';

export interface MemberWarningDto {
  memberWarningKey: string;
  level: WarningLevel;
  issuedAtUtc: string;
  expiresAtUtc: string;
  /** Who gave it: only they lift it (web canClearWarning). */
  issuedByUserKey?: string | null;
  revokedAtUtc?: string | null;
}

export interface LeadershipHistoryDto {
  leadershipHistoryKey: string;
  leadershipKey: string;
  role: string;
  leadershipType?: string | null;
  groupName?: string | null;
  startDate: string;
  endDate: string | null;
  member: MemberLookupDto;
}

export interface LeadershipDto {
  leadershipKey: string | null;
  type?: string | null;
  entityKey?: string | null;
  startDate: string;
  endDate: string | null;
  leadershipHistories: LeadershipHistoryDto[];
}

/** A row of a member list (`member/kurins/{k}/members`, `member/groups/{g}/members`, `members/kv/{k}`). */
export interface MemberLookupDto {
  memberKey: string;
  userKey?: string | null;
  userRole?: string | null;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  profilePhotoUrl?: string | null;
  latestPlastLevel?: string | null;
  phoneNumber?: string | null;
  dateOfBirth?: string | null;
  profileVerificationStatus?: ProfileVerificationStatus | null;
  leadershipHistories?: LeadershipHistoryDto[];
  mentoredGroupNames?: string[];
  warnings?: MemberWarningDto[];
  plastLevelHistories?: PlastLevelHistoryDto[];
  /** Where list reads place them in the kurin they were read for; Guid.Empty for no гурток. */
  groupKey?: string | null;
  groupName?: string | null;
  /** В кадрі виховників of that kurin (list reads only). */
  isStaff?: boolean;
}

export interface PlastLevelHistoryDto {
  memberKey?: string;
  plastLevelHistoryKey?: string;
  plastLevel: string;
  dateAchieved?: string | null;
}

export type AwardLevel = 'First' | 'Second' | 'Third' | 'Fourth';
export type ProgressStatus = 'Draft' | 'Submitted' | 'Confirmed' | 'Rejected';

export interface MemberAwardDto {
  memberAwardKey: string;
  memberKey: string;
  kurinKey: string;
  level: AwardLevel;
  dateAcquired: string;
  note?: string | null;
  status: ProgressStatus | number;
  imageUrl?: string | null;
}

/** `GET member/{memberKey}`: the card. */
export interface MemberDto extends MemberLookupDto {
  /** Where the account stands; sent only to those who see the person's private details. */
  accountStatus?: AccountStatus | null;
  /** Only on a write: false when the invitation letter did not go out. */
  invitationSent?: boolean | null;
  kurinKey?: string | null;
  email: string;
  address?: string | null;
  school?: string | null;
  motherFirstName?: string | null;
  motherLastName?: string | null;
  motherPhone?: string | null;
  fatherFirstName?: string | null;
  fatherLastName?: string | null;
  fatherPhone?: string | null;
  mentoredGroups?: { groupKey: string; name: string }[];
  awards?: MemberAwardDto[];
  profileVerifiedAtUtc?: string | null;
  /** The край's extra card fields by key (dates as «YYYY-MM-DD»); empty to who may not see private details. */
  cardExtras?: Record<string, string>;
}

export interface MembershipDto {
  membershipKey: string;
  kurinKey: string;
  /** Null for a підготовчий курінь. */
  kurinNumber: number | null;
  branch: KurinBranch;
  kurinNamedAfter?: string | null;
  groupKey?: string | null;
  groupName?: string | null;
  kind: 'Youth' | 'Staff';
  joinedAtUtc: string;
  leftAtUtc?: string | null;
  isCurrent: boolean;
}


export interface MentorAssignmentDto {
  mentorAssignmentKey: string;
  mentorUserKey: string;
  groupKey: string;
  groupName: string;
  assignedAtUtc: string;
  revokedAtUtc: string | null;
  member: MemberLookupDto | null;
}

// ── The край's card fields (`kurin/{k}/profile`, KraiProfile.cardFields) ────────────────────────────

export type CardFieldMode = 'required' | 'optional' | 'hidden';

/** One field the край adds to its members' cards. */
export interface CardExtraField {
  key: string;
  label: string;
  type: 'text' | 'date';
  required: boolean;
}

export interface CardFields {
  fields: { middleName: CardFieldMode; phoneNumber: CardFieldMode; address: CardFieldMode; school: CardFieldMode; parents: CardFieldMode };
  extra: CardExtraField[];
}

// ── Skills and probes (ProbesAndBadgesModule) ────────────────────────────────────────────────────

export interface BadgeCatalogItemDto {
  id: string;
  title: string;
  imagePath: string;
  country: string;
  specialization: string;
}

export interface BadgeProgressDto {
  badgeProgressKey: string;
  memberKey: string;
  badgeId: string;
  status: ProgressStatus | number;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
}

export type ProbeStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Verified';

export interface ProbeSummaryDto {
  id: string;
  title: string;
  pointsCount: number;
  sectionsCount: number;
}

export interface GroupedProbeDto {
  id: string;
  title: string;
  pointsCount: number;
  sectionsCount: number;
  sections: { id: string; code: string; title: string; points: { id: string; title: string }[] }[];
}

export interface ProbePointProgressDto {
  pointId: string;
  isSigned: boolean;
  signedAtUtc: string | null;
  signedByUserKey: string | null;
  signedByName: string | null;
  signedByRole: string | null;
}

export interface ProbeProgressDto {
  probeId: string;
  status: ProbeStatus | number;
  completedAtUtc: string | null;
  completedByUserKey?: string | null;
  completedByName?: string | null;
  completedByRole?: string | null;
  verifiedAtUtc: string | null;
  verifiedByUserKey?: string | null;
  verifiedByName?: string | null;
  verifiedByRole?: string | null;
  pointSignatures?: ProbePointProgressDto[];
}

// ── Dues (duesModule/models/group-dues.dto, MemberDuesDto) ───────────────────────────────────────

export interface QuarterDto {
  year: number;
  number: number;
}

export interface DuesEntryDto {
  duesEntryKey: string;
  kind: string;
  amount: number;
  occurredOn: string;
}

export interface MemberDuesDto {
  hasAccount: boolean;
  kurinKey: string;
  /** ISO 4217 code of what the amounts are counted in: the край's currency. */
  currency: string;
  /** How the край writes numbers, such as uk-UA. */
  numberLocale: string;
  currentQuarter: QuarterDto;
  balance: number;
  quarterRate: { total: number } | null;
  isConcessionNow: boolean;
  currentGroupKey: string | null;
  currentGroupName: string | null;
  canOpenGroupDues: boolean;
  accounts: { groupKey: string; groupName: string; standing: 'Current' | 'Moved' | 'Left'; balance: number }[];
  entries: DuesEntryDto[];
}

// ── Writes ───────────────────────────────────────────────────────────────────────────────────────

export interface UpsertAwardRequest {
  memberAwardKey?: string;
  level: AwardLevel;
  dateAcquired: string;
  note?: string;
}

/** `POST leadership` / `PUT leadership/{key}` (UpsertLeadershipRequest): who sits in which office now. */
export interface UpsertLeadershipRequest {
  type: 'Kurin' | 'Group' | 'KV';
  entityKey: string;
  startDate: string;
  endDate: null;
  leadershipHistories: {
    role: string;
    startDate: string;
    endDate: null;
    member: { memberKey: string; firstName: string; lastName: string; middleName: string | null };
  }[];
}

/** `POST group` / `PUT group/{key}` (CreateGroupRequest, UpdateGroupRequest). */
export interface GroupRequest {
  name: string;
  description: string | null;
}

import { KurinNumbering } from '../../core/kurin-label';
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
  /** Every гілка with the kurin's number there, main one first. */
  branches?: KurinNumbering[];
  stanytsia?: string | null;
  regionOrCountry?: string | null;
  namedAfter?: string | null;
  description?: string | null;
  profileVerificationEnabled?: boolean;
  /** Whether the kurin keeps точкування now (web kurin.dto). */
  scoreEnabled?: boolean;
  /** The kurin's own role names: «Pysar» → «Скриптор» (web kurin.dto). */
  roleNames?: Record<string, string>;
  /** A senior kurin's emblem, shown instead of the УПЮ plate (web kurin.dto). */
  emblemUrl?: string | null;
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
  /** Who lifted it; empty when the system closed it once it ran out. */
  revokedByUserKey?: string | null;
  revokedAtUtc?: string | null;
}

export interface LeadershipHistoryDto {
  leadershipHistoryKey: string;
  leadershipKey: string;
  role: string;
  leadershipType?: string | null;
  groupName?: string | null;
  /** The kurin the office is in; a card carries offices from every kurin the person stands in. */
  kurinKey?: string | null;
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

/** A group of a person's data the kurin shows or hides as one; the names `hiddenData` carries. */
export type MemberDataGroup = 'contacts' | 'birthDate' | 'birthday' | 'address' | 'parents' | 'extras';

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
  /** Day and month of birth as «MM-DD», sent when the kurin shows the birthday. */
  birthday?: string | null;
  /** The groups of data the server hid from this viewer; their fields are not shown at all. */
  hiddenData?: MemberDataGroup[];
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
  /** The kurin's tags on them that the viewer may see; names come from the kurin's tag list. */
  tagKeys?: string[];
}

export interface PlastLevelHistoryDto {
  memberKey?: string;
  plastLevelHistoryKey?: string;
  plastLevel: string;
  dateAchieved?: string | null;
  /** A пластунка's ступінь rather than a пластун's: its own name and відзнака in the catalogue. */
  isPlastunka?: boolean;
  /** The ступінь in the catalogue (web PlastLevelHistoryDto.catalogId). Read only. */
  catalogId?: string | null;
}

/** A ступінь or a senior відзначення from the catalogue, with its picture (`GET api/catalog/honours`). */
export interface CatalogHonour {
  id: string;
  kind: 'Degree' | 'Award';
  title: string;
  group: string;
  ulad: string;
  order: number;
  /** Empty when the catalogue has no picture. */
  imageUrl: string;
}

export type AwardLevel = 'First' | 'Second' | 'Third' | 'Fourth';
export type ProgressStatus = 'Draft' | 'Submitted' | 'Confirmed' | 'Rejected';

export interface MemberAwardDto {
  memberAwardKey: string;
  memberKey: string;
  kurinKey: string;
  /** An УПЮ відзначення's level; null for a senior one. */
  level?: AwardLevel | null;
  /** A senior відзначення's catalogue id; null for an УПЮ one. */
  catalogId?: string | null;
  dateAcquired: string;
  /** What it was given for. */
  note?: string | null;
  /** The person pinned it to the front of their card. */
  isPinned?: boolean;
  status: ProgressStatus | number;
  imageUrl?: string | null;
  /** Who put it forward; they may withdraw it while it waits. */
  submittedByUserKey?: string | null;
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
  /** Every гілка of the kurin with its number there (API MembershipRecord.Numbering). */
  numbering?: KurinNumbering[];
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
  /** Edition of the programme; null for the one kurins have walked from the start (web ProbeSummaryDto). */
  edition?: string | null;
  /** Place within the edition; probes open one after another by it. */
  order?: number;
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
  /** Unpaid quarters already behind them — what keeps them in the kurin (web MemberDuesDto). */
  owedToLeave: number;
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
  level?: AwardLevel;
  /** A senior відзначення from the catalogue, instead of a level. */
  catalogId?: string;
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

/** A picture of a project's texts; the link expires, the key stays in the Markdown. */
export interface ProbeProjectImageDto {
  imageKey: string;
  url: string;
}

export type ProbeProjectArea = 'OathAndIdea' | 'Community' | 'PlastActivities' | 'Nature' | 'Leadership';
export type ProbeProjectReach = 'GroupOrKurin' | 'Stanytsia' | 'UkrainianCommunity' | 'WiderSociety';

/** One project of the third probe, as the УПЮ template lays it out (web probe-project.dto.ts). */
export interface ProbeProjectDto {
  projectKey: string;
  title: string;
  area: ProbeProjectArea | null;
  reach: ProbeProjectReach[];
  isShared: boolean;
  sharedWith: string | null;
  isSpare: boolean;
  startsOn: string | null;
  endsOn: string | null;
  location: string | null;
  eventLeads: string | null;
  coordinatorName: string | null;
  coordinatorContact: string | null;
  /** Markdown, like goal, plan and results; pictures are written as `project-image:{imageKey}`. */
  description: string;
  goal: string;
  plan: string;
  results: string;
  approvedAtUtc: string | null;
  approvedByName: string | null;
  completedAtUtc: string | null;
  completedByName: string | null;
  coordinatorComment: string | null;
  updatedAtUtc: string;
  updatedByName: string | null;
  images: ProbeProjectImageDto[];
}

/** The stages the referent of the third probe tracks, each the day it happened. */
export interface ThirdProbeStagesDto {
  motivationLetterOn: string | null;
  selfReflectionOn: string | null;
  applicationSubmittedOn: string | null;
  openedOn: string | null;
  completedOn: string | null;
  namedOn: string | null;
  archivedOn: string | null;
  updatedAtUtc: string | null;
  updatedByName: string | null;
}

export type ThirdProbeStagesRequest = Omit<ThirdProbeStagesDto, 'updatedAtUtc' | 'updatedByName'>;

export interface ThirdProbeFileDto {
  openedOn: string | null;
  openedOnIsDerived: boolean;
  mentorZvyazkovyi: string | null;
  mentorVporiadnyk: string | null;
  mentorReferent: string | null;
  reflectionOn: string | null;
  reflectionWith: string | null;
  reflectionConfirmedAtUtc: string | null;
  reflectionConfirmedByName: string | null;
  campKind: 'Kvt' | 'Alternative' | null;
  campName: string | null;
  campFrom: string | null;
  campTo: string | null;
  campCertificate: { url: string; contentType: string; fileName: string | null } | null;
  physicalBadgeId: string | null;
  resume: string;
  stages: ThirdProbeStagesDto;
  storedBytes: number;
}

/** `PUT …/third-probe`: what the person writes; confirmations and stages go their own way. */
export interface ThirdProbeFileRequest {
  mentorZvyazkovyi: string | null;
  mentorVporiadnyk: string | null;
  mentorReferent: string | null;
  reflectionOn: string | null;
  reflectionWith: string | null;
  campKind: 'Kvt' | 'Alternative' | null;
  campName: string | null;
  campFrom: string | null;
  campTo: string | null;
  physicalBadgeId: string | null;
  resume: string;
}

/** `POST|PUT …/projects`: a project as the template lays it out. */
export interface ProbeProjectRequest {
  title: string;
  area: ProbeProjectArea | null;
  reach: ProbeProjectReach[];
  isShared: boolean;
  sharedWith: string | null;
  startsOn: string | null;
  endsOn: string | null;
  location: string | null;
  eventLeads: string | null;
  coordinatorName: string | null;
  coordinatorContact: string | null;
  description: string;
  goal: string;
  plan: string;
  results: string;
}

export interface ThirdProbeBadgeDto {
  badgeId: string;
  title: string;
  imagePath: string | null;
  specialization: string | null;
  confirmedAtUtc: string | null;
  duringProbe: boolean;
}

/** `GET member/{key}/probes/{probeId}/third-probe`: only the probe at the third place of the line has one. */
export interface ThirdProbeDto {
  isProjectProbe: boolean;
  canEdit: boolean;
  canSign: boolean;
  canUnsign: boolean;
  file: ThirdProbeFileDto | null;
  projects: ProbeProjectDto[];
  badges: ThirdProbeBadgeDto[];
  limits: {
    maxMainProjects: number;
    maxSpareProjects: number;
    maxSharedMainProjects: number;
    maxImagesPerProject: number;
    maxImageBytes: number;
    maxCertificateBytes: number;
    maxStoredBytes: number;
    maxTextLength: number;
    requiredBadges: number;
    youthEndsAtAge: number;
  };
}

// ── Requests between kurins (web membership.service) ────────────────────────────────────────────

export type MembershipRequestKind = 'Transfer' | 'AlsoJoin';
export type MembershipRequestStatus = 'Pending' | 'Approved' | 'Declined' | 'Cancelled' | 'Expired';

export interface MembershipRequestKurinDto {
  kurinKey: string;
  number: number | null;
  namedAfter: string | null;
  branches: KurinNumbering[];
}

/** `owedToLeave`: for the answering kurin on a pending «перевести» — above zero, it cannot be approved. */
export interface MembershipRequestDto {
  membershipRequestKey: string;
  kind: MembershipRequestKind;
  status: MembershipRequestStatus;
  memberKey: string;
  memberName: string;
  fromKurin: MembershipRequestKurinDto;
  toKurin: MembershipRequestKurinDto;
  membershipKind: 'Youth' | 'Staff';
  createdAtUtc: string;
  expiresAtUtc: string;
  decidedAtUtc: string | null;
  owedToLeave: number;
}

export interface MembershipCandidateDto {
  member: { memberKey: string; publicId: string; fullName: string };
  alreadyInThisKurin: boolean;
  currentKurins: MembershipRequestKurinDto[];
}

/** A former member and the kurins they stand in now (web FormerMemberStandingDto); empty — nowhere, taken back at once. */
export interface FormerMemberStandingDto {
  former: {
    memberKey: string;
    firstName: string;
    middleName?: string | null;
    lastName: string;
    groupName?: string | null;
    joinedAtUtc: string;
    leftAtUtc: string;
  };
  standsIn: MembershipRequestKurinDto[];
}

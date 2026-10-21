import { AuthState } from '../../auth/auth.models';
import { amountFormat, currencySymbol, DEFAULT_CURRENCY, DEFAULT_NUMBER_LOCALE } from '../../me/labels';
import {
  AwardLevel,
  BadgeCatalogItemDto,
  BadgeProgressDto,
  GroupDto,
  GroupedProbeDto,
  KurinBranch,
  LeadershipHistoryDto,
  MemberAwardDto,
  MemberLookupDto,
  MembershipDto,
  MemberWarningDto,
  MentorAssignmentDto,
  ProbeProgressDto,
  ProbeStatus,
  ProbeSummaryDto,
  ProgressStatus,
  QuarterDto,
  WarningLevel,
} from './kurin.models';

/*
 * Pure wording and ordering for the kurin screens, ported from the web's kurinModule/functions
 * (leadership-role-*, member-view-mapper, upcoming-birthdays, member-skills-view-mapper,
 * member-probe-*-mapper, member-branch) and the permission service, so both say the same thing.
 */

// ── Access: the web's PermissionService predicates over the session's grants ─────────────────────

function has(state: AuthState | null, prefix: string): boolean {
  return (state?.permissions ?? []).some((permission) => permission.startsWith(prefix));
}

export interface KurinAccess {
  /** Звʼязковий or admin. */
  manageWholeKurin: boolean;
  /** Виховник and the whole-kurin managers: confirm skills, sign probes (with check-access). */
  reviewSkills: boolean;
  /** Keeps some гурток's box, or reads them all. */
  seeGroupDues: boolean;
  /** The kurin's box: the курінний скарбник, the Звʼязковий, an admin. A youth's grant is about their own balance. */
  seeKurinDues: boolean;
  /**
   * Seats a гурток's провід (web canSetupLeadership('group')): the Звʼязковий and admins, and a
   * Виховник or гуртковий for their own гуртки — the server still says no to someone else's.
   */
  setupGroupLeadership: boolean;
  /**
   * Seats the kurin's провід (web canSetupLeadership('kurin')): the Звʼязковий and admins, and the
   * курінний, who may seat everyone but himself.
   */
  setupKurinLeadership: boolean;
  /** Seats the курінний himself: the server lets only the Звʼязковий and admins (AssignableOffices). */
  seatKurinHead: boolean;
}

export function kurinAccess(state: AuthState | null): KurinAccess {
  const isAdmin = state?.isAdmin ?? false;
  const manageWholeKurin = isAdmin || has(state, 'Group:Manage:KurinWide');
  return {
    manageWholeKurin,
    reviewSkills: has(state, 'Group:Update') || manageWholeKurin,
    seeGroupDues: isAdmin || has(state, 'GroupDues:Read:OwnGroups') || has(state, 'GroupDues:Read:KurinWide'),
    seeKurinDues: isAdmin || has(state, 'KurinDues:Read'),
    setupGroupLeadership: isAdmin || has(state, 'Leadership:Manage:KurinWide') || has(state, 'Leadership:Update:OwnGroups'),
    setupKurinLeadership: isAdmin || has(state, 'Leadership:Manage:KurinWide') || has(state, 'Leadership:Update:KurinWide'),
    seatKurinHead: isAdmin || has(state, 'Leadership:Manage:KurinWide'),
  };
}

// ── Dates ────────────────────────────────────────────────────────────────────────────────────────

/** A date-only value («2012-03-14») as a local date, or null. */
export function dateOnly(value: string | null | undefined): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value ?? '');
  if (!match) return null;
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return Number.isNaN(date.getTime()) ? null : date;
}

/** A UTC moment, with the zone added when the server left it off (as the web's parseUtcDateTime). */
export function utcDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const trimmed = value.trim();
  const normalized =
    /^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}/.test(trimmed) && !/(Z|[+-]\d{2}:?\d{2})$/i.test(trimmed)
      ? `${trimmed.replace(' ', 'T')}Z`
      : trimmed;
  const date = new Date(normalized);
  return Number.isNaN(date.getTime()) ? null : date;
}

/** «14.03.2012» */
export function shortDate(date: Date | null): string {
  if (!date) return '—';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}`;
}

/** A Date as the API's date-only string. */
export function toDateOnly(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

// ── Kurin ────────────────────────────────────────────────────────────────────────────────────────

export const BRANCH_LABELS: Record<KurinBranch, string> = { UPYu: 'УПЮ', USP: 'УСП', UPS: 'УПС' };

/** Probes, skills and awards are the youth's (УПЮ); a kurin from before branches is УПЮ. */
export function hasYouthProgram(branch: KurinBranch | null | undefined): boolean {
  return (branch ?? 'UPYu') === 'UPYu';
}

// ── Offices ──────────────────────────────────────────────────────────────────────────────────────

const ROLE_NAMES: Record<string, string> = {
  Kurinnuy: 'Курінний',
  Hurtkoviy: 'Гуртковий',
  Suddya: 'Суддя',
  Pysar: 'Писар',
  Skarbnyk: 'Скарбник',
  Horunjiy: 'Хорунжий',
  Gospodar: 'Господар',
  Hronikar: 'Хронікар',
  Instruktor: 'Інструктор',
  Zvyazkovyi: 'Звʼязковий',
  Vykhovnyk: 'Впорядник',
  OtherKurin: 'Інша курінна роль',
  OtherGroup: 'Інша гурткова роль',
};

const ROLE_ORDER = [
  'Zvyazkovyi',
  'Kurinnuy',
  'Hurtkoviy',
  'Suddya',
  'Pysar',
  'Skarbnyk',
  'Horunjiy',
  'Gospodar',
  'Hronikar',
  'Instruktor',
  'Vykhovnyk',
  'OtherKurin',
  'OtherGroup',
];

export function roleName(role: string): string {
  return ROLE_NAMES[role] ?? role;
}

export function roleWeight(role: string | null | undefined): number {
  const index = ROLE_ORDER.indexOf(role ?? '');
  return index < 0 ? Number.MAX_SAFE_INTEGER : index;
}

/** «Прізвище Імʼя По батькові», the way every list names a member. */
export function fullName(member: Pick<MemberLookupDto, 'firstName' | 'lastName' | 'middleName'>): string {
  return [member.lastName, member.firstName, member.middleName].filter(Boolean).join(' ');
}

/** Active first, then by office, then by name, then the latest first (web compareLeadershipHistoriesByDefault). */
export function compareHistories(left: LeadershipHistoryDto, right: LeadershipHistoryDto): number {
  if (Boolean(left.endDate) !== Boolean(right.endDate)) return left.endDate ? 1 : -1;
  const weight = roleWeight(left.role) - roleWeight(right.role);
  if (weight !== 0) return weight;
  const name = fullName(left.member).toLowerCase().localeCompare(fullName(right.member).toLowerCase());
  if (name !== 0) return name;
  return (dateOnly(right.startDate)?.getTime() ?? 0) - (dateOnly(left.startDate)?.getTime() ?? 0);
}

/** The offices held now, sorted as the web shows them. */
export function currentOffices(histories: LeadershipHistoryDto[] | null | undefined): LeadershipHistoryDto[] {
  return (histories ?? []).filter((h) => !h.endDate).sort(compareHistories);
}

/** A system-role name `"KV.Zvyazkovyi"` holds the office `Zvyazkovyi` (web holdsOffice). */
export function holdsOffice(userRole: string | null | undefined, role: string): boolean {
  const parts = (userRole ?? '').split('.');
  return parts.length === 2 && ['Kurin', 'Group', 'KV'].includes(parts[0]) && parts[1] === role;
}

/** The colours of the web's p-tag severities; each has an `lk-tag--*` class in lileyka.scss. */
export type TagTone = 'info' | 'success' | 'warn' | 'danger' | 'secondary';

/** A coloured label, as the web's `<p-tag [value] [severity]>`. */
export interface RoleTag {
  label: string;
  tone: TagTone;
}

/** The colour an office is shown in (web leadershipRoleSeverityForRole), the same on every screen. */
export function roleTone(role: string): TagTone {
  switch (role) {
    case 'Kurinnuy':
    case 'Zvyazkovyi':
    case 'Hurtkoviy':
      return 'danger';
    case 'Suddya':
      return 'warn';
    case 'Skarbnyk':
      return 'success';
    default:
      return 'info';
  }
}

/** An office as a tag; an ended one is grey whatever it was (web leadershipRoleSeverity). */
export function officeTag(history: Pick<LeadershipHistoryDto, 'role' | 'endDate'>): RoleTag {
  return { label: roleName(history.role), tone: history.endDate ? 'secondary' : roleTone(history.role) };
}

/**
 * The offices a member holds now, one tag each; a mentor assignment joins the Впорядник tag
 * (web member-list getMemberRoleTags).
 */
export function memberRoleTags(member: MemberLookupDto): RoleTag[] {
  const active = currentOffices(member.leadershipHistories);
  const tags = active.map((h) => ({
    label: (h.leadershipType ?? '').toLowerCase() === 'group' && h.groupName ? `${roleName(h.role)}: ${h.groupName}` : roleName(h.role),
    tone: roleTone(h.role),
  }));
  const mentored = member.mentoredGroupNames ?? [];
  if (!mentored.length) return tags;
  const tag = { label: `${roleName('Vykhovnyk')}: ${mentored.join(', ')}`, tone: roleTone('Vykhovnyk') };
  const seated = active.findIndex((h) => h.role === 'Vykhovnyk');
  if (seated >= 0) tags[seated] = tag;
  else tags.push(tag);
  return tags;
}

function memberRoleWeight(member: MemberLookupDto): number {
  const office = (member.userRole ?? '').split('.')[1];
  const kv = office ? roleWeight(office) : Number.MAX_SAFE_INTEGER;
  const held = currentOffices(member.leadershipHistories).reduce((low, h) => Math.min(low, roleWeight(h.role)), Number.MAX_SAFE_INTEGER);
  const mentor = member.mentoredGroupNames?.length ? roleWeight('Vykhovnyk') : Number.MAX_SAFE_INTEGER;
  return Math.min(kv, held, mentor);
}

export type MemberSort = 'name' | 'role';

/** By surname, or by office (провід first, then everyone else by surname). */
export function sortMembers<T extends MemberLookupDto>(members: T[], by: MemberSort): T[] {
  const byName = (a: T, b: T) => fullName(a).toLowerCase().localeCompare(fullName(b).toLowerCase(), 'uk');
  return [...members].sort((a, b) => (by === 'role' ? memberRoleWeight(a) - memberRoleWeight(b) || byName(a, b) : byName(a, b)));
}

/** The web's member search: name, ступінь or phone. */
export function filterMembers<T extends MemberLookupDto>(members: T[], query: string): T[] {
  const q = query.trim().toLowerCase();
  if (!q) return members;
  return members.filter(
    (m) =>
      `${m.lastName} ${m.firstName} ${m.middleName ?? ''}`.toLowerCase().includes(q) ||
      `${m.firstName} ${m.lastName}`.toLowerCase().includes(q) ||
      (plastLevelLabel(m) ?? '').toLowerCase().includes(q) ||
      (m.phoneNumber ?? '').toLowerCase().includes(q),
  );
}

// ── КВ ───────────────────────────────────────────────────────────────────────────────────────────

export interface KvRow {
  member: MemberLookupDto;
  status: string;
  groups: string[];
  /** The same гуртки by key, for «Налаштувати гуртки». */
  groupKeys: string[];
  isManager: boolean;
}

/**
 * The Звʼязковий first, then the rest of the КВ by name, each with the гуртки they are closed over.
 * The гуртки name the status, not the seat: a впорядник runs at least one, an інструктор none.
 */
export function kvRows(kvMembers: MemberLookupDto[], assignments: MentorAssignmentDto[], groups: GroupDto[]): KvRow[] {
  const same = (a: MemberLookupDto, b: MemberLookupDto) =>
    a.userKey && b.userKey ? a.userKey === b.userKey : a.memberKey === b.memberKey;
  const groupNames = new Map(groups.map((g) => [g.groupKey, g.name]));
  const rows = new Map<string, { member: MemberLookupDto; groups: string[]; groupKeys: string[] }>();
  for (const assignment of assignments) {
    if (assignment.revokedAtUtc || !assignment.member) continue;
    const name = groupNames.get(assignment.groupKey);
    if (!name) continue;
    const member = kvMembers.find((m) => same(m, assignment.member!)) ?? assignment.member;
    const key = member.userKey ?? member.memberKey;
    const row = rows.get(key) ?? { member, groups: [], groupKeys: [] };
    row.groups.push(name);
    row.groupKeys.push(assignment.groupKey);
    rows.set(key, row);
  }
  for (const member of kvMembers) {
    if (!['Hurtkoviy', 'Vykhovnyk', 'Instruktor'].some((office) => holdsOffice(member.userRole, office))) continue;
    const key = member.userKey ?? member.memberKey;
    if (!rows.has(key)) rows.set(key, { member, groups: [], groupKeys: [] });
  }
  const mentors = [...rows.values()]
    .sort((a, b) => fullName(a.member).localeCompare(fullName(b.member), 'uk'))
    .map((row) => ({ ...row, status: row.groups.length ? 'Впорядник' : 'Інструктор', isManager: false }));
  const manager = kvMembers.find((m) => holdsOffice(m.userRole, 'Zvyazkovyi'));
  if (!manager) return mentors;
  const own = mentors.find((row) => same(row.member, manager));
  return [
    { member: manager, status: 'Звʼязковий', groups: own?.groups ?? [], groupKeys: own?.groupKeys ?? [], isManager: true },
    ...mentors.filter((row) => !same(row.member, manager)),
  ];
}

/**
 * A КВ row's tags (web kv-panel «Статус» and «Гуртки»): the status in its own colour, which is not
 * the office colour (the Звʼязковий red, a впорядник with гуртки green, an інструктор without
 * them amber), then each гурток.
 */
export function kvRowTags(row: Pick<KvRow, 'status' | 'groups' | 'isManager'>): RoleTag[] {
  const tone: TagTone = row.isManager ? 'danger' : row.groups.length ? 'success' : 'warn';
  return [{ label: row.status, tone }, ...row.groups.map((group) => ({ label: group, tone: 'info' as const }))];
}

/** The ended assignments, the latest ended first (web kv-panel archivedAssignments). */
export function kvArchive(assignments: MentorAssignmentDto[]): MentorAssignmentDto[] {
  return assignments
    .filter((a) => !!a.revokedAtUtc)
    .sort((a, b) => (utcDate(b.revokedAtUtc)?.getTime() ?? 0) - (utcDate(a.revokedAtUtc)?.getTime() ?? 0));
}

// ── Ступені ──────────────────────────────────────────────────────────────────────────────────────

const LEVEL_LABELS: Record<string, string> = {
  Entry: '',
  Prykhylnyk: 'пл. прих.',
  Uchasnyk: 'пл. уч.',
  Rozviduvach: 'пл. розв.',
  Skob: 'пл. скоб',
  HetmanskiySkob: 'пл. гетьм. скоб',
  Starshoplastun: 'ст. пл.',
  Senior: 'пл. сен.',
  SeniorPratsi: 'пл. сен. пр.',
  SeniorDovirja: 'пл. сен. дов.',
  SeniorKerivnytstva: 'пл. сен. кер.',
};

const LADDER = Object.keys(LEVEL_LABELS);
const SENIOR = ['Senior', 'SeniorPratsi', 'SeniorDovirja', 'SeniorKerivnytstva'];

/** The ступінь as the web prints it before a name («пл. уч.», «ст. пл. скоб»); null when none. */
export function plastLevelLabel(member: Pick<MemberLookupDto, 'latestPlastLevel' | 'plastLevelHistories'>): string | null {
  const level = member.latestPlastLevel;
  if (!level) return null;
  if (!(level in LEVEL_LABELS)) return level;
  if (level === 'Starshoplastun') {
    const levels = (member.plastLevelHistories ?? []).map((h) => h.plastLevel);
    if (levels.includes('HetmanskiySkob')) return 'ст. пл. гетьм. скоб';
    if (levels.includes('Skob')) return 'ст. пл. скоб';
  }
  return LEVEL_LABELS[level] || null;
}

/**
 * The person's branch where they are looked at: a senior ступінь outranks the kurin, else the
 * membership here decides (web memberBranchHere + personalBranch).
 */
export function memberBranch(
  member: Pick<MemberLookupDto, 'latestPlastLevel' | 'plastLevelHistories'> & { kurinKey?: string | null },
  memberships: MembershipDto[],
): KurinBranch {
  const here = memberships.find((m) => m.isCurrent && m.kurinKey === member.kurinKey) ?? memberships.find((m) => m.isCurrent);
  const levels = (member.plastLevelHistories ?? []).map((h) => h.plastLevel);
  if (member.latestPlastLevel) levels.push(member.latestPlastLevel);
  const top = levels.reduce<string | null>((best, l) => (best === null || LADDER.indexOf(l) > LADDER.indexOf(best) ? l : best), null);
  if (top === 'Starshoplastun') return 'USP';
  if (top && SENIOR.includes(top)) return 'UPS';
  return here?.branch ?? 'UPYu';
}

// ── Birthdays (web upcoming-birthdays.function) ──────────────────────────────────────────────────

export interface UpcomingBirthday {
  member: MemberLookupDto;
  date: Date;
  daysUntil: number;
}

export function upcomingBirthdays(members: MemberLookupDto[], daysAhead: number, reference = new Date()): UpcomingBirthday[] {
  const today = new Date(reference.getFullYear(), reference.getMonth(), reference.getDate());
  return members
    .map((member) => {
      const born = dateOnly(member.dateOfBirth);
      if (!born) return null;
      let next = new Date(today.getFullYear(), born.getMonth(), born.getDate());
      if (next < today) next = new Date(today.getFullYear() + 1, born.getMonth(), born.getDate());
      return { member, date: next, daysUntil: Math.round((next.getTime() - today.getTime()) / 86_400_000) };
    })
    .filter((item): item is UpcomingBirthday => item !== null && item.daysUntil <= daysAhead)
    .sort((a, b) => a.daysUntil - b.daysUntil || fullName(a.member).localeCompare(fullName(b.member), 'uk'));
}

/** «сьогодні», «завтра», «через 5 дн.» */
export function birthdayWhen(daysUntil: number): string {
  if (daysUntil === 0) return 'сьогодні';
  if (daysUntil === 1) return 'завтра';
  return `через ${daysUntil} дн.`;
}

// ── Перестороги ──────────────────────────────────────────────────────────────────────────────────

const WARNING_WEIGHT: Record<WarningLevel, number> = { Level1: 1, Level2: 2, Level3: 3 };
const WARNING_NAMES = ['', 'Перша', 'Друга', 'Третя'];

/** The highest warning in force, 1–3, and how many days it has left; null when none. */
export function activeWarning(warnings: MemberWarningDto[] | null | undefined, now = Date.now()): { level: number; daysLeft: number } | null {
  const active = (warnings ?? []).filter((w) => !w.revokedAtUtc && (utcDate(w.expiresAtUtc)?.getTime() ?? 0) > now);
  if (!active.length) return null;
  const top = active.reduce((a, b) => (WARNING_WEIGHT[b.level] > WARNING_WEIGHT[a.level] ? b : a), active[0]);
  const expires = utcDate(top.expiresAtUtc)?.getTime() ?? now;
  return { level: WARNING_WEIGHT[top.level] ?? 0, daysLeft: Math.max(0, Math.ceil((expires - now) / 86_400_000)) };
}

/** «Поточна пересторога: Друга. Залишилось днів: 12.» */
export function warningText(warning: { level: number; daysLeft: number }): string {
  return `Поточна пересторога: ${WARNING_NAMES[warning.level] ?? 'Пересторога'}. Залишилось днів: ${warning.daysLeft}.`;
}

// ── Skills ───────────────────────────────────────────────────────────────────────────────────────

const PROGRESS: ProgressStatus[] = ['Draft', 'Submitted', 'Confirmed', 'Rejected'];
const PROBE: ProbeStatus[] = ['NotStarted', 'InProgress', 'Completed', 'Verified'];

export function progressStatus(status: ProgressStatus | number | null | undefined): ProgressStatus {
  if (typeof status === 'number') return PROGRESS[status] ?? 'Draft';
  return status && PROGRESS.includes(status) ? status : 'Draft';
}

export function probeStatus(status: ProbeStatus | number | null | undefined): ProbeStatus {
  if (typeof status === 'number') return PROBE[status] ?? 'NotStarted';
  return status && PROBE.includes(status) ? status : 'NotStarted';
}

export interface SkillView {
  badgeId: string;
  title: string;
  imagePath: string | null;
  status: ProgressStatus;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
}

/** Confirmed (latest review first) and on review (latest submission first). */
export function skillsSummary(progresses: BadgeProgressDto[], catalog: BadgeCatalogItemDto[]): { confirmed: SkillView[]; pending: SkillView[] } {
  const byId = new Map(catalog.map((badge) => [badge.id, badge]));
  const view = (p: BadgeProgressDto): SkillView => ({
    badgeId: p.badgeId,
    title: byId.get(p.badgeId)?.title ?? p.badgeId,
    imagePath: byId.get(p.badgeId)?.imagePath ?? null,
    status: progressStatus(p.status),
    submittedAtUtc: p.submittedAtUtc,
    reviewedAtUtc: p.reviewedAtUtc,
  });
  const time = (value: string | null) => utcDate(value)?.getTime() ?? 0;
  return {
    confirmed: progresses
      .filter((p) => progressStatus(p.status) === 'Confirmed')
      .sort((a, b) => time(b.reviewedAtUtc) - time(a.reviewedAtUtc))
      .map(view),
    pending: progresses
      .filter((p) => progressStatus(p.status) === 'Submitted')
      .sort((a, b) => time(b.submittedAtUtc) - time(a.submittedAtUtc))
      .map(view),
  };
}

/** What a catalogue row says about the person's own progress on it (web getBadgeProgressStatusLabel). */
export function existingSkillLabel(status: ProgressStatus): string {
  switch (status) {
    case 'Submitted':
      return 'Вже подано, очікує підтвердження';
    case 'Confirmed':
      return 'Вже підтверджено';
    case 'Rejected':
      return 'Було відхилено. Можна подати повторно';
    default:
      return 'Вже додано';
  }
}

/** A skill may be (re)submitted when it is new, a draft or rejected. */
export function canSubmitSkill(status: ProgressStatus | null): boolean {
  return status === null || status === 'Draft' || status === 'Rejected';
}

// ── Probes ───────────────────────────────────────────────────────────────────────────────────────

export interface ProbeRow {
  probeId: string;
  label: string;
  status: ProbeStatus;
  completedAtUtc: string | null;
  isCompleted: boolean;
  isDisabled: boolean;
  canOpen: boolean;
  pointsCount: number | null;
  signedPoints: number;
  percent: number | null;
}

const PROBE_ROWS = [
  { probeId: 'probe-1', label: 'Перша проба', policyDisabled: false },
  { probeId: 'probe-2', label: 'Друга проба', policyDisabled: false },
  { probeId: 'probe-3', label: 'Третя проба', policyDisabled: true },
];

const closed = (status: ProbeStatus) => status === 'Completed' || status === 'Verified';

/** The three probes as the card lists them; the second opens once the first is closed (web buildMemberProbeRows). */
export function probeRows(probes: ProbeSummaryDto[], progresses: ProbeProgressDto[]): ProbeRow[] {
  const rows = PROBE_ROWS.map((template) => {
    const probe = probes.find((p) => p.id === template.probeId);
    const progress = progresses.find((p) => p.probeId === template.probeId);
    const status = probeStatus(progress?.status);
    const total = probe?.pointsCount ?? 0;
    const counted = (progress?.pointSignatures ?? []).filter((p) => p.isSigned).length;
    const signedPoints = closed(status) && counted === 0 ? total : counted;
    return {
      probeId: template.probeId,
      label: template.label,
      status,
      completedAtUtc: progress?.completedAtUtc ?? progress?.verifiedAtUtc ?? null,
      isCompleted: closed(status),
      isDisabled: template.policyDisabled,
      canOpen: !template.policyDisabled && !!probe,
      pointsCount: probe?.pointsCount ?? null,
      signedPoints,
      percent: total ? Math.round((signedPoints / total) * 100) : closed(status) ? 100 : null,
    };
  });
  const firstClosed = rows[0].isCompleted;
  return rows.map((row) => {
    if (row.probeId !== 'probe-2') return row;
    const unlocked = firstClosed || row.status !== 'NotStarted' || !!row.completedAtUtc;
    return { ...row, isDisabled: !unlocked, canOpen: unlocked && row.canOpen };
  });
}

const PROBE_STATUS_LABELS: Record<ProbeStatus, string> = {
  NotStarted: 'Не розпочато',
  InProgress: 'В процесі',
  Completed: 'Завершено',
  Verified: 'Підтверджено',
};

/** The line under a probe on the card (web getProbeSummaryMeta). */
export function probeMeta(row: ProbeRow): string {
  if (row.probeId === 'probe-2' && row.isDisabled) return 'Відкриється після закриття першої проби';
  if (row.probeId === 'probe-3' && row.isDisabled) return 'Третя проба буде реалізована окремим етапом';
  if (row.completedAtUtc) return `Завершено: ${shortDate(utcDate(row.completedAtUtc))}`;
  const status = PROBE_STATUS_LABELS[row.status];
  return row.pointsCount === null ? status : `${status} · ${row.pointsCount} точок`;
}

export interface ProbePointView {
  sectionId: string;
  pointId: string;
  title: string;
  isSigned: boolean;
  signedByName: string | null;
  signedByRole: string | null;
  signedByUserKey: string | null;
  signedAtUtc: string | null;
}

export interface ProbeSectionView {
  sectionId: string;
  code: string;
  title: string;
  points: ProbePointView[];
}

/**
 * Sections with every point and who signed it; a probe closed before points were signed one by one
 * reads as signed throughout by whoever closed it (web buildMemberProbeDetailPointRows).
 */
export function probeSections(probe: GroupedProbeDto | null, progress: ProbeProgressDto | null): ProbeSectionView[] {
  if (!probe) return [];
  const signatures = progress?.pointSignatures ?? [];
  const perPoint = signatures.length > 0;
  const byPoint = new Map(signatures.map((s) => [s.pointId, s]));
  const wholeSigned = closed(probeStatus(progress?.status));
  const fallback = {
    userKey: progress?.verifiedByUserKey ?? progress?.completedByUserKey ?? null,
    name: progress?.verifiedByName ?? progress?.completedByName ?? null,
    role: progress?.verifiedByRole ?? progress?.completedByRole ?? null,
    at: progress?.verifiedAtUtc ?? progress?.completedAtUtc ?? null,
  };
  return probe.sections.map((section) => ({
    sectionId: section.id,
    code: section.code,
    title: section.title,
    points: section.points.map((point) => {
      const own = byPoint.get(point.id);
      const isSigned = perPoint ? (own?.isSigned ?? false) : wholeSigned;
      return {
        sectionId: section.id,
        pointId: point.id,
        title: point.title,
        isSigned,
        signedByName: isSigned ? (own?.signedByName ?? fallback.name) : null,
        signedByRole: isSigned ? (own?.signedByRole ?? fallback.role) : null,
        signedByUserKey: isSigned ? (own?.signedByUserKey ?? fallback.userKey) : null,
        signedAtUtc: isSigned ? (own?.signedAtUtc ?? fallback.at) : null,
      };
    }),
  }));
}

/** «Олена Петренко (Впорядник)» (web getProbePointSignerLabel). */
export function signerLabel(point: ProbePointView): string {
  if (!point.isSigned) return 'Не підписано';
  const name = point.signedByName?.trim();
  const role = signerRole(point.signedByRole);
  if (name) return role ? `${name} (${role})` : name;
  if (point.signedByUserKey) return role ? `${point.signedByUserKey} (${role})` : point.signedByUserKey;
  if (role) return `Невідомий користувач (${role})`;
  return 'Підписант не вказаний';
}

/** The office as people say it («Впорядник»), not the stored system role («KV.Vykhovnyk»). */
function signerRole(systemRole: string | null | undefined): string | null {
  if (!systemRole) return null;
  const parts = systemRole.split('.');
  if (parts.length === 2 && ['Kurin', 'Group', 'KV'].includes(parts[0])) return roleName(parts[1]);
  if (systemRole === 'Admin') return 'Адміністратор';
  // Older signatures already carry the office's name.
  return /^[A-Za-z.]+$/.test(systemRole) ? null : systemRole;
}

export function isProbeClosed(progress: ProbeProgressDto | null): boolean {
  return closed(probeStatus(progress?.status));
}

// ── Awards ───────────────────────────────────────────────────────────────────────────────────────

export const AWARD_LEVELS: { value: AwardLevel; label: string }[] = [
  { value: 'First', label: 'Перша' },
  { value: 'Second', label: 'Друга' },
  { value: 'Third', label: 'Третя' },
  { value: 'Fourth', label: 'Четверта' },
];

const AWARD_TITLES: Record<AwardLevel, string> = {
  First: 'Перше відзначення',
  Second: 'Друге відзначення',
  Third: 'Третє відзначення',
  Fourth: 'Четверте відзначення',
};

export function awardTitle(level: AwardLevel): string {
  return AWARD_TITLES[level] ?? 'Невідоме відзначення';
}

export function awardWeight(level: AwardLevel): number {
  return AWARD_LEVELS.findIndex((l) => l.value === level) + 1;
}

export interface AwardGroup {
  level: AwardLevel;
  count: number;
  latest: MemberAwardDto;
}

/** Submitted and confirmed awards, one row per level with the latest of them (web groupedAwards). */
export function awardGroups(awards: MemberAwardDto[] | null | undefined): AwardGroup[] {
  const groups = new Map<AwardLevel, AwardGroup>();
  for (const award of awards ?? []) {
    const status = progressStatus(award.status);
    if (status !== 'Submitted' && status !== 'Confirmed') continue;
    const existing = groups.get(award.level);
    if (!existing) groups.set(award.level, { level: award.level, count: 1, latest: award });
    else {
      existing.count++;
      if ((dateOnly(award.dateAcquired)?.getTime() ?? 0) > (dateOnly(existing.latest.dateAcquired)?.getTime() ?? 0)) existing.latest = award;
    }
  }
  return [...groups.values()].sort((a, b) => awardWeight(a.level) - awardWeight(b.level));
}

// ── Dues ─────────────────────────────────────────────────────────────────────────────────────────

/** «1 240 ₴», «−300 Kč» with a real minus (web dues-format money). */
export function duesMoney(amount: number, currency: string = DEFAULT_CURRENCY, locale: string = DEFAULT_NUMBER_LOCALE): string {
  return `${amount < 0 ? '−' : ''}${amountFormat(locale).format(Math.abs(amount))} ${currencySymbol(currency)}`;
}

/** «II кв. 2026» */
export function quarterLabel(quarter: QuarterDto): string {
  return `${['I', 'II', 'III', 'IV'][quarter.number - 1] ?? quarter.number} кв. ${quarter.year}`;
}

export const DUES_KIND_LABELS: Record<string, string> = {
  Contribution: 'Вкладка',
  Refund: 'Повернення',
  TransferToKurin: 'Передача курінному',
  TransferToStanytsia: 'Передача в станицю',
  TransferToKrai: 'Передача в край',
  Expense: 'Витрата',
  OtherIncome: 'Інший прихід',
  Exchange: 'Обмін готівка ↔ картка',
  Correction: 'Корекція',
};

/** Signed as the history shows it: in is plus, out is minus, an exchange bare. */
export function duesAmountLabel(
  entry: { kind: string; amount: number },
  currency: string = DEFAULT_CURRENCY,
  locale: string = DEFAULT_NUMBER_LOCALE,
): string {
  if (entry.kind === 'Exchange') return duesMoney(entry.amount, currency, locale);
  const signed = entry.kind === 'Correction' ? entry.amount : ['Contribution', 'OtherIncome'].includes(entry.kind) ? entry.amount : -entry.amount;
  return signed > 0 ? `+${duesMoney(signed, currency, locale)}` : duesMoney(signed, currency, locale);
}

// ── Memberships ──────────────────────────────────────────────────────────────────────────────────

/** «з 2020» while current, else «2018 — 2021». */
export function membershipPeriod(membership: MembershipDto): string {
  const year = (value: string | null | undefined) => {
    const date = value ? new Date(value) : null;
    return date && !Number.isNaN(date.getTime()) ? String(date.getUTCFullYear()) : '—';
  };
  const from = year(membership.joinedAtUtc);
  if (membership.isCurrent) return `з ${from}`;
  const to = year(membership.leftAtUtc);
  return from === to ? from : `${from} — ${to}`;
}

export const MEMBERSHIP_KIND_LABELS: Record<string, string> = { Youth: 'Юнацтво', Staff: 'Виховний склад' };

// ── Contacts ─────────────────────────────────────────────────────────────────────────────────────

export function phoneHref(phone: string | null | undefined): string | null {
  const digits = (phone ?? '').replaceAll(/[^\d+]/g, '');
  return digits.replaceAll('+', '').length >= 5 ? `tel:${digits}` : null;
}

export function emailHref(email: string | null | undefined): string | null {
  const address = (email ?? '').trim();
  return address.includes('@') ? `mailto:${address}` : null;
}

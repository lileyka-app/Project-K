import { compareHistories, dateOnly, fullName, roleName } from './kurin.labels';
import { LeadershipDto, LeadershipHistoryDto, MemberLookupDto, UpsertLeadershipRequest } from './kurin.models';

/*
 * The rules of the web's leadership form (`leadership.ts`, LEADERSHIP_ROLE_MAP) and of the
 * leadership panel's archive, for a kurin, a гурток and the КВ, pure so the page and the sheet stay
 * views. Also the Звʼязковий handover (web kv-panel transferManagerRole), which is the КВ's провід
 * written back with a new holder of that office.
 */

export type LeadershipKind = 'kurin' | 'group' | 'kv';

const COMMON_ROLES = ['Suddya', 'Pysar', 'Skarbnyk', 'Horunjiy', 'Gospodar', 'Hronikar'];

/** Each провід's offices in the web form's order. */
export const LEADERSHIP_ROLES: Record<LeadershipKind, readonly string[]> = {
  kv: ['Instruktor', 'Vykhovnyk', 'Zvyazkovyi'],
  kurin: ['Kurinnuy', ...COMMON_ROLES, 'OtherKurin'],
  group: ['Hurtkoviy', ...COMMON_ROLES, 'OtherGroup'],
};

/** The offices several people hold at once (web MULTI_MEMBER_ROLES); the rest take one person. */
const MULTI_ROLES = new Set(['Vykhovnyk', 'Instruktor']);

export function holdsSeveral(role: string): boolean {
  return MULTI_ROLES.has(role);
}

/** Office → who holds it now (empty when nobody does). */
export type Seats = Record<string, MemberLookupDto[]>;

/**
 * Who sits where now: every active holder of each office, the earliest first. An office meant for
 * one person that has two (data from before the form) shows both, as the web's rows do, until
 * someone is picked for it.
 */
export function seatsOf(kind: LeadershipKind, histories: LeadershipHistoryDto[] | null | undefined): Seats {
  const seats: Seats = Object.fromEntries(LEADERSHIP_ROLES[kind].map((role) => [role, []]));
  const active = (histories ?? []).filter((h) => !h.endDate && h.role in seats && h.member);
  active.sort((a, b) => (a.startDate ?? '').localeCompare(b.startDate ?? ''));
  for (const history of active) {
    if (!seats[history.role].some((m) => m.memberKey === history.member.memberKey)) seats[history.role].push(history.member);
  }
  return seats;
}

export function sameSeats(a: Seats, b: Seats): boolean {
  const keys = (seats: Seats, role: string) => (seats[role] ?? []).map((m) => m.memberKey).sort((x, y) => x.localeCompare(y)).join(',');
  const roles = new Set([...Object.keys(a), ...Object.keys(b)]);
  return [...roles].every((role) => keys(a, role) === keys(b, role));
}

/** What an office's row says: who holds it, or nothing while nobody does. */
export function holdersLine(members: MemberLookupDto[] | undefined): string {
  return members?.length ? members.map((m) => `${m.lastName} ${m.firstName}`).join(', ') : '';
}

const REQUEST_TYPE: Record<LeadershipKind, UpsertLeadershipRequest['type']> = { kurin: 'Kurin', group: 'Group', kv: 'KV' };

function lookup(member: MemberLookupDto): UpsertLeadershipRequest['leadershipHistories'][number]['member'] {
  return { memberKey: member.memberKey, firstName: member.firstName, lastName: member.lastName, middleName: member.middleName ?? null };
}

/**
 * The body of `POST leadership` / `PUT leadership/{key}`. Only the offices held now go: the server
 * closes whatever is missing from the list and opens whatever is new to it.
 */
export function leadershipRequest(kind: LeadershipKind, entityKey: string, seats: Seats, today: string): UpsertLeadershipRequest {
  return {
    type: REQUEST_TYPE[kind],
    entityKey,
    startDate: today,
    endDate: null,
    leadershipHistories: LEADERSHIP_ROLES[kind].flatMap((role) =>
      (seats[role] ?? []).map((member) => ({ role, startDate: today, endDate: null, member: lookup(member) })),
    ),
  };
}

// ── Архів (web leadership-panel with «Архів» on) ─────────────────────────────────────────────────

/** The offices held before, in the panel's order; with a query, those whose name or office match. */
export function pastOffices(histories: LeadershipHistoryDto[] | null | undefined, query = ''): LeadershipHistoryDto[] {
  const q = query.trim().toLowerCase();
  return (histories ?? [])
    .filter((h) => !!h.endDate && !!h.member)
    .filter((h) => !q || fullName(h.member).toLowerCase().includes(q) || roleName(h.role).toLowerCase().includes(q))
    .sort(compareHistories);
}

/** «01.09.24 – 31.08.25», or «з 01.09.24» while it lasts (web 'dd.MM.yy'). */
export function officePeriod(start: string | null | undefined, end: string | null | undefined): string {
  const short = (value: string | null | undefined) => {
    const date = dateOnly(value);
    if (!date) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${String(date.getFullYear()).slice(2)}`;
  };
  return end ? `${short(start)} – ${short(end)}` : `з ${short(start)}`;
}

// ── Передати роль Звʼязкового (web kv-panel) ─────────────────────────────────────────────────────

/**
 * The КВ's провід with the Звʼязковий office moved to `incoming`: everyone else seated there stays,
 * the outgoing Звʼязковий's office ends. With no КВ провід yet, a first one holding only them.
 */
export function handoverRequest(kv: LeadershipDto | null, kurinKey: string, incoming: MemberLookupDto, today: string): UpsertLeadershipRequest {
  const retained = (kv?.leadershipHistories ?? []).filter((h) => !h.endDate && h.member && h.role !== 'Zvyazkovyi');
  return {
    type: 'KV',
    entityKey: kurinKey,
    startDate: today,
    endDate: null,
    leadershipHistories: [
      ...retained.map((h) => ({ role: h.role, startDate: h.startDate, endDate: null, member: lookup(h.member) })),
      { role: 'Zvyazkovyi', startDate: today, endDate: null, member: lookup(incoming) },
    ],
  };
}

// ── Місце в КВ за гуртками (web kv-panel syncKvSeat) ────────────────────────────────────────────

/**
 * The КВ's провід with `member`'s seat in step with their гуртки: with none they sit as інструктор
 * and a впорядник seat ends; with some the mentor assignment makes them a впорядник, so the
 * інструктор seat ends. Null when the провід already says so.
 */
export function kvSeatRequest(
  kv: LeadershipDto | null,
  kurinKey: string,
  member: MemberLookupDto,
  hasGroups: boolean,
  today: string,
): UpsertLeadershipRequest | null {
  const active = (kv?.leadershipHistories ?? []).filter((h) => !h.endDate && h.member);
  const theirs = (h: LeadershipHistoryDto) => h.member.memberKey === member.memberKey;
  const outgoing = hasGroups ? 'Instruktor' : 'Vykhovnyk';
  const toEnd = active.filter((h) => theirs(h) && h.role === outgoing);
  const toSeat = !hasGroups && !active.some((h) => theirs(h) && h.role === 'Instruktor');
  if (!toSeat && toEnd.length === 0) return null;

  return {
    type: 'KV',
    entityKey: kurinKey,
    startDate: today,
    endDate: null,
    leadershipHistories: [
      ...active.filter((h) => !toEnd.includes(h)).map((h) => ({ role: h.role, startDate: h.startDate, endDate: null, member: lookup(h.member) })),
      ...(toSeat ? [{ role: 'Instruktor', startDate: today, endDate: null, member: lookup(member) }] : []),
    ],
  };
}

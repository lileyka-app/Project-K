import { LeadershipRole } from '../models/enums/leadership-role.enum';

export type LeadershipTypeName = 'Kurin' | 'Group' | 'KV';

export interface OfficeRef {
  type: LeadershipTypeName;
  role: LeadershipRole;
}

const LEADERSHIP_TYPES: ReadonlySet<LeadershipTypeName> = new Set<LeadershipTypeName>(['Kurin', 'Group', 'KV']);
const LEADERSHIP_ROLES = new Set<string>(Object.values(LeadershipRole));

/**
 * Parses a backend system-role name — `"{провід}.{офіс}"`, e.g. `"KV.Zvyazkovyi"` — into its parts.
 * Returns null for the baseline `Member`, for `Admin`, and for anything unrecognised.
 *
 * Members carry these names in `userRole`. Before the офіс model they held `"Manager"`/`"Mentor"`,
 * and several call sites still compared against those, so their branches silently stopped matching.
 */
export function parseOfficeRole(systemRole?: string | null): OfficeRef | null {
  const parts = (systemRole ?? '').split('.');
  if (parts.length !== 2) {
    return null;
  }

  const [type, role] = parts;
  if (!LEADERSHIP_TYPES.has(type as LeadershipTypeName) || !LEADERSHIP_ROLES.has(role)) {
    return null;
  }

  return { type: type as LeadershipTypeName, role: role as LeadershipRole };
}

/** Whether the system-role name denotes the given офіс, ignoring which провід it sits in. */
export function holdsOffice(systemRole: string | null | undefined, role: LeadershipRole): boolean {
  return parseOfficeRole(systemRole)?.role === role;
}

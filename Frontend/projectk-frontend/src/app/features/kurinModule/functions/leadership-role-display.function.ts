import { LeadershipRole } from '../models/enums/leadership-role.enum';
import { LeadershipHistoryDto } from '../models/requests/leadership/leadership.dto';
import { MemberLookupDto } from '../models/requests/member/member-lookup.dto';
import { ROLE_DISPLAY_NAMES } from '../models/role-display-name.model';

/** The severities the tag component understands. `warning` is not one of them. */
export type RoleSeverity = 'success' | 'secondary' | 'info' | 'warn' | 'danger' | 'contrast';

/**
 * The colour an office is shown in.
 *
 * The member list and the leadership panel each had their own version of this, and they disagreed:
 * Зв'язковий was red in one and blue in the other, Скарбник green in one and blue in the other, so
 * the same office changed colour depending on the screen. This is the union of both, which keeps
 * every distinction either of them made.
 */
export function leadershipRoleSeverity(history: LeadershipHistoryDto): RoleSeverity {
  return history.endDate ? 'secondary' : leadershipRoleSeverityForRole(history.role);
}

/** The same colour, for callers holding the office name alone rather than a history record. */
export function leadershipRoleSeverityForRole(role: LeadershipRole | string): RoleSeverity {
  switch (role as LeadershipRole) {
    case LeadershipRole.Kurinnuy:
    case LeadershipRole.Zvyazkovyi:
    case LeadershipRole.Hurtkoviy:
      return 'danger';
    case LeadershipRole.Suddya:
      return 'warn';
    case LeadershipRole.Skarbnyk:
      return 'success';
    default:
      return 'info';
  }
}

/**
 * Kurins' own names for their roles, by kurin, and the kurin the person acts in. Module state on
 * purpose: the name is read from templates, pipes and plain functions all over, and every one of them
 * must say «Скриптор» where the kurin calls its писар so. KurinService remembers the names of every
 * kurin it reads or saves; KurinFeaturesService says which kurin is current. Keyed by kurin, so an
 * administrator saving another kurin's settings changes nothing on the screens of the current one.
 */
const roleNamesByKurin = new Map<string, Partial<Record<string, string>>>();
let currentKurinKey: string | null = null;

/** Keeps a kurin's own role names as the server sent them; an empty map means the usual ones. */
export function rememberKurinRoleNames(kurinKey: string, names: Record<string, string> | null | undefined): void {
  roleNamesByKurin.set(kurinKey, { ...(names ?? {}) });
}

/** The kurin whose role names unscoped calls use: the one the person acts in now. */
export function actInKurin(kurinKey: string | null): void {
  currentKurinKey = kurinKey;
}

/**
 * The office's Ukrainian name — the kurin's own when it renamed it — falling back to the raw value
 * for anything unmapped. Without a kurin, the one the person acts in.
 */
export function leadershipRoleDisplayName(role: LeadershipRole | string, kurinKey: string | null = currentKurinKey): string {
  const own = kurinKey ? roleNamesByKurin.get(kurinKey)?.[role] : undefined;
  return own || ROLE_DISPLAY_NAMES[role as LeadershipRole] || String(role);
}

/** "Прізвище Ім'я По батькові", the way every list renders a member. */
export function memberDisplayName(member: MemberLookupDto): string {
  const middleName = member.middleName ? ` ${member.middleName}` : '';
  return `${member.lastName} ${member.firstName}${middleName}`;
}

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

/** The office's Ukrainian name, falling back to the raw value for anything unmapped. */
export function leadershipRoleDisplayName(role: LeadershipRole | string): string {
  return ROLE_DISPLAY_NAMES[role as LeadershipRole] || String(role);
}

/** "Прізвище Ім'я По батькові", the way every list renders a member. */
export function memberDisplayName(member: MemberLookupDto): string {
  const middleName = member.middleName ? ` ${member.middleName}` : '';
  return `${member.lastName} ${member.firstName}${middleName}`;
}

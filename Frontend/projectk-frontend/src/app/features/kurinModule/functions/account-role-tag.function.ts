import { KurinBranch, isSeniorBranch } from '../models/enums/kurin-branch.enum';
import { LeadershipRole } from '../models/enums/leadership-role.enum';
import { getLeadershipRoleSortWeight } from './leadership-role-order.function';
import { leadershipRoleDisplayName, leadershipRoleSeverityForRole, RoleSeverity } from './leadership-role-display.function';
import { parseOfficeRole } from './system-role.function';

export interface AccountRoleTag {
  label: string;
  severity: RoleSeverity;
}

export const GENERIC_MEMBER_TAG: AccountRoleTag = { label: 'Учасник', severity: 'info' };

/** A member with no office, in the kurin's own words: a УСП or УПС kurin has members (membershipKindLabel). */
export function memberTag(branch?: KurinBranch | null): AccountRoleTag {
  return isSeniorBranch(branch) ? { label: 'Член куреня', severity: 'info' } : GENERIC_MEMBER_TAG;
}

/**
 * The office to show when an account holds several. Ordered by the same weights the member list
 * sorts by, so "most senior" means one thing across the app.
 */
export function mostSeniorOffice(systemRoles: string[]): LeadershipRole | null {
  return systemRoles
    .map(parseOfficeRole)
    .filter((office): office is NonNullable<ReturnType<typeof parseOfficeRole>> => office !== null)
    .map(office => office.role)
    .sort((left, right) => getLeadershipRoleSortWeight(left) - getLeadershipRoleSortWeight(right))[0] ?? null;
}

/**
 * How an account is labelled in the account switcher: administrator, else its most senior office,
 * else `fallback`. The sidebar passes the tier it reads from the signed-in account's permissions.
 */
export function accountRoleTag(isAdmin: boolean, systemRoles: string[], fallback: AccountRoleTag = GENERIC_MEMBER_TAG): AccountRoleTag {
  if (isAdmin) {
    return { label: 'Адміністратор', severity: 'danger' };
  }

  const office = mostSeniorOffice(systemRoles);
  return office
    ? { label: leadershipRoleDisplayName(office), severity: leadershipRoleSeverityForRole(office) }
    : fallback;
}

/** What the role tag needs to know about the signed-in account beyond its offices. */
export interface RoleTier {
  isAdmin(): boolean;
  canManageWholeKurin(): boolean;
  canLeadGroups(): boolean;
}

/**
 * The signed-in account's tag, as the sidebar footer and the account page show it: its office first,
 * else the tier its permissions give it ("Провід куреня", "Гуртковий провід"), else a member — named
 * as the membership names one in the kurin's `branch`.
 */
export function signedInRoleTag(tier: RoleTier, systemRoles: string[], branch?: KurinBranch | null): AccountRoleTag {
  let fallback = memberTag(branch);
  if (tier.canManageWholeKurin()) {
    fallback = { label: 'Провід куреня', severity: 'warn' };
  } else if (tier.canLeadGroups()) {
    fallback = { label: 'Гуртковий провід', severity: 'success' };
  }

  return accountRoleTag(tier.isAdmin(), systemRoles, fallback);
}

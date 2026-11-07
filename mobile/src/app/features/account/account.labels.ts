import { AuthState, KurinBranch, KurinScopeOption, MembershipKind, SavedAccount } from '../../auth/auth.models';
import { RoleTag, roleName, roleTone, roleWeight } from '../kurin/kurin.labels';
import { MyGroupDto } from './account.models';
import { compareKurinNumbers, isSeniorBranch, kurinBranchesName, kurinLabel as kurinNumberLabel } from '../../core/kurin-label';

/** The web's KURIN_BRANCH_LABELS and MEMBERSHIP_KIND_LABELS. */
export const BRANCH_LABELS: Record<KurinBranch, string> = { UPYu: 'УПЮ', USP: 'УСП', UPS: 'УПС' };
export const KIND_LABELS: Record<MembershipKind, string> = { Youth: 'Юнацтво', Staff: 'Виховний склад' };

/** The same in the kurin's own words (web membershipKindLabel): a УСП or УПС kurin has members, not юнацтво. */
export function kindLabel(kind: MembershipKind, branch?: KurinBranch | null): string {
  return kind === 'Youth' && isSeniorBranch(branch) ? 'Член куреня' : KIND_LABELS[kind];
}

/** «к. ч. 7», «підг. курінь», «5-й курінь УСП» */
export function kurinShortLabel(option: Pick<KurinScopeOption, 'kurinNumber'> & Partial<Pick<KurinScopeOption, 'branch'>>): string {
  return kurinNumberLabel(option.kurinNumber, option.branch);
}

/**
 * «к. ч. 7 ім. Сірого Лева», as the web's my-kurins tile names a kurin; a senior kurin by all its
 * гілки after its own name, «Орден Хрестоносців · 5-й курінь УСП · 20-й курінь УПС».
 */
export function kurinLabel(option: Pick<KurinScopeOption, 'kurinNumber' | 'namedAfter'> & Partial<Pick<KurinScopeOption, 'branch' | 'branches'>>): string {
  if (isSeniorBranch(option.branch)) {
    const numbers = option.branches?.length ? kurinBranchesName(option.branches) : kurinShortLabel(option);
    return option.namedAfter ? `${option.namedAfter} · ${numbers}` : numbers;
  }
  return option.namedAfter ? `${kurinShortLabel(option)} ім. ${option.namedAfter}` : kurinShortLabel(option);
}

/** The one acted in first, then by number (the web tile's order). */
export function sortKurins(options: KurinScopeOption[], currentKey: string | null): KurinScopeOption[] {
  return [...options].sort(
    (a, b) =>
      Number(b.kurinKey === currentKey) - Number(a.kurinKey === currentKey) || compareKurinNumbers(a.kurinNumber, b.kurinNumber),
  );
}

/**
 * How the account switcher tags another account's standing: administrator, else its most senior
 * office in that office's colour (as the web's account-role-tag does), else a member. `roles` are
 * system-role names such as «KV.Zvyazkovyi».
 */
export function accountRoleTag(account: Pick<SavedAccount, 'isAdmin' | 'roles'>): RoleTag {
  if (account.isAdmin) return { label: 'Адміністратор', tone: 'danger' };
  const offices = account.roles
    .map((role) => role.split('.'))
    .filter((parts) => parts.length === 2 && ['Kurin', 'Group', 'KV'].includes(parts[0]))
    .map((parts) => parts[1])
    .sort((a, b) => roleWeight(a) - roleWeight(b));
  return offices.length ? { label: roleName(offices[0]), tone: roleTone(offices[0]) } : { label: 'Учасник', tone: 'info' };
}

/** Leading one's own гурток is sitting in its провід; leading another's is being its впорядник. */
export function groupRole(group: Pick<MyGroupDto, 'isOwn' | 'isLed'>): string {
  if (group.isOwn) return group.isLed ? 'мій гурток · провід' : 'мій гурток';
  return 'впорядник';
}

/**
 * Admins and whole-kurin managers (Звʼязковий) must keep the second factor: they «Скинути» it,
 * everyone else may «Вимкнути». The web's account-settings decides the same way.
 */
export function isPrivileged(user: Pick<AuthState, 'isAdmin' | 'permissions'> | null): boolean {
  if (!user) return false;
  return user.isAdmin || user.permissions.some((permission) => permission.startsWith('Group:Manage:KurinWide'));
}

export interface PasswordRule {
  readonly id: string;
  readonly label: string;
  readonly test: (password: string) => boolean;
}

/** The web's PASSWORD_RULES (shared/functions/password-rules.function.ts). */
export const PASSWORD_RULES: readonly PasswordRule[] = [
  { id: 'length', label: 'щонайменше 8 символів', test: (password) => password.length >= 8 },
  { id: 'upper', label: 'велика латинська літера (A–Z)', test: (password) => /[A-Z]/.test(password) },
  { id: 'digit', label: 'цифра', test: (password) => /\d/.test(password) },
  { id: 'symbol', label: 'символ, як-от ! ? # або _', test: (password) => /[^A-Za-z0-9]/.test(password) },
];

export function passwordMeetsRules(password: string): boolean {
  return PASSWORD_RULES.every((rule) => rule.test(password));
}

/**
 * The code name to show beside a version, or null: none, or a local build's placeholder
 * (`LocalDevelopment`). The web's release-code-name.function.ts.
 */
export function displayCodeName(codeName: string | null | undefined): string | null {
  const value = codeName?.trim();
  return !value || /development/i.test(value) ? null : value;
}

/** «v1.2.0 «Ant»» or just the version. */
export function versionLabel(version: string, codeName: string | null | undefined): string {
  const code = displayCodeName(codeName);
  return code ? `${version} «${code}»` : version;
}

/** `/health` sits beside `/api`, not under it. */
export function healthUrl(api: string): string {
  const trimmed = api.endsWith('/') ? api.slice(0, -1) : api;
  return trimmed.endsWith('/api') ? `${trimmed.slice(0, -4)}/health` : `${trimmed}/health`;
}

/** The size a picture is drawn at so its longer side is at most `max`; never enlarged. */
export function fitWithin(width: number, height: number, max: number): { width: number; height: number } {
  const longer = Math.max(width, height);
  if (longer <= max || longer === 0) return { width, height };
  const scale = max / longer;
  return { width: Math.round(width * scale), height: Math.round(height * scale) };
}

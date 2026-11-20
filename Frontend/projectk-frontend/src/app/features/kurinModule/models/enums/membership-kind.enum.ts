import { KurinBranch, isSeniorBranch } from './kurin-branch.enum';

/** Чим людина є в цьому курені: юнацтвом чи виховним складом. */
export enum MembershipKind {
  Youth = 'Youth',
  Staff = 'Staff'
}

export const MEMBERSHIP_KIND_LABELS: Record<MembershipKind, string> = {
  [MembershipKind.Youth]: 'Юнацтво',
  [MembershipKind.Staff]: 'Виховний склад'
};

/** The same in the kurin's own words: a УСП or УПС kurin has members, not юнацтво. */
export function membershipKindLabel(kind: MembershipKind, branch?: KurinBranch | null): string {
  return kind === MembershipKind.Youth && isSeniorBranch(branch) ? 'Член куреня' : MEMBERSHIP_KIND_LABELS[kind];
}

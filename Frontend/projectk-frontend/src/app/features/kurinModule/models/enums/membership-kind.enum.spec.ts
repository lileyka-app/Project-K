import { KurinBranch } from './kurin-branch.enum';
import { MembershipKind, membershipKindLabel } from './membership-kind.enum';

describe('membershipKindLabel', () => {
  it('calls a senior kurin\'s members members, not юнацтво', () => {
    expect(membershipKindLabel(MembershipKind.Youth, KurinBranch.USP)).toBe('Член куреня');
    expect(membershipKindLabel(MembershipKind.Youth, KurinBranch.UPYu)).toBe('Юнацтво');
    expect(membershipKindLabel(MembershipKind.Staff, KurinBranch.UPS)).toBe('Виховний склад');
  });
});

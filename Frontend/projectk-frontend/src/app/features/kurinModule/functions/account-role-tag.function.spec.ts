import { KurinBranch } from '../models/enums/kurin-branch.enum';
import { signedInRoleTag } from './account-role-tag.function';

describe('signedInRoleTag', () => {
  const plain = { isAdmin: () => false, canManageWholeKurin: () => false, canLeadGroups: () => false };

  it('calls a member with no office what the membership calls them in the kurin', () => {
    expect(signedInRoleTag(plain, [], KurinBranch.UPYu).label).toBe('Учасник');
    expect(signedInRoleTag(plain, [], KurinBranch.USP).label).toBe('Член куреня');
    expect(signedInRoleTag(plain, [], KurinBranch.UPS).label).toBe('Член куреня');
    expect(signedInRoleTag(plain, []).label).toBe('Учасник');
  });

  it('keeps the tier above the member word in a senior kurin', () => {
    expect(signedInRoleTag({ ...plain, canManageWholeKurin: () => true }, [], KurinBranch.USP).label).toBe('Провід куреня');
  });
});

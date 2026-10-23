import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { PermissionService } from './permission.service';
import { AuthService } from '../auth-service/auth.service';
import { AuthState } from '../../models/auth-state.model';

describe('PermissionService', () => {
  let service: PermissionService;
  let authService: MockedObject<AuthService>;

  function setState(permissions: string[], isAdmin = false): void {
    const state: AuthState = {
      userKey: 'user-1',
      memberKey: 'member-1',
      email: 'user@example.com',
      isAdmin,
      permissions,
      roles: [],
      kurinKey: 'kurin-1',
      accessToken: 'token'
    };
    authService.getAuthStateValue.mockReturnValue(state);
  }

  const stewardPerms = [
    'Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Member:Manage:KurinWide',
    'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'
  ];
  const leadPerms = [
    'Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Member:Manage:KurinWide',
    'PlanningSession:Manage:KurinWide'
  ];
  const vykhovnykPerms = ['Group:Update:OwnGroups', 'Member:Update:OwnGroups', 'AgendaItem:Create:KurinWide', 'PlanningSession:Create:OwnGroups'];
  const providPerms = ['AgendaItem:Create:KurinWide', 'PlanningSession:Create:KurinWide'];
  const kurinnyyPerms = [...providPerms, 'Leadership:Update:KurinWide'];
  const memberPerms = ['Group:Read:KurinWide', 'Member:Read:KurinWide'];

  beforeEach(() => {
    authService = {
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    authService.getAuthStateValue.mockReturnValue(null);

    TestBed.configureTestingModule({
      providers: [
        PermissionService,
        { provide: AuthService, useValue: authService }
      ]
    });

    service = TestBed.inject(PermissionService);
  });

  // Курінну касу бачать лише ті, кому вона дана грантом: скарбник куреня і Звʼязковий.
  it('shows the kurin box to its keepers only', () => {
    setState([...kurinnyyPerms, 'KurinDues:Read:KurinWide', 'KurinDues:Update:KurinWide']);
    expect(service.canSeeKurinDues()).toBe(true);

    setState([...memberPerms, 'GroupDues:Read:Own']);
    expect(service.canSeeKurinDues()).toBe(false);

    setState(vykhovnykPerms);
    expect(service.canSeeKurinDues()).toBe(false);
  });

  // A суддя scores; a youth with only their own points does not, and only the kurin's суддя sets the rules.
  it('tells who scores and who sets the rules of точкування', () => {
    setState([...memberPerms, 'GroupScore:Create:OwnGroups', 'GroupScore:Read:OwnGroups']);
    expect(service.canScore()).toBe(true);
    expect(service.canManageScore()).toBe(false);

    setState([...kurinnyyPerms, 'GroupScore:Create:KurinWide', 'KurinScore:Manage:KurinWide']);
    expect(service.canManageScore()).toBe(true);

    setState([...memberPerms, 'GroupScore:Read:Own']);
    expect(service.canScore()).toBe(false);
  });

  it('shows a гурток box to its keepers, not to a youth with only their own balance', () => {
    setState([...vykhovnykPerms, 'GroupDues:Read:OwnGroups']);
    expect(service.canSeeGroupDues()).toBe(true);

    setState([...memberPerms, 'GroupDues:Read:Own']);
    expect(service.canSeeGroupDues()).toBe(false);
  });

  it('treats a Зв\'язковий (steward) as a whole-kurin manager', () => {
    setState(stewardPerms);
    expect(service.canManageWholeKurin()).toBe(true);
    expect(service.canManageGroups()).toBe(true);
    expect(service.canManageKurinSettings()).toBe(true);
    expect(service.canSetupLeadership('kv')).toBe(true);
    expect(service.canSetupLeadership('group')).toBe(true);
  });

  it('treats a Курінний (lead) as a manager without kurin settings or office assignment', () => {
    setState(leadPerms);
    expect(service.canManageWholeKurin()).toBe(true);
    expect(service.canManageGroups()).toBe(true);
    expect(service.canManageKurinSettings()).toBe(false);
    expect(service.canSetupLeadership('kurin')).toBe(false);
  });

  it('treats a Виховник as a group leader but not a whole-kurin manager', () => {
    setState(vykhovnykPerms);
    expect(service.canManageWholeKurin()).toBe(false);
    expect(service.canLeadGroups()).toBe(true);
    expect(service.isReviewer()).toBe(true);
    expect(service.canManageAgenda()).toBe(true);
    expect(service.canManageKurinSettings()).toBe(false);
  });

  it('lets a провід office raise agenda and planning without touching groups', () => {
    setState(providPerms);
    expect(service.canManageWholeKurin()).toBe(false);
    expect(service.canLeadGroups()).toBe(false);
    expect(service.canManageGroups()).toBe(false);
    expect(service.canManageAgenda()).toBe(true);
    expect(service.canCreatePlanning()).toBe(true);
    expect(service.canSetupLeadership('group')).toBe(false);
  });

  it('lets a Курінний seat offices but not moderate members', () => {
    setState(kurinnyyPerms);
    expect(service.canSetupLeadership('kurin')).toBe(true);
    expect(service.canSetupLeadership('group')).toBe(false);
    expect(service.canSetupLeadership('kv')).toBe(false);
    expect(service.canLeadGroups()).toBe(false);
    expect(service.canManageMembers()).toBe(false);
  });

  it('treats a bare member as read-only', () => {
    setState(memberPerms);
    expect(service.isAdmin()).toBe(false);
    expect(service.canManageWholeKurin()).toBe(false);
    expect(service.canLeadGroups()).toBe(false);
    expect(service.isReviewer()).toBe(false);
    expect(service.canManageAgenda()).toBe(false);
    expect(service.canManageKurinSettings()).toBe(false);
  });

  it('treats an admin as able to do everything', () => {
    setState([], true);
    expect(service.isAdmin()).toBe(true);
    expect(service.canManageWholeKurin()).toBe(true);
    expect(service.canManageKurinSettings()).toBe(true);
    expect(service.canSetupLeadership('kv')).toBe(true);
  });

  // A Гуртковий seats his own гурток's провід and nothing above it: the kurin провід form is not his.
  it('lets a Гуртковий seat only his group провід', () => {
    setState([...providPerms, 'Leadership:Update:OwnGroups']);
    expect(service.canSetupLeadership('group')).toBe(true);
    expect(service.canSetupLeadership('kurin')).toBe(false);
    expect(service.canSetupLeadership('kv')).toBe(false);
  });

  it('defaults to no access when there is no auth state', () => {
    authService.getAuthStateValue.mockReturnValue(null);
    expect(service.isAdmin()).toBe(false);
    expect(service.canManageWholeKurin()).toBe(false);
    expect(service.canManageAgenda()).toBe(false);
  });
});

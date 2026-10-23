import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthState } from '../models/auth-state.model';
import { AuthService } from '../services/auth-service/auth.service';
import { PermissionService } from '../services/permission-service/permission.service';
import { kurinAccessGuard } from './kurin.guard';

describe('kurinAccessGuard', () => {
  let authService: MockedObject<AuthService>;
  let permissionService: MockedObject<PermissionService>;
  let router: MockedObject<Router>;

  beforeEach(() => {
    authService = {
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    permissionService = {
      isAdmin: vi.fn()
    } as MockedObject<PermissionService>;
    router = {
      createUrlTree: vi.fn()
    } as MockedObject<Router>;

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: PermissionService, useValue: permissionService },
        { provide: Router, useValue: router }
      ]
    });
  });

  it('redirects panel route to active kurin even for admin', () => {
    const tree = { target: '/kurin' };
    authService.getAuthStateValue.mockReturnValue(createAuthState({ isAdmin: true, permissions: [], roles: ['Admin'], kurinKey: 'kurin-123' }));
    permissionService.isAdmin.mockReturnValue(true);
    router.createUrlTree.mockReturnValue(tree as never);

    const result = runGuard('panel');

    expect(router.createUrlTree).toHaveBeenCalledWith(['/kurin']);
    expect(result).toBe(tree as never);
  });

  it('allows panel route when admin has no active kurin', () => {
    authService.getAuthStateValue.mockReturnValue(createAuthState({ isAdmin: true, permissions: [], roles: ['Admin'], kurinKey: null }));
    permissionService.isAdmin.mockReturnValue(true);

    const result = runGuard('panel');

    expect(result).toBe(true);
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('redirects kurin route to panel for admin without active kurin', () => {
    const tree = { target: '/panel' };
    authService.getAuthStateValue.mockReturnValue(createAuthState({ isAdmin: true, permissions: [], roles: ['Admin'], kurinKey: null }));
    permissionService.isAdmin.mockReturnValue(true);
    router.createUrlTree.mockReturnValue(tree as never);

    const result = runGuard('kurin');

    expect(router.createUrlTree).toHaveBeenCalledWith(['/panel']);
    expect(result).toBe(tree as never);
  });

  function runGuard(resource: string): unknown {
    return TestBed.runInInjectionContext(() => kurinAccessGuard(resource)({} as never, {} as never));
  }

  function createAuthState(overrides: Partial<AuthState> = {}): AuthState {
    return {
      userKey: 'user-123',
      memberKey: 'member-123',
      email: 'test@example.com',
      isAdmin: false, permissions: ['Group:Manage:KurinWide', 'Group:Update:KurinWide', 'Kurin:Update:KurinWide', 'Leadership:Manage:KurinWide', 'PlanningSession:Manage:KurinWide'], roles: ['KV.Zvyazkovyi'],
      kurinKey: 'kurin-123',
      accessToken: 'token-123',
      ...overrides
    };
  }
});

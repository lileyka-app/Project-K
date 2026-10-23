import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable, of } from 'rxjs';
import { AuthService } from '../services/auth-service/auth.service';
import { capabilityGuard } from './capability.guard';

describe('capabilityGuard', () => {
  let authService: MockedObject<AuthService>;
  let router: MockedObject<Router>;
  let forbiddenTree: UrlTree;

  beforeEach(() => {
    forbiddenTree = {} as UrlTree;
    authService = {
      getAuthState: vi.fn(),
      getAuthStateValue: vi.fn()
    } as MockedObject<AuthService>;
    router = {
      createUrlTree: vi.fn()
    } as MockedObject<Router>;
    router.createUrlTree.mockReturnValue(forbiddenTree);

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    });
  });

  function runGuard(capabilities: Parameters<typeof capabilityGuard>): Observable<boolean | UrlTree> {
    return capabilityGuard(...capabilities)(
      {} as ActivatedRouteSnapshot,
      {} as RouterStateSnapshot
    ) as Observable<boolean | UrlTree>;
  }

  it('allows any configured role', () => new Promise<void>(done => {
    authService.getAuthState.mockReturnValue(of({
      userKey: 'user-1',
      memberKey: null,
      email: 'admin@example.com',
      isAdmin: true, permissions: [], roles: ['Admin'],
      kurinKey: 'kurin-1',
      accessToken: 'token'
    }));
    authService.getAuthStateValue.mockReturnValue({ userKey: 'user-1', memberKey: null, email: 'admin@example.com', isAdmin: true, permissions: [], roles: ['Admin'], kurinKey: 'kurin-1', accessToken: 'token' } as never);

    TestBed.runInInjectionContext(() => {
      const result$ = runGuard(['admin', 'kurinManagement']);

      result$.subscribe(result => {
        expect(result).toBe(true);
        done();
      });
    });
  }));

  it('redirects when the current role is not configured', () => new Promise<void>(done => {
    authService.getAuthState.mockReturnValue(of({
      userKey: 'user-1',
      memberKey: null,
      email: 'mentor@example.com',
      isAdmin: false, permissions: ['Group:Update:OwnGroups'], roles: ['Group.Hurtkoviy'],
      kurinKey: 'kurin-1',
      accessToken: 'token'
    }));
    authService.getAuthStateValue.mockReturnValue({ userKey: 'user-1', memberKey: null, email: 'mentor@example.com', isAdmin: false, permissions: ['Group:Update:OwnGroups'], roles: ['Group.Hurtkoviy'], kurinKey: 'kurin-1', accessToken: 'token' } as never);

    TestBed.runInInjectionContext(() => {
      const result$ = runGuard(['admin', 'kurinManagement']);

      result$.subscribe(result => {
        expect(result).toBe(forbiddenTree);
        expect(router.createUrlTree).toHaveBeenCalledWith(['/forbidden']);
        done();
      });
    });
  }));
});

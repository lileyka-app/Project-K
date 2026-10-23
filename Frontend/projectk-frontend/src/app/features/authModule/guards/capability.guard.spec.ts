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

  function stateWith(permissions: string[]) {
    return { userKey: 'user-1', memberKey: null, email: 'kv@example.com', isAdmin: false, permissions, roles: ['KV.Instruktor'], kurinKey: 'kurin-1', accessToken: 'token' } as never;
  }

  it('lets the journal in whoever the kurin gives KurinJournal:Read', () => new Promise<void>(done => {
    authService.getAuthState.mockReturnValue(of(stateWith(['KurinJournal:Read:KurinWide'])));
    authService.getAuthStateValue.mockReturnValue(stateWith(['KurinJournal:Read:KurinWide']));

    TestBed.runInInjectionContext(() => {
      runGuard(['journal']).subscribe(result => {
        expect(result).toBe(true);
        done();
      });
    });
  }));

  it('keeps out of the journal a leader the kurin has not given it to', () => new Promise<void>(done => {
    authService.getAuthState.mockReturnValue(of(stateWith(['Group:Update:OwnGroups'])));
    authService.getAuthStateValue.mockReturnValue(stateWith(['Group:Update:OwnGroups']));

    TestBed.runInInjectionContext(() => {
      runGuard(['journal']).subscribe(result => {
        expect(result).toBe(forbiddenTree);
        done();
      });
    });
  }));
});

import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { ActivatedRouteSnapshot, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { EntityGuard } from './entity.guard';
import { EntityService } from '../services/entity-service/entity.service';

describe('EntityGuard', () => {
  let guard: EntityGuard;
  let entityService: MockedObject<EntityService>;
  let router: MockedObject<Router>;

  function routeFor(entityType: string | null, entityKey: string | null): ActivatedRouteSnapshot {
    return {
      data: entityType ? { entityType } : {},
      paramMap: convertToParamMap(entityKey && entityType ? { [`${entityType}Key`]: entityKey } : {})
    } as unknown as ActivatedRouteSnapshot;
  }

  beforeEach(() => {
    entityService = {
      checkEntityAccess: vi.fn()
    } as MockedObject<EntityService>;
    router = {
      navigate: vi.fn()
    } as MockedObject<Router>;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        { provide: EntityService, useValue: entityService },
        { provide: Router, useValue: router }
      ]
    });

    guard = TestBed.inject(EntityGuard);
  });

  it('allows navigation when the route carries no entity to check', () => new Promise<void>(done => {
    guard.canActivate(routeFor(null, null)).subscribe(allowed => {
      expect(allowed).toBe(true);
      expect(entityService.checkEntityAccess).not.toHaveBeenCalled();
      done();
    });
  }));

  it('allows navigation when access is granted', () => new Promise<void>(done => {
    entityService.checkEntityAccess.mockReturnValue(of(true));

    guard.canActivate(routeFor('member', 'member-1')).subscribe(allowed => {
      expect(allowed).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
      done();
    });
  }));

  it('redirects to forbidden when access is denied', () => new Promise<void>(done => {
    entityService.checkEntityAccess.mockReturnValue(of(false));

    guard.canActivate(routeFor('member', 'member-1')).subscribe(allowed => {
      expect(allowed).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(['/forbidden']);
      done();
    });
  }));

  it('redirects to forbidden on a cross-kurin 403', () => new Promise<void>(done => {
    entityService.checkEntityAccess.mockReturnValue(throwError(() =>
      new HttpErrorResponse({ status: 403, error: { message: 'Resource belongs to a different kurin scope.' } })));

    guard.canActivate(routeFor('member', 'member-1')).subscribe(allowed => {
      expect(allowed).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(['/forbidden']);
      done();
    });
  }));

  it('lets an MFA challenge through so its dialog can appear', () => new Promise<void>(done => {
    entityService.checkEntityAccess.mockReturnValue(throwError(() =>
      new HttpErrorResponse({ status: 403, error: { message: 'MFA is required for this operation.' } })));

    guard.canActivate(routeFor('member', 'member-1')).subscribe(allowed => {
      expect(allowed).toBe(true);
      expect(router.navigate).not.toHaveBeenCalled();
      done();
    });
  }));

  it('lets navigation through when the server was never reached', () => new Promise<void>(done => {
    entityService.checkEntityAccess.mockReturnValue(throwError(() =>
      new HttpErrorResponse({ status: 0 })));

    guard.canActivate(routeFor('member', 'member-1')).subscribe(allowed => {
      expect(allowed).toBe(true);
      done();
    });
  }));

  it('blocks navigation when the server answers with an error it cannot read', () => new Promise<void>(done => {
    entityService.checkEntityAccess.mockReturnValue(throwError(() =>
      new HttpErrorResponse({ status: 500 })));

    guard.canActivate(routeFor('member', 'member-1')).subscribe(allowed => {
      expect(allowed).toBe(false);
      expect(router.navigate).toHaveBeenCalledWith(['/forbidden']);
      done();
    });
  }));
});

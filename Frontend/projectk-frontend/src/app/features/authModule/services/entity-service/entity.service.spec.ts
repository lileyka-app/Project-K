import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EntityService } from './entity.service';
import { AuthService } from '../auth-service/auth.service';
import { ClientCacheService } from '../../../kurinModule/services/client-cache/client-cache.service';
import { ACCESS_CACHE_PREFIX, KURIN_SCOPED_CACHE_PREFIXES } from '../../../kurinModule/services/client-cache/cache-policy';
import { environment } from '../../../../../environments/environment';
import { AuthState } from '../../models/auth-state.model';

describe('EntityService', () => {
  let service: EntityService;
  let httpMock: HttpTestingController;
  let cache: ClientCacheService;
  let state: AuthState | null;
  const url = `${environment.apiUrl}/auth/check-access`;

  const signedIn = (kurinKey: string, userKey = 'user-1'): AuthState => ({
    userKey,
    memberKey: null,
    email: 'a@b.c',
    isAdmin: false,
    permissions: [],
    roles: [],
    kurinKey,
    accessToken: 'token'
  });

  beforeEach(() => {
    state = signedIn('kurin-1');
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { getAuthStateValue: () => state } }
      ]
    });
    service = TestBed.inject(EntityService);
    httpMock = TestBed.inject(HttpTestingController);
    cache = TestBed.inject(ClientCacheService);
    vi.spyOn(console, 'debug').mockReturnValue(undefined);
  });

  afterEach(() => {
    httpMock.verify();
    cache.clear();
  });

  it('asks the server once for the same object and answer', () => {
    const answers: boolean[] = [];

    service.checkEntityAccess('group', 'g-1', 'Update').subscribe(a => answers.push(a));
    service.checkEntityAccess('group', 'g-1', 'Update').subscribe(a => answers.push(a));

    const req = httpMock.expectOne(url);
    expect(req.request.body).toEqual({ entityType: 'group', entityKey: 'g-1', action: 'Update' });
    req.flush(true);
    httpMock.expectNone(url);
    expect(answers).toEqual([true, true]);
  });

  it('asks again for another action on the same object', () => {
    service.checkEntityAccess('group', 'g-1', 'Update').subscribe();
    service.checkEntityAccess('group', 'g-1', 'Create').subscribe();

    expect(httpMock.match(url)).toHaveLength(2);
  });

  it('asks again from another kurin, and after the kurin scope is dropped', () => {
    const answers: boolean[] = [];
    service.checkEntityAccess('member', 'm-1', 'Update').subscribe(a => answers.push(a));
    httpMock.expectOne(url).flush(true);

    state = signedIn('kurin-2');
    service.checkEntityAccess('member', 'm-1', 'Update').subscribe(a => answers.push(a));
    httpMock.expectOne(url).flush(false);

    // What the auth service does on a scope switch.
    for (const prefix of KURIN_SCOPED_CACHE_PREFIXES) {
      cache.invalidateByPrefix(prefix);
    }
    service.checkEntityAccess('member', 'm-1', 'Update').subscribe(a => answers.push(a));
    httpMock.expectOne(url).flush(true);

    expect(answers).toEqual([true, false, true]);
  });

  it('keeps one person\'s answer from another person', () => {
    service.checkEntityAccess('member', 'm-1', 'Update').subscribe();
    httpMock.expectOne(url).flush(true);

    state = signedIn('kurin-1', 'user-2');
    let answer: boolean | undefined;
    service.checkEntityAccess('member', 'm-1', 'Update').subscribe(a => answer = a);
    httpMock.expectOne(url).flush(false);

    expect(answer).toBe(false);
  });

  it('does not keep a refused answer', () => {
    let failed = false;
    service.checkEntityAccess('member', 'm-1', 'Update').subscribe({ error: () => failed = true });
    httpMock.expectOne(url).flush('nope', { status: 500, statusText: 'Server Error' });

    let answer: boolean | undefined;
    service.checkEntityAccess('member', 'm-1', 'Update').subscribe(a => answer = a);
    httpMock.expectOne(url).flush(true);

    expect(failed).toBe(true);
    expect(answer).toBe(true);
  });

  it('is scoped to the kurin, so a scope switch forgets it', () => {
    expect(KURIN_SCOPED_CACHE_PREFIXES).toContain(ACCESS_CACHE_PREFIX);
  });
});

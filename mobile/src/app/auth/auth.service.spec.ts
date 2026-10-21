import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { apiUrl } from '../runtime-config';
import { AuthService, loginErrorText } from './auth.service';

const signedIn = {
  userKey: 'u1',
  memberKey: 'm1',
  email: 'yunak@example.com',
  isAdmin: false,
  permissions: [],
  roles: [],
  kurinKey: 'k1',
  requiresMfa: false,
  tokens: { accessToken: 'access-1' },
};

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;
  const api = apiUrl();

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: 'login', children: [] }])],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('keeps the session like the web does: token in memory, the rest in localStorage', async () => {
    const login = auth.login('yunak@example.com', 'secret');
    http.expectOne(`${api}/auth/login`).flush(signedIn);

    expect(await login).toEqual({ kind: 'signed-in' });
    expect(auth.accessToken()).toBe('access-1');
    const stored = JSON.parse(localStorage.getItem('authState') ?? '{}');
    expect(stored.email).toBe('yunak@example.com');
    expect(stored.accessToken).toBeNull();
  });

  it('asks for the second step when the account has MFA', async () => {
    const login = auth.login('lead@example.com', 'secret');
    http.expectOne(`${api}/auth/login`).flush({ ...signedIn, requiresMfa: true, mfaToken: 'mfa-1', tokens: null });

    expect(await login).toEqual({ kind: 'mfa', mfaToken: 'mfa-1' });
    expect(auth.signedIn()).toBe(false);

    const verify = auth.verifyMfa('lead@example.com', '123456', 'mfa-1');
    const request = http.expectOne(`${api}/auth/mfa/login-verify`);
    expect(request.request.body).toEqual({ email: 'lead@example.com', code: '123456', rememberMe: true, mfaToken: 'mfa-1' });
    request.flush(signedIn);
    await verify;
    expect(auth.signedIn()).toBe(true);
  });

  it('opens a stored session at once and renews the token from the cookie', async () => {
    localStorage.setItem('authState', JSON.stringify({ ...signedIn, accessToken: null }));
    const fresh = TestBed.runInInjectionContext(() => new AuthService());

    expect(fresh.ensureSession()).toBe(true);
    http.expectOne(`${api}/auth/refresh`).flush({ accessToken: 'access-2' });
    await settle();
    expect(fresh.accessToken()).toBe('access-2');
  });

  it('signs out when the server refuses the cookie', async () => {
    localStorage.setItem('authState', JSON.stringify({ ...signedIn, accessToken: null }));
    const fresh = TestBed.runInInjectionContext(() => new AuthService());

    expect(fresh.ensureSession()).toBe(true);
    http.expectOne(`${api}/auth/refresh`).flush({}, { status: 401, statusText: 'Unauthorized' });
    await settle();
    expect(fresh.signedIn()).toBe(false);
    expect(localStorage.getItem('authState')).toBeNull();
  });

  it('stays signed in while offline, also when the service worker answers 504', async () => {
    localStorage.setItem('authState', JSON.stringify({ ...signedIn, accessToken: null }));
    const fresh = TestBed.runInInjectionContext(() => new AuthService());

    expect(fresh.ensureSession()).toBe(true);
    http.expectOne(`${api}/auth/refresh`).error(new ProgressEvent('error'), { status: 0 });
    await settle();
    expect(fresh.signedIn()).toBe(true);

    fresh.ensureSession();
    http.expectOne(`${api}/auth/refresh`).flush('', { status: 504, statusText: 'Gateway Timeout' });
    await settle();
    expect(fresh.signedIn()).toBe(true);
    expect(localStorage.getItem('authState')).not.toBeNull();
  });
});

function settle(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve));
}

describe('loginErrorText', () => {
  it('tells a paused email to wait 15 minutes, not a minute', () => {
    const paused = new HttpErrorResponse({ status: 429, error: { error: 'TooManyAttempts' } });
    expect(loginErrorText(paused, '')).toContain('15 хвилин');
  });

  it('keeps the short wait for the address limiter', () => {
    const limited = new HttpErrorResponse({ status: 429, error: null });
    expect(loginErrorText(limited, '')).toBe('Забагато спроб. Спробуй за хвилину.');
  });
});

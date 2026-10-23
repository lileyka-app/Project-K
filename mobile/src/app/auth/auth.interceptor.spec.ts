import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { apiUrl } from '../runtime-config';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

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

describe('authInterceptor', () => {
  let auth: AuthService;
  let client: HttpClient;
  let http: HttpTestingController;
  const api = apiUrl();

  beforeEach(async () => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', children: [] }]),
      ],
    });
    auth = TestBed.inject(AuthService);
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);

    const login = auth.login('yunak@example.com', 'secret');
    http.expectOne(`${api}/auth/login`).flush(signedIn);
    await login;
  });

  afterEach(() => http.verify());

  it('renews the token in the background when the answer says it is stale, once for a burst', async () => {
    const first = firstValueFrom(client.get(`${api}/member/m1`));
    const second = firstValueFrom(client.get(`${api}/kurin/k1`));
    http.expectOne(`${api}/member/m1`).flush({ ok: 1 }, { headers: { 'X-Access-Stale': '1' } });
    http.expectOne(`${api}/kurin/k1`).flush({ ok: 2 }, { headers: { 'X-Access-Stale': '1' } });

    expect(await first).toEqual({ ok: 1 });
    expect(await second).toEqual({ ok: 2 });
    const refresh = http.expectOne(`${api}/auth/refresh`);
    refresh.flush({
      accessToken: 'access-2',
      access: { isAdmin: false, roles: ['Group.Hurtkoviy'], permissions: ['Group:Update:OwnGroups'], kurinKey: 'k1' },
    });
    await settle();

    expect(auth.accessToken()).toBe('access-2');
    expect(auth.user()?.permissions).toEqual(['Group:Update:OwnGroups']);
  });

  it('leaves the token alone when the answer does not say it is stale', async () => {
    const request = firstValueFrom(client.get(`${api}/member/m1`));
    http.expectOne(`${api}/member/m1`).flush({});
    await request;

    http.expectNone(`${api}/auth/refresh`);
  });
});

function settle(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve));
}

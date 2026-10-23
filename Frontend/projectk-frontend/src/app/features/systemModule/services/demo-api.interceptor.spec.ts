import type { MockedObject } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClient, HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { MessageService } from '@openng/optimus-ui/api';
import { firstValueFrom } from 'rxjs';
import { DemoApiInterceptor, DemoFixtureFile } from './demo-api.interceptor';

/** The fixture files the interceptor would fetch, served from memory. */
function fakeFetch(files: Record<string, DemoFixtureFile>) {
  return (input: string | URL | Request) => {
    const name = String(input).split('/').pop()!.replace('.json', '');
    const file = files[name];
    return Promise.resolve(file
      ? new Response(JSON.stringify(file), { status: 200, headers: { 'content-type': 'application/json' } })
      : new Response('', { status: 404 }));
  };
}

describe('DemoApiInterceptor', () => {
  let http: HttpClient;
  let messages: MockedObject<MessageService>;
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    sessionStorage.removeItem('lileyka-demo-seat');
    globalThis.fetch = fakeFetch({
      public: { seat: 'public', recordedAt: '', entries: [{ m: 'GET', p: '/api/auth/setup/status', s: 200, b: { isInitialized: true } }] },
      Youth: {
        seat: 'Youth', recordedAt: '', entries: [
          { m: 'POST', p: '/api/demo/login', s: 200, b: { userKey: 'u1', email: 'y@example', roles: ['Member'], kurinKey: 'k1', tokens: { accessToken: 't' } } },
          { m: 'GET', p: '/api/kurin/k1', s: 200, b: { name: '1 курінь' } },
          { m: 'GET', p: '/api/agenda/k1/calendar?from=a&to=b', s: 200, b: [{ title: 'Сходини' }] },
          {
            m: 'GET', p: '/api/kurin/k1/journal?skip=0&take=2', s: 200, b: {
              items: [
                { key: 'j3', occurredAtUtc: '2026-10-20T10:00:00Z', area: 'Award', actorUserKey: 'a1', subjectMemberKey: 'm1' },
                { key: 'j2', occurredAtUtc: '2026-10-10T10:00:00Z', area: 'Probe', actorUserKey: 'a2', subjectMemberKey: 'm2' }
              ],
              hasMore: true
            }
          },
          {
            m: 'GET', p: '/api/kurin/k1/journal?skip=2&take=2', s: 200, b: {
              items: [{ key: 'j1', occurredAtUtc: '2026-09-01T10:00:00Z', area: 'Probe', actorUserKey: 'a1', subjectMemberKey: 'm1' }],
              hasMore: false
            }
          }
        ]
      }
    }) as typeof fetch;
    messages = {
      add: vi.fn()
    } as MockedObject<MessageService>;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        { provide: MessageService, useValue: messages },
        { provide: HTTP_INTERCEPTORS, useClass: DemoApiInterceptor, multi: true }
      ]
    });
    http = TestBed.inject(HttpClient);
  });

  afterEach(() => { globalThis.fetch = originalFetch; });

  it('answers the health probe and the signed-out pages from the public fixture', async () => {
    const health = await firstValueFrom(http.get('https://demo.invalid/health', { responseType: 'text' }));
    expect(health).toContain('"status":"ready"');

    const setup = await firstValueFrom(http.get<{ isInitialized: boolean }>('https://demo.invalid/api/auth/setup/status'));
    expect(setup.isInitialized).toBe(true);
  });

  it('remembers the seat from the demo login and serves that seat’s recording', async () => {
    const login = await firstValueFrom(http.post<{ kurinKey: string }>('https://demo.invalid/api/demo/login', { seat: 'Youth' }));
    expect(login.kurinKey).toBe('k1');
    expect(sessionStorage.getItem('lileyka-demo-seat')).toBe('Youth');

    const kurin = await firstValueFrom(http.get<{ name: string }>('https://demo.invalid/api/kurin/k1'));
    expect(kurin.name).toBe('1 курінь');
  });

  // A query the recording did not see still finds the path-only entry; a page it never saw is a 404.
  it('falls back from query to path, and says so when a page was never recorded', async () => {
    await firstValueFrom(http.post('https://demo.invalid/api/demo/login', { seat: 'Youth' }));

    const events = await firstValueFrom(http.get<{ title: string }[]>('https://demo.invalid/api/agenda/k1/calendar?from=a&to=b'));
    expect(events[0].title).toBe('Сходини');

    await expect(firstValueFrom(http.get('https://demo.invalid/api/member/nobody'))).rejects.toEqual(expect.objectContaining({ status: 404, statusText: 'У демо цієї сторінки немає.' }));
  });

  it('answers any filter and page of the journal from the recorded pages', async () => {
    await firstValueFrom(http.post('https://demo.invalid/api/demo/login', { seat: 'Youth' }));
    type Page = { items: { key: string }[]; hasMore: boolean };
    const page = (query: string) => firstValueFrom(http.get<Page>(`https://demo.invalid/api/kurin/k1/journal?${query}`));

    const probes = await page('area=Probe&skip=0&take=20');
    expect(probes.items.map(i => i.key)).toEqual(['j2', 'j1']);
    expect(probes.hasMore).toBe(false);

    const byPerson = await page('memberKey=m1&actorUserKey=a1&skip=0&take=1');
    expect(byPerson.items.map(i => i.key)).toEqual(['j3']);
    expect(byPerson.hasMore).toBe(true);

    const october = await page('fromUtc=2026-10-01T00:00:00Z&toUtc=2026-10-15T00:00:00Z');
    expect(october.items.map(i => i.key)).toEqual(['j2']);
  });

  it('acknowledges a write it cannot keep and tells the visitor once', async () => {
    await firstValueFrom(http.post('https://demo.invalid/api/demo/login', { seat: 'Youth' }));

    await firstValueFrom(http.put('https://demo.invalid/api/member/m1', { firstName: 'X' }));
    await firstValueFrom(http.delete('https://demo.invalid/api/agenda/k1/items/i1'));

    expect(messages.add).toHaveBeenCalledTimes(1);
    expect(vi.mocked(messages.add).mock.lastCall![0].summary).toBe('Це демо');
  });
});

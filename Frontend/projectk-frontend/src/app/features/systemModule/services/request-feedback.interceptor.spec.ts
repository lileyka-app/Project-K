import { TestBed } from '@angular/core/testing';
import { HttpClient, HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MessageService, ToastMessageOptions } from '@openng/optimus-ui/api';
import { RequestFeedbackInterceptor } from './request-feedback.interceptor';
import { UserAction, UserActionService } from './user-action-service/user-action.service';
import { requestFeedback } from '../../../shared/functions/request-feedback.function';

describe('RequestFeedbackInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let messages: MessageService;
  let shown: ToastMessageOptions[];
  let armed: UserAction | null;
  let released: number;

  const userActions = {
    claim: () => armed ? { action: armed, release: () => released++ } : null,
    resume: (action: UserAction) => { armed = action; }
  };

  beforeEach(() => {
    vi.useFakeTimers();
    armed = null;
    released = 0;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
        MessageService,
        { provide: UserActionService, useValue: userActions },
        { provide: HTTP_INTERCEPTORS, useClass: RequestFeedbackInterceptor, multi: true }
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    messages = TestBed.inject(MessageService);
    shown = [];
    messages.messageObserver.subscribe(message => shown.push(message as ToastMessageOptions));
  });

  afterEach(() => {
    backend.verify();
    vi.useRealTimers();
  });

  function click(): void {
    armed = new UserAction(null);
  }

  it('says «Збережено» after a save the person clicked', () => {
    click();
    http.put('/api/member/1', {}).subscribe();
    armed = null;
    backend.expectOne('/api/member/1').flush({});
    vi.runAllTimers();

    expect(shown).toEqual([{ severity: 'success', summary: 'Збережено' }]);
    expect(released).toBe(1);
  });

  it('says «Видалено» after a delete', () => {
    click();
    http.delete('/api/member/1').subscribe();
    backend.expectOne('/api/member/1').flush({});
    vi.runAllTimers();

    expect(shown[0].summary).toBe('Видалено');
  });

  it('stays quiet about a save nobody clicked and about any successful read', () => {
    http.put('/api/layout', {}).subscribe();
    backend.expectOne('/api/layout').flush({});
    click();
    http.get('/api/member/1').subscribe();
    backend.expectOne('/api/member/1').flush({});
    vi.runAllTimers();

    expect(shown).toEqual([]);
  });

  it('lets the component speak first and then says nothing', () => {
    click();
    http.post('/api/member', {}).subscribe(() => messages.add({ severity: 'success', summary: 'Учасника додано' }));
    backend.expectOne('/api/member').flush({});
    vi.runAllTimers();

    expect(shown.map(message => message.summary)).toEqual(['Учасника додано']);
  });

  it('maps 400, 403, 409 and 500 to their own words', () => {
    const cases: [number, string][] = [
      [400, 'Не вдалося зберегти'],
      [403, 'Немає доступу'],
      [409, 'Не вдалося зберегти'],
      [500, 'Щось пішло не так']
    ];
    for (const [status, summary] of cases) {
      shown = [];
      click();
      http.post('/api/x', {}).subscribe({ error: () => undefined });
      backend.expectOne('/api/x').flush({ error: 'Code' }, { status, statusText: 'x' });
      vi.runAllTimers();
      expect(shown.length, String(status)).toBe(1);
      expect(shown[0].summary, String(status)).toBe(summary);
    }
  });

  it('reports a failed save even when nobody clicked, but not a failed background read', () => {
    http.put('/api/x', {}).subscribe({ error: () => undefined });
    backend.expectOne('/api/x').flush(null, { status: 500, statusText: 'x' });
    http.get('/api/y').subscribe({ error: () => undefined });
    backend.expectOne('/api/y').flush(null, { status: 500, statusText: 'x' });
    vi.runAllTimers();

    expect(shown.map(message => message.severity)).toEqual(['error']);
  });

  it('tells a rejected upload from a lost connection', () => {
    const form = new FormData();
    form.append('blob', new Blob(['x']));
    click();
    http.put('/api/member/1', form).subscribe({ error: () => undefined });
    backend.expectOne('/api/member/1').error(new ProgressEvent('error'));
    vi.runAllTimers();
    click();
    http.put('/api/member/2', {}).subscribe({ error: () => undefined });
    backend.expectOne('/api/member/2').error(new ProgressEvent('error'));
    vi.runAllTimers();

    expect(shown.map(message => message.summary)).toEqual(['Файл не надіслано', 'Сервер не відповів']);
  });

  it('leaves 401 to the auth interceptor', () => {
    click();
    http.post('/api/x', {}).subscribe({ error: () => undefined });
    backend.expectOne('/api/x').flush(null, { status: 401, statusText: 'x' });
    vi.runAllTimers();

    expect(shown).toEqual([]);
  });

  it('honours silent, errors-only and handled statuses', () => {
    click();
    http.post('/api/refresh', {}, { context: requestFeedback('silent') }).subscribe({ error: () => undefined });
    backend.expectOne('/api/refresh').flush(null, { status: 500, statusText: 'x' });

    click();
    http.post('/api/login', {}, { context: requestFeedback('errors') }).subscribe();
    backend.expectOne('/api/login').flush({});

    click();
    http.put('/api/sign', {}, { context: requestFeedback('auto', [409]) }).subscribe({ error: () => undefined });
    backend.expectOne('/api/sign').flush(null, { status: 409, statusText: 'x' });
    vi.runAllTimers();

    expect(shown).toEqual([]);
  });

  it('carries one action through the request its response starts, with one toast', () => {
    click();
    http.get('/api/leadership').subscribe(() => {
      http.put('/api/leadership', {}).subscribe(() => http.delete('/api/warning').subscribe());
    });
    backend.expectOne('/api/leadership').flush({});
    backend.expectOne(request => request.method === 'PUT').flush({});
    backend.expectOne('/api/warning').flush({});
    vi.runAllTimers();

    expect(shown).toEqual([{ severity: 'success', summary: 'Збережено' }]);
    expect(released).toBe(3);
  });

  it('does not repeat the same toast twice in a row', () => {
    for (let i = 0; i < 2; i++) {
      http.put('/api/x', {}).subscribe({ error: () => undefined });
      backend.expectOne('/api/x').flush(null, { status: 500, statusText: 'x' });
      vi.runAllTimers();
    }

    expect(shown).toHaveLength(1);
  });
});

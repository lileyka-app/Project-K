import {
  HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest, HttpResponse
} from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { MessageService } from '@openng/optimus-ui/api';
import { Observable, from, of, switchMap, throwError, timer } from 'rxjs';
import { environment } from '../../../../environments/environment';

/** One recorded exchange with the real API: method, path with query, status, body. */
export interface DemoFixtureEntry {
  m: string;
  p: string;
  s: number;
  b: unknown;
}

export interface DemoFixtureFile {
  seat: string;
  recordedAt: string;
  entries: DemoFixtureEntry[];
}

/** The fields of a journal entry the demo filters and orders by; the rest passes through as recorded. */
interface JournalItem {
  key: string;
  occurredAtUtc: string;
  area: string;
  actorUserKey: string | null;
  subjectMemberKey: string | null;
}

interface JournalPage {
  items: JournalItem[];
  hasMore: boolean;
}

const SEAT_KEY = 'lileyka-demo-seat';
const NOTICE_COOLDOWN_MS = 4000;

/**
 * The static demo's stand-in for the API. Answers every `/api/` and `/health` request from
 * fixtures that `scripts/record-demo-fixtures.mjs` captured on a real demo stack, one file per
 * seat, so the screens are exactly what the server would have shown. Changes the visitor makes are
 * acknowledged, not kept: the demo says so once, and a reload puts everything back.
 *
 * Registered only in the `demo` build (`environment.isStaticDemo`); everywhere else this class is
 * not in the provider list at all.
 */
@Injectable()
export class DemoApiInterceptor implements HttpInterceptor {
  private readonly messages = inject(MessageService);
  private readonly loaded = new Map<string, Promise<Map<string, DemoFixtureEntry>>>();
  private lastNotice = 0;

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const path = this.apiPath(req.urlWithParams);
    if (path === null) {
      return next.handle(req);
    }

    if (path === '/health') {
      return this.reply(200, `{"status":"ready","version":"${environment.version}","codeName":"${environment.codeName}"}`);
    }

    // Badge pictures are served by the API behind the token; the recorder saved every one the
    // catalogue names, and here they come back as the blob the app asked for.
    if (path.startsWith('/badges_images/')) {
      const file = path.slice('/badges_images/'.length).split('?')[0];
      return from(fetch(`${document.baseURI}assets/demo/badges_images/${file}`)).pipe(
        switchMap(r => r.ok ? from(r.blob()) : throwError(() => new HttpErrorResponse({ status: 404, statusText: 'no picture' }))),
        switchMap(blob => of(new HttpResponse({ status: 200, body: blob })))
      );
    }

    if (req.method === 'POST' && path === '/api/demo/login') {
      const seat = String((req.body as { seat?: string } | null)?.seat ?? 'Zvyazkovyi');
      sessionStorage.setItem(SEAT_KEY, seat);
      return from(this.fixtures(seat)).pipe(switchMap(entries => this.replyFrom(entries, 'POST', '/api/demo/login')));
    }

    if (req.method === 'POST' && path === '/api/auth/logout') {
      sessionStorage.removeItem(SEAT_KEY);
      return this.reply(200, null);
    }

    const seat = sessionStorage.getItem(SEAT_KEY);
    const source = seat ? this.fixtures(seat) : this.fixtures('public');
    return from(source).pipe(switchMap(entries => {
      const journal = req.method === 'GET' ? this.journalPage(entries, path) : null;
      if (journal) {
        return this.reply(200, journal);
      }

      const hit = entries.get(`${req.method} ${path}`) ?? entries.get(`${req.method} ${path.split('?')[0]}`);
      if (hit) {
        return this.reply(hit.s, hit.b);
      }

      if (req.method === 'GET') {
        // Loud on purpose: a miss means the recording has a hole, and the fix is to re-record.
        console.warn(`[demo] no fixture for GET ${path}`);
        return this.fail(404, 'У демо цієї сторінки немає.');
      }

      // A write the demo did not record: acknowledge it so the screen does not break, and say
      // once that nothing is kept.
      this.notice();
      return this.reply(200, {});
    }));
  }

  /** The request path (with query) when the URL targets the API, a badge picture or the health probe; null otherwise. */
  private apiPath(url: string): string | null {
    const marker = url.indexOf('/api/');
    if (marker >= 0) {
      return url.slice(marker);
    }
    const badge = url.indexOf('/badges_images/');
    if (badge >= 0) {
      return url.slice(badge);
    }
    if (url.endsWith('/health') || url.includes('/health?')) {
      return '/health';
    }
    return null;
  }

  /**
   * The journal is filtered by area, person, actor and period and paged — far more combinations
   * than a recording can hold. The recorder pages through the whole journal once; here every
   * recorded page of it is merged and the filters and paging are applied the way the API applies
   * them. Null when the request is not the journal or the seat has no recording of it.
   */
  private journalPage(entries: Map<string, DemoFixtureEntry>, path: string): JournalPage | null {
    const [base, search = ''] = path.split('?');
    if (!/^\/api\/kurin\/[^/]+\/journal$/.test(base)) {
      return null;
    }

    const recorded = new Map<string, JournalItem>();
    for (const entry of entries.values()) {
      if (entry.m === 'GET' && entry.s === 200 && entry.p.split('?')[0] === base) {
        for (const item of (entry.b as JournalPage | null)?.items ?? []) {
          recorded.set(item.key, item);
        }
      }
    }
    if (recorded.size === 0) {
      return null;
    }

    const query = new URLSearchParams(search);
    const area = query.get('area');
    const memberKey = query.get('memberKey');
    const actorUserKey = query.get('actorUserKey');
    const fromUtc = query.get('fromUtc');
    const toUtc = query.get('toUtc');
    const skip = Number(query.get('skip') ?? 0);
    const take = Number(query.get('take') ?? 20);

    const items = [...recorded.values()]
      .filter(item =>
        (!area || item.area === area)
        && (!memberKey || item.subjectMemberKey === memberKey)
        && (!actorUserKey || item.actorUserKey === actorUserKey)
        && (!fromUtc || item.occurredAtUtc >= fromUtc)
        && (!toUtc || item.occurredAtUtc < toUtc))
      .sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc));

    return { items: items.slice(skip, skip + take), hasMore: skip + take < items.length };
  }

  private fixtures(name: string): Promise<Map<string, DemoFixtureEntry>> {
    let pending = this.loaded.get(name);
    if (!pending) {
      pending = fetch(`${document.baseURI}assets/demo/${name}.json`)
        .then(r => r.ok ? (r.json() as Promise<DemoFixtureFile>) : Promise.reject(new Error(`no fixtures for ${name}`)))
        .then(file => new Map(file.entries.map(e => [`${e.m} ${e.p}`, e])));
      this.loaded.set(name, pending);
    }
    return pending;
  }

  private replyFrom(entries: Map<string, DemoFixtureEntry>, method: string, path: string): Observable<HttpEvent<unknown>> {
    const hit = entries.get(`${method} ${path}`);
    return hit ? this.reply(hit.s, hit.b) : this.fail(404, 'У демо цього немає.');
  }

  private reply(status: number, body: unknown): Observable<HttpEvent<unknown>> {
    if (status >= 400) {
      return this.fail(status, typeof body === 'string' ? body : (body as { message?: string } | null)?.message ?? 'Помилка');
    }
    // A short delay keeps loaders and transitions honest: the real API is not instant either.
    return timer(60).pipe(switchMap(() => of(new HttpResponse({ status, body: body ?? null }))));
  }

  private fail(status: number, message: string): Observable<never> {
    return timer(60).pipe(switchMap(() => throwError(() => new HttpErrorResponse({ status, statusText: message, error: { message } }))));
  }

  private notice(): void {
    const now = Date.now();
    if (now - this.lastNotice < NOTICE_COOLDOWN_MS) {
      return;
    }
    this.lastNotice = now;
    this.messages.add({ severity: 'info', summary: 'Це демо', detail: 'Зміни не зберігаються: після перезавантаження курінь знову як був.', life: 5000 });
  }
}

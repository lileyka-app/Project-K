import { HttpBackend, HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, Injectable, OnDestroy, inject, signal } from '@angular/core';
import { IonSpinner } from '@ionic/angular';
import { firstValueFrom, timeout } from 'rxjs';
import { apiUrl } from '../../runtime-config';
import { healthUrl } from './account.labels';

/** What one ping of `/health` said. */
export type Ping = 'up' | 'waking' | 'unreachable';

/**
 * Reads one answer of `/health`. A server that is waking up holds the request or answers 502–504
 * (the web's health-banner.service); no answer at all (offline, a refused connection) is not a
 * server starting, so it shows nothing.
 */
export function pingResult(error: unknown): Ping {
  if (!error) return 'up';
  if (error instanceof HttpErrorResponse) {
    if ([502, 503, 504].includes(error.status)) return 'waking';
    return error.status === 0 ? 'unreachable' : 'up';
  }
  // The timeout: the request is still waiting on the server.
  return 'waking';
}

/**
 * The cold-start check of the web (health-banner.service): ping the API's `/health` once at start;
 * while it does not answer in time, show a notice and ping again with a backoff until it does.
 * The web runs it on the free hosting tier only; the PWA can be pointed at any server by /env.js,
 * so it always pings, and a warm server answers long before the notice would show.
 */
@Injectable({ providedIn: 'root' })
export class ColdStartService implements OnDestroy {
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly backoffMs = [3000, 5000, 10000];
  private timer: ReturnType<typeof setTimeout> | null = null;
  private started = false;

  readonly waking = signal(false);

  start(): void {
    if (this.started) return;
    this.started = true;
    if (globalThis.navigator?.onLine === false) return;
    void this.check(0);
  }

  ngOnDestroy(): void {
    if (this.timer) clearTimeout(this.timer);
  }

  private async check(attempt: number): Promise<void> {
    const result = await this.ping();
    if (result !== 'waking') {
      this.waking.set(false);
      return;
    }
    this.waking.set(true);
    const delay = this.backoffMs[Math.min(attempt, this.backoffMs.length - 1)];
    this.timer = setTimeout(() => void this.check(attempt + 1), delay);
  }

  private async ping(): Promise<Ping> {
    try {
      await firstValueFrom(
        this.http.get(healthUrl(apiUrl()), { responseType: 'text' }).pipe(timeout({ first: 2000 })),
      );
      return 'up';
    } catch (error) {
      return pingResult(error);
    }
  }
}

/** A slim notice at the top of the app while the server wakes up; it goes as soon as it answers. */
@Component({
  selector: 'app-cold-start-banner',
  imports: [IonSpinner],
  styles: `
    :host {
      position: fixed;
      z-index: 30000;
      top: calc(env(safe-area-inset-top) + 8px);
      left: 16px;
      right: 16px;
      display: flex;
      justify-content: center;
      pointer-events: none;
    }
    output {
      display: flex;
      align-items: center;
      gap: 10px;
      max-width: 420px;
      padding: 8px 14px;
      border-radius: 18px;
      background: var(--lk-accent-50);
      color: var(--lk-accent-700);
      border: 1px solid color-mix(in srgb, var(--lk-accent-700) 20%, transparent);
      box-shadow: 0 4px 16px rgb(0 0 0 / 12%);
      font-size: 13px;
      line-height: 17px;
      pointer-events: auto;
    }
    ion-spinner {
      flex: none;
      width: 16px;
      height: 16px;
      color: var(--lk-accent-700);
    }
    strong {
      display: block;
      font-size: 14px;
    }
  `,
  template: `
    @if (waking()) {
      <output aria-live="polite" data-testid="cold-start">
        <ion-spinner name="crescent" aria-hidden="true" />
        <span>
          <strong>Сервер запускається</strong>
          Зачекай до хвилини, поки він відповість.
        </span>
      </output>
    }
  `,
})
export class ColdStartBanner {
  protected readonly waking = inject(ColdStartService).waking;
}

import { Injectable, InjectionToken, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { SwUpdate } from '@angular/service-worker';
import { filter } from 'rxjs';

/** How long after the app opens a new build is taken at once: nothing on screen is worth keeping yet. */
export const STARTUP_MS = 10_000;

/** A full reload of the page; a token so a spec can count reloads instead of losing the test page. */
export const PAGE_RELOAD = new InjectionToken<() => void>('PAGE_RELOAD', {
  providedIn: 'root',
  factory: () => () => location.reload(),
});

/** Milliseconds since the page opened; a token so a spec can move the clock. */
export const PAGE_AGE = new InjectionToken<() => number>('PAGE_AGE', {
  providedIn: 'root',
  factory: () => () => performance.now(),
});

/**
 * Keeps the PWA on the build the server runs. The service worker opens the cached build and fetches
 * the new one in the background, so after a deploy the app would talk to the new API with the old
 * code until fully restarted — and an iPhone keeps a PWA in memory for days.
 *
 * The new build is taken only where it loses nothing: at once while the app is still opening,
 * otherwise right after the next move to another screen, so an open form is never reloaded under
 * the person. Coming back to the app asks the server for a new build.
 */
@Injectable({ providedIn: 'root' })
export class AppUpdate {
  private readonly updates = inject(SwUpdate);
  private readonly router = inject(Router);
  private readonly reload = inject(PAGE_RELOAD);
  private readonly pageAge = inject(PAGE_AGE);
  private waiting = false;

  start(): void {
    if (!this.updates.isEnabled) return;

    this.updates.versionUpdates.pipe(filter((event) => event.type === 'VERSION_READY')).subscribe(() => {
      if (this.pageAge() < STARTUP_MS) this.reload();
      else this.waiting = true;
    });

    // The cached build lost files the server no longer has; it cannot go on.
    this.updates.unrecoverable.subscribe(() => this.reload());

    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      if (this.waiting) this.reload();
    });

    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') this.updates.checkForUpdate().catch(() => undefined);
    });
  }
}

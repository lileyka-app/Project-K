import { TestBed } from '@angular/core/testing';
import { NavigationEnd, Router } from '@angular/router';
import { SwUpdate, VersionEvent } from '@angular/service-worker';
import { Subject } from 'rxjs';
import { AppUpdate, PAGE_AGE, PAGE_RELOAD, STARTUP_MS } from './app-update';

describe('AppUpdate', () => {
  let versions: Subject<VersionEvent>;
  let unrecoverable: Subject<unknown>;
  let routerEvents: Subject<unknown>;
  let reloads: number;
  let age: number;
  let checks: number;

  const ready = { type: 'VERSION_READY', currentVersion: { hash: 'a' }, latestVersion: { hash: 'b' } } as VersionEvent;

  function start(enabled = true) {
    versions = new Subject();
    unrecoverable = new Subject();
    routerEvents = new Subject();
    reloads = 0;
    checks = 0;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: SwUpdate,
          useValue: {
            isEnabled: enabled,
            versionUpdates: versions,
            unrecoverable,
            checkForUpdate: () => { checks++; return Promise.resolve(false); },
          },
        },
        { provide: Router, useValue: { events: routerEvents } },
        { provide: PAGE_RELOAD, useValue: () => reloads++ },
        { provide: PAGE_AGE, useValue: () => age },
      ],
    });
    TestBed.inject(AppUpdate).start();
  }

  it('takes a new build at once while the app is still opening', () => {
    age = STARTUP_MS - 1;
    start();
    versions.next(ready);
    expect(reloads).toBe(1);
  });

  it('later waits for the next screen instead of reloading under the person', () => {
    age = STARTUP_MS + 1;
    start();
    versions.next(ready);
    expect(reloads).toBe(0);
    routerEvents.next(new NavigationEnd(1, '/kurin', '/kurin'));
    expect(reloads).toBe(1);
  });

  it('does not reload on moving between screens when no build waits', () => {
    age = STARTUP_MS + 1;
    start();
    routerEvents.next(new NavigationEnd(1, '/kurin', '/kurin'));
    expect(reloads).toBe(0);
  });

  it('reloads a build the worker can no longer serve', () => {
    age = STARTUP_MS + 1;
    start();
    unrecoverable.next({ type: 'UNRECOVERABLE_STATE', reason: 'gone' });
    expect(reloads).toBe(1);
  });

  it('asks for a new build when the app comes back to the screen', () => {
    age = STARTUP_MS + 1;
    start();
    document.dispatchEvent(new Event('visibilitychange'));
    expect(checks).toBeGreaterThan(0);
  });

  it('does nothing without a service worker', () => {
    age = 0;
    start(false);
    versions.next(ready);
    expect(reloads).toBe(0);
  });
});

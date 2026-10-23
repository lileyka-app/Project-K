import type { Mock } from 'vitest';
import { NavigationError } from '@angular/router';

import {
  clearStaleAppShellMarker,
  isStaleAppShellError,
  recoverFromStaleAppShell
} from '../functions/stale-app-shell.function';

function navigationError(error: unknown, url = '/kurin/registry'): NavigationError {
  return { id: 1, url, error, type: 1 } as unknown as NavigationError;
}

describe('staleAppShell', () => {
  let goTo: Mock;

  beforeEach(() => {
    sessionStorage.clear();
    // Куди йти — параметр саме заради цього: справжній перехід забрав би з собою сторінку карми.
    goTo = vi.fn().mockName('goTo');
  });

  afterEach(() => sessionStorage.clear());

  /**
   * Кожен рушій формулює це своїми словами, і коду помилки немає — лишається текст. Ці рядки —
   * те, що реально приходить від браузерів; саме перший із них показала жива вкладка після деплою.
   */
  [
    'Failed to fetch dynamically imported module: http://localhost:4201/chunk-EK5NMCZE.js',
    'error loading dynamically imported module',
    'Importing a module script failed.',
    'Loading chunk 42 failed.',
    'Failed to load module script: MIME type text/html'
  ].forEach(message => {
    it(`should recognise "${message.slice(0, 40)}…" as a stale shell`, () => {
      expect(isStaleAppShellError(new Error(message))).toBe(true);
    });
  });

  it('should leave ordinary navigation failures alone', () => {
    expect(isStaleAppShellError(new Error('Cannot match any routes. URL Segment: nope'))).toBe(false);
    expect(isStaleAppShellError(null)).toBe(false);
    expect(isStaleAppShellError(undefined)).toBe(false);
  });

  it('should reload at the url the person was going to', () => {
    const recovered = recoverFromStaleAppShell(
      navigationError(new Error('Failed to fetch dynamically imported module'), '/member/42/probe/probe-1'),
      goTo
    );

    expect(recovered).toBe(true);
    expect(goTo).toHaveBeenCalledTimes(1);
    expect(goTo).toHaveBeenCalledWith('/member/42/probe/probe-1');
  });

  /**
   * Головне тут. Якщо чанка немає і після свіжого `index.html`, то зламана не вкладка, а деплой —
   * і друга спроба перетворила б помилку на нескінченне перезавантаження.
   */
  it('should try once and then let the error show', () => {
    const error = navigationError(new Error('Failed to fetch dynamically imported module'));

    expect(recoverFromStaleAppShell(error, goTo)).toBe(true);
    expect(recoverFromStaleAppShell(error, goTo)).toBe(false);
    expect(goTo).toHaveBeenCalledTimes(1);
  });

  it('should allow a fresh attempt once the app has navigated successfully again', () => {
    const error = navigationError(new Error('Failed to fetch dynamically imported module'));

    expect(recoverFromStaleAppShell(error, goTo)).toBe(true);
    clearStaleAppShellMarker();
    expect(recoverFromStaleAppShell(error, goTo)).toBe(true);
    expect(goTo).toHaveBeenCalledTimes(2);
  });

  it('should not touch a navigation that failed for any other reason', () => {
    expect(recoverFromStaleAppShell(navigationError(new Error('Forbidden')), goTo)).toBe(false);
    expect(goTo).not.toHaveBeenCalled();
  });
});

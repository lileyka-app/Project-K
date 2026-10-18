/**
 * Links out of the app: the docs site and the web's own pages (password reset, activation, the
 * join form), which the PWA does not repeat.
 */

/** Where the web's docs point when its env.js names none (the web's environment.prod.ts). */
const DEFAULT_DOCS_URL = 'https://docs.lileyka.app/';

/**
 * The docs site's root. The server's /env.js names it (`PROJECTK_DOCS_URL`) for the web and the
 * PWA alike, so a self-hosted install points both at its own docs.
 */
export function docsUrl(): string {
  const fromEnv = (globalThis.__PROJECTK_CONFIG__ as { docsUrl?: string } | undefined)?.docsUrl;
  return fromEnv && !fromEnv.includes('${') ? fromEnv : DEFAULT_DOCS_URL;
}

/** «Довідка» opens the guide's first page, like the web's sidebar. */
export function helpUrl(): string {
  return docsPage('user/start/what-is/');
}

/** The guide's page on installing Лілейка, linked from /install. */
export function installHelpUrl(): string {
  return docsPage('user/howto/install-app/');
}

function docsPage(path: string): string {
  let base = docsUrl();
  while (base.endsWith('/')) base = base.slice(0, -1);
  return `${base}/${path}`;
}

/** A page of the web app on this origin: the PWA lives under /m/ next to it. */
export function webPage(path: string): string {
  return new URL(path, globalThis.location?.origin ?? 'http://localhost').href;
}

/**
 * The web redirects a phone to this app (Frontend main.ts, mobile-redirect.function.ts). A web page
 * the app opens on purpose (the registry, the admin pages) carries `web=1`, which lets that one load
 * stay on the web without changing what the next visit does.
 */
export function webOnlyPage(path: string): string {
  const url = new URL(webPage(path));
  url.searchParams.set('web', '1');
  return url.href;
}

/** «Повна версія» is remembered under this key on this origin; the web reads the same key. */
export const PREFER_WEB_KEY = 'lk.preferWeb';

/**
 * «Повна версія»: the web from now on, until its «Мобільна версія» says otherwise. The flag goes in
 * the URL too (`full=1`), because a home-screen app on iOS keeps its storage apart from Safari's.
 */
export function fullVersionUrl(): string {
  try {
    globalThis.localStorage?.setItem(PREFER_WEB_KEY, '1');
  } catch {
    // The URL still carries it.
  }
  const url = new URL(webPage('/'));
  url.searchParams.set('full', '1');
  return url.href;
}

/** Opens a page outside the app: a browser tab, or the system's in-app browser from the home screen. */
export function openExternal(url: string): void {
  globalThis.open?.(url, '_blank', 'noopener');
}

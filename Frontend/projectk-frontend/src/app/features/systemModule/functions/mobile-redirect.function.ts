import { isMobileUserAgent, PHONE_MEDIA_QUERY, pwaPathFor } from './install-app.function';

/**
 * A phone that opens the web goes to the phone app (the PWA under /m/, same origin, so it arrives
 * signed in), on the matching screen where there is one. main.ts decides before Angular starts, so
 * the web never flashes. Pure, for the specs.
 *
 * The way out is «Повна версія» in the app: it remembers PREFER_WEB_KEY and comes here with
 * `full=1` (the URL carries it because a home-screen app on iOS has its own storage); the web's
 * «Мобільна версія» forgets it. A page the app opens on the web on purpose (the registry, the admin
 * pages) carries `web=1`: that one load stays, the next visit is redirected again.
 */
export const PREFER_WEB_KEY = 'lk.preferWeb';

/**
 * Pages that stay on the web even on a phone: the app links to them (password reset, the join form)
 * or email links land there (activation, reset), they work without an account (privacy, about,
 * a shared card, which a phone's camera opens and the app has no screen for), or they are not pages
 * of this app at all.
 */
const STAY_ON_WEB = [
  '/m',
  '/api',
  '/v',
  '/c',
  '/activate',
  '/reset-password',
  '/forgot-password',
  '/join',
  '/setup',
  '/logout',
  '/privacy',
  '/about'
];

export interface RedirectFacts {
  path: string;
  search: string;
  /** PHONE_MEDIA_QUERY matches or the user agent is a phone's. */
  phone: boolean;
  /** PREFER_WEB_KEY is set on this device. */
  preferWeb: boolean;
}

export interface RedirectDecision {
  /** Where to go instead, or null to start the web here. */
  redirectTo: string | null;
  /** Whether the web is (now) the remembered choice. */
  preferWeb: boolean;
  /** The query without `full`/`web`, which are for this decision only. */
  search: string;
}

export function mobileRedirect(facts: RedirectFacts): RedirectDecision {
  const params = new URLSearchParams(facts.search);
  const full = params.get('full') === '1';
  const once = params.get('web') === '1';
  params.delete('full');
  params.delete('web');
  const rest = params.toString();
  const search = rest ? `?${rest}` : '';
  const preferWeb = facts.preferWeb || full;

  if (!facts.phone || preferWeb || once || staysOnWeb(facts.path)) {
    return { redirectTo: null, preferWeb, search };
  }
  const target = pwaPathFor(facts.path);
  // A matching screen keeps the query (a link from an email); the app's start has no use for it.
  return { redirectTo: target === '/m/' ? target : target + search, preferWeb, search };
}

function staysOnWeb(path: string): boolean {
  return STAY_ON_WEB.some(prefix => path === prefix || path.startsWith(`${prefix}/`));
}

/** The facts of this page load, read where main.ts can call it before Angular. */
export function currentRedirectFacts(): RedirectFacts {
  let preferWeb = false;
  try {
    preferWeb = localStorage.getItem(PREFER_WEB_KEY) === '1';
  } catch {
    // Storage blocked: no remembered choice.
  }
  return {
    path: location.pathname,
    search: location.search,
    phone: globalThis.matchMedia?.(PHONE_MEDIA_QUERY).matches === true || isMobileUserAgent(navigator.userAgent),
    preferWeb
  };
}

/**
 * Runs the decision on this page load. True when the browser is on its way to the app (then the
 * web is not started); otherwise the remembered choice is stored and `full`/`web` leave the URL.
 */
export function redirectPhoneToApp(): boolean {
  const decision = mobileRedirect(currentRedirectFacts());
  if (decision.redirectTo) {
    location.replace(decision.redirectTo);
    return true;
  }
  if (decision.preferWeb) {
    try {
      localStorage.setItem(PREFER_WEB_KEY, '1');
    } catch {
      // Not remembered: the next visit asks the URL again.
    }
  }
  if (decision.search !== location.search) {
    history.replaceState(history.state, '', location.pathname + decision.search + location.hash);
  }
  return false;
}

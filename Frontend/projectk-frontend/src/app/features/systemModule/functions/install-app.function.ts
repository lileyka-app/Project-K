/**
 * «Лілейка на телефоні»: when the web offers the phone app, and where in the app (the PWA under /m/,
 * same origin, so the person arrives signed in) a web page continues. Pure, for the specs.
 */

/** «Не зараз» keeps the banner away on this device for two weeks. */
export const INSTALL_BANNER_DISMISSED_KEY = 'lil-install-banner-dismissed';
export const INSTALL_BANNER_QUIET_DAYS = 14;
/** A phone-sized touch screen; the user agent covers the rest (a phone in landscape, a tablet in split view). */
export const PHONE_MEDIA_QUERY = '(max-width: 767px) and (pointer: coarse)';

const DAY_MS = 86_400_000;

export interface InstallBannerFacts {
  /** PHONE_MEDIA_QUERY matches. */
  phoneScreen: boolean;
  userAgent: string;
  /** Already running as an installed app (display-mode: standalone, iOS navigator.standalone). */
  standalone: boolean;
  /** When «Не зараз» was pressed, ms since epoch; null if never (or unreadable). */
  dismissedAt: number | null;
  now: number;
}

/** A phone's browser: iPhone, an Android phone (its tablets leave out «Mobile»), the odd others. */
export function isMobileUserAgent(userAgent: string): boolean {
  if (/iPhone|iPod|Opera Mini|IEMobile|Windows Phone/.test(userAgent)) {
    return true;
  }
  return /Mobile/.test(userAgent) && !/iPad|Tablet/.test(userAgent);
}

export function shouldShowInstallBanner(facts: InstallBannerFacts): boolean {
  if (facts.standalone) {
    return false;
  }
  if (!facts.phoneScreen && !isMobileUserAgent(facts.userAgent)) {
    return false;
  }
  return facts.dismissedAt === null || facts.now - facts.dismissedAt >= INSTALL_BANNER_QUIET_DAYS * DAY_MS;
}

/** The banner lives on the signed-in shell and on the login page, not on the guest's public pages. */
export function installBannerRoute(path: string, publicShell: boolean): boolean {
  return !publicShell || path === '/login' || path.startsWith('/login/');
}

/**
 * The phone app's screen that matches a web page, where one obviously does; anything else opens the
 * app's start (/m/), which lands on Головна or the sign-in.
 */
export function pwaPathFor(webUrl: string): string {
  let path = webUrl.split(/[?#]/)[0];
  while (path.length > 1 && path.endsWith('/')) {
    path = path.slice(0, -1);
  }
  const rules: [RegExp, (match: RegExpMatchArray) => string][] = [
    [/^\/$/, () => '/m/tabs/home'],
    [/^\/login$/, () => '/m/login'],
    [/^\/calendar(\/[^/]+)?$/, () => '/m/tabs/calendar'],
    [/^\/planning(\/[^/]+)?$/, () => '/m/tabs/calendar/planning'],
    [/^\/tasks(\/[^/]+)?$/, () => '/m/tabs/tasks'],
    [/^\/kurin$/, () => '/m/tabs/kurin'],
    [/^\/group\/([^/]+)$/, ([, group]) => `/m/tabs/kurin/group/${group}`],
    [/^\/member\/([^/]+)$/, ([, member]) => `/m/tabs/kurin/member/${member}`],
    [/^\/settings\/account$/, () => '/m/tabs/more/account'],
    [/^\/about$/, () => '/m/tabs/more/about'],
  ];
  for (const [pattern, target] of rules) {
    const match = pattern.exec(path);
    if (match) {
      return target(match);
    }
  }
  return '/m/';
}

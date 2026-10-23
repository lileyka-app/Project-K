import {
  InstallBannerFacts,
  installBannerRoute,
  isMobileUserAgent,
  pwaPathFor,
  shouldShowInstallBanner
} from './install-app.function';

const ANDROID = 'Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Mobile Safari/537.36';
const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.5 Mobile/15E148 Safari/604.1';
const DESKTOP = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36';
const DAY = 86400000;
const NOW = Date.UTC(2026, 9, 11);

function facts(overrides: Partial<InstallBannerFacts>): InstallBannerFacts {
  return { phoneScreen: false, userAgent: DESKTOP, standalone: false, dismissedAt: null, now: NOW, ...overrides };
}

describe('install-app banner', () => {
  it('should show on a phone, told by its screen or by its user agent', () => {
    expect(shouldShowInstallBanner(facts({ phoneScreen: true }))).toBe(true);
    expect(shouldShowInstallBanner(facts({ userAgent: ANDROID }))).toBe(true);
    expect(shouldShowInstallBanner(facts({ userAgent: IPHONE }))).toBe(true);
  });

  it('should stay away on a computer', () => {
    expect(isMobileUserAgent(DESKTOP)).toBe(false);
    // An Android tablet sends no «Mobile»: the web suits it.
    expect(isMobileUserAgent('Mozilla/5.0 (Linux; Android 14; SM-X710) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36')).toBe(false);
    expect(shouldShowInstallBanner(facts({}))).toBe(false);
  });

  it('should stay away inside the installed app', () => {
    expect(shouldShowInstallBanner(facts({ userAgent: ANDROID, phoneScreen: true, standalone: true }))).toBe(false);
  });

  it('should keep «Не зараз» for two weeks, then ask again', () => {
    expect(shouldShowInstallBanner(facts({ userAgent: ANDROID, dismissedAt: NOW - 13 * DAY }))).toBe(false);
    expect(shouldShowInstallBanner(facts({ userAgent: ANDROID, dismissedAt: NOW - 14 * DAY }))).toBe(true);
  });

  it('should live on the signed-in shell and the login page only', () => {
    expect(installBannerRoute('/kurin', false)).toBe(true);
    expect(installBannerRoute('/login', true)).toBe(true);
    expect(installBannerRoute('/join', true)).toBe(false);
    expect(installBannerRoute('/welcome', true)).toBe(false);
  });
});

describe('pwaPathFor', () => {
  it('should continue on the matching screen of the phone app', () => {
    expect(pwaPathFor('/')).toBe('/m/tabs/home');
    expect(pwaPathFor('/calendar/k1?view=month')).toBe('/m/tabs/calendar');
    expect(pwaPathFor('/tasks/k1')).toBe('/m/tabs/tasks');
    expect(pwaPathFor('/planning/k1')).toBe('/m/tabs/calendar/planning');
    expect(pwaPathFor('/kurin')).toBe('/m/tabs/kurin');
    expect(pwaPathFor('/group/g1')).toBe('/m/tabs/kurin/group/g1');
    expect(pwaPathFor('/member/m1#probe')).toBe('/m/tabs/kurin/member/m1');
    expect(pwaPathFor('/settings/account')).toBe('/m/tabs/more/account');
    expect(pwaPathFor('/login')).toBe('/m/login');
  });

  it('should open the app\'s start for anything else', () => {
    expect(pwaPathFor('/kurin/registry')).toBe('/m/');
    expect(pwaPathFor('/users')).toBe('/m/');
    expect(pwaPathFor('/group/g1/dues')).toBe('/m/');
  });
});

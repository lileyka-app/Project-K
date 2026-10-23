import { mobileRedirect, RedirectFacts } from './mobile-redirect.function';

function facts(overrides: Partial<RedirectFacts>): RedirectFacts {
  return { path: '/', search: '', phone: true, preferWeb: false, ...overrides };
}

describe('mobileRedirect', () => {
  it('should send a phone to the matching screen of the app', () => {
    expect(mobileRedirect(facts({})).redirectTo).toBe('/m/tabs/home');
    expect(mobileRedirect(facts({ path: '/login' })).redirectTo).toBe('/m/login');
    expect(mobileRedirect(facts({ path: '/member/m1', search: '?tab=skills' })).redirectTo)
      .toBe('/m/tabs/kurin/member/m1?tab=skills');
  });

  it('should send a page the app has no screen for to the app\'s start, without the query', () => {
    expect(mobileRedirect(facts({ path: '/kurin/registry', search: '?q=1' })).redirectTo).toBe('/m/');
  });

  it('should leave a computer on the web', () => {
    expect(mobileRedirect(facts({ phone: false })).redirectTo).toBeNull();
  });

  it('should keep the pages email links and the app\'s own links land on', () => {
    for (const path of ['/activate/abc', '/reset-password', '/forgot-password', '/join', '/privacy', '/about', '/setup']) {
      expect(mobileRedirect(facts({ path })).redirectTo, path).toBeNull();
    }
  });

  it('should never bounce back into the app from the app\'s own path', () => {
    expect(mobileRedirect(facts({ path: '/m/' })).redirectTo).toBeNull();
    expect(mobileRedirect(facts({ path: '/m/tabs/home' })).redirectTo).toBeNull();
  });

  it('should stay on the web once «Повна версія» was chosen, and remember a choice that came in the URL', () => {
    expect(mobileRedirect(facts({ preferWeb: true })).redirectTo).toBeNull();
    const fromApp = mobileRedirect(facts({ search: '?full=1' }));
    expect(fromApp).toEqual({ redirectTo: null, preferWeb: true, search: '' });
  });

  it('should keep one page the app opened on the web without remembering anything', () => {
    expect(mobileRedirect(facts({ path: '/kurin/registry', search: '?web=1&q=2' })))
      .toEqual({ redirectTo: null, preferWeb: false, search: '?q=2' });
  });
});

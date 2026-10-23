import { PushFacts, base64UrlBytes, pushState, sameKey } from './push';

const KEY = 'BDeLW-FFPXDIu7DhSQo_OnzzEmrLljJO2-0bszZ5cxFscIcrosM2WTzjOGuZZOCWwujNEObZm0iLGconJxSdIcg';

const facts = (over: Partial<PushFacts>): PushFacts => ({
  serverKey: KEY,
  iphone: false,
  standalone: false,
  supported: true,
  permission: 'default',
  subscribed: false,
  ...over,
});

describe('pushState', () => {
  it('hides the row when the server sends no pushes, and waits for its answer', () => {
    expect(pushState(facts({ serverKey: null }))).toBe('hidden');
    expect(pushState(facts({ serverKey: undefined }))).toBe('loading');
  });

  it('asks an iPhone in a Safari tab to add the app to the home screen first', () => {
    expect(pushState(facts({ iphone: true, standalone: false, supported: false }))).toBe('install');
    expect(pushState(facts({ iphone: true, standalone: true, supported: false }))).toBe('unsupported');
    expect(pushState(facts({ iphone: true, standalone: true }))).toBe('off');
  });

  it('says when the system settings forbid notifications', () => {
    expect(pushState(facts({ permission: 'denied', subscribed: true }))).toBe('blocked');
  });

  it('is on only with both the permission and a subscription', () => {
    expect(pushState(facts({ permission: 'granted', subscribed: true }))).toBe('on');
    expect(pushState(facts({ permission: 'granted', subscribed: false }))).toBe('off');
    expect(pushState(facts({ permission: 'default', subscribed: true }))).toBe('off');
    expect(pushState(facts({ permission: 'granted', subscribed: null }))).toBe('loading');
  });
});

describe('sameKey', () => {
  it('compares a subscription key with the server key', () => {
    const bytes = base64UrlBytes(KEY);
    expect(bytes).toHaveLength(65);
    expect(sameKey(bytes.slice().buffer, KEY)).toBe(true);
    const other = bytes.slice();
    other[10] ^= 1;
    expect(sameKey(other.buffer, KEY)).toBe(false);
    expect(sameKey(null, KEY)).toBe(true);
  });
});

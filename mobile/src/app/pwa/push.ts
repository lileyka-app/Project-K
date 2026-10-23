/**
 * What the «Сповіщення на телефон» row shows:
 * - `hidden` — the server sends no pushes (no VAPID key), so there is nothing to switch on;
 * - `install` — iPhone in a Safari tab: iOS sends pushes only to an app on the home screen (16.4+);
 * - `unsupported` — this browser has no Push API (an older iOS, an in-app browser);
 * - `blocked` — the person said no; only the system settings can undo that;
 * - `off` / `on` — this device does not / does get pushes;
 * - `loading` — not known yet (the service worker can take a moment on a first visit).
 */
export type PushState = 'loading' | 'hidden' | 'install' | 'unsupported' | 'blocked' | 'off' | 'on';

export interface PushFacts {
  /** The server's VAPID key; null when it sends no pushes, undefined until asked. */
  serverKey: string | null | undefined;
  iphone: boolean;
  /** Opened from the home screen. */
  standalone: boolean;
  /** A service worker, the Push API and notifications are all there. */
  supported: boolean;
  permission: NotificationPermission | null;
  /** Whether the browser holds a push subscription; null until the service worker answers. */
  subscribed: boolean | null;
}

export function pushState(facts: PushFacts): PushState {
  if (facts.serverKey === undefined) return 'loading';
  if (facts.serverKey === null) return 'hidden';
  if (facts.iphone && !facts.standalone) return 'install';
  if (!facts.supported) return 'unsupported';
  if (facts.permission === 'denied') return 'blocked';
  if (facts.subscribed === null) return 'loading';
  return facts.subscribed && facts.permission === 'granted' ? 'on' : 'off';
}

/** The line under the row's title, where the switch alone does not say enough. */
export const PUSH_HINTS: Partial<Record<PushState, string>> = {
  install: 'Спершу додай Лілейку на початковий екран: «Поділитися» → «На початковий екран».',
  unsupported: 'Цей браузер не вміє показувати сповіщення.',
  blocked: 'Сповіщення заборонені. Дозволь їх для Лілейки в налаштуваннях телефону.',
};

/**
 * Whether a browser subscription was made with this key. A server that changed its VAPID pair can
 * no longer reach subscriptions made with the old one, so those are made anew.
 */
export function sameKey(subscriptionKey: ArrayBuffer | null | undefined, serverKey: string): boolean {
  if (!subscriptionKey) return true; // Not every browser reports it; assume it matches.
  const expected = base64UrlBytes(serverKey);
  const actual = new Uint8Array(subscriptionKey);
  return actual.length === expected.length && actual.every((byte, i) => byte === expected[i]);
}

export function base64UrlBytes(text: string): Uint8Array {
  let base64 = text.replaceAll('-', '+').replaceAll('_', '/');
  while (base64.endsWith('=')) base64 = base64.slice(0, -1);
  const binary = atob(base64 + '='.repeat((4 - (base64.length % 4)) % 4));
  return Uint8Array.from(binary, (char) => char.codePointAt(0) ?? 0);
}

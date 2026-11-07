/** What a scanned QR leads to, decided before anything is opened. */
export type ScanTarget =
  | { kind: 'transfer'; code: string }
  | { kind: 'card'; url: string }
  | { kind: 'foreign'; text: string };

const LILEYKA_HOSTS = ['lileyka.app'];

/**
 * Reads a scanned QR. Only Лілейка's own links are followed: a transfer code opens «Переходи»
 * here in the app, a shared card opens its page. Anything else — another site, plain text — is only
 * shown, never opened: a QR on a poster is not something to follow blindly.
 */
export function scanTarget(text: string, origin: string): ScanTarget {
  let url: URL;
  try {
    url = new URL(text.trim());
  } catch {
    return { kind: 'foreign', text };
  }

  const ours = url.origin === origin || LILEYKA_HOSTS.some((host) => url.hostname === host || url.hostname.endsWith(`.${host}`));
  if (!ours || (url.protocol !== 'https:' && url.origin !== origin)) {
    return { kind: 'foreign', text };
  }

  const code = url.searchParams.get('code');
  // Printed cards carry /kurin/transfers; the web's own address for the mode is /kurin/registry/transfers.
  if ((url.pathname === '/kurin/transfers' || url.pathname === '/kurin/registry/transfers') && code) {
    return { kind: 'transfer', code };
  }
  if (/^\/(v|c)\/[A-Za-z0-9_-]+$/.test(url.pathname)) {
    return { kind: 'card', url: url.href };
  }
  return { kind: 'foreign', text };
}

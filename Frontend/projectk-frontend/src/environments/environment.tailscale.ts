const runtimeConfig = (globalThis as { __PROJECTK_CONFIG__?: { apiUrl?: string; environmentName?: string; docsUrl?: string } }).__PROJECTK_CONFIG__;

export const environment = {
  production: false,
  // The tailnet address is not committed: the tailscale compose passes it through env.js
  // (PROJECTK_API_URL in docker/env/tailscale.env). This is only what a bare `ng serve` gets.
  apiUrl: runtimeConfig?.apiUrl || 'http://localhost:5205/api',
  version: 'v0.0.0-dev',
  codeName: 'TailscaleDevelopment',
  envName: runtimeConfig?.environmentName || 'Development',
  // Fixed, not configurable: the licence forbids running the system under another name.
  appName: 'Лілейка',
  docsUrl: runtimeConfig?.docsUrl || 'https://docs-dev.lileyka.app/',
  isF1TierBackend: false,
  isStaticDemo: false
};

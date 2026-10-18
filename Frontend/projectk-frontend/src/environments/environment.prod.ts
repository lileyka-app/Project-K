const runtimeConfig = (globalThis as { __PROJECTK_CONFIG__?: { apiUrl?: string; environmentName?: string; docsUrl?: string } }).__PROJECTK_CONFIG__;

export const environment = {
  production: true,
  apiUrl: runtimeConfig?.apiUrl || 'https://api.lileyka.app/api',
  version: 'v0.0.0-dev',
  codeName: 'LocalDevelopment',
  envName: runtimeConfig?.environmentName || 'Production',
  // Fixed, not configurable: the licence forbids running the system under another name.
  appName: 'Лілейка',
  docsUrl: runtimeConfig?.docsUrl || 'https://docs.lileyka.app/',
  isF1TierBackend: false,
  isStaticDemo: false
};

const runtimeConfig = (globalThis as { __PROJECTK_CONFIG__?: { apiUrl?: string; environmentName?: string; docsUrl?: string } }).__PROJECTK_CONFIG__;

export const environment = {
  production: true,
  apiUrl: runtimeConfig?.apiUrl || 'https://api-projectk-prod-b9dedsaucbcgf5fh.polandcentral-01.azurewebsites.net/api',
  version: 'v0.0.0-dev',
  codeName: 'LocalDevelopment',
  envName: runtimeConfig?.environmentName || 'Staging',
  // Fixed, not configurable: the licence forbids running the system under another name.
  appName: 'Лілейка',
  docsUrl: runtimeConfig?.docsUrl || 'https://docs-dev.lileyka.app/',
  isF1TierBackend: true,
  isStaticDemo: false
};

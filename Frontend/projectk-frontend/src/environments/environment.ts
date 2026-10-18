const runtimeConfig = (globalThis as { __PROJECTK_CONFIG__?: { apiUrl?: string; environmentName?: string; docsUrl?: string } }).__PROJECTK_CONFIG__;

export const environment = {
  production: false,
  apiUrl: runtimeConfig?.apiUrl || 'http://localhost:5205/api',
  version: 'v0.0.0-dev',
  codeName: 'LocalDevelopment',
  envName: runtimeConfig?.environmentName || 'Development',
  // Fixed, not configurable: the licence forbids running the system under another name.
  appName: 'Лілейка',
  // The docs site's root (runtime PROJECTK_DOCS_URL): the welcome page links to it as is, the
  // sidebar appends the guide's own page. Production points at the site's production deployment;
  // every other tier reads the dev-branch deployment, which is where docs land first.
  docsUrl: runtimeConfig?.docsUrl || 'https://docs-dev.lileyka.app/',
  isF1TierBackend: false,
  isStaticDemo: false
};

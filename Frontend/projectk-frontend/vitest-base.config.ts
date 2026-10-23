import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    // The builder runs every spec file in one shared jsdom (isolate: false), so a spy left on
    // sessionStorage or console would leak into the next file. Jasmine restored them after each test.
    restoreMocks: true,
    unstubGlobals: true
  }
});

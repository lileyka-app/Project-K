// jsdom has no layout and no media queries; Optimus tables observe their size and menus listen to
// breakpoints on init. Inert stand-ins keep those components rendering as they did in Chrome.

globalThis.ResizeObserver ??= class {
  observe(): void { /* no layout in jsdom */ }
  unobserve(): void { /* no layout in jsdom */ }
  disconnect(): void { /* no layout in jsdom */ }
};

window.matchMedia ??= (query: string): MediaQueryList => ({
  matches: false,
  media: query,
  onchange: null,
  addEventListener: () => undefined,
  removeEventListener: () => undefined,
  addListener: () => undefined,
  removeListener: () => undefined,
  dispatchEvent: () => false
});

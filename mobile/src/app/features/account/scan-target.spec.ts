import { scanTarget } from './scan-target';

describe('scanTarget', () => {
  const origin = 'https://lileyka.app';

  it('opens a transfer code in «Переходи»', () => {
    expect(scanTarget('https://lileyka.app/kurin/transfers?code=PL-4F7A2-9D4A1', origin)).toEqual({ kind: 'transfer', code: 'PL-4F7A2-9D4A1' });
    expect(scanTarget('https://lileyka.app/kurin/registry/transfers?code=PL-4F7A2-9D4A1', origin)).toEqual({ kind: 'transfer', code: 'PL-4F7A2-9D4A1' });
  });

  it('opens a shared card, checked or linked', () => {
    expect(scanTarget('https://lileyka.app/v/abc_DEF-123', origin)).toEqual({ kind: 'card', url: 'https://lileyka.app/v/abc_DEF-123' });
    expect(scanTarget('https://staging.lileyka.app/c/xyz', origin).kind).toBe('card');
  });

  it('only shows anything else', () => {
    expect(scanTarget('https://evil.example/v/abc', origin)).toEqual({ kind: 'foreign', text: 'https://evil.example/v/abc' });
    expect(scanTarget('http://lileyka.app/v/abc', 'https://other.app').kind).toBe('foreign');
    expect(scanTarget('https://lileyka.app/settings/account', origin).kind).toBe('foreign');
    expect(scanTarget('просто текст', origin)).toEqual({ kind: 'foreign', text: 'просто текст' });
  });

  it('follows the instance it runs on, wherever it is hosted', () => {
    expect(scanTarget('http://localhost:4213/v/abc', 'http://localhost:4213').kind).toBe('card');
  });
});

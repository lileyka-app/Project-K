import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

const STORAGE_KEY = 'lileyka-theme';

describe('ThemeService', () => {
  function create(): ThemeService {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    return TestBed.inject(ThemeService);
  }

  afterEach(() => {
    localStorage.removeItem(STORAGE_KEY);
    delete document.documentElement.dataset['theme'];
  });

  it('applies the stored preference on construction', () => {
    localStorage.setItem(STORAGE_KEY, 'dark');

    const service = create();

    expect(service.isDark()).toBe(true);
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('falls back to the system preference when nothing is stored', () => {
    localStorage.removeItem(STORAGE_KEY);
    vi.spyOn(globalThis, 'matchMedia').mockReturnValue({ matches: true } as MediaQueryList);

    const service = create();

    expect(service.current()).toBe('dark');
  });

  it('toggles the attribute the Optimus UI dark selector keys on', () => {
    localStorage.setItem(STORAGE_KEY, 'light');
    const service = create();

    service.toggle();
    expect(document.documentElement.dataset['theme']).toBe('dark');

    service.toggle();
    expect(document.documentElement.dataset['theme']).toBeUndefined();
  });

  it('persists the choice so it survives a reload', () => {
    const service = create();

    service.set('dark');

    expect(localStorage.getItem(STORAGE_KEY)).toBe('dark');
  });

  it('keeps working when storage throws', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('quota');
    });
    const service = create();

    expect(() => service.set('dark')).not.toThrow();
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });
});

import { Injectable, computed, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { StatusBar, Style } from '@capacitor/status-bar';

export type ThemeChoice = 'system' | 'light' | 'dark';

/**
 * Its own key, not the web's `lileyka-theme`: the web has no «system» choice and stores whatever
 * the system was on the first visit, so sharing it would pin the phone to that.
 */
const STORAGE_KEY = 'lileyka-mobile-theme';

/** The status bar and task switcher colour per theme (index.html's theme-color metas). */
const BAR_COLOR = { light: '#ffffff', dark: '#000000' } as const;

/**
 * Системна / Світла / Темна, per device (it survives sign-out, like the web's theme.service).
 *
 * The page is dark when `<html>` has `ion-palette-dark`: Ionic's dark palette and rdlabo's Liquid
 * Glass dark theme are loaded in their class form (theme/lileyka.scss) and the Лілейка tokens
 * follow the same class. `lk-light` keeps the tokens light on a dark system, for the moment before
 * the app starts, when only the media query can decide.
 */
@Injectable({ providedIn: 'root' })
export class AppearanceService {
  private readonly media = globalThis.matchMedia?.('(prefers-color-scheme: dark)');
  private readonly systemDark = signal(this.media?.matches ?? false);
  private readonly stored = signal<ThemeChoice>(readStored());

  readonly choice = this.stored.asReadonly();
  readonly dark = computed(() => {
    const choice = this.stored();
    return choice === 'system' ? this.systemDark() : choice === 'dark';
  });

  /** Applies the remembered choice before the first screen and follows the system from then on. */
  init(): void {
    this.media?.addEventListener?.('change', (event) => {
      this.systemDark.set(event.matches);
      this.apply();
    });
    this.apply();
  }

  set(choice: ThemeChoice): void {
    this.stored.set(choice);
    try {
      if (choice === 'system') localStorage.removeItem(STORAGE_KEY);
      else localStorage.setItem(STORAGE_KEY, choice);
    } catch {
      // Storage refused: the choice holds until the app closes.
    }
    this.apply();
  }

  private apply(): void {
    const root = document.documentElement;
    const dark = this.dark();
    const choice = this.stored();
    root.classList.toggle('ion-palette-dark', dark);
    root.classList.toggle('lk-light', choice === 'light');
    // Native form controls and scrollbars.
    root.style.colorScheme = choice === 'system' ? '' : choice;
    this.paintBars(choice, dark);
  }

  private paintBars(choice: ThemeChoice, dark: boolean): void {
    for (const meta of Array.from(document.querySelectorAll<HTMLMetaElement>('meta[name="theme-color"]'))) {
      // Each meta answers one system scheme; a chosen theme answers both the same way.
      const darkBar = choice === 'system' ? meta.media.includes('dark') : dark;
      meta.content = BAR_COLOR[darkBar ? 'dark' : 'light'];
    }
    if (Capacitor.isNativePlatform()) {
      // Style.Dark is light text, for a dark page.
      StatusBar.setStyle({ style: dark ? Style.Dark : Style.Light }).catch(() => undefined);
    }
  }
}

function readStored(): ThemeChoice {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return value === 'light' || value === 'dark' ? value : 'system';
  } catch {
    return 'system';
  }
}

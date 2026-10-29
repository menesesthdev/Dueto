import { DOCUMENT } from '@angular/common';
import { Injectable, InjectionToken, computed, effect, inject, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'auto';
export type ResolvedTheme = 'light' | 'dark';

const STORAGE_KEY = 'dueto.theme';
const VALID: ReadonlySet<string> = new Set<ThemePreference>(['light', 'dark', 'auto']);

export const THEME_STORAGE = new InjectionToken<Storage | null>('THEME_STORAGE', {
  providedIn: 'root',
  factory: () => {
    try {
      return window.localStorage;
    } catch {
      return null;
    }
  },
});

export const WINDOW_REF = new InjectionToken<Window | null>('WINDOW_REF', {
  providedIn: 'root',
  factory: () => (typeof window !== 'undefined' ? window : null),
});

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly storage = inject(THEME_STORAGE);
  private readonly doc = inject(DOCUMENT);
  private readonly win = inject(WINDOW_REF);

  /** What the system reports right now. */
  private readonly systemDark = signal(this.querySystemDark());

  /** What the user chose (persisted). */
  readonly preference = signal<ThemePreference>(this.load());

  /** The actual theme applied to the page. */
  readonly resolved = computed<ResolvedTheme>(() => {
    const pref = this.preference();
    return pref === 'auto' ? (this.systemDark() ? 'dark' : 'light') : pref;
  });

  constructor() {
    // Listen for OS theme changes.
    const mq = this.win?.matchMedia('(prefers-color-scheme: dark)');
    if (mq) {
      mq.addEventListener('change', (e: MediaQueryListEvent) => this.systemDark.set(e.matches));
    }

    // Sync `data-theme` on <html> and persist whenever the preference changes.
    effect(() => {
      const theme = this.resolved();
      this.doc.documentElement.setAttribute('data-theme', theme);
    });

    effect(() => {
      const pref = this.preference();
      this.save(pref);
    });
  }

  set(pref: ThemePreference): void {
    this.startTransition();
    this.preference.set(pref);
  }

  /** Cycle through auto → light → dark → auto. */
  cycle(): void {
    const order: ThemePreference[] = ['auto', 'light', 'dark'];
    const next = order[(order.indexOf(this.preference()) + 1) % order.length];
    this.set(next);
  }

  private transitionTimer: ReturnType<typeof setTimeout> | null = null;

  private startTransition(): void {
    const el = this.doc?.documentElement;
    if (!el) return;
    el.setAttribute('data-theme-transition', '');
    if (this.transitionTimer) clearTimeout(this.transitionTimer);
    this.transitionTimer = setTimeout(() => {
      el.removeAttribute('data-theme-transition');
      this.transitionTimer = null;
    }, 500);
  }

  private querySystemDark(): boolean {
    try {
      return !!this.win?.matchMedia('(prefers-color-scheme: dark)').matches;
    } catch {
      return false;
    }
  }

  private load(): ThemePreference {
    try {
      const raw = this.storage?.getItem(STORAGE_KEY);
      return raw && VALID.has(raw) ? (raw as ThemePreference) : 'auto';
    } catch {
      return 'auto';
    }
  }

  private save(pref: ThemePreference): void {
    try {
      if (pref === 'auto') {
        this.storage?.removeItem(STORAGE_KEY);
      } else {
        this.storage?.setItem(STORAGE_KEY, pref);
      }
    } catch {
      // Storage unavailable — preference lives only in memory.
    }
  }
}

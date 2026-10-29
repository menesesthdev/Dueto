import { TestBed } from '@angular/core/testing';
import { ThemeService, THEME_STORAGE, WINDOW_REF, type ThemePreference } from './theme.service';

function createFakeStorage(): Storage {
  const store = new Map<string, string>();
  return {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => { store.set(key, value); },
    removeItem: (key: string) => { store.delete(key); },
    clear: () => { store.clear(); },
    get length() { return store.size; },
    key: () => null,
  };
}

const noop = (): void => { /* listener stub */ };

function setup(stored: string | null = null, systemDark = false): { service: ThemeService; storage: Storage } {
  const storage = createFakeStorage();
  if (stored) {
    storage.setItem('dueto.theme', stored);
  }

  const fakeWindow = {
    matchMedia: () => ({ matches: systemDark, addEventListener: noop }),
  } as unknown as Window;

  TestBed.configureTestingModule({
    providers: [
      { provide: THEME_STORAGE, useValue: storage },
      { provide: WINDOW_REF, useValue: fakeWindow },
    ],
  });

  return { service: TestBed.inject(ThemeService), storage };
}

describe('ThemeService', () => {
  it('defaults to auto when nothing is stored', () => {
    const { service } = setup();
    expect(service.preference()).toBe('auto');
  });

  it('loads a persisted preference', () => {
    const { service } = setup('dark');
    expect(service.preference()).toBe('dark');
  });

  it('ignores an invalid stored value', () => {
    const { service } = setup('neon');
    expect(service.preference()).toBe('auto');
  });

  it('resolves auto to light when the system is light', () => {
    const { service } = setup(null, false);
    expect(service.resolved()).toBe('light');
  });

  it('resolves auto to dark when the system is dark', () => {
    const { service } = setup(null, true);
    expect(service.resolved()).toBe('dark');
  });

  it('resolves an explicit preference regardless of system', () => {
    const { service } = setup('light', true);
    expect(service.resolved()).toBe('light');
  });

  it('persists the preference on set', () => {
    const { service, storage } = setup();
    service.set('dark');
    TestBed.flushEffects();
    expect(storage.getItem('dueto.theme')).toBe('dark');
  });

  it('removes the key when set to auto', () => {
    const { service, storage } = setup('dark');
    service.set('auto');
    TestBed.flushEffects();
    expect(storage.getItem('dueto.theme')).toBeNull();
  });

  it('cycles through auto → light → dark → auto', () => {
    const { service } = setup();
    const seen: ThemePreference[] = [service.preference()];
    service.cycle();
    seen.push(service.preference());
    service.cycle();
    seen.push(service.preference());
    service.cycle();
    seen.push(service.preference());
    expect(seen).toEqual(['auto', 'light', 'dark', 'auto']);
  });

  it('sets data-theme on the document element', () => {
    const { service } = setup();
    service.set('dark');
    TestBed.flushEffects();
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('works when storage is null', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: THEME_STORAGE, useValue: null },
        { provide: WINDOW_REF, useValue: null },
      ],
    });
    const service = TestBed.inject(ThemeService);
    expect(service.preference()).toBe('auto');
    service.set('dark');
    expect(service.preference()).toBe('dark');
  });
});

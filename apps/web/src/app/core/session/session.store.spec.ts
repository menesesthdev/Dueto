import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { SESSION_STORAGE, SessionStore } from './session.store';

function memoryStorage(initial: Record<string, string> = {}): Storage {
  const data = new Map(Object.entries(initial));
  return {
    get length() {
      return data.size;
    },
    clear: () => data.clear(),
    getItem: (key: string) => data.get(key) ?? null,
    key: (index: number) => [...data.keys()][index] ?? null,
    removeItem: (key: string) => void data.delete(key),
    setItem: (key: string, value: string) => void data.set(key, value),
  };
}

function throwingStorage(): Storage {
  const fail = () => {
    throw new Error('storage blocked');
  };
  return { length: 0, clear: fail, getItem: fail, key: fail, removeItem: fail, setItem: fail };
}

function create(storage: Storage | null): SessionStore {
  TestBed.configureTestingModule({ providers: [{ provide: SESSION_STORAGE, useValue: storage }] });
  return TestBed.inject(SessionStore);
}

describe('SessionStore', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('starts empty and not paired', () => {
    const store = create(memoryStorage());

    expect(store.session()).toBeNull();
    expect(store.token()).toBeNull();
    expect(store.isPaired()).toBe(false);
  });

  it('is not paired while the other side has not typed the code', () => {
    const store = create(memoryStorage());

    store.startPending('tok', 'desktop', 'Meu PC', { code: '123456', expiresAt: '2026-10-28T12:05:00Z' });

    expect(store.token()).toBe('tok');
    expect(store.isPaired()).toBe(false);
    expect(store.session()?.pairing?.code).toBe('123456');
  });

  it('becomes paired when the other side connects and forgets the code', () => {
    const store = create(memoryStorage());
    store.startPending('tok', 'desktop', 'Meu PC', { code: '123456', expiresAt: '2026-10-28T12:05:00Z' });

    store.markPaired();

    expect(store.isPaired()).toBe(true);
    expect(store.session()?.pairing).toBeNull();
  });

  it('is paired right away for the side that typed the code', () => {
    const store = create(memoryStorage());

    store.savePaired('tok', 'mobile', 'iPhone');

    expect(store.isPaired()).toBe(true);
  });

  it('restores the session after a reload', () => {
    const storage = memoryStorage();
    create(storage).savePaired('tok', 'mobile', 'iPhone');
    TestBed.resetTestingModule();

    const restored = create(storage);

    expect(restored.token()).toBe('tok');
    expect(restored.session()?.deviceName).toBe('iPhone');
    expect(restored.isPaired()).toBe(true);
  });

  it('forgets everything on clear', () => {
    const storage = memoryStorage();
    const store = create(storage);
    store.savePaired('tok', 'mobile', 'iPhone');

    store.clear();

    expect(store.session()).toBeNull();
    expect(storage.getItem('dueto.session')).toBeNull();
  });

  it.each(['not json', '{}', '{"token":"","kind":"desktop","deviceName":"x"}', '{"token":"t","kind":"tablet","deviceName":"x"}', 'null'])(
    'ignores a corrupted stored session: %s',
    (raw) => {
      const store = create(memoryStorage({ 'dueto.session': raw }));

      expect(store.session()).toBeNull();
    },
  );

  it('keeps working in memory when the storage is blocked', () => {
    const store = create(throwingStorage());

    expect(store.session()).toBeNull();
    store.savePaired('tok', 'desktop', 'Meu PC');
    expect(store.isPaired()).toBe(true);
    expect(() => store.clear()).not.toThrow();
    expect(store.session()).toBeNull();
  });

  it('keeps working in memory when there is no storage at all', () => {
    const store = create(null);

    store.savePaired('tok', 'desktop', 'Meu PC');

    expect(store.token()).toBe('tok');
  });
});

import { vi, describe, it, expect } from 'vitest';

const { tempDir } = vi.hoisted(() => {
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const fs = require('node:fs') as typeof import('node:fs');
  const os = require('node:os') as typeof import('node:os');
  const p = require('node:path') as typeof import('node:path');
  return { tempDir: fs.mkdtempSync(p.join(os.tmpdir(), 'dueto-store-test-')) };
});

vi.mock('electron', () => ({
  app: {
    getPath: () => tempDir,
    isPackaged: false,
  },
  safeStorage: {
    isEncryptionAvailable: () => false,
    encryptString: (s: string) => Buffer.from(s),
    decryptString: (b: Buffer) => b.toString(),
  },
}));

const { getAppliedState, setAppliedState, getDeviceToken, setDeviceToken, clearDeviceToken } =
  await import('../src/main/store.js');

// The store keeps module-level state, so these tests are intentionally order-dependent.
describe('store', () => {
  it('starts with empty applied state', () => {
    const state = getAppliedState();
    expect(state.wallpaperId).toBeNull();
    expect(state.version).toBe(0);
  });

  it('accepts a newer version', () => {
    expect(setAppliedState('aurora-dunes', 1)).toBe(true);
    expect(getAppliedState()).toEqual({ wallpaperId: 'aurora-dunes', version: 1 });
  });

  it('rejects the same version', () => {
    expect(setAppliedState('neon-tide', 1)).toBe(false);
    expect(getAppliedState().wallpaperId).toBe('aurora-dunes');
  });

  it('rejects a lower version', () => {
    expect(setAppliedState('neon-tide', 0)).toBe(false);
    expect(getAppliedState().wallpaperId).toBe('aurora-dunes');
  });

  it('accepts a higher version', () => {
    expect(setAppliedState('neon-tide', 2)).toBe(true);
    expect(getAppliedState()).toEqual({ wallpaperId: 'neon-tide', version: 2 });
  });

  it('starts with no device token', () => {
    expect(getDeviceToken()).toBeNull();
  });

  it('stores and retrieves a device token', () => {
    setDeviceToken('test-token-abc');
    expect(getDeviceToken()).toBe('test-token-abc');
  });

  it('clears the device token', () => {
    clearDeviceToken();
    expect(getDeviceToken()).toBeNull();
  });
});

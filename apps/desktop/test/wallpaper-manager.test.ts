import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';
import { createHash } from 'node:crypto';

const { tempDir } = vi.hoisted(() => {
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const fs = require('node:fs') as typeof import('node:fs');
  const os = require('node:os') as typeof import('node:os');
  const p = require('node:path') as typeof import('node:path');
  return { tempDir: fs.mkdtempSync(p.join(os.tmpdir(), 'dueto-wp-test-')) };
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

const setWallpaperMock = vi.hoisted(() => vi.fn());
vi.mock('wallpaper', () => ({ setWallpaper: setWallpaperMock }));

const { setDeviceToken } = await import('../src/main/store.js');
const { applyWallpaperById } = await import('../src/main/wallpaper-manager.js');

setDeviceToken('test-token');

const IMAGE_DATA = Buffer.from('fake-png-image-data-for-testing');
const CORRECT_SHA = createHash('sha256').update(IMAGE_DATA).digest('hex');

function mockFetch(wallpaperInfo: object, imageData: Buffer): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      if (url.includes('/api/wallpapers/')) {
        return { ok: true, json: async () => wallpaperInfo };
      }
      if (url.includes('/api/state/applied')) {
        return { ok: true };
      }
      if (url.includes('github.com')) {
        return {
          ok: true,
          url,
          headers: new Headers({ 'content-length': String(imageData.length) }),
          body: new ReadableStream({
            start(controller) {
              controller.enqueue(new Uint8Array(imageData));
              controller.close();
            },
          }),
        };
      }
      return { ok: false, status: 500 };
    }),
  );
}

describe('applyWallpaperById', () => {
  beforeEach(() => {
    setWallpaperMock.mockReset();
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(console, 'log').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('downloads and applies a wallpaper with the correct SHA-256', async () => {
    mockFetch(
      {
        id: 'test-wp',
        kind: 'static',
        desktopUrl: 'https://github.com/releases/test-wp-desktop.png',
        desktopSha256: CORRECT_SHA,
      },
      IMAGE_DATA,
    );

    expect(await applyWallpaperById('test-wp', 1)).toBe(true);
    expect(setWallpaperMock).toHaveBeenCalledOnce();
  });

  it('rejects a wallpaper with an incorrect SHA-256', async () => {
    mockFetch(
      {
        id: 'bad-sha',
        kind: 'static',
        desktopUrl: 'https://github.com/releases/bad-sha-desktop.png',
        desktopSha256: 'a'.repeat(64),
      },
      IMAGE_DATA,
    );

    expect(await applyWallpaperById('bad-sha', 2)).toBe(false);
    expect(setWallpaperMock).not.toHaveBeenCalled();
  });

  it('rejects a host not in the allowlist', async () => {
    mockFetch(
      {
        id: 'bad-host',
        kind: 'static',
        desktopUrl: 'https://evil.com/malware.png',
        desktopSha256: CORRECT_SHA,
      },
      IMAGE_DATA,
    );

    expect(await applyWallpaperById('bad-host', 3)).toBe(false);
    expect(setWallpaperMock).not.toHaveBeenCalled();
  });

  it('rejects an invalid wallpaper id without calling the network', async () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);

    expect(await applyWallpaperById('INVALID_ID!', 1)).toBe(false);
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('skips animated wallpapers', async () => {
    mockFetch(
      {
        id: 'animated-one',
        kind: 'animated',
        desktopUrl: 'https://github.com/releases/animated-one-desktop.mp4',
        desktopSha256: CORRECT_SHA,
      },
      IMAGE_DATA,
    );

    expect(await applyWallpaperById('animated-one', 4)).toBe(false);
    expect(setWallpaperMock).not.toHaveBeenCalled();
  });
});

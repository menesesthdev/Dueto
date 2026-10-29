import { createHash } from 'node:crypto';
import { createWriteStream, mkdirSync } from 'node:fs';
import { readFile, rename, unlink } from 'node:fs/promises';
import path from 'node:path';
import { app } from 'electron';
import { getApiBaseUrl, getAllowedHosts, MAX_DOWNLOAD_SIZE, WALLPAPER_ID_PATTERN } from './config.js';
import { getDeviceToken, setAppliedState } from './store.js';

// The wallpaper package is ESM-only; loaded lazily.
async function setWallpaper(filePath: string): Promise<void> {
  const { setWallpaper: set } = await import('wallpaper');
  await set(filePath);
}

interface WallpaperInfo {
  id: string;
  kind: 'static' | 'animated';
  desktopUrl: string;
  desktopSha256: string;
}

const WALLPAPERS_DIR = path.join(app.getPath('userData'), 'wallpapers');

function isHostAllowed(url: string): boolean {
  try {
    const { hostname } = new URL(url);
    return getAllowedHosts().some((host) => hostname === host || hostname.endsWith(`.${host}`));
  } catch {
    return false;
  }
}

async function fetchWallpaperInfo(wallpaperId: string): Promise<WallpaperInfo | null> {
  const token = getDeviceToken();
  if (!token) return null;

  try {
    const response = await fetch(`${getApiBaseUrl()}/api/wallpapers/${wallpaperId}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok) return null;
    const data = (await response.json()) as WallpaperInfo;
    if (typeof data.desktopUrl !== 'string' || typeof data.desktopSha256 !== 'string') return null;
    return {
      id: data.id,
      kind: data.kind,
      desktopUrl: data.desktopUrl,
      desktopSha256: data.desktopSha256,
    };
  } catch {
    return null;
  }
}

async function downloadAndVerify(url: string, expectedSha256: string, destPath: string): Promise<boolean> {
  if (!isHostAllowed(url)) {
    console.error(`[wallpaper] Host not in allowlist: ${url}`);
    return false;
  }

  const tempPath = `${destPath}.tmp`;
  try {
    const response = await fetch(url, { redirect: 'follow' });
    if (!response.ok || !response.body) return false;

    if (!isHostAllowed(response.url)) {
      console.error(`[wallpaper] Redirect target not in allowlist: ${response.url}`);
      return false;
    }

    const contentLength = Number(response.headers.get('content-length') ?? '0');
    if (contentLength > MAX_DOWNLOAD_SIZE) {
      console.error(`[wallpaper] File too large: ${contentLength}`);
      return false;
    }

    mkdirSync(WALLPAPERS_DIR, { recursive: true });
    const hash = createHash('sha256');
    let totalBytes = 0;
    const writable = createWriteStream(tempPath);
    const reader = response.body.getReader();

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      totalBytes += value.byteLength;
      if (totalBytes > MAX_DOWNLOAD_SIZE) {
        writable.destroy();
        await reader.cancel().catch(() => {});
        await unlink(tempPath).catch(() => {});
        console.error('[wallpaper] Download exceeded max size');
        return false;
      }
      hash.update(value);
      if (!writable.write(value)) {
        await new Promise<void>((resolve) => writable.once('drain', resolve));
      }
    }

    await new Promise<void>((resolve, reject) => {
      writable.once('error', reject);
      writable.end(() => resolve());
    });

    const actualSha256 = hash.digest('hex');
    if (actualSha256 !== expectedSha256.toLowerCase()) {
      console.error(`[wallpaper] SHA-256 mismatch: expected ${expectedSha256}, got ${actualSha256}`);
      await unlink(tempPath).catch(() => {});
      return false;
    }

    await rename(tempPath, destPath);
    return true;
  } catch (err) {
    await unlink(tempPath).catch(() => {});
    console.error('[wallpaper] Download failed:', err);
    return false;
  }
}

async function confirmApplied(wallpaperId: string, version: number): Promise<void> {
  const token = getDeviceToken();
  if (!token) return;

  try {
    await fetch(`${getApiBaseUrl()}/api/state/applied`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ wallpaperId, version }),
    });
  } catch (err) {
    console.error('[wallpaper] Failed to confirm applied:', err);
  }
}

async function hasValidCachedFile(destPath: string, expectedSha256: string): Promise<boolean> {
  try {
    const existing = await readFile(destPath);
    return createHash('sha256').update(existing).digest('hex') === expectedSha256.toLowerCase();
  } catch {
    return false;
  }
}

export async function applyWallpaperById(wallpaperId: string, version: number): Promise<boolean> {
  if (!WALLPAPER_ID_PATTERN.test(wallpaperId)) return false;

  const info = await fetchWallpaperInfo(wallpaperId);
  if (!info) return false;

  // Phase 7: animated wallpapers are desktop-only but handled later.
  if (info.kind === 'animated') {
    console.log(`[wallpaper] Animated wallpaper ${wallpaperId} skipped (Phase 7)`);
    return false;
  }

  let extension: string;
  try {
    extension = path.extname(new URL(info.desktopUrl).pathname) || '.png';
  } catch {
    return false;
  }
  if (!/^\.[a-z0-9]{1,5}$/i.test(extension)) extension = '.png';
  const destPath = path.join(WALLPAPERS_DIR, `${wallpaperId}${extension}`);

  if (!(await hasValidCachedFile(destPath, info.desktopSha256))) {
    const ok = await downloadAndVerify(info.desktopUrl, info.desktopSha256, destPath);
    if (!ok) return false;
  }

  try {
    await setWallpaper(destPath);
    setAppliedState(wallpaperId, version);
    await confirmApplied(wallpaperId, version);
    return true;
  } catch (err) {
    console.error('[wallpaper] Failed to apply:', err);
    return false;
  }
}

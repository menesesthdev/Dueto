import { WALLPAPER_ID_PATTERN, DEEP_LINK_SCHEME, getApiBaseUrl } from './config.js';
import { applyWallpaperById } from './wallpaper-manager.js';
import { getDeviceToken } from './store.js';

export function parseDeepLink(url: string): string | null {
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== `${DEEP_LINK_SCHEME}:`) return null;
    if (parsed.hostname !== 'apply') return null;
    const id = parsed.searchParams.get('id');
    if (!id || !WALLPAPER_ID_PATTERN.test(id)) return null;
    return id;
  } catch {
    return null;
  }
}

async function fetchCurrentVersion(): Promise<number> {
  const token = getDeviceToken();
  if (!token) return 1;
  try {
    const response = await fetch(`${getApiBaseUrl()}/api/state`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok) return 1;
    const state = (await response.json()) as { version?: unknown };
    return typeof state.version === 'number' ? state.version : 1;
  } catch {
    return 1;
  }
}

export function handleDeepLink(url: string): void {
  const id = parseDeepLink(url);
  if (!id) return;

  void (async () => {
    const version = await fetchCurrentVersion();
    await applyWallpaperById(id, version);
  })().catch((err) => console.error('[deep-link] Failed to apply wallpaper:', err));
}

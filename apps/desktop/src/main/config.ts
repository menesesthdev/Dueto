import { app } from 'electron';
import path from 'node:path';

export const WALLPAPER_ID_PATTERN = /^[a-z0-9-]{1,64}$/;
export const DEFAULT_ALLOWED_HOSTS = [
  'github.com',
  'objects.githubusercontent.com',
  'github-releases.githubusercontent.com',
];
export const MAX_DOWNLOAD_SIZE = 50 * 1024 * 1024;
export const PROTOCOL_SCHEME = 'app';
export const DEEP_LINK_SCHEME = 'dueto';
export const HUB_PATH = '/hub/sync';
export const DEV_SERVER_URL = 'http://localhost:4200';

export function getApiBaseUrl(): string {
  return process.env.DUETO_API_URL ?? 'https://dueto.example.com';
}

export function getAllowedHosts(): string[] {
  const raw = process.env.DUETO_ALLOWED_DOWNLOAD_HOSTS;
  if (!raw) return DEFAULT_ALLOWED_HOSTS;
  const hosts = raw.split(',').map((host) => host.trim()).filter(Boolean);
  return hosts.length > 0 ? hosts : DEFAULT_ALLOWED_HOSTS;
}

export function isDev(): boolean {
  return !!process.env.DUETO_DEV || !app.isPackaged;
}

// Resolved lazily because process.resourcesPath is only reliable after app is ready.
export function WEB_DIST_PATH(): string {
  if (isDev()) {
    return path.resolve(import.meta.dirname, '..', '..', '..', 'web', 'dist', 'web', 'browser');
  }
  return path.join(process.resourcesPath, 'web');
}

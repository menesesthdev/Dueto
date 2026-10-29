import { app, safeStorage } from 'electron';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';

interface PersistedState {
  appliedWallpaperId: string | null;
  appliedVersion: number;
  encryptedToken: string | null; // base64 of safeStorage.encryptString output
}

const STATE_FILE = path.join(app.getPath('userData'), 'dueto-state.json');

function loadState(): PersistedState {
  try {
    const raw = readFileSync(STATE_FILE, 'utf-8');
    const parsed = JSON.parse(raw) as Partial<PersistedState>;
    return {
      appliedWallpaperId: typeof parsed.appliedWallpaperId === 'string' ? parsed.appliedWallpaperId : null,
      appliedVersion: typeof parsed.appliedVersion === 'number' ? parsed.appliedVersion : 0,
      encryptedToken: typeof parsed.encryptedToken === 'string' ? parsed.encryptedToken : null,
    };
  } catch {
    return { appliedWallpaperId: null, appliedVersion: 0, encryptedToken: null };
  }
}

let state: PersistedState = loadState();

function saveState(): void {
  try {
    mkdirSync(path.dirname(STATE_FILE), { recursive: true });
    writeFileSync(STATE_FILE, JSON.stringify(state, null, 2));
  } catch (err) {
    console.error('[store] Failed to save state:', err);
  }
}

export function getAppliedState(): { wallpaperId: string | null; version: number } {
  return { wallpaperId: state.appliedWallpaperId, version: state.appliedVersion };
}

// Returns true if the state was updated (version is newer).
export function setAppliedState(wallpaperId: string, version: number): boolean {
  if (version <= state.appliedVersion) return false;
  state.appliedWallpaperId = wallpaperId;
  state.appliedVersion = version;
  saveState();
  return true;
}

export function getDeviceToken(): string | null {
  if (!state.encryptedToken) return null;
  if (!safeStorage.isEncryptionAvailable()) {
    // Fallback: plain text when encryption is unavailable (e.g., Linux without keyring).
    return state.encryptedToken;
  }
  try {
    return safeStorage.decryptString(Buffer.from(state.encryptedToken, 'base64'));
  } catch {
    return null;
  }
}

export function setDeviceToken(token: string): void {
  state.encryptedToken = safeStorage.isEncryptionAvailable()
    ? safeStorage.encryptString(token).toString('base64')
    : token;
  saveState();
}

export function clearDeviceToken(): void {
  state.encryptedToken = null;
  saveState();
}

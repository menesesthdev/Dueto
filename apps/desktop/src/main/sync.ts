import * as signalR from '@microsoft/signalr';
import { getApiBaseUrl, HUB_PATH } from './config.js';
import { getDeviceToken, getAppliedState } from './store.js';
import { applyWallpaperById } from './wallpaper-manager.js';
import { getMainWindow } from './window.js';

let connection: signalR.HubConnection | null = null;
let deviceId: string | null = null;

interface StateChangedEvent {
  wallpaperId: string;
  version: number;
  originDeviceId: string | null;
}

interface AppliedEntry {
  deviceId?: string;
  kind?: string;
}

function notifyRenderer(channel: string, data: unknown): void {
  getMainWindow()?.webContents.send(channel, data);
}

async function fetchOwnDeviceId(token: string): Promise<string | null> {
  try {
    const response = await fetch(`${getApiBaseUrl()}/api/state`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!response.ok) return null;
    const state = (await response.json()) as { applied?: AppliedEntry[] };
    return state.applied?.find((entry) => entry.kind === 'desktop')?.deviceId ?? null;
  } catch {
    return null;
  }
}

async function handleStateChanged(event: StateChangedEvent): Promise<void> {
  // No echo (rule 2).
  if (event.originDeviceId && event.originDeviceId === deviceId) return;

  // Version must be newer (rule 1).
  if (event.version <= getAppliedState().version) return;

  notifyRenderer('state-changed', event);

  await applyWallpaperById(event.wallpaperId, event.version);
}

export async function startSync(): Promise<void> {
  const token = getDeviceToken();
  if (!token || connection) return;

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${getApiBaseUrl()}${HUB_PATH}`, { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  connection.on('StateChanged', (event: StateChangedEvent) => {
    void handleStateChanged(event).catch((err) => console.error('[sync] Failed to handle event:', err));
  });

  connection.onreconnecting(() => notifyRenderer('connection-status', false));
  // The hub sends a snapshot on reconnect, handled by the StateChanged listener.
  connection.onreconnected(() => notifyRenderer('connection-status', true));
  connection.onclose(() => notifyRenderer('connection-status', false));

  try {
    await connection.start();
    notifyRenderer('connection-status', true);
    deviceId = await fetchOwnDeviceId(token);
  } catch (err) {
    console.error('[sync] Failed to connect:', err);
    notifyRenderer('connection-status', false);
  }
}

export async function stopSync(): Promise<void> {
  if (connection) {
    await connection.stop();
    connection = null;
  }
}

export function isConnected(): boolean {
  return connection?.state === signalR.HubConnectionState.Connected;
}

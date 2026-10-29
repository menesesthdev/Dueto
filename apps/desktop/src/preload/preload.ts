import { contextBridge, ipcRenderer } from 'electron';

interface StateChangedPayload {
  wallpaperId: string;
  version: number;
  originDeviceId?: string;
}

const bridge = {
  platform: 'electron' as const,

  onStateChanged(callback: (data: StateChangedPayload) => void): () => void {
    const handler = (_event: Electron.IpcRendererEvent, data: StateChangedPayload) => callback(data);
    ipcRenderer.on('state-changed', handler);
    return () => ipcRenderer.removeListener('state-changed', handler);
  },

  onConnectionStatus(callback: (connected: boolean) => void): () => void {
    const handler = (_event: Electron.IpcRendererEvent, connected: boolean) => callback(connected);
    ipcRenderer.on('connection-status', handler);
    return () => ipcRenderer.removeListener('connection-status', handler);
  },

  getAppliedState(): Promise<{ wallpaperId: string | null; version: number }> {
    return ipcRenderer.invoke('get-applied-state');
  },
};

contextBridge.exposeInMainWorld('dueto', bridge);

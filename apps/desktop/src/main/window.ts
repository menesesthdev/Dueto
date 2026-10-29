import { BrowserWindow, session } from 'electron';
import path from 'node:path';
import { isDev, DEV_SERVER_URL, PROTOCOL_SCHEME } from './config.js';

let mainWindow: BrowserWindow | null = null;
let isQuitting = false;
let cspInstalled = false;

const PROD_CSP = [
  "default-src 'self'",
  "script-src 'self'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: https:",
  "font-src 'self'",
  "connect-src 'self' wss: https:",
  "object-src 'none'",
  "base-uri 'self'",
  "frame-ancestors 'none'",
].join('; ');

function installCsp(): void {
  if (cspInstalled || isDev()) return;
  cspInstalled = true;
  session.defaultSession.webRequest.onHeadersReceived((details, callback) => {
    callback({
      responseHeaders: {
        ...details.responseHeaders,
        'Content-Security-Policy': [PROD_CSP],
      },
    });
  });
}

export function getMainWindow(): BrowserWindow | null {
  return mainWindow;
}

export function setQuitting(value: boolean): void {
  isQuitting = value;
}

export async function createWindow(): Promise<BrowserWindow> {
  installCsp();

  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    minWidth: 800,
    minHeight: 600,
    show: true,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      preload: path.join(import.meta.dirname, '..', 'preload', 'preload.js'),
    },
  });

  mainWindow.on('close', (event) => {
    if (!isQuitting) {
      event.preventDefault();
      mainWindow?.hide();
    }
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  if (isDev()) {
    await mainWindow.loadURL(DEV_SERVER_URL);
  } else {
    await mainWindow.loadURL(`${PROTOCOL_SCHEME}://dueto/index.html`);
  }

  return mainWindow;
}

export function showWindow(): void {
  if (mainWindow) {
    mainWindow.show();
    mainWindow.focus();
  }
}

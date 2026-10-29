import { app, ipcMain } from 'electron';
import { registerProtocol, registerProtocolPrivileges } from './protocol.js';
import { createWindow, getMainWindow } from './window.js';
import { setupTray } from './tray.js';
import { handleDeepLink } from './deep-link.js';
import { startSync } from './sync.js';
import { getAppliedState } from './store.js';
import { isDev, DEEP_LINK_SCHEME } from './config.js';

registerProtocolPrivileges();

const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
} else {
  if (!isDev()) {
    app.setAsDefaultProtocolClient(DEEP_LINK_SCHEME);
  }

  app.on('second-instance', (_event, argv) => {
    const win = getMainWindow();
    if (win) {
      if (win.isMinimized()) win.restore();
      win.show();
      win.focus();
    }
    const deepLinkUrl = argv.find((arg) => arg.startsWith(`${DEEP_LINK_SCHEME}://`));
    if (deepLinkUrl) {
      handleDeepLink(deepLinkUrl);
    }
  });

  app.on('open-url', (_event, url) => {
    handleDeepLink(url);
  });

  app.whenReady().then(async () => {
    registerProtocol();
    ipcMain.handle('get-applied-state', () => getAppliedState());
    await createWindow();
    setupTray();
    void startSync();
  });

  // The app lives in the tray (CLAUDE.md §12); the default would quit.
  app.on('window-all-closed', () => {});
}

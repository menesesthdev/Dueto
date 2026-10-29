import { Tray, Menu, nativeImage, app } from 'electron';
import path from 'node:path';
import { showWindow, setQuitting } from './window.js';

let tray: Tray | null = null;

export function setupTray(): void {
  const iconPath = app.isPackaged
    ? path.join(process.resourcesPath, 'icon.png')
    : path.join(import.meta.dirname, '..', '..', 'resources', 'icon.png');

  let icon = nativeImage.createFromPath(iconPath);
  icon = icon.isEmpty() ? nativeImage.createEmpty() : icon.resize({ width: 16, height: 16 });

  tray = new Tray(icon);
  tray.setToolTip('Dueto');

  const contextMenu = Menu.buildFromTemplate([
    { label: 'Mostrar Dueto', click: showWindow },
    { type: 'separator' },
    {
      label: 'Sair',
      click: () => {
        setQuitting(true);
        app.quit();
      },
    },
  ]);

  tray.setContextMenu(contextMenu);
  tray.on('double-click', showWindow);
}

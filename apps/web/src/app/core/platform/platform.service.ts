import { Injectable, InjectionToken, inject } from '@angular/core';
import type { DeviceKind } from '../api/models';

/** Ponte mínima que o preload do Electron expõe em `window.dueto` (Fase 4). Ausente no navegador. */
export interface DuetoDesktopBridge {
  readonly platform: 'electron';
}

declare global {
  interface Window {
    dueto?: DuetoDesktopBridge;
  }
  interface Navigator {
    /** Só existe no Safari do iOS: verdadeiro quando o PWA foi aberto pela tela de início. */
    standalone?: boolean;
  }
}

/** Injetável para os testes poderem simular outro navegador sem mexer no `window` real. */
export const WINDOW = new InjectionToken<Window>('WINDOW', { providedIn: 'root', factory: () => window });

export interface DetectedPlatform {
  kind: DeviceKind;
  deviceName: string;
  isIos: boolean;
}

/**
 * Decide o tipo de dispositivo a partir do ambiente. O tipo define o lado do pareamento (desktop gera o
 * código, celular digita) e quais telas aparecem. Função pura para ser testada sem navegador.
 */
export function detectPlatform(userAgent: string, maxTouchPoints: number, isElectron: boolean): DetectedPlatform {
  if (isElectron) {
    return { kind: 'desktop', deviceName: defaultDesktopName(userAgent), isIos: false };
  }

  if (/iPhone|iPod/.test(userAgent)) {
    return { kind: 'mobile', deviceName: 'iPhone', isIos: true };
  }

  // O iPadOS se apresenta como Mac; o que o entrega é a tela sensível ao toque.
  if (/iPad/.test(userAgent) || (/Macintosh/.test(userAgent) && maxTouchPoints > 1)) {
    return { kind: 'mobile', deviceName: 'iPad', isIos: true };
  }

  if (/Android/.test(userAgent)) {
    return { kind: 'mobile', deviceName: 'Android', isIos: false };
  }

  return { kind: 'desktop', deviceName: defaultDesktopName(userAgent), isIos: false };
}

function defaultDesktopName(userAgent: string): string {
  return /Macintosh/.test(userAgent) ? 'Meu Mac' : 'Meu PC';
}

/**
 * Único ponto que conhece o ambiente (navegador, PWA instalado ou Electron). Os componentes perguntam
 * aqui em vez de espalhar `if (electron)` pelo código (CLAUDE.md §11).
 */
@Injectable({ providedIn: 'root' })
export class PlatformService {
  private readonly win = inject(WINDOW);
  private readonly detected = detectPlatform(
    this.win.navigator.userAgent,
    this.win.navigator.maxTouchPoints ?? 0,
    this.win.dueto?.platform === 'electron',
  );

  readonly isElectron = this.win.dueto?.platform === 'electron';
  readonly deviceKind: DeviceKind = this.detected.kind;
  readonly defaultDeviceName: string = this.detected.deviceName;
  readonly isIos: boolean = this.detected.isIos;

  /** Verdadeiro quando o app foi instalado (tela de início do iPhone ou janela própria do PWA). */
  get isInstalledPwa(): boolean {
    return this.win.navigator.standalone === true || this.win.matchMedia?.('(display-mode: standalone)').matches === true;
  }
}

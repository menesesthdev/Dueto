import { Injectable, InjectionToken, computed, inject, signal } from '@angular/core';
import type { DeviceKind } from '../api/models';

/** Pareamento iniciado neste aparelho que ainda espera o outro lado digitar o código. */
export interface PendingPairing {
  code: string;
  expiresAt: string;
}

export interface Session {
  /** Token do dispositivo. É a única credencial: não há conta nem senha. */
  token: string;
  kind: DeviceKind;
  deviceName: string;
  /** Preenchido só enquanto o par não foi formado. Nulo depois que os dois lados se conectaram. */
  pairing: PendingPairing | null;
}

const STORAGE_KEY = 'dueto.session';

/**
 * Storage injetável. Pode ser nulo: em janela privada, com dados de site bloqueados ou em prévias,
 * o acesso ao localStorage lança exceção, e o app precisa continuar funcionando (sem lembrar da sessão).
 */
export const SESSION_STORAGE = new InjectionToken<Storage | null>('SESSION_STORAGE', {
  providedIn: 'root',
  factory: () => {
    try {
      return window.localStorage;
    } catch {
      return null;
    }
  },
});

function isSession(value: unknown): value is Session {
  const candidate = value as Partial<Session> | null;
  return (
    typeof candidate === 'object' &&
    candidate !== null &&
    typeof candidate.token === 'string' &&
    candidate.token.length > 0 &&
    (candidate.kind === 'desktop' || candidate.kind === 'mobile') &&
    typeof candidate.deviceName === 'string'
  );
}

/** Estado da sessão do dispositivo, persistido para sobreviver a um recarregamento. */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly storage = inject(SESSION_STORAGE);
  private readonly state = signal<Session | null>(this.load());

  readonly session = this.state.asReadonly();
  readonly token = computed(() => this.state()?.token ?? null);
  /** Pareado de verdade: tem token e o outro lado já se conectou. */
  readonly isPaired = computed(() => {
    const session = this.state();
    return session !== null && session.pairing === null;
  });

  /** Guarda o token logo após `pairing/start`, antes de o outro lado digitar o código. */
  startPending(token: string, kind: DeviceKind, deviceName: string, pairing: PendingPairing): void {
    this.set({ token, kind, deviceName, pairing });
  }

  /** Guarda o token de quem concluiu o pareamento digitando o código. */
  savePaired(token: string, kind: DeviceKind, deviceName: string): void {
    this.set({ token, kind, deviceName, pairing: null });
  }

  /** O outro lado se conectou: o par está formado. */
  markPaired(): void {
    const current = this.state();
    if (current) {
      this.set({ ...current, pairing: null });
    }
  }

  clear(): void {
    this.state.set(null);
    try {
      this.storage?.removeItem(STORAGE_KEY);
    } catch {
      // Sem storage disponível: a sessão só vive em memória.
    }
  }

  private set(session: Session): void {
    this.state.set(session);
    try {
      this.storage?.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      // Sem storage disponível: a sessão só vive em memória.
    }
  }

  private load(): Session | null {
    try {
      const raw = this.storage?.getItem(STORAGE_KEY);
      if (!raw) {
        return null;
      }

      const parsed: unknown = JSON.parse(raw);
      return isSession(parsed) ? { ...parsed, pairing: parsed.pairing ?? null } : null;
    } catch {
      return null;
    }
  }
}

import { firstValueFrom } from 'rxjs';
import { DestroyRef, Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { describeError } from '../../core/api/api-error';
import type { ApiErrorInfo } from '../../core/api/api-error';
import { PairingApi } from '../../core/api/pairing.api';
import { StateApi } from '../../core/api/state.api';
import { PlatformService } from '../../core/platform/platform.service';
import { SessionStore } from '../../core/session/session.store';

export type PairingMode = 'generate' | 'enter';
export type PairingPhase = 'idle' | 'working' | 'waiting' | 'connected';

export const CODE_LENGTH = 6;
export const MAX_DEVICE_NAME_LENGTH = 100;

const POLL_INTERVAL_MS = 2_000;
const TICK_INTERVAL_MS = 1_000;

/** Mensagem para o usuário a partir do erro da API. Nunca mostra detalhe técnico (CLAUDE.md §8). */
export function pairingErrorMessage(info: ApiErrorInfo, action: 'generate' | 'enter'): string {
  switch (info.status) {
    case 400:
      return action === 'enter'
        ? 'Código inválido ou expirado. Confira o número no seu PC ou gere um novo código.'
        : 'Não foi possível gerar o código. Confira o nome do aparelho e tente de novo.';
    case 409:
      return 'Este par já tem um aparelho desse tipo. Gere o código no outro aparelho.';
    case 429:
      return 'Muitas tentativas seguidas. Aguarde um minuto e tente de novo.';
    case null:
      return 'Sem conexão com o Dueto. Verifique sua internet e tente de novo.';
    default:
      return 'Algo deu errado do nosso lado. Tente de novo em instantes.';
  }
}

/**
 * Fluxo de pareamento de um aparelho. Qualquer lado pode iniciar (CLAUDE.md §8): quem gera mostra o código
 * de 6 dígitos e espera o outro digitar; quem tem o código digita e conclui na hora.
 *
 * Provido no nível da página (não é singleton): cada visita à tela começa um fluxo novo.
 */
@Injectable()
export class PairingFlow {
  private readonly platform = inject(PlatformService);
  private readonly session = inject(SessionStore);
  private readonly pairingApi = inject(PairingApi);
  private readonly stateApi = inject(StateApi);

  private readonly expiresAt = signal<number | null>(null);
  private readonly ttlMs = signal(0);
  private readonly now = signal(Date.now());
  private tickTimer: ReturnType<typeof setInterval> | null = null;
  private pollTimer: ReturnType<typeof setInterval> | null = null;

  /** O desktop gera o código; o celular digita (o link secundário inverte). */
  readonly mode = signal<PairingMode>(this.platform.deviceKind === 'desktop' ? 'generate' : 'enter');
  readonly phase = signal<PairingPhase>('idle');
  readonly code = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly remainingSeconds = computed(() => {
    const expires = this.expiresAt();
    return expires === null ? 0 : Math.max(0, Math.ceil((expires - this.now()) / 1000));
  });
  /** Fração do tempo que ainda resta, de 1 (acabou de gerar) a 0 (expirou). */
  readonly fractionLeft = computed(() => {
    const ttl = this.ttlMs();
    return ttl === 0 ? 0 : Math.min(1, (this.remainingSeconds() * 1000) / ttl);
  });
  readonly expired = computed(() => this.phase() === 'waiting' && this.remainingSeconds() === 0);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stopTimers());

    // Se a sessão sumir enquanto esperamos (token revogado, 401), o código não vale mais: gera outro.
    effect(() => {
      if (this.phase() === 'waiting' && this.session.token() === null) {
        untracked(() => void this.generate());
      }
    });
  }

  /** Chamado ao abrir a tela: retoma um pareamento pendente ou, no desktop, já gera o código. */
  begin(): void {
    const pending = this.session.session()?.pairing;
    if (pending && this.mode() === 'generate') {
      const expires = Date.parse(pending.expiresAt);
      if (expires > Date.now()) {
        this.startWaiting(pending.code, expires, null);
        return;
      }

      this.session.clear();
    }

    if (this.mode() === 'generate') {
      void this.generate();
    }
  }

  switchMode(mode: PairingMode): void {
    if (this.mode() === mode) {
      return;
    }

    this.stopTimers();
    this.error.set(null);
    this.phase.set('idle');
    this.mode.set(mode);

    if (mode === 'generate') {
      this.begin();
    }
  }

  /** Cria um par novo e mostra o código. Também serve para "gerar novo código" depois de expirar. */
  async generate(): Promise<void> {
    this.stopTimers();
    this.error.set(null);
    this.phase.set('working');

    const deviceName = this.platform.defaultDeviceName;
    const kind = this.platform.deviceKind;

    try {
      const started = await firstValueFrom(this.pairingApi.start({ deviceName, kind }));
      const expires = Date.parse(started.expiresAt);
      // O token precisa ser guardado já: é com ele que perguntamos se o outro lado se conectou.
      this.session.startPending(started.deviceToken, kind, deviceName, { code: started.code, expiresAt: started.expiresAt });
      this.startWaiting(started.code, expires, Math.max(0, expires - Date.now()));
    } catch (error) {
      this.phase.set('idle');
      this.error.set(pairingErrorMessage(describeError(error), 'generate'));
    }
  }

  /** Conclui o pareamento com o código que o outro aparelho está mostrando. */
  async complete(code: string, deviceName: string): Promise<void> {
    const name = deviceName.trim();
    if (!/^\d{6}$/.test(code)) {
      this.error.set(`Digite os ${CODE_LENGTH} dígitos do código.`);
      return;
    }

    if (name.length === 0 || name.length > MAX_DEVICE_NAME_LENGTH) {
      this.error.set(`Dê um nome ao aparelho, com até ${MAX_DEVICE_NAME_LENGTH} caracteres.`);
      return;
    }

    this.error.set(null);
    this.phase.set('working');
    const kind = this.platform.deviceKind;

    try {
      const completed = await firstValueFrom(this.pairingApi.complete({ code, deviceName: name, kind }));
      this.session.savePaired(completed.deviceToken, kind, name);
      this.phase.set('connected');
    } catch (error) {
      this.phase.set('idle');
      this.error.set(pairingErrorMessage(describeError(error), 'enter'));
    }
  }

  private startWaiting(code: string, expires: number, ttlMs: number | null): void {
    this.code.set(code);
    this.expiresAt.set(expires);
    this.ttlMs.set(ttlMs ?? Math.max(1, expires - Date.now()));
    this.now.set(Date.now());
    this.phase.set('waiting');

    this.tickTimer = setInterval(() => this.now.set(Date.now()), TICK_INTERVAL_MS);
    this.pollTimer = setInterval(() => void this.checkPartner(), POLL_INTERVAL_MS);
  }

  /** O outro lado se conectou quando o estado do par passa a listar um aparelho do outro tipo. */
  private async checkPartner(): Promise<void> {
    if (this.expired()) {
      return;
    }

    try {
      const snapshot = await firstValueFrom(this.stateApi.get());
      const ownKind = this.session.session()?.kind;
      if (this.phase() === 'waiting' && snapshot.applied.some((device) => device.kind !== ownKind)) {
        this.stopTimers();
        this.session.markPaired();
        this.phase.set('connected');
      }
    } catch {
      // Falha momentânea de rede: a próxima consulta tenta de novo. Um 401 é tratado pelo interceptor.
    }
  }

  private stopTimers(): void {
    if (this.tickTimer !== null) {
      clearInterval(this.tickTimer);
      this.tickTimer = null;
    }

    if (this.pollTimer !== null) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }
}


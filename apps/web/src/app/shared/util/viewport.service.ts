import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { WINDOW } from '../../core/platform/platform.service';

/** Largura a partir da qual a interface vira a do desktop (barra lateral). Igual ao breakpoint "tablet" do design. */
export const WIDE_BREAKPOINT_PX = 834;

/** Informa se a tela é larga (layout de desktop) ou estreita (layout de celular), e reage a mudanças. */
@Injectable({ providedIn: 'root' })
export class ViewportService {
  private readonly query = inject(WINDOW).matchMedia?.(`(min-width: ${WIDE_BREAKPOINT_PX}px)`);
  private readonly wide = signal(this.query?.matches ?? true);

  readonly isWide = this.wide.asReadonly();

  constructor() {
    const listener = (event: MediaQueryListEvent) => this.wide.set(event.matches);
    this.query?.addEventListener('change', listener);
    inject(DestroyRef).onDestroy(() => this.query?.removeEventListener('change', listener));
  }
}

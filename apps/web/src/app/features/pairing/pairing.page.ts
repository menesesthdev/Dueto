import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { PlatformService } from '../../core/platform/platform.service';
import { SessionStore } from '../../core/session/session.store';
import { IconComponent } from '../../shared/ui/icon';
import { LogoMarkComponent } from '../../shared/ui/logo-mark';
import { ViewportService } from '../../shared/util/viewport.service';
import { OtpInputComponent } from './otp-input';
import { CODE_LENGTH, MAX_DEVICE_NAME_LENGTH, PairingFlow } from './pairing-flow';

/** Formata segundos como m:ss (ex.: 272 → "4:32"). */
export function formatCountdown(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

@Component({
  selector: 'dt-pairing-page',
  imports: [IconComponent, LogoMarkComponent, OtpInputComponent],
  providers: [PairingFlow],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pairing.page.html',
  styleUrl: './pairing.page.scss',
})
export class PairingPage {
  protected readonly flow = inject(PairingFlow);
  protected readonly wide = inject(ViewportService).isWide;
  protected readonly session = inject(SessionStore);
  private readonly router = inject(Router);

  protected readonly codeLength = CODE_LENGTH;
  protected readonly maxNameLength = MAX_DEVICE_NAME_LENGTH;
  protected readonly typedCode = signal('');
  protected readonly deviceName = signal(inject(PlatformService).defaultDeviceName);

  protected readonly working = computed(() => this.flow.phase() === 'working');
  protected readonly canSubmit = computed(
    () => this.typedCode().length === CODE_LENGTH && this.deviceName().trim().length > 0 && !this.working(),
  );
  protected readonly digits = computed(() => (this.flow.code() ?? '').split(''));
  /** O selo da tela final fala do OUTRO aparelho, o que acabou de se conectar. */
  protected readonly partnerLabel = computed(() => (this.session.session()?.kind === 'desktop' ? 'Seu iPhone' : 'Seu PC'));
  protected readonly countdown = computed(() => formatCountdown(this.flow.remainingSeconds()));

  constructor() {
    this.flow.begin();
  }

  protected submit(): void {
    if (this.canSubmit()) {
      void this.flow.complete(this.typedCode(), this.deviceName());
    }
  }

  protected onNameInput(event: Event): void {
    this.deviceName.set((event.target as HTMLInputElement).value);
  }

  protected goToCatalog(): void {
    void this.router.navigateByUrl('/');
  }
}

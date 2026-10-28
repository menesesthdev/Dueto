import { ChangeDetectionStrategy, Component, ElementRef, computed, input, model, viewChildren } from '@angular/core';
import { CODE_LENGTH } from './pairing-flow';

/**
 * Aplica o que foi digitado (ou colado) à sequência de dígitos. A sequência é sempre contígua: não existe
 * "buraco" no meio, o que simplifica o foco e o apagar. Função pura para ser testada sem DOM.
 */
export function applyTyped(current: string, typed: string, length: number): string {
  const digits = typed.replace(/\D/g, '');
  if (digits.length === 0) {
    return current;
  }

  // Várias teclas de uma vez (colar ou preencher automático do SMS) substituem o que havia antes.
  return digits.length > 1 ? digits.slice(0, length) : (current + digits).slice(0, length);
}

@Component({
  selector: 'dt-otp-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './otp-input.scss',
  template: `
    <div class="otp" role="group" [attr.aria-label]="'Código de ' + length() + ' dígitos'">
      @for (index of positions(); track index) {
        <input
          #box
          class="otp__box"
          [class.otp__box--invalid]="invalid()"
          type="text"
          inputmode="numeric"
          pattern="[0-9]*"
          maxlength="6"
          [attr.autocomplete]="index === 0 ? 'one-time-code' : 'off'"
          [attr.aria-label]="'Dígito ' + (index + 1) + ' de ' + length()"
          [attr.aria-invalid]="invalid()"
          [disabled]="disabled()"
          [value]="digitAt(index)"
          (focus)="redirectFocus(index)"
          (input)="onInput($event, index)"
          (keydown)="onKeydown($event)"
          (paste)="onPaste($event)"
        />
      }
    </div>
  `,
})
export class OtpInputComponent {
  readonly length = input(CODE_LENGTH);
  readonly disabled = input(false);
  readonly invalid = input(false);
  /** Dígitos digitados até agora (de 0 a `length`), sem buracos. */
  readonly value = model('');

  private readonly boxes = viewChildren<ElementRef<HTMLInputElement>>('box');
  protected readonly positions = computed(() => Array.from({ length: this.length() }, (_, index) => index));

  protected digitAt(index: number): string {
    return this.value()[index] ?? '';
  }

  /** Clicar numa caixa à frente leva o foco para a primeira vazia, mantendo a sequência contígua. */
  protected redirectFocus(index: number): void {
    const target = Math.min(this.value().length, this.length() - 1);
    if (index !== target) {
      this.focusBox(target);
    }
  }

  protected onInput(event: Event, index: number): void {
    const element = event.target as HTMLInputElement;
    // A caixa pode já conter um dígito: o que o usuário digitou é o que veio depois dele.
    const previous = this.digitAt(index);
    const typed = previous && element.value.startsWith(previous) ? element.value.slice(previous.length) : element.value;
    const next = applyTyped(this.value(), typed, this.length());
    element.value = previous; // o DOM volta ao valor do modelo; o modelo manda
    this.update(next);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Backspace') {
      event.preventDefault();
      this.update(this.value().slice(0, -1));
    }
  }

  protected onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    this.update(applyTyped(this.value(), event.clipboardData?.getData('text') ?? '', this.length()));
  }

  private update(next: string): void {
    this.value.set(next);
    queueMicrotask(() => this.focusBox(Math.min(next.length, this.length() - 1)));
  }

  private focusBox(index: number): void {
    this.boxes()[index]?.nativeElement.focus();
  }
}

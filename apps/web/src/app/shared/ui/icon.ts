import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type IconName = 'grid' | 'play' | 'monitor' | 'phone' | 'check' | 'eye' | 'search' | 'refresh' | 'warning' | 'arrow' | 'close' | 'info';

/**
 * Ícones de traço do Dueto (24×24, traço 2). Decorativos: o significado vem do texto ao lado, então ficam
 * escondidos dos leitores de tela. Quando o ícone é a única informação, o botão pai precisa de rótulo.
 */
@Component({
  selector: 'dt-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true', '[style.width.px]': 'size()', '[style.height.px]': 'size()' },
  styles: `
    :host {
      display: inline-flex;
      flex-shrink: 0;
    }
    svg {
      display: block;
      height: 100%;
      width: 100%;
    }
  `,
  template: `
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" [attr.stroke-width]="stroke()" stroke-linecap="round" stroke-linejoin="round">
      @switch (name()) {
        @case ('grid') {
          <rect x="3" y="3" width="7" height="7" rx="2" />
          <rect x="14" y="3" width="7" height="7" rx="2" />
          <rect x="3" y="14" width="7" height="7" rx="2" />
          <rect x="14" y="14" width="7" height="7" rx="2" />
        }
        @case ('play') {
          <circle cx="12" cy="12" r="8" />
          <path d="M10 9l5 3-5 3V9z" fill="currentColor" />
        }
        @case ('monitor') {
          <rect x="3" y="4" width="18" height="12" rx="2" />
          <path d="M9 20h6" />
        }
        @case ('phone') {
          <rect x="7" y="2" width="10" height="20" rx="3" />
        }
        @case ('check') {
          <path d="M5 13l4 4L19 7" />
        }
        @case ('eye') {
          <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" />
          <circle cx="12" cy="12" r="3" />
        }
        @case ('search') {
          <circle cx="11" cy="11" r="6.5" />
          <path d="M16 16l4 4" />
        }
        @case ('refresh') {
          <path d="M20 12a8 8 0 11-2.6-5.9M20 4v5h-5" />
        }
        @case ('warning') {
          <circle cx="12" cy="12" r="9" />
          <path d="M12 7.5v5.5M12 16.2v.1" />
        }
        @case ('arrow') {
          <path d="M1 12h18M14 7l5 5-5 5" />
        }
        @case ('close') {
          <path d="M6 6l12 12M18 6L6 18" />
        }
        @case ('info') {
          <circle cx="12" cy="12" r="9" />
          <path d="M12 11v5M12 7.8v.1" />
        }
      }
    </svg>
  `,
})
export class IconComponent {
  readonly name = input.required<IconName>();
  readonly size = input(18);
  readonly stroke = input(2);
}

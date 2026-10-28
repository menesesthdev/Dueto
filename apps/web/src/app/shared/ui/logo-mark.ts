import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Símbolo do Dueto no tratamento "B Sólido two-tone": traços brancos e setas azul-gelo, dentro de um
 * container azul-marinho escuro (o azul-gelo some sobre o azure do gradiente da marca).
 */
@Component({
  selector: 'dt-logo-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'img', 'aria-label': 'Dueto', '[style.width.px]': 'size()', '[style.height.px]': 'size()', '[style.border-radius.px]': 'size() * 0.27' },
  styles: `
    :host {
      align-items: center;
      background: var(--gradient-logo);
      box-shadow: 0 4px 10px -2px rgb(10 42 74 / 35%);
      display: inline-flex;
      flex-shrink: 0;
      justify-content: center;
    }
  `,
  template: `
    <svg [attr.width]="size() * 0.66" [attr.height]="size() * 0.44" viewBox="0 0 120 80" aria-hidden="true">
      <rect x="17" y="22" width="8" height="36" rx="4" fill="#fff" />
      <rect x="33" y="22" width="8" height="36" rx="4" fill="#fff" />
      <rect x="79" y="22" width="8" height="36" rx="4" fill="#fff" />
      <rect x="95" y="22" width="8" height="36" rx="4" fill="#fff" />
      <path d="M49 32 h14 v-5 l9 7.5 -9 7.5 v-5 h-14 z" fill="#5ac8fa" />
      <path d="M71 48 h-14 v-5 l-9 7.5 9 7.5 v-5 h14 z" fill="#5ac8fa" />
    </svg>
  `,
})
export class LogoMarkComponent {
  readonly size = input(30);
}

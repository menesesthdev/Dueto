import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ThemeService, type ThemePreference } from '../../core/theme/theme.service';
import { IconComponent, type IconName } from './icon';

@Component({
  selector: 'dt-theme-toggle',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      align-items: center;
      backdrop-filter: blur(12px);
      background: rgb(var(--shadow-base) / 6%);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-pill);
      display: inline-flex;
      gap: 2px;
      padding: 2px;
      width: fit-content;
    }
    button {
      align-items: center;
      background: none;
      border: 0;
      border-radius: var(--radius-pill);
      color: var(--text-faint);
      cursor: pointer;
      display: flex;
      height: 26px;
      justify-content: center;
      transition: background var(--duration-fast) var(--ease-smooth),
        color var(--duration-fast) var(--ease-smooth),
        box-shadow var(--duration-fast) var(--ease-smooth);
      width: 26px;
    }
    button:hover {
      color: var(--text-secondary);
    }
    button.active {
      background: var(--surface-raised);
      box-shadow: 0 1px 3px rgb(var(--shadow-base) / 14%),
        0 0 0 1px var(--border-subtle);
      color: var(--text-primary);
    }
  `,
  template: `
    @for (opt of options; track opt.value) {
      <button type="button"
        [class.active]="theme.preference() === opt.value"
        [attr.aria-label]="opt.label"
        [attr.aria-pressed]="theme.preference() === opt.value"
        (click)="theme.set(opt.value)">
        <dt-icon [name]="opt.icon" [size]="14" />
      </button>
    }
  `,
})
export class ThemeToggleComponent {
  protected readonly theme = inject(ThemeService);

  protected readonly options: { value: ThemePreference; icon: IconName; label: string }[] = [
    { value: 'light', icon: 'sun', label: 'Tema claro' },
    { value: 'auto', icon: 'system', label: 'Tema do sistema' },
    { value: 'dark', icon: 'moon', label: 'Tema escuro' },
  ];
}

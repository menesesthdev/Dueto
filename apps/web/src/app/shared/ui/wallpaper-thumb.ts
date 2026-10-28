import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { gradientFor } from '../util/wallpaper-gradient';

/**
 * Miniatura de um wallpaper. O gradiente Tidal fica por baixo e aparece enquanto a imagem carrega ou se ela
 * falhar, então a grade nunca mostra um buraco nem um ícone de imagem quebrada.
 */
@Component({
  selector: 'dt-wallpaper-thumb',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[style.background]': 'gradient()' },
  styles: `
    :host {
      display: block;
      overflow: clip;
      position: relative;
    }
    img {
      height: 100%;
      inset: 0;
      object-fit: cover;
      opacity: 0;
      position: absolute;
      transition: opacity 200ms;
      width: 100%;
    }
    img.loaded {
      opacity: 1;
    }
  `,
  template: `
    @if (!failed()) {
      <img [src]="src()" alt="" loading="lazy" decoding="async" [class.loaded]="loaded()" (load)="loaded.set(true)" (error)="failed.set(true)" />
    }
  `,
})
export class WallpaperThumbComponent {
  readonly wallpaperId = input.required<string>();
  readonly src = input.required<string>();

  protected readonly loaded = signal(false);
  protected readonly failed = signal(false);
  protected readonly gradient = computed(() => gradientFor(this.wallpaperId()));
}

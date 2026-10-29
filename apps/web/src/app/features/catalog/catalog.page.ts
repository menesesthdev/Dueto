import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import type { Wallpaper } from '../../core/api/models';
import { SessionStore } from '../../core/session/session.store';
import { IconComponent } from '../../shared/ui/icon';
import { LogoMarkComponent } from '../../shared/ui/logo-mark';
import { ThemeToggleComponent } from '../../shared/ui/theme-toggle';
import { WallpaperThumbComponent } from '../../shared/ui/wallpaper-thumb';
import { ViewportService } from '../../shared/util/viewport.service';
import { CatalogStore } from './catalog-store';

/** "Estático · paisagem, escuro" — o protótipo mostrava resolução, que a API não tem (decisão do dono). */
export function wallpaperSubtitle(wallpaper: Wallpaper): string {
  if (wallpaper.kind === 'animated') {
    return 'Animado · só PC';
  }

  return wallpaper.tags.length > 0 ? `Estático · ${wallpaper.tags.slice(0, 2).join(', ')}` : 'Estático';
}

@Component({
  selector: 'dt-catalog-page',
  imports: [NgTemplateOutlet, IconComponent, LogoMarkComponent, ThemeToggleComponent, WallpaperThumbComponent],
  providers: [CatalogStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './catalog.page.html',
  styleUrl: './catalog.page.scss',
})
export class CatalogPage {
  protected readonly store = inject(CatalogStore);
  protected readonly wide = inject(ViewportService).isWide;
  protected readonly session = inject(SessionStore);
  protected readonly subtitle = wallpaperSubtitle;

  protected readonly featuredId = computed(() => this.store.featured()?.id ?? null);
  protected readonly selfName = computed(() => this.session.session()?.deviceName ?? 'Este aparelho');
  protected readonly partnerLabel = computed(() => (this.session.session()?.kind === 'desktop' ? 'iPhone' : 'PC'));

  /** Linha sob o título do destaque, sempre a partir de dados reais do estado. */
  protected readonly featuredCaption = computed(() => {
    const featured = this.store.featured();
    if (!featured) {
      return '';
    }

    const kind = featured.kind === 'animated' ? 'Animado · só PC' : 'Estático';
    return this.store.featuredIsCurrent() ? `${kind} · definido no seu par` : kind;
  });

  constructor() {
    void this.store.load();
  }

  protected onSearch(event: Event): void {
    this.store.query.set((event.target as HTMLInputElement).value);
  }
}

import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { describeError } from '../../core/api/api-error';
import type { StateSnapshot, Wallpaper, WallpaperKind } from '../../core/api/models';
import { StateApi } from '../../core/api/state.api';
import { WallpapersApi } from '../../core/api/wallpapers.api';
import { PlatformService } from '../../core/platform/platform.service';

export type LoadStatus = 'loading' | 'ready' | 'error';

/** Filtro por texto: título ou tags, sem diferenciar maiúsculas nem acentos. */
export function matchesQuery(wallpaper: Wallpaper, query: string): boolean {
  const needle = normalize(query.trim());
  if (needle.length === 0) {
    return true;
  }

  return normalize(wallpaper.title).includes(needle) || wallpaper.tags.some((tag) => normalize(tag).includes(needle));
}

function normalize(text: string): string {
  return text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

/** Mensagem para quem tentou definir o wallpaper. Nunca mostra detalhe técnico. */
export function setWallpaperErrorMessage(status: number | null): string {
  switch (status) {
    case 422:
      return 'Wallpapers animados só podem ser usados no PC.';
    case 404:
      return 'Este wallpaper não está mais no catálogo.';
    case 429:
      return 'Muitas trocas seguidas. Aguarde um instante.';
    case null:
      return 'Sem conexão com o Dueto. Tente de novo.';
    default:
      return 'Não foi possível definir o wallpaper. Tente de novo.';
  }
}

/** Estado da tela de catálogo: o catálogo, o estado do par e a troca de wallpaper. */
@Injectable()
export class CatalogStore {
  private readonly wallpapersApi = inject(WallpapersApi);
  private readonly stateApi = inject(StateApi);
  private readonly platform = inject(PlatformService);

  readonly status = signal<LoadStatus>('loading');
  readonly wallpapers = signal<Wallpaper[]>([]);
  readonly state = signal<StateSnapshot | null>(null);
  readonly kind = signal<WallpaperKind>('static');
  readonly query = signal('');
  /** Wallpaper em destaque escolhido pelo usuário. Nulo = mostrar o atual do par. */
  readonly selectedId = signal<string | null>(null);
  readonly setting = signal(false);
  readonly setError = signal<string | null>(null);

  /** O celular só usa estáticos: animado é só do desktop (CLAUDE.md §1). */
  readonly isMobile = this.platform.deviceKind === 'mobile';

  readonly staticCount = computed(() => this.wallpapers().filter((w) => w.kind === 'static').length);
  readonly animatedCount = computed(() => this.wallpapers().filter((w) => w.kind === 'animated').length);

  readonly visible = computed(() => {
    const kind = this.isMobile ? 'static' : this.kind();
    const query = this.query();
    return this.wallpapers().filter((w) => w.kind === kind && matchesQuery(w, query));
  });

  readonly current = computed(() => {
    const id = this.state()?.wallpaperId;
    return id ? (this.wallpapers().find((w) => w.id === id) ?? null) : null;
  });

  /**
   * O que aparece no destaque: a escolha do usuário; senão o atual do par, se for do tipo da aba aberta;
   * senão o primeiro da lista. Assim a aba "Animados" nunca destaca um estático, e vice-versa.
   */
  readonly featured = computed(() => {
    const selected = this.selectedId();
    const chosen = selected ? this.wallpapers().find((w) => w.id === selected) : null;
    if (chosen) {
      return chosen;
    }

    const current = this.current();
    const tabKind = this.isMobile ? 'static' : this.kind();
    return (current?.kind === tabKind ? current : null) ?? this.visible()[0] ?? null;
  });

  readonly featuredIsCurrent = computed(() => {
    const featured = this.featured();
    return featured !== null && featured.id === this.state()?.wallpaperId;
  });

  /** Este aparelho e o outro do par, a partir do estado. */
  readonly partnerConnected = computed(() => {
    const ownKind = this.platform.deviceKind;
    return this.state()?.applied.some((device) => device.kind !== ownKind) ?? false;
  });

  async load(): Promise<void> {
    this.status.set('loading');
    try {
      const [wallpapers, state] = await Promise.all([
        firstValueFrom(this.wallpapersApi.list()),
        firstValueFrom(this.stateApi.get()),
      ]);
      this.wallpapers.set(wallpapers);
      this.state.set(state);
      this.status.set('ready');
    } catch {
      this.status.set('error');
    }
  }

  select(id: string): void {
    this.selectedId.set(id);
    this.setError.set(null);
  }

  setKind(kind: WallpaperKind): void {
    this.kind.set(kind);
    this.selectedId.set(null);
  }

  /** Define o wallpaper em destaque para o par. O outro aparelho é avisado pelo servidor. */
  async setFeatured(): Promise<boolean> {
    const featured = this.featured();
    if (!featured || this.setting()) {
      return false;
    }

    if (this.isMobile && featured.kind === 'animated') {
      this.setError.set(setWallpaperErrorMessage(422));
      return false;
    }

    this.setting.set(true);
    this.setError.set(null);
    try {
      await firstValueFrom(this.stateApi.set(featured.id));
      this.state.set(await firstValueFrom(this.stateApi.get()));
      this.selectedId.set(null);
      return true;
    } catch (error) {
      this.setError.set(setWallpaperErrorMessage(describeError(error).status));
      return false;
    } finally {
      this.setting.set(false);
    }
  }
}

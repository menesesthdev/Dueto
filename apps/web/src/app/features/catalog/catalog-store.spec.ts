import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import type { StateSnapshot, Wallpaper } from '../../core/api/models';
import { API_BASE_URL } from '../../core/http/api-base-url';
import { PlatformService } from '../../core/platform/platform.service';
import { CatalogStore, matchesQuery, setWallpaperErrorMessage } from './catalog-store';

function wallpaper(id: string, kind: 'static' | 'animated' = 'static', title = id, tags: string[] = []): Wallpaper {
  return {
    id,
    title,
    kind,
    desktopUrl: '',
    desktopSha256: '',
    mobileUrl: kind === 'static' ? '' : null,
    mobileSha256: null,
    thumbnailUrl: '',
    tags,
    createdAt: '2026-10-01T12:00:00Z',
  };
}

const CATALOG = [wallpaper('aurora', 'static', 'Aurora Dunes', ['landscape']), wallpaper('neon', 'static', 'Neon Tide'), wallpaper('drift', 'animated', 'Drift Loop')];

function state(wallpaperId: string | null, applied: StateSnapshot['applied'] = []): StateSnapshot {
  return { wallpaperId, version: wallpaperId ? 1 : 0, originDeviceId: null, applied };
}

async function setup(kind: 'desktop' | 'mobile', snapshot: StateSnapshot) {
  TestBed.configureTestingModule({
    providers: [
      CatalogStore,
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: '' },
      { provide: PlatformService, useValue: { deviceKind: kind, defaultDeviceName: kind === 'desktop' ? 'Meu PC' : 'iPhone' } },
    ],
  });
  const store = TestBed.inject(CatalogStore);
  const http = TestBed.inject(HttpTestingController);
  const loading = store.load();
  http.expectOne('/api/wallpapers').flush(CATALOG);
  http.expectOne('/api/state').flush(snapshot);
  await loading;
  return { store, http };
}

describe('matchesQuery', () => {
  it('matches the title ignoring case and accents', () => {
    expect(matchesQuery(wallpaper('a', 'static', 'Céu Ártico'), 'ceu art')).toBe(true);
  });

  it('matches tags', () => {
    expect(matchesQuery(wallpaper('a', 'static', 'X', ['paisagem']), 'PAIS')).toBe(true);
  });

  it('an empty query matches everything', () => {
    expect(matchesQuery(wallpaper('a'), '   ')).toBe(true);
  });

  it('does not match unrelated text', () => {
    expect(matchesQuery(wallpaper('a', 'static', 'Aurora'), 'neon')).toBe(false);
  });
});

describe('setWallpaperErrorMessage', () => {
  it.each([
    [422, 'animados'],
    [404, 'não está mais'],
    [429, 'Aguarde'],
    [null, 'Sem conexão'],
    [500, 'Tente de novo'],
  ])('explains status %s', (status, fragment) => {
    expect(setWallpaperErrorMessage(status)).toContain(fragment);
  });
});

describe('CatalogStore', () => {
  it('loads the catalog and the pair state', async () => {
    const { store } = await setup('desktop', state('neon'));

    expect(store.status()).toBe('ready');
    expect(store.staticCount()).toBe(2);
    expect(store.animatedCount()).toBe(1);
    expect(store.current()?.id).toBe('neon');
  });

  it('features the current wallpaper of the pair', async () => {
    const { store } = await setup('desktop', state('neon'));

    expect(store.featured()?.id).toBe('neon');
    expect(store.featuredIsCurrent()).toBe(true);
  });

  it('features the first wallpaper when the pair has none yet', async () => {
    const { store } = await setup('desktop', state(null));

    expect(store.featured()?.id).toBe('aurora');
    expect(store.featuredIsCurrent()).toBe(false);
  });

  it('never features a static wallpaper on the animated tab', async () => {
    const { store } = await setup('desktop', state('neon'));

    store.setKind('animated');

    expect(store.visible().map((w) => w.id)).toEqual(['drift']);
    expect(store.featured()?.id).toBe('drift');
  });

  it('features what the user picked', async () => {
    const { store } = await setup('desktop', state('neon'));

    store.select('aurora');

    expect(store.featured()?.id).toBe('aurora');
    expect(store.featuredIsCurrent()).toBe(false);
  });

  it('hides animated wallpapers on the phone, whatever the tab', async () => {
    const { store } = await setup('mobile', state(null));

    store.setKind('animated');

    expect(store.visible().every((w) => w.kind === 'static')).toBe(true);
  });

  it('filters by the search query', async () => {
    const { store } = await setup('desktop', state(null));

    store.query.set('landscape');

    expect(store.visible().map((w) => w.id)).toEqual(['aurora']);
  });

  it('knows whether the other device of the pair is connected', async () => {
    const lonely = await setup('desktop', state(null, [{ deviceId: 'd', kind: 'desktop', wallpaperId: null, version: null, appliedAt: null }]));
    expect(lonely.store.partnerConnected()).toBe(false);

    TestBed.resetTestingModule();
    const paired = await setup('desktop', state(null, [
      { deviceId: 'd', kind: 'desktop', wallpaperId: null, version: null, appliedAt: null },
      { deviceId: 'm', kind: 'mobile', wallpaperId: null, version: null, appliedAt: null },
    ]));
    expect(paired.store.partnerConnected()).toBe(true);
  });

  it('sets the featured wallpaper and refreshes the state', async () => {
    const { store, http } = await setup('desktop', state(null));
    store.select('neon');

    const setting = store.setFeatured();
    const put = http.expectOne((r) => r.method === 'PUT' && r.url === '/api/state');
    expect(put.request.body).toEqual({ wallpaperId: 'neon' });
    put.flush({ version: 1 });
    await Promise.resolve();
    http.expectOne((r) => r.method === 'GET' && r.url === '/api/state').flush(state('neon'));

    expect(await setting).toBe(true);
    expect(store.featuredIsCurrent()).toBe(true);
    expect(store.setError()).toBeNull();
  });

  it('shows a friendly error when setting fails', async () => {
    const { store, http } = await setup('desktop', state(null));
    store.select('neon');

    const setting = store.setFeatured();
    http.expectOne((r) => r.method === 'PUT').flush({ title: 'Wallpaper not found.' }, { status: 404, statusText: 'Not Found' });

    expect(await setting).toBe(false);
    expect(store.setError()).toContain('não está mais');
    expect(store.setting()).toBe(false);
  });

  it('refuses to send an animated wallpaper from the phone without calling the API', async () => {
    const { store, http } = await setup('mobile', state(null));
    store.select('drift');

    expect(await store.setFeatured()).toBe(false);
    http.expectNone((r) => r.method === 'PUT');
    expect(store.setError()).toContain('animados');
  });

  it('reports a loading error', async () => {
    TestBed.configureTestingModule({
      providers: [CatalogStore, provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: '' },
        { provide: PlatformService, useValue: { deviceKind: 'desktop', defaultDeviceName: 'Meu PC' } }],
    });
    const store = TestBed.inject(CatalogStore);
    const http = TestBed.inject(HttpTestingController);

    const loading = store.load();
    http.expectOne('/api/wallpapers').flush(null, { status: 500, statusText: 'Error' });
    http.expectOne('/api/state').flush(state(null));
    await loading;

    expect(store.status()).toBe('error');
  });
});

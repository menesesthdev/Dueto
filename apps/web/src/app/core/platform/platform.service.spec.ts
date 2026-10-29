import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { PlatformService, WINDOW, detectPlatform } from './platform.service';

const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148';
const IPAD_AS_MAC = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 Safari/605.1.15';
const ANDROID = 'Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 Chrome/130 Mobile Safari/537.36';
const WINDOWS = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130 Safari/537.36';
const MAC = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 Safari/605.1.15';

describe('detectPlatform', () => {
  it('treats an iPhone as mobile', () => {
    expect(detectPlatform(IPHONE, 5, false)).toEqual({ kind: 'mobile', deviceName: 'iPhone', isIos: true });
  });

  it('recognizes an iPad that presents itself as a Mac by its touch screen', () => {
    expect(detectPlatform(IPAD_AS_MAC, 5, false)).toEqual({ kind: 'mobile', deviceName: 'iPad', isIos: true });
  });

  it('keeps a real Mac without touch as desktop', () => {
    expect(detectPlatform(MAC, 0, false)).toEqual({ kind: 'desktop', deviceName: 'Meu Mac', isIos: false });
  });

  it('treats Android as mobile', () => {
    expect(detectPlatform(ANDROID, 5, false)).toEqual({ kind: 'mobile', deviceName: 'Android', isIos: false });
  });

  it('treats Windows as a desktop named "Meu PC"', () => {
    expect(detectPlatform(WINDOWS, 0, false)).toEqual({ kind: 'desktop', deviceName: 'Meu PC', isIos: false });
  });

  it('always treats Electron as desktop, even with a mobile-looking user agent', () => {
    expect(detectPlatform(IPHONE, 5, true).kind).toBe('desktop');
  });
});

describe('PlatformService', () => {
  function create(userAgent: string, extra: Partial<Window> = {}, touch = 0, standalone?: boolean) {
    const fakeWindow = {
      navigator: { userAgent, maxTouchPoints: touch, standalone },
      matchMedia: () => ({ matches: false }),
      ...extra,
    } as unknown as Window;
    TestBed.configureTestingModule({ providers: [{ provide: WINDOW, useValue: fakeWindow }] });
    return TestBed.inject(PlatformService);
  }

  it('exposes the kind and the default device name', () => {
    const platform = create(IPHONE, {}, 5);

    expect(platform.deviceKind).toBe('mobile');
    expect(platform.defaultDeviceName).toBe('iPhone');
    expect(platform.isIos).toBe(true);
  });

  it('detects Electron through the preload bridge', () => {
    const platform = create(WINDOWS, {
      dueto: {
        platform: 'electron',
        onStateChanged: () => () => undefined,
        onConnectionStatus: () => () => undefined,
        getAppliedState: () => Promise.resolve({ wallpaperId: null, version: 0 }),
      },
    } as Partial<Window>);

    expect(platform.isElectron).toBe(true);
    expect(platform.deviceKind).toBe('desktop');
  });

  it('is not Electron in a plain browser', () => {
    expect(create(WINDOWS).isElectron).toBe(false);
  });

  it('detects an installed PWA on iOS through navigator.standalone', () => {
    expect(create(IPHONE, {}, 5, true).isInstalledPwa).toBe(true);
    TestBed.resetTestingModule();
    expect(create(IPHONE, {}, 5, false).isInstalledPwa).toBe(false);
  });
});

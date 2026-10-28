import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { API_BASE_URL } from '../http/api-base-url';
import { describeError } from './api-error';
import { PairingApi } from './pairing.api';
import { StateApi } from './state.api';
import { WallpapersApi } from './wallpapers.api';

describe('API clients', () => {
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: 'https://api.example' }],
    });
    controller = TestBed.inject(HttpTestingController);
  });

  it('lists the wallpapers', () => {
    let result: unknown;
    TestBed.inject(WallpapersApi).list().subscribe((value) => (result = value));

    const request = controller.expectOne('https://api.example/api/wallpapers');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 'aurora' }]);

    expect(result).toEqual([{ id: 'aurora' }]);
  });

  it('starts a pairing with the device name and kind', () => {
    TestBed.inject(PairingApi).start({ deviceName: 'Meu PC', kind: 'desktop' }).subscribe();

    const request = controller.expectOne('https://api.example/api/pairing/start');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ deviceName: 'Meu PC', kind: 'desktop' });
    request.flush({ deviceToken: 't', code: '123456', expiresAt: '2026-10-28T12:05:00Z' });
  });

  it('completes a pairing with the code', () => {
    TestBed.inject(PairingApi).complete({ code: '123456', deviceName: 'iPhone', kind: 'mobile' }).subscribe();

    const request = controller.expectOne('https://api.example/api/pairing/complete');
    expect(request.request.body).toEqual({ code: '123456', deviceName: 'iPhone', kind: 'mobile' });
    request.flush({ deviceToken: 't' });
  });

  it('reads and sets the pair state', () => {
    const api = TestBed.inject(StateApi);

    api.get().subscribe();
    controller.expectOne('https://api.example/api/state').flush({ wallpaperId: null, version: 0, originDeviceId: null, applied: [] });

    api.set('neon-tide').subscribe();
    const put = controller.expectOne('https://api.example/api/state');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ wallpaperId: 'neon-tide' });
    put.flush({ version: 1 });
  });
});

describe('describeError', () => {
  it('extracts the status and the ProblemDetails title', () => {
    const error = new HttpErrorResponse({ status: 400, error: { title: 'Invalid or expired pairing code.' } });

    expect(describeError(error)).toEqual({ status: 400, title: 'Invalid or expired pairing code.' });
  });

  it('reports a network failure (status 0) as no status', () => {
    expect(describeError(new HttpErrorResponse({ status: 0 }))).toEqual({ status: null, title: null });
  });

  it('handles an error body that is not a ProblemDetails', () => {
    expect(describeError(new HttpErrorResponse({ status: 502, error: 'Bad Gateway' }))).toEqual({ status: 502, title: null });
  });

  it('handles values that are not HTTP errors', () => {
    expect(describeError(new Error('boom'))).toEqual({ status: null, title: null });
    expect(describeError(undefined)).toEqual({ status: null, title: null });
  });
});

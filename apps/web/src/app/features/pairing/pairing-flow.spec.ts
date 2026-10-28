import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { DeviceKind } from '../../core/api/models';
import { API_BASE_URL } from '../../core/http/api-base-url';
import { PlatformService } from '../../core/platform/platform.service';
import { SESSION_STORAGE, SessionStore } from '../../core/session/session.store';
import { PairingFlow } from './pairing-flow';

const NOW = new Date('2026-10-28T12:00:00Z').getTime();
const FIVE_MINUTES = 5 * 60 * 1000;

function setup(kind: DeviceKind) {
  TestBed.configureTestingModule({
    providers: [
      PairingFlow,
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: '' },
      { provide: SESSION_STORAGE, useValue: null },
      { provide: PlatformService, useValue: { deviceKind: kind, defaultDeviceName: kind === 'desktop' ? 'Meu PC' : 'iPhone' } },
    ],
  });
  return {
    flow: TestBed.inject(PairingFlow),
    http: TestBed.inject(HttpTestingController),
    session: TestBed.inject(SessionStore),
  };
}

const flush = () => vi.advanceTimersByTimeAsync(0);

/** Estado do par visto por um desktop: ele mesmo e, se já se conectou, o celular. */
function partnerState(partner: DeviceKind | null) {
  const applied: { deviceId: string; kind: DeviceKind; wallpaperId: null; version: null; appliedAt: null }[] = [
    { deviceId: 'self', kind: 'desktop', wallpaperId: null, version: null, appliedAt: null },
  ];
  if (partner) {
    applied.push({ deviceId: 'other', kind: partner, wallpaperId: null, version: null, appliedAt: null });
  }
  return { wallpaperId: null, version: 0, originDeviceId: null, applied };
}

describe('PairingFlow', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });

  it('a desktop generates a code as soon as the screen opens', async () => {
    const { flow, http, session } = setup('desktop');

    flow.begin();
    const start = http.expectOne('/api/pairing/start');
    expect(start.request.body).toEqual({ deviceName: 'Meu PC', kind: 'desktop' });
    start.flush({ deviceToken: 'tok', code: '482913', expiresAt: new Date(NOW + FIVE_MINUTES).toISOString() });
    await flush();

    expect(flow.mode()).toBe('generate');
    expect(flow.phase()).toBe('waiting');
    expect(flow.code()).toBe('482913');
    expect(flow.remainingSeconds()).toBe(300);
    expect(session.token()).toBe('tok');
    expect(session.isPaired()).toBe(false);
  });

  it('a phone waits for the user to type the code', () => {
    const { flow, http } = setup('mobile');

    flow.begin();

    expect(flow.mode()).toBe('enter');
    expect(flow.phase()).toBe('idle');
    http.expectNone('/api/pairing/start');
  });

  it('counts down and expires after five minutes', async () => {
    const { flow, http } = setup('desktop');
    flow.begin();
    http.expectOne('/api/pairing/start').flush({ deviceToken: 'tok', code: '482913', expiresAt: new Date(NOW + FIVE_MINUTES).toISOString() });
    await flush();

    await vi.advanceTimersByTimeAsync(28_000);
    http.match('/api/state').forEach((r) => r.flush(partnerState(null)));
    expect(flow.remainingSeconds()).toBe(272);
    expect(flow.fractionLeft()).toBeCloseTo(272 / 300);

    await vi.advanceTimersByTimeAsync(FIVE_MINUTES);
    http.match('/api/state').forEach((r) => r.flush(partnerState(null)));
    expect(flow.remainingSeconds()).toBe(0);
    expect(flow.expired()).toBe(true);
  });

  it('notices the phone connecting and marks the pair as formed', async () => {
    const { flow, http, session } = setup('desktop');
    flow.begin();
    http.expectOne('/api/pairing/start').flush({ deviceToken: 'tok', code: '482913', expiresAt: new Date(NOW + FIVE_MINUTES).toISOString() });
    await flush();

    await vi.advanceTimersByTimeAsync(2_000);
    http.expectOne('/api/state').flush(partnerState(null));
    await flush();
    expect(flow.phase()).toBe('waiting');

    await vi.advanceTimersByTimeAsync(2_000);
    http.expectOne('/api/state').flush(partnerState('mobile'));
    await flush();

    expect(flow.phase()).toBe('connected');
    expect(session.isPaired()).toBe(true);

    // Depois de conectado, para de perguntar.
    await vi.advanceTimersByTimeAsync(10_000);
    http.expectNone('/api/state');
  });

  it('keeps waiting through a temporary network failure', async () => {
    const { flow, http } = setup('desktop');
    flow.begin();
    http.expectOne('/api/pairing/start').flush({ deviceToken: 'tok', code: '482913', expiresAt: new Date(NOW + FIVE_MINUTES).toISOString() });
    await flush();

    await vi.advanceTimersByTimeAsync(2_000);
    http.expectOne('/api/state').error(new ProgressEvent('error'));
    await flush();

    expect(flow.phase()).toBe('waiting');
    expect(flow.error()).toBeNull();
  });

  it('resumes a pending code after a reload instead of creating another pair', async () => {
    const { flow, http, session } = setup('desktop');
    session.startPending('tok', 'desktop', 'Meu PC', { code: '111222', expiresAt: new Date(NOW + 60_000).toISOString() });

    flow.begin();

    http.expectNone('/api/pairing/start');
    expect(flow.code()).toBe('111222');
    expect(flow.remainingSeconds()).toBe(60);
  });

  it('drops an expired pending code and generates a new one', async () => {
    const { flow, http, session } = setup('desktop');
    session.startPending('old', 'desktop', 'Meu PC', { code: '111222', expiresAt: new Date(NOW - 1_000).toISOString() });

    flow.begin();

    http.expectOne('/api/pairing/start').flush({ deviceToken: 'new', code: '333444', expiresAt: new Date(NOW + FIVE_MINUTES).toISOString() });
    await flush();
    expect(flow.code()).toBe('333444');
    expect(session.token()).toBe('new');
  });

  it('shows a friendly message when generating fails', async () => {
    const { flow, http } = setup('desktop');

    flow.begin();
    http.expectOne('/api/pairing/start').flush({}, { status: 429, statusText: 'Too Many Requests' });
    await flush();

    expect(flow.phase()).toBe('idle');
    expect(flow.error()).toContain('Muitas tentativas');
  });

  it('completes with the typed code and saves a paired session', async () => {
    const { flow, http, session } = setup('mobile');

    const completing = flow.complete('482913', '  iPhone 15  ');
    const request = http.expectOne('/api/pairing/complete');
    expect(request.request.body).toEqual({ code: '482913', deviceName: 'iPhone 15', kind: 'mobile' });
    request.flush({ deviceToken: 'phone-tok' });
    await completing;

    expect(flow.phase()).toBe('connected');
    expect(session.isPaired()).toBe(true);
    expect(session.session()?.deviceName).toBe('iPhone 15');
  });

  it('explains a wrong code and lets the user try again', async () => {
    const { flow, http, session } = setup('mobile');

    const completing = flow.complete('000000', 'iPhone');
    http.expectOne('/api/pairing/complete').flush({ title: 'Invalid or expired pairing code.' }, { status: 400, statusText: 'Bad Request' });
    await completing;

    expect(flow.phase()).toBe('idle');
    expect(flow.error()).toContain('Código inválido ou expirado');
    expect(session.token()).toBeNull();
  });

  it.each([['12345'], ['abcdef'], ['']])('does not call the API with an incomplete code: "%s"', async (code) => {
    const { flow, http } = setup('mobile');

    await flow.complete(code, 'iPhone');

    http.expectNone('/api/pairing/complete');
    expect(flow.error()).toContain('6 dígitos');
  });

  it('does not call the API without a device name', async () => {
    const { flow, http } = setup('mobile');

    await flow.complete('482913', '   ');

    http.expectNone('/api/pairing/complete');
    expect(flow.error()).toContain('nome');
  });

  it('switching to "generate" on a phone creates a code there', async () => {
    const { flow, http } = setup('mobile');

    flow.switchMode('generate');

    http.expectOne('/api/pairing/start').flush({ deviceToken: 'tok', code: '555666', expiresAt: new Date(NOW + FIVE_MINUTES).toISOString() });
    await flush();
    expect(flow.code()).toBe('555666');
  });

  it('switching to "enter" on a desktop stops waiting', async () => {
    const { flow, http } = setup('desktop');
    flow.begin();
    http.expectOne('/api/pairing/start').flush({ deviceToken: 'tok', code: '482913', expiresAt: new Date(NOW + FIVE_MINUTES).toISOString() });
    await flush();

    flow.switchMode('enter');
    await vi.advanceTimersByTimeAsync(10_000);

    expect(flow.phase()).toBe('idle');
    http.expectNone('/api/state');
  });
});

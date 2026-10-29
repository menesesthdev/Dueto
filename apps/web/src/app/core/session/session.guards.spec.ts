import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import type { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { pairedGuard, unpairedGuard } from './session.guards';
import { SESSION_STORAGE, SessionStore } from './session.store';

describe('session guards', () => {
  let session: SessionStore;
  const run = (guard: typeof pairedGuard) =>
    TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
  const path = (result: unknown) => (result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : result);

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: SESSION_STORAGE, useValue: null }] });
    session = TestBed.inject(SessionStore);
  });

  it('sends an unpaired device to /pair', () => {
    expect(path(run(pairedGuard))).toBe('/pair');
  });

  it('keeps a device that is still waiting for the other side on /pair', () => {
    session.startPending('tok', 'desktop', 'Meu PC', { code: '123456', expiresAt: '2030-01-01T00:00:00Z' });

    expect(path(run(pairedGuard))).toBe('/pair');
    expect(run(unpairedGuard)).toBe(true);
  });

  it('lets a paired device into the catalog and keeps it off /pair', () => {
    session.savePaired('tok', 'mobile', 'iPhone');

    expect(run(pairedGuard)).toBe(true);
    expect(path(run(unpairedGuard))).toBe('/');
  });
});

import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SESSION_STORAGE, SessionStore } from '../session/session.store';
import { API_BASE_URL } from './api-base-url';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let session: SessionStore;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: SESSION_STORAGE, useValue: null },
        { provide: API_BASE_URL, useValue: '' },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionStore);
    router = TestBed.inject(Router);
  });

  it('adds the bearer token to API calls', () => {
    session.savePaired('secret-token', 'desktop', 'Meu PC');

    http.get('/api/state').subscribe();

    const request = controller.expectOne('/api/state');
    expect(request.request.headers.get('Authorization')).toBe('Bearer secret-token');
    request.flush({});
  });

  it('sends no Authorization header without a session', () => {
    http.get('/api/wallpapers').subscribe();

    const request = controller.expectOne('/api/wallpapers');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush([]);
  });

  it('never sends the token to another origin', () => {
    session.savePaired('secret-token', 'desktop', 'Meu PC');

    http.get('https://evil.example/api/steal').subscribe();

    const request = controller.expectOne('https://evil.example/api/steal');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('clears the session and goes to /pair on a 401 with a token', () => {
    session.savePaired('revoked', 'desktop', 'Meu PC');
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    http.get('/api/state').subscribe({ error: () => undefined });
    controller.expectOne('/api/state').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(session.session()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/pair');
  });

  it('does not touch the session on other errors', () => {
    session.savePaired('tok', 'desktop', 'Meu PC');

    http.get('/api/state').subscribe({ error: () => undefined });
    controller.expectOne('/api/state').flush({}, { status: 500, statusText: 'Server Error' });

    expect(session.token()).toBe('tok');
  });

  it('does not redirect a 401 that happens without a session', () => {
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    http.get('/api/state').subscribe({ error: () => undefined });
    controller.expectOne('/api/state').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).not.toHaveBeenCalled();
  });
});

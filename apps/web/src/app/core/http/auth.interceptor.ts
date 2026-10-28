import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SessionStore } from '../session/session.store';
import { API_BASE_URL } from './api-base-url';

/**
 * Anexa `Authorization: Bearer <token>` nas chamadas à API e trata o 401 (CLAUDE.md §11).
 * O token só vai para a nossa API: nunca para outra origem.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const apiBase = inject(API_BASE_URL);
  const session = inject(SessionStore);
  const router = inject(Router);

  if (!request.url.startsWith(`${apiBase}/api/`)) {
    return next(request);
  }

  const token = session.token();
  const authorized = token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      // Um 401 com token significa token revogado ou desconhecido: a sessão não vale mais.
      if (error instanceof HttpErrorResponse && error.status === 401 && token) {
        session.clear();
        void router.navigateByUrl('/pair');
      }

      return throwError(() => error);
    }),
  );
};

import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { Router } from '@angular/router';
import { SessionStore } from './session.store';

/** Telas que exigem um par formado: sem ele, vai para o pareamento. */
export const pairedGuard: CanActivateFn = () => {
  return inject(SessionStore).isPaired() || inject(Router).parseUrl('/pair');
};

/** A tela de pareamento não faz sentido para quem já está pareado. */
export const unpairedGuard: CanActivateFn = () => {
  return !inject(SessionStore).isPaired() || inject(Router).parseUrl('/');
};

import type { Routes } from '@angular/router';
import { pairedGuard, unpairedGuard } from './core/session/session.guards';

export const routes: Routes = [
  {
    path: '',
    canActivate: [pairedGuard],
    title: 'Dueto',
    loadComponent: () => import('./features/catalog/catalog.page').then((m) => m.CatalogPage),
  },
  {
    path: 'pair',
    canActivate: [unpairedGuard],
    title: 'Parear · Dueto',
    loadComponent: () => import('./features/pairing/pairing.page').then((m) => m.PairingPage),
  },
  { path: '**', redirectTo: '' },
];

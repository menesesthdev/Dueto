import { InjectionToken } from '@angular/core';
import { environment } from '../../../environments/environment';

/** URL base da API, vinda do ambiente de build (nunca fixa no código). Vazia = mesma origem. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => environment.apiBaseUrl,
});
